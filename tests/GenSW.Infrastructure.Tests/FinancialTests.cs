using GenSW.Application;
using GenSW.Application.Financial;
using GenSW.Domain.Financial;
using GenSW.Domain.People;
using GenSW.Infrastructure;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class FinancialTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql pg = null!;
    private ServiceProvider services = null!;
    private readonly Guid autor = Guid.NewGuid();
    private static readonly DateOnly August = new(2026, 8, 1), September = new(2026, 9, 1), October = new(2026, 10, 1);
    private static readonly Guid Income = Guid.Parse("37600000-0000-0000-0000-000000000006"), Expense = Guid.Parse("37600000-0000-0000-0000-000000000001");
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 2, 2, 0, 0, TimeSpan.Zero); }
    public async Task InitializeAsync()
    {
        pg = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
        var c = new ServiceCollection(); c.AddLogging(); c.AddSingleton<TimeProvider>(new Clock()); c.AddApplication();
        c.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:GenSW"] = pg.ConnectionString }).Build());
        services = c.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<GenSWDbContext>().Database.MigrateAsync();
    }
    public async Task DisposeAsync() { await services.DisposeAsync(); await pg.DisposeAsync(); }
    private async Task<T> Run<T>(Func<IFinancialRepository, Task<T>> action)
    {
        await using var scope = services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IFinancialRepository>());
    }
    private Task<ConfiguracaoCaixa> Configure(DateOnly? start = null, decimal balance = 100m) => Run(s => s.ConfigureAsync(new(start ?? August, balance), autor, default));
    private static LancamentoCommand Entry(decimal value = 250m, NaturezaFinanceira tipo = NaturezaFinanceira.Receita, DateOnly? date = null) => new(tipo, date ?? August, value, tipo == NaturezaFinanceira.Receita ? "Venda recebida" : "Ração paga", tipo == NaturezaFinanceira.Receita ? Income : Expense, FormaPagamento.Pix);
    private Task<LancamentoCaixa> Create(LancamentoCommand c, string? key = null) => Run(s => s.CreateAsync(c, autor, key ?? Guid.NewGuid().ToString(), default));
    private Task<ResumoCaixa> Summary(DateOnly date) => Run(s => s.ResumoAsync(date, default));
    private async Task<FechamentoCaixa> Close(DateOnly month)
    {
        var summary = await Summary(month); return await Run(s => s.CloseAsync(month, new(summary.Versao, "Conferido", summary.SaldoFinal), autor, default));
    }
    [Fact]
    public async Task Exact_balance_cancellation_audit_and_pagination()
    {
        await Configure(); await Create(Entry()); var expense = await Create(Entry(80, NaturezaFinanceira.Despesa));
        var summary = await Summary(August); Assert.Equal(170m, summary.Resultado); Assert.Equal(270m, summary.SaldoFinal); Assert.Equal(270m, (await Summary(September)).SaldoAbertura);
        var list = await Run(s => s.ListAsync(new(PageSize:1, Search:"Ração"), default)); Assert.Single(list.Items); Assert.Equal(270m, (await Summary(August)).SaldoFinal);
        await Run(s => s.CancelAsync(expense.Id, new(1, "Registro duplicado"), autor, default)); Assert.Equal(350m, (await Summary(August)).SaldoFinal);
        var audit = await Run(s => s.HistoricoAsync(expense.Id, default)); Assert.Equal(2, audit.Count); Assert.False(System.Text.Json.JsonSerializer.Deserialize<LancamentoCaixa>(audit.Single(x=>x.Operacao=="Cancelado").AntesJson!)!.Cancelado);
        Assert.True((await Run(s => s.GetAsync(expense.Id, default))).Lancamento.Cancelado);
    }
    [Fact]
    public async Task Idempotency_is_persisted_and_not_derived_from_current_entry()
    {
        await Configure(); var c = Entry(); var results = await Task.WhenAll(Create(c,"same"), Create(c,"same")); Assert.Equal(results[0].Id, results[1].Id);
        await Run(s => s.UpdateAsync(results[0].Id, new(c with { Valor = 12 }, 1, "Corrigir valor"), autor, default));
        Assert.Equal(results[0].Id, (await Create(c,"same")).Id); Assert.Equal(12, (await Summary(August)).Receitas);
        await Assert.ThrowsAsync<CaixaConflictException>(() => Create(c with { Valor = 300 }, "same"));
        Assert.Equal(1, (await Run(s => s.ListAsync(new(), default))).TotalItems);
    }
    [Fact]
    public async Task Same_version_concurrent_corrections_have_one_winner()
    {
        await Configure(); var entry = await Create(Entry());
        async Task<bool> Update(decimal value) { try { await Run(s => s.UpdateAsync(entry.Id, new(Entry(value), 1, "Revisão"), autor, default)); return true; } catch (CaixaConflictException) { return false; } }
        Assert.Single(await Task.WhenAll(Update(200), Update(300)), x => x);
        var history = await Run(s => s.HistoricoAsync(entry.Id, default)); Assert.Equal(2, history.Count); Assert.Equal(250m,System.Text.Json.JsonSerializer.Deserialize<LancamentoCaixa>(history.Single(x=>x.Operacao=="Corrigido").AntesJson!)!.Valor);
    }
    [Fact]
    public async Task Closing_races_with_creation_and_moving_dates_without_late_writes()
    {
        await Configure(); var september = await Create(Entry(date:September));
        var summary = await Summary(August);
        async Task<bool> Write(Func<Task> action) { try { await action(); return true; } catch(CaixaConflictException) { return false; } }
        await Task.WhenAll(Write(async () => { await Run(s => s.CloseAsync(August, new(summary.Versao,"Fechar"),autor,default)); }), Write(async () => { await Create(Entry()); }), Write(async () => { await Run(s => s.UpdateAsync(september.Id,new(Entry(date:August),1,"Mover data"),autor,default)); }));
        var after = await Summary(August); if (!after.Fechado) await Close(August);
        var snapshot = await Run(s => s.FechamentoAsync(August,default));
        await Assert.ThrowsAsync<CaixaConflictException>(() => Create(Entry()));
        var rows = await Run(s => s.ListAsync(new(Ano:2026,Mes:8),default)); Assert.Equal(rows.Items.Sum(x=>x.Lancamento.Valor),snapshot!.Receitas);
    }
    [Fact]
    public async Task Sequential_empty_closing_and_compensatory_adjustment_preserve_snapshots()
    {
        await Configure(); var original = await Create(Entry()); var first = await Close(August); await Close(September);
        await Assert.ThrowsAsync<CaixaConflictException>(() => Close(October));
        var adjust = new AjusteCommand(1,"Valor correto 200",false,Entry(200,date:October));
        async Task<bool> Adjust(string key) { try { await Run(s=>s.AdjustAsync(original.Id,adjust,autor,key,default)); return true; } catch(CaixaConflictException) { return false; } }
        Assert.Single(await Task.WhenAll(Adjust("a"),Adjust("b")),x=>x);
        var current = await Summary(October); Assert.Equal(200, current.Receitas); Assert.Equal(250,current.Despesas); Assert.Equal(300,current.SaldoFinal);
        Assert.Equal(first, await Run(s=>s.FechamentoAsync(August,default)));
        // This clock is frozen: tied UTC timestamps are sorted by UUID, not operation order.
        var audit = await Run(s=>s.HistoricoAsync(original.Id,default));
        Assert.Equal(2, audit.Count);
        Assert.Single(audit, x => x.Operacao == "Criado");
        Assert.Single(audit, x => x.Operacao == "Ajustado");
        Assert.True((await Run(s=>s.GetAsync(original.Id,default))).Revertido);
    }
    [Fact]
    public async Task Invalid_precision_dates_sequence_and_aggregate_overflow_roll_back()
    {
        await Configure(balance:-100); Assert.Equal(-100,(await Summary(August)).SaldoFinal);
        foreach(var value in new[]{0m,-1m,1.001m,CaixaRules.Limite+0.01m}) await Assert.ThrowsAsync<ArgumentException>(()=>Create(Entry(value)));
        await Assert.ThrowsAsync<ArgumentException>(()=>Create(Entry(date:new DateOnly(2026,10,2)))); // UTC Oct 2 is operational Oct 1.
        await Assert.ThrowsAsync<ArgumentException>(()=>Create(Entry(date:new DateOnly(2026,7,31))));
        await Assert.ThrowsAsync<CaixaConflictException>(()=>Close(September));
        await Create(Entry(CaixaRules.Limite)); await Assert.ThrowsAsync<ArgumentException>(()=>Create(Entry(1)));
        Assert.Equal(1,(await Run(s=>s.ListAsync(new(),default))).TotalItems);
        await using var scope=services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>(); Assert.Equal(1,await db.Set<AuditoriaLancamento>().CountAsync()); Assert.Equal(1,await db.Set<IdempotenciaFinanceira>().CountAsync());
    }
    [Fact]
    public async Task Initial_configuration_has_no_artificial_income_and_locks_after_use()
    {
        var config = await Configure(); Assert.Equal(7,(await Run(s=>s.CategoriasAsync(default))).Count);
        Assert.Equal(0,(await Run(s=>s.ListAsync(new(),default))).TotalItems);
        await Run(s=>s.ConfigureAsync(new(August,200,config.Versao),autor,default)); await Close(August);
        await Assert.ThrowsAsync<CaixaConflictException>(()=>Run(s=>s.ConfigureAsync(new(August,300,2),autor,default)));
    }
    [Fact]
    public async Task Inactive_references_are_preserved_but_not_newly_selectable()
    {
        await Configure(); Guid pessoa;
        await using(var scope=services.CreateAsyncScope()) { var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>(); var p=Pessoa.Criar(TipoPessoa.Fisica,"Cliente",null,DateTimeOffset.UtcNow); db.Add(p); await db.SaveChangesAsync(); pessoa=p.Id; }
        var command=Entry() with {PessoaId=pessoa}; var entry=await Create(command);
        await using(var scope=services.CreateAsyncScope()) { var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>(); await db.Pessoas.Where(x=>x.Id==pessoa).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Ativo,false)); }
        await Run(s=>s.UpdateAsync(entry.Id,new(command with {Descricao="Descrição corrigida"},1,"Detalhar"),autor,default));
        Assert.Equal("Cliente",(await Run(s=>s.GetAsync(entry.Id,default))).PessoaNome);
        await Assert.ThrowsAsync<ArgumentException>(()=>Create(command));
    }
    [Fact]
    public async Task Outer_transaction_rollback_removes_entries_audit_idempotency_and_snapshot()
    {
        await Configure();
        await using(var scope=services.CreateAsyncScope()) { var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>(); await using var tx=await db.Database.BeginTransactionAsync(); var s=scope.ServiceProvider.GetRequiredService<IFinancialRepository>(); await s.CreateAsync(Entry(),autor,"rollback",default); var summary=await s.ResumoAsync(August,default); await s.CloseAsync(August,new(summary.Versao,"Rollback"),autor,default); }
        await using var check=services.CreateAsyncScope(); var context=check.ServiceProvider.GetRequiredService<GenSWDbContext>(); Assert.Empty(await context.Set<LancamentoCaixa>().ToListAsync()); Assert.Empty(await context.Set<AuditoriaLancamento>().ToListAsync()); Assert.Empty(await context.Set<FechamentoCaixa>().ToListAsync()); Assert.Empty(await context.Set<IdempotenciaFinanceira>().ToListAsync());
    }
    [Fact]
    public async Task Concurrent_closings_and_identical_adjustment_retries_publish_once()
    {
        await Configure(); var original=await Create(Entry()); var summary=await Summary(August);
        async Task<bool> CloseOnce(){try{await Run(s=>s.CloseAsync(August,new(summary.Versao,"Concorrente"),autor,default));return true;}catch(CaixaConflictException){return false;}}
        Assert.Single(await Task.WhenAll(CloseOnce(),CloseOnce()),x=>x);
        var command=new AjusteCommand(1,"Compensar",false);
        var results=await Task.WhenAll(Run(s=>s.AdjustAsync(original.Id,command,autor,"adjust-repeat",default)),Run(s=>s.AdjustAsync(original.Id,command,autor,"adjust-repeat",default)));
        Assert.Equal(results[0].Reversao!.Id,results[1].Reversao!.Id);
        Assert.Equal(2,(await Run(s=>s.ListAsync(new(),default))).TotalItems);
    }
    [Fact]
    public async Task Failure_in_audit_or_snapshot_rolls_back_the_entire_operation()
    {
        await Configure();
        await using(var scope=services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fin_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected financial failure'; END; $$; CREATE TRIGGER fin_fail_audit BEFORE INSERT ON \"AuditoriasLancamento\" FOR EACH ROW EXECUTE FUNCTION fin_fail();");
        }
        await Assert.ThrowsAsync<DbUpdateException>(()=>Create(Entry(),"failure"));
        Assert.Equal(0,(await Run(s=>s.ListAsync(new(),default))).TotalItems);
        await using(var scope=services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
            Assert.Empty(await db.Set<MesCaixa>().ToArrayAsync());Assert.Empty(await db.Set<IdempotenciaFinanceira>().ToArrayAsync());
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fin_fail_audit ON \"AuditoriasLancamento\"; CREATE TRIGGER fin_fail_snapshot BEFORE INSERT ON \"FechamentosCaixa\" FOR EACH ROW EXECUTE FUNCTION fin_fail();");
        }
        await Create(Entry()); await Assert.ThrowsAsync<DbUpdateException>(()=>Close(August)); Assert.False((await Summary(August)).Fechado);
        await using var check=services.CreateAsyncScope(); Assert.Empty(await check.ServiceProvider.GetRequiredService<GenSWDbContext>().Set<FechamentoCaixa>().ToArrayAsync());
    }
    [Fact]
    public async Task Bounded_lock_timeout_returns_conflict_without_writes()
    {
        await Configure();
        await using var scope=services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>(); await using var tx=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(37620261002)");
        await Assert.ThrowsAsync<CaixaConflictException>(()=>Create(Entry()));
        Assert.Empty(await db.Set<LancamentoCaixa>().ToArrayAsync());
    }
    [Fact]
    public async Task Additive_migration_on_existing_base_does_not_backfill_or_change_people()
    {
        await using var scope=services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
        var migrator=db.GetService<IMigrator>(); await migrator.MigrateAsync("20261002133435_AddGenealogyTraversalIndex");
        var person=Pessoa.Criar(TipoPessoa.Fisica,"Histórico preservado",null,DateTimeOffset.UtcNow); db.Add(person); await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); var before=System.Text.Json.JsonSerializer.Serialize(await db.Pessoas.SingleAsync());
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear(); Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(await db.Pessoas.SingleAsync()));
        Assert.Equal(7,await db.Set<CategoriaFinanceira>().CountAsync()); Assert.Empty(await db.Set<LancamentoCaixa>().ToArrayAsync()); Assert.Empty(await db.Set<ConfiguracaoCaixa>().ToArrayAsync());
        await db.Database.MigrateAsync(); Assert.Equal(7,await db.Set<CategoriaFinanceira>().CountAsync());
    }
}
