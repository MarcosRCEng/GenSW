using System.Net;
using System.Net.Http.Json;
using GenSW.Application.EggBridge;
using GenSW.Application.Inventory;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

#pragma warning disable EF1002 // Fault triggers are scoped to isolated fixtures with generated identifiers.
public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_05_historical_association_after_consumption_does_not_restore_stock()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId, "5"); var eggs = await Eggs(2);
            var movement = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            await Success<System.Text.Json.JsonElement>(await Post(client, "saidas-manuais", Movement(m, user.UserId, "5", 1), inventory: true));
            var scope = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(movement.Id, "5", user.UserId, Evidence, Evidence)));
            var before = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.CountAsync());
            var preview = await Preview(client, Association(scope.Id, user.UserId, proof.Id, eggs, 1));
            Assert.True(preview.PodeConfirmar); await Confirm(client, preview);
            Assert.Equal("0", (await Balance(client, m))!.Quantidade);
            Assert.Equal(before, await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.CountAsync()));
        }
    }

    [Fact]
    public async Task F03_06_only_original_integer_un_manual_entries_are_eligible_proofs()
    {
        var (client, user) = await Session(true); using (client)
        {
            var kg = await Stock(client, user.UserId, "5", "kg");
            var kgMove = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == kg.Lot.Id));
            using var badUnit = await Post(client, "provas", new EggProofCommand(kgMove.Id, "5", user.UserId, Evidence, Evidence));
            Assert.True(badUnit.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
            var m = await Stock(client, user.UserId, "5"); var eggs = await Eggs(1);
            await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var move = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            await Error(await Post(client, "provas", new EggProofCommand(move.Id, "1.5", user.UserId, Evidence, Evidence)), HttpStatusCode.BadRequest);
            using var over = await Post(client, "provas", new EggProofCommand(move.Id, "6", user.UserId, Evidence, Evidence));
            Assert.True(over.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
            Assert.Equal("5", (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_07_two_independent_writers_share_one_lifetime_identity_and_one_physical_effect()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs));
            var replies = await Race(() => Post(client, "confirmacoes", new EggConfirmCommand(preview.Token)), () => Post(client, "confirmacoes", new EggConfirmCommand(preview.Token)));
            try
            {
                Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Created);
                Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
                Assert.Equal("1", (await Balance(client, m))!.Quantidade);
                Assert.Equal(1, await Factory.ExecuteDbContextAsync(db => db.Set<PonteProducaoOvo>().CountAsync(x => x.ProducaoOvoId == eggs[0].Id)));
            }
            finally { foreach (var reply in replies) reply.Dispose(); }
        }
    }

    [Theory]
    [InlineData("GruposPonteOvos")]
    [InlineData("PontesProducaoOvos")]
    [InlineData("ComandosPonteOvos")]
    [InlineData("MovimentosEstoque")]
    public async Task F03_13_failure_before_commit_rolls_back_all_facts_and_same_key_can_retry(string table)
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs)); var key = Unique(); var before = await Snapshot();
            var function = "EggBridgeFault_" + Unique();
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"CREATE FUNCTION \"{function}\"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Synthetic isolated F03 failure' USING ERRCODE='P0001'; END $$; CREATE TRIGGER \"{function}\" BEFORE INSERT ON \"{table}\" FOR EACH ROW EXECUTE FUNCTION \"{function}\"();"));
            try
            {
                await Error(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token), key), HttpStatusCode.InternalServerError);
                Assert.Equal(before, await Snapshot());
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"DROP TRIGGER \"{function}\" ON \"{table}\"; DROP FUNCTION \"{function}\"();"));
            }
            var group = await Confirm(client, preview, key); Assert.Single(group.Pontes);
            Assert.Equal("1", (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_11_15_dedicated_lot_rejects_extra_receipt_and_later_metadata_revision_blocks_compensation()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var group = await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            var before = await Snapshot();
            await Error(await Post(client, "entradas", Movement(m, user.UserId, "1", 1), inventory: true), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
            await Success<LoteView>(await Post(client, $"lotes/{m.Lot.Id}/bloqueio", new InventoryStateCommand(m.Lot.Revisao, "Alteração posterior sintética", Evidence), inventory: true));
            var blocked = await Success<EggTreatmentPreview>(await Post(client, $"grupos/{group.Grupo.Id}/previas-tratamento", new EggTreatmentCommand("Compensacao", user.UserId, Evidence, Evidence, true)));
            Assert.False(blocked.PodeConfirmar); Assert.NotEmpty(blocked.Impedimentos);
            await Error(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(blocked.Token)), HttpStatusCode.Conflict);
            Assert.Equal("1", (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_13_association_slot_failure_rolls_back_capacity_and_same_key_can_retry()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId, "5"); var eggs = await Eggs(1);
            var move = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            var scope = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(move.Id, "5", user.UserId, Evidence, Evidence)));
            var preview = await Preview(client, Association(scope.Id, user.UserId, proof.Id, eggs, 1)); var key = Unique(); var before = await Snapshot();
            var function = "EggSlotFault_" + Unique();
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"CREATE FUNCTION \"{function}\"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Synthetic isolated slot failure' USING ERRCODE='P0001'; END $$; CREATE TRIGGER \"{function}\" BEFORE INSERT ON \"AlocacoesProvaOvos\" FOR EACH ROW EXECUTE FUNCTION \"{function}\"();"));
            try
            {
                await Error(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token), key), HttpStatusCode.InternalServerError);
                Assert.Equal(before, await Snapshot());
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"DROP TRIGGER \"{function}\" ON \"AlocacoesProvaOvos\"; DROP FUNCTION \"{function}\"();"));
            }
            Assert.Single((await Confirm(client, preview, key)).Pontes);
            Assert.Equal("5", (await Balance(client, m))!.Quantidade);
            Assert.Single(await Factory.ExecuteDbContextAsync(db => db.Set<AlocacaoProvaOvos>().Where(x => x.ProvaId == proof.Id).ToArrayAsync()));
        }
    }

    [Fact]
    public async Task F03_12_20_tampered_preview_and_payload_mismatch_cannot_create_any_second_effect()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs[0])); var before = await Snapshot();
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token + "tampered")), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
            var other = await Preview(client, Entry(m, scope.Id, user.UserId, eggs[1]));
            var key = Unique(); await Confirm(client, preview, key);
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(other.Token), key), HttpStatusCode.Conflict);
            Assert.Equal("1", (await Balance(client, m))!.Quantidade);
        }
    }

    private async Task<HttpResponseMessage[]> Race(Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second)
    {
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Factory.ExecuteDbContextAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413,421)"); locked.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync();
        });
        Task<HttpResponseMessage>? one = null, two = null;
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10)); one = first(); two = second();
            var deadline = DateTimeOffset.UtcNow.AddSeconds(4); var waiting = false;
            while (DateTimeOffset.UtcNow < deadline)
            {
                var count = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND query LIKE '%413%421%'").SingleAsync());
                if (count >= 2) { waiting = true; break; }
                if (one.IsCompleted || two.IsCompleted) break;
                await Task.Delay(20);
            }
            Assert.True(waiting, "Both independent F03 writers must be observed waiting before releasing the barrier.");
        }
        finally { release.TrySetResult(); await holder; }
        return await Task.WhenAll(one!, two!).WaitAsync(TimeSpan.FromSeconds(15));
    }
}
