using System.Data;
using System.Net;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, false)]
    [InlineData(IsolationLevel.RepeatableRead, false)]
    [InlineData(IsolationLevel.ReadCommitted, true)]
    [InlineData(IsolationLevel.RepeatableRead, true)]
    public async Task F03_10_sql_receipt_protocol_serializes_with_bridge_in_both_orders(IsolationLevel isolation, bool bridgeFirst)
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var other = await AnotherDestination(client, user.UserId, m); var eggs = await Eggs(1);
            var scope = await Scope(client, m, user.UserId, eggs); var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs));
            if (bridgeFirst)
            {
                await Confirm(client, preview); var before = await Snapshot();
                var failure = await Assert.ThrowsAnyAsync<Exception>(() => SqlReceipt(other, user.UserId, scope.Id, isolation));
                var postgres = failure as PostgresException ?? (failure as DbUpdateException)?.InnerException as PostgresException;
                Assert.NotNull(postgres); Assert.Equal("23514", postgres!.SqlState);
                Assert.Equal(before, await Snapshot()); Assert.Equal("1", (await Balance(client, m))!.Quantidade);
            }
            else
            {
                var inserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var sql = SqlReceipt(other, user.UserId, scope.Id, isolation, inserted, release);
                Task<HttpResponseMessage>? confirmation = null;
                try
                {
                    await inserted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    confirmation = Post(client, "confirmacoes", new EggConfirmCommand(preview.Token));
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(3); var waiting = false;
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        var count = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND (query LIKE '%AncorasConciliacaoOvos%' OR query LIKE '%Itens%')").SingleAsync());
                        if (count >= 1) { waiting = true; break; }
                        if (confirmation.IsCompleted) break;
                        await Task.Delay(20);
                    }
                    Assert.True(waiting, "The API writer must be observed waiting on the SQL writer's item anchor before releasing its commit.");
                }
                finally { release.TrySetResult(); await sql; }
                await Error(await confirmation!, HttpStatusCode.Conflict);
                Assert.Equal("5", (await Balance(client, other))!.Quantidade);
                Assert.False(await Factory.ExecuteDbContextAsync(db => db.Set<PonteProducaoOvo>().AnyAsync(x => x.ProducaoOvoId == eggs[0].Id)));
            }
            Assert.Equal(1, (await Factory.ExecuteDbContextAsync(db => db.Set<AncoraConciliacaoOvos>().SingleAsync(x => x.ItemId == m.Item.Id))).Revisao);
        }
    }

    private Task SqlReceipt(Material m, Guid actor, Guid scope, IsolationLevel isolation,
        TaskCompletionSource? inserted = null, TaskCompletionSource? release = null,
        TaskCompletionSource? capturedSnapshot = null, TaskCompletionSource? allowWrite = null,
        string? writerApplication = null) => Factory.ExecuteDbContextAsync(async db =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(isolation);
        if (writerApplication is not null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('application_name',{writerApplication},true)");
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_locks WHERE pid=pg_backend_pid() AND locktype='advisory'").SingleAsync());
        await db.Set<GenSW.Domain.Catalog.Item>().AsNoTracking().Where(x => x.Id == m.Item.Id).Select(x => x.Revisao).SingleAsync();
        capturedSnapshot?.SetResult();
        if (allowWrite is not null) await allowWrite.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var ev = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var sequence = await db.Database.SqlQueryRaw<long>("SELECT nextval('\"SequenciaEstoque\"') AS \"Value\"").SingleAsync();
        var command = scope == Guid.Empty ? Movement(m, actor, "5", 0) : DistinctReceipt(m, actor, scope);
        var quantity = new PhysicalQuantity("5", "un", "5", "5", "0", "un", null, null, null, false);
        var snapshot = InventoryJson.Write(new { Pedido = command, Previa = new { Quantidade = quantity } });
        var position = PosicaoEstoque.Create(m.Lot.Id, m.Local.Id, "un"); position.SetBalance(5, 0);
        db.AddRange(position,
            new EventoEstoque(ev, sequence, "Entrada", actor, actor, now, InventoryRules.Today(TimeProvider.System), null, Evidence, Evidence, null, InventoryRules.Algorithm, snapshot),
            new MovimentoEstoque(Guid.NewGuid(), ev, 1, m.Lot.Id, m.Local.Id, m.Item.Id, "un", "Entrada", 5, "5", "un", "0", 0, 5, null, null, snapshot, "Entrada"),
            new ComandoEstoque(Guid.NewGuid(), actor, "Entrada", m.Lot.Id, Unique(), new string('b', 64), 201, null, "{}", ev, m.Lot.Id, now));
        if (scope != Guid.Empty) db.Add(new OrigemRecebimentoOvos(Guid.NewGuid(), ev, m.Item.Id, scope, Evidence, actor, now));
        await db.SaveChangesAsync();
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_locks WHERE pid=pg_backend_pid() AND locktype='advisory'").SingleAsync());
        inserted?.SetResult();
        if (release is not null) await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await tx.CommitAsync();
    });
}
