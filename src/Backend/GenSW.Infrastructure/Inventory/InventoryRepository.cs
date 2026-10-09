using System.Data;
using System.Globalization;
using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Domain.Properties;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GenSW.Infrastructure.Inventory;

public sealed class InventoryRepository(GenSWDbContext db, TimeProvider clock) : IInventoryRepository
{
    private bool Postgres => db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
    public async Task<IInventoryScope> BeginMutationAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            if (Postgres) await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='5s'; SELECT pg_advisory_xact_lock(413,421); SELECT pg_advisory_xact_lock(430,1);", ct);
            return new Scope(transaction, db);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.LockNotAvailable)
        { await transaction.DisposeAsync(); throw new InventoryException(409, "conflito_transitorio", "Operação concorrente em andamento; tente novamente com a mesma chave."); }
        catch { await transaction.DisposeAsync(); throw; }
    }
    public async Task<IInventoryScope> BeginReadAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            if (Postgres) await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout='3s';", ct);
            return new Scope(transaction, db);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private sealed class Scope(IDbContextTransaction transaction, GenSWDbContext context) : IInventoryScope
    {
        private bool committed;
        public async Task CommitAsync(CancellationToken ct)
        {
            try { await transaction.CommitAsync(ct); committed = true; }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.CheckViolation)
            { throw new InventoryException(409, "invariante_estoque", "O evento não preserva as regras do estoque."); }
        }
        public async ValueTask DisposeAsync()
        { await transaction.DisposeAsync(); if (!committed) context.ChangeTracker.Clear(); }
    }
    public void ClearTracking() => db.ChangeTracker.Clear();
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new InventoryException(409, "versao_obsoleta", "Registro alterado; atualize a consulta."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new InventoryException(409, "registro_duplicado", "Código, posição ou chave já registrado."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        { throw new InventoryException(400, "invariante_estoque", "Quantidade, unidade ou evento inválido."); }
    }
    public async Task<long> NextSequenceAsync(CancellationToken ct)
    {
        if (Postgres) return await db.Database.SqlQueryRaw<long>("SELECT nextval('\"SequenciaEstoque\"') AS \"Value\"").SingleAsync(ct);
        return await LastSequenceAsync(ct) + 1;
    }
    public async Task<long> LastSequenceAsync(CancellationToken ct)
    {
        var events = await db.Set<EventoEstoque>().Select(x => (long?)x.Sequencia).MaxAsync(ct) ?? 0;
        var history = await db.Set<HistoricoEstoque>().Select(x => (long?)x.Sequencia).MaxAsync(ct) ?? 0;
        return Math.Max(events, history);
    }
    public void Add(LocalEstoque value) => db.Add(value);
    public void Add(LoteMaterial value) => db.Add(value);
    public void Add(PosicaoEstoque value) => db.Add(value);
    public void Add(EventoEstoque value) => db.Add(value);
    public void Add(MovimentoEstoque value) => db.Add(value);
    public void Add(HistoricoEstoque value) => db.Add(value);
    public void Add(ComandoEstoque value) => db.Add(value);
    public void Add(CatalogAudit value) => db.Add(value);
    public Task<Item?> ItemAsync(Guid id, bool tracking, CancellationToken ct) =>
        (tracking && Postgres ? db.Set<Item>().FromSqlInterpolated($"SELECT * FROM \"Itens\" WHERE \"Id\"={id} FOR UPDATE") : tracking ? db.Set<Item>() : db.Set<Item>().AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<LocalEstoque?> LocalAsync(Guid id, bool tracking, CancellationToken ct) =>
        (tracking && Postgres ? db.Set<LocalEstoque>().FromSqlInterpolated($"SELECT * FROM \"LocaisEstoque\" WHERE \"Id\"={id} FOR UPDATE") : tracking ? db.Set<LocalEstoque>() : db.Set<LocalEstoque>().AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<LoteMaterial?> LoteAsync(Guid id, bool tracking, CancellationToken ct) =>
        (tracking && Postgres ? db.Set<LoteMaterial>().FromSqlInterpolated($"SELECT * FROM \"LotesMateriais\" WHERE \"Id\"={id} FOR UPDATE") : tracking ? db.Set<LoteMaterial>() : db.Set<LoteMaterial>().AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PosicaoEstoque?> PositionAsync(Guid lot, Guid local, bool tracking, CancellationToken ct) =>
        (tracking && Postgres ? db.Set<PosicaoEstoque>().FromSqlInterpolated($"SELECT * FROM \"PosicoesEstoque\" WHERE \"LoteId\"={lot} AND \"LocalId\"={local} FOR UPDATE") : tracking ? db.Set<PosicaoEstoque>() : db.Set<PosicaoEstoque>().AsNoTracking()).SingleOrDefaultAsync(x => x.LoteId == lot && x.LocalId == local, ct);
    public Task<ConversaoItem?> ConversionAsync(Guid id, CancellationToken ct) => db.Set<ConversaoItem>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<NutritionProfile?> ProfileAsync(Guid id, CancellationToken ct) => db.Set<NutritionProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<bool> PropertyActiveAsync(Guid id, CancellationToken ct)
    {
        var query = Postgres ? db.Set<Propriedade>().FromSqlInterpolated($"SELECT * FROM \"Propriedades\" WHERE \"Id\"={id} FOR SHARE") : db.Set<Propriedade>();
        return await query.AsNoTracking().AnyAsync(x => x.Id == id && x.Ativo, ct);
    }
    public async Task<ResponsavelView?> ResponsibleAsync(Guid id, bool forUpdate, CancellationToken ct)
    {
        var users = forUpdate && Postgres ? db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={id} FOR SHARE") : db.Users;
        return await users.AsNoTracking().Where(x => x.Id == id).Join(db.Pessoas.AsNoTracking(), x => x.PessoaId, p => p.Id, (x, p) => new ResponsavelView(x.Id, p.Nome, x.IsActive)).SingleOrDefaultAsync(ct);
    }
    public Task<bool> HasBalanceAsync(Guid? lot, Guid? local, CancellationToken ct) => db.Set<PosicaoEstoque>().AnyAsync(x => (!lot.HasValue || x.LoteId == lot) && (!local.HasValue || x.LocalId == local) && x.Quantidade > 0, ct);
    public Task<bool> HasMovementsAsync(Guid lot, Guid local, CancellationToken ct) => db.Set<MovimentoEstoque>().AnyAsync(x => x.LoteId == lot && x.LocalId == local, ct);
    public Task<ComandoEstoque?> ReplayAsync(Guid actor, string operation, Guid resource, string key, CancellationToken ct) => db.Set<ComandoEstoque>().AsNoTracking().SingleOrDefaultAsync(x => x.AutorId == actor && x.Operacao == operation && x.RecursoId == resource && x.Chave == key, ct);
    private static string Escape(string value) => "%" + value.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    private async Task<InventoryPage<T>> Page<T>(IQueryable<T> rows, InventoryQuery q, CancellationToken ct, DateTimeOffset? observedAt = null)
    {
        var observed = observedAt ?? clock.GetUtcNow(); var cut = await LastSequenceAsync(ct);
        var total = await rows.CountAsync(ct); var data = await rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        return new(data, q.Page, q.PageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)q.PageSize), observed, cut.ToString(CultureInfo.InvariantCulture));
    }
    private IQueryable<LocalView> LocalRows() => from l in db.Set<LocalEstoque>().AsNoTracking()
        join p in db.Propriedades.AsNoTracking() on l.PropriedadeId equals p.Id into properties
        from p in properties.DefaultIfEmpty()
        select new LocalView(default, "", "", null, null, null, "", false, 0, default, default)
        { Id = l.Id, Codigo = l.Codigo, Nome = l.Nome, Descricao = l.Descricao, PropriedadeId = l.PropriedadeId, PropriedadeNome = p == null ? null : p.Nome, Finalidade = l.Finalidade, Ativo = l.Ativo, Revisao = l.Revisao, CreatedAtUtc = l.CreatedAtUtc, UpdatedAtUtc = l.UpdatedAtUtc };
    public Task<LocalView?> LocalViewAsync(Guid id, CancellationToken ct) => LocalRows().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<InventoryPage<LocalView>> LocaisAsync(InventoryQuery q, CancellationToken ct)
    {
        var rows = LocalRows();
        if (!string.IsNullOrWhiteSpace(q.Search)) { var search = Escape(q.Search.ToUpperInvariant()); rows = rows.Where(x => EF.Functions.Like(x.Codigo.ToUpper(), search, "\\") || EF.Functions.Like(x.Nome.ToUpper(), search, "\\")); }
        if (q.Ativo is { } active) rows = rows.Where(x => x.Ativo == active);
        if (q.PropriedadeId is { } property) rows = rows.Where(x => x.PropriedadeId == property);
        if (q.Finalidade is { } purpose) rows = rows.Where(x => x.Finalidade == purpose);
        rows = (q.SortBy, q.SortDirection) switch
        {
            ("nome", "desc") => rows.OrderByDescending(x => x.Nome).ThenBy(x => x.Id),
            ("nome", _) => rows.OrderBy(x => x.Nome).ThenBy(x => x.Id),
            ("createdAtUtc", "desc") => rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            ("createdAtUtc", _) => rows.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            (_, "desc") => rows.OrderByDescending(x => x.Codigo).ThenBy(x => x.Id),
            _ => rows.OrderBy(x => x.Codigo).ThenBy(x => x.Id)
        };
        return Page(rows, q, ct);
    }
    private IQueryable<LoteView> LoteRows() => from l in db.Set<LoteMaterial>().AsNoTracking()
        join i in db.Set<Item>().AsNoTracking() on l.ItemId equals i.Id
        join u in db.Users.AsNoTracking() on l.ResponsavelId equals u.Id
        join p in db.Pessoas.AsNoTracking() on u.PessoaId equals p.Id
        select new LoteView(default, default, "", "", false, "", null, "", "", default, "", null, null, null, null, null, null, "", false, "", null, null, null, 0, default, default)
        { Id=l.Id, ItemId=l.ItemId, ItemCodigo=i.Codigo, ItemNome=i.Nome, ItemAtivo=i.Ativo, Codigo=l.Codigo, CodigoExterno=l.CodigoExterno, Origem=l.Origem, Fonte=l.Fonte, ResponsavelId=l.ResponsavelId, ResponsavelNome=p.Nome, DataOrigem=l.DataOrigem, Fabricacao=l.Fabricacao, Coleta=l.Coleta, Validade=l.Validade, FonteValidade=l.FonteValidade, ResponsavelValidadeId=l.ResponsavelValidadeId, Situacao=l.Situacao, Ativo=l.Ativo, Unidade=l.Unidade, PerfilNutricionalId=l.PerfilNutricionalId, ConversaoItemId=l.ConversaoItemId, Aplicabilidade=l.Aplicabilidade, Revisao=l.Revisao, CreatedAtUtc=l.CreatedAtUtc, UpdatedAtUtc=l.UpdatedAtUtc };
    public Task<LoteView?> LoteViewAsync(Guid id, CancellationToken ct) => LoteRows().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<InventoryPage<LoteView>> LotesAsync(InventoryQuery q, CancellationToken ct)
    {
        var rows = LoteRows();
        if (!string.IsNullOrWhiteSpace(q.Search)) { var s = Escape(q.Search.ToUpperInvariant()); rows = rows.Where(x => EF.Functions.Like(x.Codigo.ToUpper(), s, "\\") || EF.Functions.Like(x.ItemNome.ToUpper(), s, "\\") || (x.CodigoExterno != null && EF.Functions.Like(x.CodigoExterno.ToUpper(), s, "\\"))); }
        if (q.Ativo is { } active) rows = rows.Where(x => x.Ativo == active);
        if (q.ItemId is { } item) rows = rows.Where(x => x.ItemId == item);
        if (q.LoteId is { } lot) rows = rows.Where(x => x.Id == lot);
        if (q.Situacao is { } state) rows = rows.Where(x => x.Situacao == state);
        if (q.ValidadeAte is { } until) rows = rows.Where(x => x.Validade != null && x.Validade <= until);
        if (q.SemValidade is { } noExpiry) rows = rows.Where(x => (x.Validade == null) == noExpiry);
        if (q.LocalId is { } local) rows = rows.Where(x => db.Set<PosicaoEstoque>().Any(p => p.LoteId == x.Id && p.LocalId == local));
        if (q.ComSaldo is { } withBalance) rows = rows.Where(x => db.Set<PosicaoEstoque>().Any(p => p.LoteId == x.Id && (!q.LocalId.HasValue || p.LocalId == q.LocalId) && p.Quantidade > 0) == withBalance);
        rows = (q.SortBy, q.SortDirection) switch
        {
            ("nome", "desc") => rows.OrderByDescending(x => x.ItemNome).ThenBy(x => x.Id),
            ("nome", _) => rows.OrderBy(x => x.ItemNome).ThenBy(x => x.Id),
            ("createdAtUtc", "desc") => rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            ("createdAtUtc", _) => rows.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            (_, "desc") => rows.OrderByDescending(x => x.Codigo).ThenBy(x => x.Id),
            _ => rows.OrderBy(x => x.Codigo).ThenBy(x => x.Id)
        };
        return Page(rows, q, ct);
    }
    private sealed class StockRow
    { public PosicaoEstoque Position { get; init; } = null!; public LoteMaterial Lot { get; init; } = null!; public Item Item { get; init; } = null!; public LocalEstoque Local { get; init; } = null!; }
    private IQueryable<StockRow> StockRows() => from p in db.Set<PosicaoEstoque>().AsNoTracking()
        join l in db.Set<LoteMaterial>().AsNoTracking() on p.LoteId equals l.Id
        join i in db.Set<Item>().AsNoTracking() on l.ItemId equals i.Id
        join local in db.Set<LocalEstoque>().AsNoTracking() on p.LocalId equals local.Id
        select new StockRow { Position=p, Lot=l, Item=i, Local=local };
    private static SaldoView StockView(PosicaoEstoque? p, LoteMaterial l, Item i, LocalEstoque local, DateOnly today, DateTimeOffset observed, long cut)
    {
        var reasons = new List<string>();
        if (!i.Ativo) reasons.Add("Item inativo");
        if (!l.Ativo) reasons.Add("Lote inativo");
        if (!local.Ativo) reasons.Add("Local inativo");
        if (local.Finalidade != "Ordinario") reasons.Add("Local de segregação");
        if (l.Situacao != "Liberado") reasons.Add("Lote " + l.Situacao.ToLowerInvariant());
        if (l.Validade < today) reasons.Add("Validade vencida");
        var quantity = p?.Quantidade ?? 0m; var eligible = reasons.Count == 0;
        return new(l.Id, local.Id, i.Id, i.Codigo, i.Nome, l.Codigo, local.Codigo, local.Nome, l.Unidade, CatalogRules.Format(quantity), CatalogRules.Format(eligible ? quantity : 0m), eligible, reasons, p?.Revisao ?? 0, i.Revisao, l.Revisao, local.Revisao, observed, cut.ToString(CultureInfo.InvariantCulture), l.Validade.HasValue ? [] : ["Validade não informada"]);
    }
    private (DateTimeOffset Observed, DateOnly Today) StockReadTime()
    {
        var observed = clock.GetUtcNow();
        return (observed, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(observed, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime));
    }
    public async Task<InventoryPage<SaldoView>> SaldosAsync(InventoryQuery q, DateOnly today, CancellationToken ct)
    {
        // The application date can become stale during earlier awaits in the read scope.
        // Eligibility, its SQL filter and response metadata share this captured instant.
        var readTime = StockReadTime(); today = readTime.Today;
        var rows = StockRows();
        if (!string.IsNullOrWhiteSpace(q.Search)) { var s = Escape(q.Search.ToUpperInvariant()); rows = rows.Where(x => EF.Functions.Like(x.Item.Nome.ToUpper(), s, "\\") || EF.Functions.Like(x.Item.CodigoNormalizado, s, "\\") || EF.Functions.Like(x.Lot.CodigoNormalizado, s, "\\") || EF.Functions.Like(x.Local.CodigoNormalizado, s, "\\")); }
        if (q.ItemId is { } item) rows = rows.Where(x => x.Item.Id == item);
        if (q.LoteId is { } lot) rows = rows.Where(x => x.Lot.Id == lot);
        if (q.LocalId is { } local) rows = rows.Where(x => x.Local.Id == local);
        if (q.PropriedadeId is { } property) rows = rows.Where(x => x.Local.PropriedadeId == property);
        if (q.Situacao is { } state) rows = rows.Where(x => x.Lot.Situacao == state);
        if (q.Ativo is { } active) rows = rows.Where(x => (x.Item.Ativo && x.Lot.Ativo && x.Local.Ativo) == active);
        if (q.ComSaldo is { } balance) rows = rows.Where(x => (x.Position.Quantidade > 0) == balance);
        if (q.ValidadeAte is { } until) rows = rows.Where(x => x.Lot.Validade != null && x.Lot.Validade <= until);
        if (q.SemValidade is { } noExpiry) rows = rows.Where(x => (x.Lot.Validade == null) == noExpiry);
        if (q.Elegivel is { } eligible) rows = rows.Where(x => (x.Item.Ativo && x.Lot.Ativo && x.Local.Ativo && x.Local.Finalidade == "Ordinario" && x.Lot.Situacao == "Liberado" && (!x.Lot.Validade.HasValue || x.Lot.Validade >= today)) == eligible);
        rows = (q.SortBy, q.SortDirection) switch
        {
            ("loteCodigo", "desc") => rows.OrderByDescending(x => x.Lot.Codigo).ThenBy(x => x.Position.LoteId).ThenBy(x => x.Position.LocalId),
            ("loteCodigo", _) => rows.OrderBy(x => x.Lot.Codigo).ThenBy(x => x.Position.LoteId).ThenBy(x => x.Position.LocalId),
            ("localCodigo", "desc") => rows.OrderByDescending(x => x.Local.Codigo).ThenBy(x => x.Position.LoteId).ThenBy(x => x.Position.LocalId),
            ("localCodigo", _) => rows.OrderBy(x => x.Local.Codigo).ThenBy(x => x.Position.LoteId).ThenBy(x => x.Position.LocalId),
            (_, "desc") => rows.OrderByDescending(x => x.Item.Codigo).ThenBy(x => x.Position.LoteId).ThenBy(x => x.Position.LocalId),
            _ => rows.OrderBy(x => x.Item.Codigo).ThenBy(x => x.Position.LoteId).ThenBy(x => x.Position.LocalId)
        };
        var page = await Page(rows, q, ct, readTime.Observed); var cut = long.Parse(page.SequenciaAte, CultureInfo.InvariantCulture);
        return new(page.Items.Select(x => StockView(x.Position, x.Lot, x.Item, x.Local, today, page.ObservadoEmUtc, cut)).ToArray(), page.Page, page.PageSize, page.TotalItems, page.TotalPages, page.ObservadoEmUtc, page.SequenciaAte);
    }
    public async Task<SaldoView?> SaldoAsync(Guid lot, Guid local, DateOnly today, CancellationToken ct)
    {
        var readTime = StockReadTime(); today = readTime.Today;
        var l = await LoteAsync(lot, false, ct); var loc = await LocalAsync(local, false, ct);
        if (l is null || loc is null) return null;
        var item = await ItemAsync(l.ItemId, false, ct); if (item is null) return null;
        return StockView(await PositionAsync(lot, local, false, ct), l, item, loc, today, readTime.Observed, await LastSequenceAsync(ct));
    }
    private static MovimentoView MovementView(MovimentoEstoque m) => new(m.Id, m.Ordinal, m.LoteId, m.LocalId, m.ItemId, m.Unidade, m.Sentido, CatalogRules.Format(m.Quantidade), m.QuantidadeDeclarada, m.UnidadeDeclarada, m.Residuo, CatalogRules.Format(m.SaldoAnterior), CatalogRules.Format(m.SaldoPosterior), m.ConversaoItemId, m.PerfilNutricionalId, m.SnapshotJson);
    private async Task<EventoView> EventView(EventoEstoque e, CancellationToken ct) => new(e.Id, e.Sequencia.ToString(CultureInfo.InvariantCulture), e.Tipo, e.AutorId, e.ResponsavelId, e.CreatedAtUtc, e.DataOperacional, e.DataObservada, e.Motivo, e.Documento, e.EventoReferenciaId, e.Algoritmo, e.SnapshotJson, (await db.Set<MovimentoEstoque>().AsNoTracking().Where(x => x.EventoId == e.Id).OrderBy(x => x.Ordinal).ToArrayAsync(ct)).Select(MovementView).ToArray());
    public async Task<EventoView?> EventoAsync(Guid id, CancellationToken ct)
    { var e = await db.Set<EventoEstoque>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : await EventView(e, ct); }
    public async Task<InventoryPage<EventoView>> EventosAsync(InventoryQuery q, CancellationToken ct)
    {
        var rows = db.Set<EventoEstoque>().AsNoTracking();
        if (q.Tipo is { } type) rows = rows.Where(x => x.Tipo == type);
        if (q.AutorId is { } author) rows = rows.Where(x => x.AutorId == author);
        if (q.DataDe is { } start) rows = rows.Where(x => x.DataOperacional >= start);
        if (q.DataAte is { } end) rows = rows.Where(x => x.DataOperacional <= end);
        if (q.SeqDe is { } seqStart) { var seq = long.Parse(seqStart, CultureInfo.InvariantCulture); rows = rows.Where(x => x.Sequencia >= seq); }
        if (q.SeqAte is { } seqEnd) { var seq = long.Parse(seqEnd, CultureInfo.InvariantCulture); rows = rows.Where(x => x.Sequencia <= seq); }
        if (!string.IsNullOrWhiteSpace(q.Search)) { var s = Escape(q.Search.ToUpperInvariant()); rows = rows.Where(x => EF.Functions.Like(x.Motivo.ToUpper(), s, "\\") || x.Documento != null && EF.Functions.Like(x.Documento.ToUpper(), s, "\\")); }
        if (q.ItemId.HasValue || q.LoteId.HasValue || q.LocalId.HasValue) rows = rows.Where(x => db.Set<MovimentoEstoque>().Any(m => m.EventoId == x.Id && (!q.ItemId.HasValue || m.ItemId == q.ItemId) && (!q.LoteId.HasValue || m.LoteId == q.LoteId) && (!q.LocalId.HasValue || m.LocalId == q.LocalId)));
        rows = q.SortDirection == "desc" ? rows.OrderByDescending(x => x.Sequencia).ThenBy(x => x.Id) : rows.OrderBy(x => x.Sequencia).ThenBy(x => x.Id);
        var page = await Page(rows, q, ct); var values = new List<EventoView>(); foreach (var e in page.Items) values.Add(await EventView(e, ct));
        return new(values, page.Page, page.PageSize, page.TotalItems, page.TotalPages, page.ObservadoEmUtc, page.SequenciaAte);
    }
    public async Task<InventoryPage<HistoricoView>> HistoryAsync(Guid id, InventoryQuery q, CancellationToken ct)
    {
        var page = await Page(db.Set<HistoricoEstoque>().AsNoTracking().Where(x => x.RegistroId == id).OrderByDescending(x => x.Sequencia).ThenBy(x => x.Id), q, ct);
        return new(page.Items.Select(x => new HistoricoView(x.Id, x.RegistroId, x.Tipo, x.Operacao, x.Sequencia.ToString(CultureInfo.InvariantCulture), x.AutorId, x.CreatedAtUtc, x.Motivo, x.AntesJson, x.DepoisJson)).ToArray(), page.Page, page.PageSize, page.TotalItems, page.TotalPages, page.ObservadoEmUtc, page.SequenciaAte);
    }
    public Task<InventoryPage<ResponsavelView>> ResponsaveisAsync(InventoryQuery q, CancellationToken ct)
    {
        var rows = db.Users.AsNoTracking().Join(db.Pessoas.AsNoTracking(), u => u.PessoaId, p => p.Id, (u, p) => new ResponsavelView(default,"",false) { Id=u.Id, Nome=p.Nome, Ativo=u.IsActive });
        if (q.Ativo is { } active) rows = rows.Where(x => x.Ativo == active);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var s = Escape(q.Search.ToUpperInvariant()); rows = rows.Where(x => EF.Functions.Like(x.Nome.ToUpper(), s, "\\")); }
        return Page(q.SortDirection == "desc" ? rows.OrderByDescending(x => x.Nome).ThenBy(x => x.Id) : rows.OrderBy(x => x.Nome).ThenBy(x => x.Id), q, ct);
    }
    public async Task<InventoryPage<ReconciliacaoView>> ReconcileAsync(InventoryQuery q, CancellationToken ct)
    {
        var keys = db.Set<PosicaoEstoque>().Select(p => new { p.LoteId, p.LocalId }).Union(db.Set<MovimentoEstoque>().Select(m => new { m.LoteId, m.LocalId }));
        if (q.LoteId is { } lot) keys = keys.Where(x => x.LoteId == lot);
        if (q.LocalId is { } local) keys = keys.Where(x => x.LocalId == local);
        if (q.ItemId is { } item) keys = keys.Where(x => db.Set<LoteMaterial>().Any(l => l.Id == x.LoteId && l.ItemId == item));
        var sums = (from k in keys
            join l in db.Set<LoteMaterial>().AsNoTracking() on k.LoteId equals l.Id
            join p in db.Set<PosicaoEstoque>().AsNoTracking() on new { k.LoteId, k.LocalId } equals new { p.LoteId, p.LocalId } into projections
            from p in projections.DefaultIfEmpty()
            select new { k.LoteId, k.LocalId, l.Unidade, Projection = p == null ? 0m : p.Quantidade, Missing = p == null, Ledger = db.Set<MovimentoEstoque>().Where(m => m.LoteId == k.LoteId && m.LocalId == k.LocalId).Sum(m => (decimal?)(m.Sentido == "Entrada" ? m.Quantidade : -m.Quantidade)) ?? 0m })
            .Where(x => x.Missing || x.Projection != x.Ledger).OrderBy(x => x.LoteId).ThenBy(x => x.LocalId);
        var page = await Page(sums, q, ct);
        return new(page.Items.Select(x => new ReconciliacaoView(x.LoteId, x.LocalId, x.Unidade, CatalogRules.Format(x.Ledger), CatalogRules.Format(x.Projection), CatalogRules.Format(x.Projection - x.Ledger))).ToArray(), page.Page, page.PageSize, page.TotalItems, page.TotalPages, page.ObservadoEmUtc, page.SequenciaAte);
    }
}
