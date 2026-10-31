using System.Data;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

#pragma warning disable EF1002 // Generated names are fixture-only identifiers, never runtime SQL input.
public sealed partial class EggBridgeApiTests
{
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    public async Task F03_08_18_noncooperative_sql_writers_cannot_overallocate_last_historical_slot(IsolationLevel isolation)
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId, "2"); var eggs = await Eggs(3);
            var movement = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            var scope = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(movement.Id, "2", user.UserId, Evidence, Evidence)));
            var first = await Confirm(client, await Preview(client, Association(scope.Id, user.UserId, proof.Id, [eggs[0]], 1)));
            var barrier = "f03_slot_" + Unique();
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var holder = Factory.ExecuteDbContextAsync(async db =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await db.Set<AncoraConciliacaoOvos>().FromSqlInterpolated($"SELECT * FROM \"AncorasConciliacaoOvos\" WHERE \"ItemId\"={m.Item.Id} FOR UPDATE").AsNoTracking().SingleAsync();
                held.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync();
            });
            async Task<string> Writer(GenSW.Domain.Animals.ProducaoOvo source)
            {
                try
                {
                    return await Factory.ExecuteDbContextAsync(async db =>
                    {
                        await using var tx = await db.Database.BeginTransactionAsync(isolation);
                        await db.Database.ExecuteSqlRawAsync($"SET LOCAL application_name='{barrier}'");
                        // Establish an RR snapshot before the contested tuple write.
                        await db.Set<AncoraConciliacaoOvos>().AsNoTracking().Where(x => x.ItemId == m.Item.Id).Select(x => x.Revisao).SingleAsync();
                        var persisted = await db.ProducoesOvos.AsNoTracking().SingleAsync(x => x.Id == source.Id);
                        var group = first.Grupo with { Id = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow };
                        var bridge = new PonteProducaoOvo(Guid.NewGuid(), source.Id, group.Id, EggBridgeService.SourceSnapshot(persisted),
                            EggBridgeService.SourceHash(persisted), persisted.UpdatedAtUtc, EggBridgeService.Algorithm);
                        db.AddRange(group, bridge, new AlocacaoProvaOvos(Guid.NewGuid(), bridge.Id, proof.Id, movement.Id, 2),
                            new ComandoPonteOvos(Guid.NewGuid(), "Associacao", Unique(), new string('a', 64), user.UserId,
                                group.Id, true, 201, Root + "grupos/" + group.Id, InventoryJson.Write(new { Grupo = group }), DateTimeOffset.UtcNow));
                        await db.SaveChangesAsync(); await tx.CommitAsync(); return "committed";
                    });
                }
                catch (Exception e) when (PostgresFailure(e) is not null) { return PostgresFailure(e)!.SqlState; }
            }
            Task<string>? one = null, two = null;
            try
            {
                await held.Task.WaitAsync(TimeSpan.FromSeconds(10)); one = Writer(eggs[1]); two = Writer(eggs[2]);
                var deadline = DateTimeOffset.UtcNow.AddSeconds(5); var waiting = false;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    var count = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND application_name={barrier} AND wait_event_type='Lock'").SingleAsync());
                    if (count == 2) { waiting = true; break; }
                    if (one.IsCompleted || two.IsCompleted) break;
                    await Task.Delay(20);
                }
                Assert.True(waiting, "Both SQL writers must be observed waiting on row/transaction locks, without any application advisory lock.");
            }
            finally { release.TrySetResult(); await holder; }
            var outcomes = await Task.WhenAll(one!, two!).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Single(outcomes, x => x == "committed");
            Assert.Single(outcomes, x => x is "23505" or "40001");
            Assert.Equal(2, await Factory.ExecuteDbContextAsync(db => db.Set<AlocacaoProvaOvos>().CountAsync(x => x.ProvaId == proof.Id)));
            Assert.Equal("2", (await Balance(client, m))!.Quantidade);
        }
    }

    private static PostgresException? PostgresFailure(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres;
        return null;
    }

    [Fact]
    public async Task F03_18_sql_cannot_edit_or_delete_lifetime_facts_and_cannot_prove_bridge_root_as_manual()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var group = await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            var before = await Snapshot();
            var bridge = Assert.Single(group.Pontes);
            var edits = new FormattableString[] {
                $"DELETE FROM \"PontesProducaoOvos\" WHERE \"Id\"={bridge.Id}",
                $"UPDATE \"PontesProducaoOvos\" SET \"HashOrigem\"={new string('a', 64)} WHERE \"Id\"={bridge.Id}",
                $"DELETE FROM \"GruposPonteOvos\" WHERE \"Id\"={group.Grupo.Id}",
                $"UPDATE \"RecortesConciliacaoOvos\" SET \"Motivo\"='reescrita' WHERE \"Id\"={scope.Id}",
                $"UPDATE \"AncorasConciliacaoOvos\" SET \"Revisao\"=0 WHERE \"ItemId\"={m.Item.Id}"
            };
            foreach (var edit in edits)
            {
                var failure = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync(edit)));
                Assert.Equal("23514", failure.SqlState); Assert.Equal(before, await Snapshot());
            }
            await Error(await Post(client, "provas", new EggProofCommand(group.Grupo.MovimentoEstoqueId, "1", user.UserId, Evidence, Evidence)), System.Net.HttpStatusCode.Conflict);
            await Factory.ExecuteDbContextAsync(async db =>
            {
                db.Add(new ProvaEntradaOvos(Guid.NewGuid(), group.Grupo.MovimentoEstoqueId, group.Grupo.EventoId, m.Item.Id, 1, Evidence, Evidence, user.UserId, user.UserId, DateTimeOffset.UtcNow));
                var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal("23514", Assert.IsType<PostgresException>(failure.InnerException).SqlState);
            });
            Assert.Equal(before, await Snapshot());
        }
    }
}
