using System.Net;
using System.Net.Http.Json;
using GenSW.Application.EggBridge;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

#pragma warning disable EF1002 // Generated isolated fixture trigger/sequence names.
public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_09_source_update_waits_for_confirmation_share_lock_then_preserves_original_snapshot()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs));
            var barrierId = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);
            var trigger = "EggSourceBarrier_" + Unique(); var writerName = "f03_source_writer_" + Unique();
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"CREATE FUNCTION \"{trigger}\"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(501,{barrierId}); RETURN NEW; END $$; CREATE TRIGGER \"{trigger}\" BEFORE INSERT ON \"PontesProducaoOvos\" FOR EACH ROW WHEN (NEW.\"ProducaoOvoId\"='{eggs[0].Id}') EXECUTE FUNCTION \"{trigger}\"();"));
            var holder = Factory.ExecuteDbContextAsync(async db =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(501,{barrierId})");
                held.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync();
            });
            Task<HttpResponseMessage>? confirmation = null; Task? writer = null;
            try
            {
                await held.Task.WaitAsync(TimeSpan.FromSeconds(10)); confirmation = Post(client, "confirmacoes", new EggConfirmCommand(preview.Token));
                Assert.True(await WaitForLock("wait_event='advisory' AND query LIKE '%PontesProducaoOvos%'"));
                writer = Factory.ExecuteDbContextAsync(async db =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync();
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('application_name',{writerName},true)");
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProducoesOvos\" SET \"PesoGramas\"=99,\"UpdatedAtUtc\"=now() WHERE \"Id\"={eggs[0].Id}");
                    await tx.CommitAsync();
                });
                var deadline = DateTimeOffset.UtcNow.AddSeconds(3); var waiting = false;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    var count = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND application_name={writerName} AND wait_event_type='Lock'").SingleAsync());
                    if (count == 1) { waiting = true; break; }
                    if (writer.IsCompleted) break;
                    await Task.Delay(20);
                }
                Assert.True(waiting, "An actual independent UPDATE must wait for confirmation's FOR SHARE lock until its commit.");
            }
            finally
            {
                release.TrySetResult(); await holder;
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"DROP TRIGGER \"{trigger}\" ON \"PontesProducaoOvos\"; DROP FUNCTION \"{trigger}\"();"));
            }
            var original = await Success<EggBridgeDetail>(await confirmation!); await writer!;
            using var originalSnapshot = System.Text.Json.JsonDocument.Parse(Assert.Single(original.Pontes).SnapshotJson);
            Assert.Equal("10.00", originalSnapshot.RootElement.GetProperty("pesoGramas").GetString());
            var current = (await client.GetFromJsonAsync<EggBridgeDetail>(Root + "grupos/" + original.Grupo.Id))!;
            Assert.Equal(original.Pontes[0].SnapshotJson, Assert.Single(current.Pontes).SnapshotJson);
            Assert.NotNull(Assert.Single(current.OrigensAtuais).Impedimento); Assert.Equal("1", (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_20_responsible_deactivated_while_waiting_is_revalidated_before_any_fact()
    {
        var (client, user) = await Session(true); using (client)
        {
            var responsible = await Factory.SeedUserAsync("f03_responsible_" + Unique());
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, responsible.UserId, eggs));
            await WaitThenChange(client, preview, () => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AspNetUsers\" SET \"IsActive\"=false WHERE \"Id\"={responsible.UserId}")));
        }
    }

    [Theory]
    [InlineData("Item")]
    [InlineData("Local")]
    [InlineData("Lote")]
    public async Task F03_11_destination_inactivated_while_waiting_is_revalidated_before_any_fact(string resource)
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs));
            await WaitThenChange(client, preview, () => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync(resource switch {
                "Item" => $"UPDATE \"Itens\" SET \"Ativo\"=false,\"Revisao\"=\"Revisao\"+1,\"UpdatedAtUtc\"=now() WHERE \"Id\"={m.Item.Id}",
                "Local" => $"UPDATE \"LocaisEstoque\" SET \"Ativo\"=false,\"Revisao\"=\"Revisao\"+1,\"UpdatedAtUtc\"=now() WHERE \"Id\"={m.Local.Id}",
                _ => $"UPDATE \"LotesMateriais\" SET \"Ativo\"=false,\"Revisao\"=\"Revisao\"+1,\"UpdatedAtUtc\"=now() WHERE \"Id\"={m.Lot.Id}"
            })));
        }
    }

    private async Task WaitThenChange(HttpClient client, EggPreview preview, Func<Task> change)
    {
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Factory.ExecuteDbContextAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413,421)");
            held.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync();
        });
        Task<HttpResponseMessage>? confirmation = null; string? afterChange = null;
        try
        {
            await held.Task.WaitAsync(TimeSpan.FromSeconds(10)); confirmation = Post(client, "confirmacoes", new EggConfirmCommand(preview.Token));
            Assert.True(await WaitForLock("wait_event='advisory' AND query LIKE '%413%421%'"));
            await change(); afterChange = await Snapshot();
        }
        finally { release.TrySetResult(); await holder; }
        await Error(await confirmation!, HttpStatusCode.Conflict); Assert.Equal(afterChange, await Snapshot());
    }
}
