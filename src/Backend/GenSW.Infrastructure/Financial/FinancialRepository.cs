using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Application.Financial;
using GenSW.Domain.Financial;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.Infrastructure.Financial;

public sealed class FinancialRepository(GenSWDbContext db, TimeProvider clock) : IFinancialRepository
{
    // Lock order: module advisory lock, Pessoa row, Animal row. Reference row locks
    // also serialize against status updates performed by the existing modules.
    public const long ModuleLock = 37620261002;
    private DateTimeOffset Now => clock.GetUtcNow();
    private DateOnly Today => CaixaRules.Hoje(clock);
    private IQueryable<T> Query<T>() where T : class => db.Set<T>().AsNoTracking();

    private async Task<T> Write<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ModuleLock})", ct);
            db.ChangeTracker.Clear();
            var result = await action();
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception e) when (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "55P03" } || e is DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "55P03" } })
        {
            throw new CaixaConflictException("Conflito na gravação do caixa. Recarregue e tente novamente.");
        }
    }
    private async Task<T> Read<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var result = await action();
        if (tx is not null) await tx.CommitAsync(ct);
        return result;
    }
    private async Task<T> Idempotent<T>(Guid autor, string operation, string key, object payload, Func<Task<T>> action, CancellationToken ct)
    {
        key = CaixaRules.Texto(key, 100, "Idempotency-Key");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        var existing = await Query<IdempotenciaFinanceira>().SingleOrDefaultAsync(x => x.AutorId == autor && x.Operacao == operation && x.Chave == key, ct);
        if (existing is not null)
        {
            if (existing.PayloadHash != hash) throw new CaixaConflictException("Chave de idempotência já usada com outros dados.");
            return JsonSerializer.Deserialize<T>(existing.RespostaJson)!;
        }
        var result = await action();
        db.Add(new IdempotenciaFinanceira(autor, operation, key, hash, JsonSerializer.Serialize(result), Now));
        return result;
    }
    public Task<ConfiguracaoCaixa?> ConfiguracaoAsync(CancellationToken ct) => Query<ConfiguracaoCaixa>().SingleOrDefaultAsync(ct);
    private async Task<ConfiguracaoCaixa> RequiredConfig(CancellationToken ct) => await ConfiguracaoAsync(ct) ?? throw new CaixaConflictException("Configure o saldo inicial antes de utilizar o caixa.");
    public Task<ConfiguracaoCaixa> ConfigureAsync(ConfiguracaoCommand c, Guid autor, CancellationToken ct) => Write(async () =>
    {
        if (c.DataInicio.Day != 1 || c.DataInicio > Today) throw new ArgumentException("Início deve ser o primeiro dia de um mês não futuro.");
        CaixaRules.Dinheiro(c.SaldoInicial);
        var old = await db.Set<ConfiguracaoCaixa>().SingleOrDefaultAsync(ct);
        if (old is not null)
        {
            CaixaRules.Versao(old.Versao, c.VersaoEsperada ?? 0);
            if (await Query<LancamentoCaixa>().AnyAsync(ct) || await Query<FechamentoCaixa>().AnyAsync(ct))
                throw new CaixaConflictException("Configuração imutável após o primeiro lançamento ou fechamento.");
        }
        var next = new ConfiguracaoCaixa(1, c.DataInicio, c.SaldoInicial, (old?.Versao ?? 0) + 1, autor, old?.CreatedAtUtc ?? Now, Now);
        if (old is null) db.Add(next); else db.Entry(old).CurrentValues.SetValues(next);
        return next;
    }, ct);
    public async Task<IReadOnlyList<CategoriaFinanceira>> CategoriasAsync(CancellationToken ct) => await Query<CategoriaFinanceira>().OrderBy(x => x.Natureza).ThenBy(x => x.Nome).ToArrayAsync(ct);
    public Task<CategoriaFinanceira> CreateCategoriaAsync(CategoriaCommand c, CancellationToken ct) => Write(async () =>
    {
        if (!Enum.IsDefined(c.Natureza)) throw new ArgumentException("Natureza inválida.");
        var name = CaixaRules.Texto(c.Nome, 100, "Nome");
        var category = new CategoriaFinanceira(Guid.NewGuid(), name, name.ToUpperInvariant(), c.Natureza, null, true, 1, Now, Now);
        db.Add(category); await Task.CompletedTask; return category;
    }, ct);
    public Task<CategoriaFinanceira> UpdateCategoriaAsync(Guid id, CategoriaUpdateCommand c, CancellationToken ct) => Write(async () =>
    {
        var old = await db.Set<CategoriaFinanceira>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new CaixaNotFoundException("Categoria não encontrada.");
        CaixaRules.Versao(old.Versao, c.VersaoEsperada);
        var name = CaixaRules.Texto(c.Nome, 100, "Nome");
        var next = old with { Nome = name, NomeNormalizado = name.ToUpperInvariant(), Ativa = c.Ativa, Versao = old.Versao + 1, UpdatedAtUtc = Now };
        db.Entry(old).CurrentValues.SetValues(next); return next;
    }, ct);
    private async Task<MesCaixa> Open(DateOnly month, CancellationToken ct)
    {
        var config = await RequiredConfig(ct);
        if (month < config.DataInicio) throw new ArgumentException("Mês anterior ao início do controle.");
        var row = await db.Set<MesCaixa>().FindAsync([month], ct);
        if (row?.Fechado == true) throw new CaixaConflictException("Mês fechado é imutável. Use ajuste no mês atual.");
        if (row is null) { row = new MesCaixa(month, false, 1); db.Add(row); }
        return row;
    }
    private void Touch(MesCaixa month) => db.Entry(month).CurrentValues.SetValues(month with { Versao = month.Versao + 1 });
    private async Task Validate(LancamentoCommand c, LancamentoCaixa? previous, CancellationToken ct)
    {
        var config = await RequiredConfig(ct);
        CaixaRules.Data(c.DataMovimento, config.DataInicio, Today); FinancialService.ValidateEntry(c);
        var category = await Query<CategoriaFinanceira>().SingleOrDefaultAsync(x => x.Id == c.CategoriaId, ct) ?? throw new ArgumentException("Categoria inexistente.");
        if ((!category.Ativa && previous?.CategoriaId != c.CategoriaId) || category.Natureza != c.Tipo) throw new ArgumentException("Categoria incompatível ou inativa.");
        if (c.PessoaId is { } pessoa)
        {
            var row = await db.Pessoas.FromSqlInterpolated($"SELECT * FROM \"Pessoas\" WHERE \"Id\" = {pessoa} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (row is null || (!row.Ativo && previous?.PessoaId != pessoa)) throw new ArgumentException("Pessoa inexistente ou inativa.");
        }
        if (c.AnimalId is { } animal)
        {
            if (c.Tipo != NaturezaFinanceira.Receita || category.Codigo != "VENDA_ANIMAIS") throw new ArgumentException("Animal somente para receita de venda de animais.");
            var row = await db.Animais.FromSqlInterpolated($"SELECT * FROM \"Animais\" WHERE \"Id\" = {animal} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (row is null || (!row.Ativo && previous?.AnimalId != animal)) throw new ArgumentException("Animal inexistente ou inativo.");
        }
    }
    private LancamentoCaixa New(LancamentoCommand c, Guid autor, OrigemLancamento origem = OrigemLancamento.Ordinario, Guid? original = null, string? reason = null) =>
        new(Guid.NewGuid(), c.Tipo, c.DataMovimento, c.Valor, c.Descricao.Trim(), c.CategoriaId, c.FormaPagamento, c.PessoaId, c.AnimalId,
            CaixaRules.Opcional(c.Observacao, 2000), false, 1, autor, Now, Now, origem, original, reason);
    private void Audit(LancamentoCaixa next, string operation, Guid autor, string? reason = null, LancamentoCaixa? old = null) =>
        db.Add(new AuditoriaLancamento(Guid.NewGuid(), next.Id, operation, old is null ? null : JsonSerializer.Serialize(old), JsonSerializer.Serialize(next), reason, autor, Now));
    private async Task<LancamentoCaixa> RequiredEntry(Guid id, CancellationToken ct) => await db.Set<LancamentoCaixa>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new CaixaNotFoundException("Lançamento não encontrado.");
    // Validate all occupied months after persistence, still before commit. This includes
    // cumulative opening balances affected by an earlier correction and rolls back overflow.
    private async Task CheckBalances(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var dates = await Query<LancamentoCaixa>().Select(x => x.DataMovimento).Distinct().ToArrayAsync(ct);
        foreach (var month in dates.Select(CaixaRules.Mes).Append(CaixaRules.Mes(Today)).Distinct()) await Summary(month, ct);
    }
    public Task<LancamentoCaixa> CreateAsync(LancamentoCommand c, Guid autor, string key, CancellationToken ct) => Write(() => Idempotent(autor, "criacao", key, c, async () =>
    {
        await Validate(c, null, ct); var month = await Open(CaixaRules.Mes(c.DataMovimento), ct);
        var entry = New(c, autor); db.Add(entry); Audit(entry, "Criado", autor); Touch(month); await CheckBalances(ct); return entry;
    }, ct), ct);
    public Task<LancamentoCaixa> UpdateAsync(Guid id, CorrecaoCommand c, Guid autor, CancellationToken ct) => Write(async () =>
    {
        var old = await RequiredEntry(id, ct); CaixaRules.Versao(old.Versao, c.VersaoEsperada);
        var reason = CaixaRules.Texto(c.Motivo, 2000, "Motivo");
        if (old.Cancelado || old.Origem != OrigemLancamento.Ordinario) throw new CaixaConflictException("Lançamento cancelado ou de ajuste não admite edição direta.");
        var source = await Open(CaixaRules.Mes(old.DataMovimento), ct); var target = await Open(CaixaRules.Mes(c.Lancamento.DataMovimento), ct);
        await Validate(c.Lancamento, old, ct);
        var next = New(c.Lancamento, old.AutorId) with { Id = old.Id, CreatedAtUtc = old.CreatedAtUtc, Versao = old.Versao + 1 };
        Audit(next, "Corrigido", autor, reason, old); db.Entry(old).CurrentValues.SetValues(next); Touch(source); if (source.Mes != target.Mes) Touch(target);
        await CheckBalances(ct); return next;
    }, ct);
    public Task<LancamentoCaixa> CancelAsync(Guid id, CancelamentoCommand c, Guid autor, CancellationToken ct) => Write(async () =>
    {
        var old = await RequiredEntry(id, ct); CaixaRules.Versao(old.Versao, c.VersaoEsperada);
        var reason = CaixaRules.Texto(c.Motivo, 2000, "Motivo"); var month = await Open(CaixaRules.Mes(old.DataMovimento), ct);
        if (old.Cancelado || old.Origem != OrigemLancamento.Ordinario) throw new CaixaConflictException("Cancelamento não permitido para esse lançamento.");
        var next = old with { Cancelado = true, Versao = old.Versao + 1, UpdatedAtUtc = Now };
        Audit(next, "Cancelado", autor, reason, old); db.Entry(old).CurrentValues.SetValues(next); Touch(month); await CheckBalances(ct); return next;
    }, ct);
    private IQueryable<LancamentoView> Views(IQueryable<LancamentoCaixa> entries) =>
        from entry in entries
        join category in Query<CategoriaFinanceira>() on entry.CategoriaId equals category.Id
        join person in db.Pessoas.AsNoTracking() on entry.PessoaId equals person.Id into people
        from person in people.DefaultIfEmpty()
        join animal in db.Animais.AsNoTracking() on entry.AnimalId equals animal.Id into animals
        from animal in animals.DefaultIfEmpty()
        select new LancamentoView(entry, category.Nome, person == null ? null : person.Nome,
            animal == null ? null : animal.CodigoInterno + " · " + animal.Nome,
            Query<LancamentoCaixa>().Any(x => x.LancamentoOriginalId == entry.Id && x.Origem == OrigemLancamento.AjusteReversao));
    public Task<LancamentoView> GetAsync(Guid id, CancellationToken ct) => Read(async () => await Views(Query<LancamentoCaixa>().Where(x => x.Id == id)).SingleOrDefaultAsync(ct) ?? throw new CaixaNotFoundException("Lançamento não encontrado."), ct);
    public Task<LancamentosPage> ListAsync(LancamentoQuery q, CancellationToken ct) => Read(async () =>
    {
        if (q.Page < 1 || q.PageSize is < 1 or > 100 || (q.Ano is null) != (q.Mes is null) || q.De > q.Ate || q.Search?.Length > 200 || (q.Tipo.HasValue && !Enum.IsDefined(q.Tipo.Value))) throw new ArgumentException("Filtros ou paginação inválidos.");
        var rows = Query<LancamentoCaixa>();
        if (q.Ano is { } year && q.Mes is { } mon) { var month = new DateOnly(year, mon, 1); var end = month.AddMonths(1); rows = rows.Where(x => x.DataMovimento >= month && x.DataMovimento < end); }
        if (q.De is { } de) rows = rows.Where(x => x.DataMovimento >= de);
        if (q.Ate is { } ate) rows = rows.Where(x => x.DataMovimento <= ate);
        if (q.Tipo is { } tipo) rows = rows.Where(x => x.Tipo == tipo);
        if (q.CategoriaId is { } cat) rows = rows.Where(x => x.CategoriaId == cat);
        if (q.Cancelado is { } cancelled) rows = rows.Where(x => x.Cancelado == cancelled);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var search = q.Search.Trim().ToLower(); rows = rows.Where(x => x.Descricao.ToLower().Contains(search)); }
        var count = await rows.CountAsync(ct);
        return new LancamentosPage(await Views(rows.OrderByDescending(x => x.DataMovimento).ThenByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(checked((q.Page - 1) * q.PageSize)).Take(q.PageSize)).ToArrayAsync(ct), q.Page, q.PageSize, count, (int)Math.Ceiling(count / (double)q.PageSize));
    }, ct);
    public Task<IReadOnlyList<AuditoriaLancamento>> HistoricoAsync(Guid id, CancellationToken ct) => Read<IReadOnlyList<AuditoriaLancamento>>(async () =>
    {
        if (!await Query<LancamentoCaixa>().AnyAsync(x => x.Id == id, ct)) throw new CaixaNotFoundException("Lançamento não encontrado.");
        return await Query<AuditoriaLancamento>().Where(x => x.LancamentoId == id).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
    }, ct);
    private async Task<ResumoCaixa> Summary(DateOnly month, CancellationToken ct)
    {
        var config = await RequiredConfig(ct); if (month < config.DataInicio) throw new ArgumentException("Mês anterior ao início do controle.");
        var state = await Query<MesCaixa>().SingleOrDefaultAsync(x => x.Mes == month, ct);
        var snapshot = await Query<FechamentoCaixa>().SingleOrDefaultAsync(x => x.Mes == month, ct);
        if (snapshot is not null) return new(month, true, state!.Versao, snapshot.SaldoAbertura, snapshot.Receitas, snapshot.Despesas, snapshot.Receitas - snapshot.Despesas, snapshot.SaldoFinal, snapshot.QuantidadeReceitas, snapshot.QuantidadeDespesas);
        var end = month.AddMonths(1); var effective = Query<LancamentoCaixa>().Where(x => !x.Cancelado);
        var before = await effective.Where(x => x.DataMovimento < month).SumAsync(x => x.Tipo == NaturezaFinanceira.Receita ? x.Valor : -x.Valor, ct);
        var current = effective.Where(x => x.DataMovimento >= month && x.DataMovimento < end);
        var income = CaixaRules.Dinheiro(await current.Where(x => x.Tipo == NaturezaFinanceira.Receita).SumAsync(x => x.Valor, ct));
        var expense = CaixaRules.Dinheiro(await current.Where(x => x.Tipo == NaturezaFinanceira.Despesa).SumAsync(x => x.Valor, ct));
        var opening = CaixaRules.Dinheiro(config.SaldoInicial + before); var balance = CaixaRules.Dinheiro(opening + income - expense);
        return new(month, false, state?.Versao ?? 1, opening, income, expense, income - expense, balance, await current.CountAsync(x => x.Tipo == NaturezaFinanceira.Receita, ct), await current.CountAsync(x => x.Tipo == NaturezaFinanceira.Despesa, ct));
    }
    public Task<ResumoCaixa> ResumoAsync(DateOnly month, CancellationToken ct) => Read(() => Summary(month, ct), ct);
    public Task<FechamentoCaixa?> FechamentoAsync(DateOnly month, CancellationToken ct) => Query<FechamentoCaixa>().SingleOrDefaultAsync(x => x.Mes == month, ct);
    public async Task<IReadOnlyList<FechamentoCaixa>> FechamentosAsync(CancellationToken ct) => await Query<FechamentoCaixa>().OrderByDescending(x => x.Mes).ToArrayAsync(ct);
    public Task<FechamentoCaixa> CloseAsync(DateOnly month, FechamentoCommand c, Guid autor, CancellationToken ct) => Write(async () =>
    {
        if (month >= CaixaRules.Mes(Today)) throw new CaixaConflictException("Somente meses civis encerrados podem ser fechados.");
        var config = await RequiredConfig(ct);
        var last = await Query<FechamentoCaixa>().OrderByDescending(x => x.Mes).FirstOrDefaultAsync(ct);
        if (month != (last?.Mes.AddMonths(1) ?? config.DataInicio)) throw new CaixaConflictException("Feche os meses em sequência desde o início do controle.");
        var state = await Open(month, ct); CaixaRules.Versao(state.Versao, c.VersaoEsperada);
        var observation = CaixaRules.Texto(c.Observacao, 2000, "Observação do fechamento");
        if (c.SaldoConferido is { } checkedBalance) CaixaRules.Dinheiro(checkedBalance);
        var summary = await Summary(month, ct);
        var snapshot = new FechamentoCaixa(Guid.NewGuid(), month, summary.SaldoAbertura, summary.Receitas, summary.Despesas, summary.SaldoFinal, summary.QuantidadeReceitas, summary.QuantidadeDespesas, c.SaldoConferido, observation, autor, Now);
        db.Add(snapshot); db.Entry(state).CurrentValues.SetValues(state with { Fechado = true, Versao = state.Versao + 1 }); return snapshot;
    }, ct);
    public Task<AjusteResult> AdjustAsync(Guid id, AjusteCommand c, Guid autor, string key, CancellationToken ct) => Write(() => Idempotent(autor, $"ajuste/{id}", key, c, async () =>
    {
        var original = await RequiredEntry(id, ct); CaixaRules.Versao(original.Versao, c.VersaoEsperada);
        var reason = CaixaRules.Texto(c.Motivo, 2000, "Motivo do ajuste");
        var source = await Query<MesCaixa>().SingleOrDefaultAsync(x => x.Mes == CaixaRules.Mes(original.DataMovimento), ct);
        if (source?.Fechado != true && original.Origem == OrigemLancamento.Ordinario) throw new CaixaConflictException("Em mês aberto, utilize correção ou cancelamento.");
        if (original.Cancelado) throw new CaixaConflictException("Lançamento cancelado não admite ajuste.");
        if (c.SomenteAnotacao)
        {
            if (c.Substituto is not null) throw new ArgumentException("Anotação não cria substituto.");
            Audit(original, "Anotado", autor, reason); return new AjusteResult(id, null, null, true);
        }
        if (await Query<LancamentoCaixa>().AnyAsync(x => x.LancamentoOriginalId == id && x.Origem == OrigemLancamento.AjusteReversao, ct)) throw new CaixaConflictException("Original já revertido. Referencie o ajuste para nova correção.");
        var current = await Open(CaixaRules.Mes(Today), ct);
        LancamentoCaixa? replacement = null;
        if (c.Substituto is { } sub)
        {
            if (sub.DataMovimento != Today) throw new ArgumentException("Substituto deve usar a data operacional atual.");
            await Validate(sub, original, ct); replacement = New(sub, autor, OrigemLancamento.AjusteSubstituicao, id, reason); db.Add(replacement); Audit(replacement, "Criado", autor, reason);
        }
        var reversal = original with { Id = Guid.NewGuid(), Tipo = original.Tipo == NaturezaFinanceira.Receita ? NaturezaFinanceira.Despesa : NaturezaFinanceira.Receita,
            DataMovimento = Today, Descricao = "Reversão: " + original.Descricao[..Math.Min(original.Descricao.Length, 190)], Versao = 1, AutorId = autor,
            CreatedAtUtc = Now, UpdatedAtUtc = Now, Origem = OrigemLancamento.AjusteReversao, LancamentoOriginalId = id, MotivoAjuste = reason };
        db.Add(reversal); Audit(reversal, "Criado", autor, reason); Audit(original, "Ajustado", autor, reason); Touch(current); await CheckBalances(ct);
        return new AjusteResult(id, reversal, replacement, false);
    }, ct), ct);
}
