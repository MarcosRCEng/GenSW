using System.Data;
using System.Text.Json;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

#pragma warning disable EF1002 // Generated test-only trigger names and barrier integers.
public sealed partial class EggBridgeApiTests
{
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, false)]
    [InlineData(IsolationLevel.RepeatableRead, false)]
    [InlineData(IsolationLevel.ReadCommitted, true)]
    [InlineData(IsolationLevel.RepeatableRead, true)]
    public async Task F03_10_enrollment_boundary_serializes_with_preexisting_sql_writer_snapshot(IsolationLevel isolation, bool enrollmentFirst)
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId);
            if (!enrollmentFirst)
            {
                var inserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var sql = SqlReceipt(m, user.UserId, Guid.Empty, isolation, inserted, release);
                Task<HttpResponseMessage>? enrollment = null;
                try
                {
                    await inserted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    enrollment = Post(client, "itens", new EggEnrollmentCommand(m.Item.Id, Evidence));
                    Assert.True(await WaitForLock("(query LIKE '%Itens%' OR query LIKE '%AncorasConciliacaoOvos%')"), "Enrollment must wait for the writer that started before management existed.");
                }
                finally { release.TrySetResult(); await sql; }
                await Success<JsonElement>(await enrollment!);
                var anchor = await Factory.ExecuteDbContextAsync(db => db.Set<AncoraConciliacaoOvos>().SingleAsync(x => x.ItemId == m.Item.Id));
                var sequence = await Factory.ExecuteDbContextAsync(db => db.EventosEstoque.Where(x => db.MovimentosEstoque.Any(movement => movement.EventoId == x.Id && movement.ItemId == m.Item.Id)).Select(x => x.Sequencia).SingleAsync());
                Assert.Equal(0, anchor.Revisao); Assert.True(anchor.SequenciaInicial >= sequence);
                Assert.Equal("5", (await Balance(client, m))!.Quantidade);
                return;
            }

            var barrierId = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);
            var name = "EggEnrollmentBarrier_" + Unique(); var key = Unique(); var writer = "f03_enrollment_" + Unique();
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseHolder = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"CREATE FUNCTION \"{name}\"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(501,{barrierId}); RETURN NEW; END $$; CREATE TRIGGER \"{name}\" BEFORE INSERT ON \"ComandosPonteOvos\" FOR EACH ROW WHEN (NEW.\"Chave\"='{key}') EXECUTE FUNCTION \"{name}\"();"));
            var holder = Factory.ExecuteDbContextAsync(async db =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(501,{barrierId})");
                held.SetResult(); await releaseHolder.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync();
            });
            var receipt = SqlReceipt(m, user.UserId, Guid.Empty, isolation, capturedSnapshot: captured, allowWrite: allowWrite, writerApplication: writer);
            Task<HttpResponseMessage>? enrolling = null;
            try
            {
                await held.Task.WaitAsync(TimeSpan.FromSeconds(10)); await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
                enrolling = Post(client, "itens", new EggEnrollmentCommand(m.Item.Id, Evidence), key);
                Assert.True(await WaitForLock("wait_event='advisory' AND query LIKE '%ComandosPonteOvos%'"), "The enrollment must hold its item tuple while paused before its command commit.");
                allowWrite.SetResult();
                var deadline = DateTimeOffset.UtcNow.AddSeconds(3); var waiting = false;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    var count = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND application_name={writer} AND wait_event_type='Lock'").SingleAsync());
                    if (count == 1) { waiting = true; break; }
                    if (receipt.IsCompleted) break;
                    await Task.Delay(20);
                }
                Assert.True(waiting, "The uncooperative credit writer must wait on the enrolled item's tuple, despite its older snapshot.");
            }
            finally
            {
                allowWrite.TrySetResult(); releaseHolder.TrySetResult(); await holder;
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"DROP TRIGGER \"{name}\" ON \"ComandosPonteOvos\"; DROP FUNCTION \"{name}\"();"));
            }
            await Success<JsonElement>(await enrolling!);
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => receipt);
            Assert.Contains(PostgresFailure(failure)?.SqlState, new[] { "23514", "40001" });
            Assert.False(await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.AnyAsync(x => x.ItemId == m.Item.Id)));
            Assert.Equal(0, (await Factory.ExecuteDbContextAsync(db => db.Set<AncoraConciliacaoOvos>().SingleAsync(x => x.ItemId == m.Item.Id))).Revisao);
        }
    }

    private async Task<bool> WaitForLock(string predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var count = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND (" + predicate + ")").SingleAsync());
            if (count > 0) return true;
            await Task.Delay(20);
        }
        return false;
    }
}
