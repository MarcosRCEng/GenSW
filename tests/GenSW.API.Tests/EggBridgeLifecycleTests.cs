using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_02_03_12_explicit_partial_selection_preview_is_readonly_and_global_replay_returns_original_bytes()
    {
        var (admin, owner) = await Session(true); using (admin)
        {
            var m = await Stock(admin, owner.UserId); var eggs = await Eggs(3);
            var scope = await Scope(admin, m, owner.UserId, eggs, decisions: new Dictionary<Guid, string> { [eggs[1].Id] = "Pendente" });
            var before = await Snapshot();
            var all = await Preview(admin, Entry(m, scope.Id, owner.UserId, eggs));
            Assert.False(all.PodeConfirmar); Assert.Equal(3, all.Origens.Count);
            await Error(await Post(admin, "confirmacoes", new EggConfirmCommand(all.Token)), HttpStatusCode.Conflict);
            var preview = await Preview(admin, Entry(m, scope.Id, owner.UserId, eggs[0], eggs[2]));
            Assert.Equal(before, await Snapshot()); Assert.True(preview.PodeConfirmar);
            Assert.Equal("2", preview.Quantidade); Assert.Equal("2", preview.DeltaEstoque); Assert.Equal("un", preview.Unidade);
            Assert.Equal(2, preview.Origens.Count); Assert.DoesNotContain(preview.Origens, x => x.ProducaoOvoId == eggs[1].Id);
            var key = Unique(); using var committed = await Post(admin, "confirmacoes", new EggConfirmCommand(preview.Token), key);
            Assert.Equal(HttpStatusCode.Created, committed.StatusCode); Assert.NotNull(committed.Headers.Location);
            var original = await committed.Content.ReadAsStringAsync(); var detail = GenSW.Domain.Inventory.InventoryJson.Read<EggBridgeDetail>(original);
            Assert.Equal(2, detail.Pontes.Count); Assert.Equal("2", (await Balance(admin, m))!.Quantidade);
            Assert.Equal(2, await Factory.ExecuteDbContextAsync(db => db.Set<PonteProducaoOvo>().CountAsync(x => x.GrupoId == detail.Grupo.Id)));
            await Factory.ExecuteDbContextAsync(async db =>
            {
                var egg = await db.ProducoesOvos.SingleAsync(x => x.Id == eggs[0].Id);
                egg.Atualizar(egg.DataPostura, 44m, "Edição posterior sintética", DateTimeOffset.UtcNow); await db.SaveChangesAsync();
            });
            var (other, _) = await Session(); using (other)
            {
                using var replay = await Post(other, "confirmacoes", new EggConfirmCommand(preview.Token), key);
                Assert.Equal(committed.StatusCode, replay.StatusCode); Assert.Equal(committed.Headers.Location, replay.Headers.Location);
                Assert.Equal(original, await replay.Content.ReadAsStringAsync()); Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
                using var recovered = await other.GetAsync(Root + "comandos/Entrada/" + key);
                Assert.Equal(committed.StatusCode, recovered.StatusCode); Assert.Equal(committed.Headers.Location, recovered.Headers.Location);
                Assert.Equal(original, await recovered.Content.ReadAsStringAsync());
            }
            var current = (await admin.GetFromJsonAsync<EggBridgeDetail>(Root + "grupos/" + detail.Grupo.Id))!;
            Assert.Equal(detail.Pontes[0].SnapshotJson, current.Pontes.Single(x => x.Id == detail.Pontes[0].Id).SnapshotJson);
            Assert.Contains(current.OrigensAtuais, x => x.ProducaoOvoId == eggs[0].Id && x.Impedimento is not null);
            Assert.Equal("2", (await Balance(admin, m))!.Quantidade);
            await Error(await Post(admin, "confirmacoes", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
            Assert.Equal("2", (await Balance(admin, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_01_02_06_pending_collective_duplicate_and_cycle_id_never_create_stock()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2);
            var scope = await Scope(client, m, user.UserId, eggs, "Pendente", false);
            var before = await Snapshot(); var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs));
            Assert.False(preview.PodeConfirmar); Assert.All(preview.Origens, x => Assert.NotNull(x.Impedimento));
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
            var duplicate = Entry(m, scope.Id, user.UserId, eggs[0], eggs[0]);
            await Error(await Post(client, "previas", duplicate), HttpStatusCode.BadRequest);
            // An actual cycle identity is never expanded into egg identities.
            var cycleId = await CycleId();
            var foreign = Entry(m, scope.Id, user.UserId, eggs[0]) with { Selecao = [new EggSelection(cycleId)] };
            var foreignResponse = await Post(client, "previas", foreign);
            Assert.True(foreignResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict);
            foreignResponse.Dispose(); Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task F03_04_05_16_manual_association_consumes_historical_capacity_with_zero_delta_and_invalidation_never_frees_slots()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId, "5"); var eggs = await Eggs(6);
            var movement = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            var scope = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(movement.Id, "5", user.UserId, Evidence, Evidence)));
            var physicalCount = await Factory.ExecuteDbContextAsync(db => db.EventosEstoque.CountAsync());
            var first = await Preview(client, Association(scope.Id, user.UserId, proof.Id, eggs.Take(2).ToArray(), 1));
            Assert.True(first.PodeConfirmar); Assert.Equal("0", first.DeltaEstoque);
            var firstGroup = await Confirm(client, first);
            var second = await Preview(client, Association(scope.Id, user.UserId, proof.Id, eggs.Skip(2).Take(3).ToArray(), 3));
            Assert.True(second.PodeConfirmar); await Confirm(client, second);
            Assert.Equal(physicalCount, await Factory.ExecuteDbContextAsync(db => db.EventosEstoque.CountAsync()));
            Assert.Equal("5", (await Balance(client, m))!.Quantidade);
            Assert.Equal(5, await Factory.ExecuteDbContextAsync(db => db.Set<AlocacaoProvaOvos>().CountAsync(x => x.ProvaId == proof.Id)));
            var treatment = await Success<EggTreatmentPreview>(await Post(client, $"grupos/{firstGroup.Grupo.Id}/previas-tratamento", new EggTreatmentCommand("Invalidacao", user.UserId, Evidence, Evidence)));
            Assert.True(treatment.PodeConfirmar); Assert.Equal("0", treatment.DeltaEstoque);
            await Success<EggBridgeDetail>(await Post(client, $"grupos/{firstGroup.Grupo.Id}/tratamentos", new EggConfirmCommand(treatment.Token)));
            await Error(await Post(client, "previas", Association(scope.Id, user.UserId, proof.Id, [eggs[5]], 1)), HttpStatusCode.Conflict);
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(first.Token)), HttpStatusCode.Conflict);
            Assert.Equal("5", (await Balance(client, m))!.Quantidade);
            Assert.Equal(physicalCount, await Factory.ExecuteDbContextAsync(db => db.EventosEstoque.CountAsync()));
        }
    }

    [Fact]
    public async Task F03_09_before_edit_invalidates_preview_even_when_persisted_timestamp_does_not_change()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs)); var before = await Snapshot();
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProducoesOvos\" SET \"PesoGramas\"=33 WHERE \"Id\"={eggs[0].Id}"));
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task F03_04_08_capacity_and_ordinals_support_fourteen_digits_without_materializing_unused_slots()
    {
        const string capacity = "99999999999999";
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId, capacity); var eggs = await Eggs(1);
            var move = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            var scope = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(move.Id, capacity, user.UserId, Evidence, Evidence)));
            var preview = await Preview(client, Association(scope.Id, user.UserId, proof.Id, eggs, 99999999999999L));
            Assert.True(preview.PodeConfirmar); Assert.Equal(capacity, preview.CapacidadeRestante); Assert.Equal("0", preview.DeltaEstoque);
            await Confirm(client, preview);
            var slot = Assert.Single(await Factory.ExecuteDbContextAsync(db => db.Set<AlocacaoProvaOvos>().Where(x => x.ProvaId == proof.Id).ToArrayAsync()));
            Assert.Equal(99999999999999L, slot.Ordinal); Assert.Equal(capacity, (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_14_compensation_is_exact_once_and_preserves_terminal_identity()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2); var scope = await Scope(client, m, user.UserId, eggs);
            var entry = await Preview(client, Entry(m, scope.Id, user.UserId, eggs)); var group = await Confirm(client, entry);
            var preview = await Success<EggTreatmentPreview>(await Post(client, $"grupos/{group.Grupo.Id}/previas-tratamento", new EggTreatmentCommand("Compensacao", user.UserId, "Erro de registro sintético comprovado", Evidence, true)));
            Assert.True(preview.PodeConfirmar); Assert.Equal("-2", preview.DeltaEstoque);
            var key = Unique(); var result = await Success<EggBridgeDetail>(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(preview.Token), key));
            Assert.Equal("0", (await Balance(client, m))!.Quantidade); Assert.Equal(2, result.Pontes.Count);
            Assert.Single(result.Tratamentos, x => x.Tipo == "Compensacao");
            var before = await Snapshot();
            await Success<EggBridgeDetail>(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(preview.Token), key));
            Assert.Equal(before, await Snapshot());
            await Error(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(entry.Token)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task F03_20_anonymous_common_and_revoked_admin_cannot_approve_or_replay_admin_facts()
    {
        using var anonymous = Factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Root + "candidatos")).StatusCode);
        var (admin, user) = await Session(true); using (admin)
        {
            var m = await Stock(admin, user.UserId); var (common, _) = await Session(); using (common)
            {
                await Error(await Post(common, "itens", new EggEnrollmentCommand(m.Item.Id, Evidence)), HttpStatusCode.Forbidden);
            }
            var key = Unique(); await Success<JsonElement>(await Post(admin, "itens", new EggEnrollmentCommand(m.Item.Id, Evidence), key));
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"AspNetUserRoles\" WHERE \"UserId\"={user.UserId}"));
            await Error(await Post(admin, "itens", new EggEnrollmentCommand(m.Item.Id, Evidence), key), HttpStatusCode.Forbidden);
        }
    }
}
