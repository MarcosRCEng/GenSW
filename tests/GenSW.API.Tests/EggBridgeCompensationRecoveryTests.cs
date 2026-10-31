using System.Net;
using System.Net.Http.Json;
using GenSW.Application.EggBridge;
using GenSW.Application.Inventory;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

#pragma warning disable EF1002 // Generated fixture-only trigger and sequence identifiers.
public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_13_compensation_failure_after_debit_and_terminal_lot_rolls_back_and_same_key_retries()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2); var scope = await Scope(client, m, user.UserId, eggs);
            var group = await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            var preview = await Success<EggTreatmentPreview>(await Post(client, $"grupos/{group.Grupo.Id}/previas-tratamento", new EggTreatmentCommand("Compensacao", user.UserId, Evidence, Evidence, true)));
            Assert.True(preview.PodeConfirmar);
            var before = await Snapshot(); var lotBefore = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{m.Lot.Id}"))!;
            var trigger = "EggCompFault_" + Unique(); var reached = "EggCompReached_" + Unique(); var key = Unique();
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"CREATE SEQUENCE \"{reached}\"; CREATE FUNCTION \"{trigger}\"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF EXISTS(SELECT 1 FROM \"PosicoesEstoque\" WHERE \"LoteId\"='{m.Lot.Id}' AND \"LocalId\"='{m.Local.Id}' AND \"Quantidade\"=0) AND EXISTS(SELECT 1 FROM \"LotesMateriais\" WHERE \"Id\"='{m.Lot.Id}' AND \"Situacao\"='Encerrado' AND \"Revisao\"={lotBefore.Revisao + 1}) AND EXISTS(SELECT 1 FROM \"TratamentosPonteOvos\" WHERE \"GrupoId\"='{group.Grupo.Id}' AND \"Tipo\"='Compensacao') THEN PERFORM nextval('\"{reached}\"'); END IF; RAISE EXCEPTION 'Synthetic late compensation failure' USING ERRCODE='P0001'; END $$; CREATE TRIGGER \"{trigger}\" BEFORE INSERT ON \"ComandosPonteOvos\" FOR EACH ROW WHEN (NEW.\"Operacao\"='Compensacao' AND NEW.\"GrupoId\"='{group.Grupo.Id}') EXECUTE FUNCTION \"{trigger}\"();"));
            try
            {
                await Error(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(preview.Token), key), HttpStatusCode.InternalServerError);
                // Sequences are deliberately nontransactional: this proves the
                // injected failure actually occurred after every late phase.
                Assert.True(await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<bool>($"SELECT is_called AS \"Value\" FROM \"{reached}\"").SingleAsync()));
                Assert.Equal(before, await Snapshot());
                var restored = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{m.Lot.Id}"))!;
                Assert.Equal(lotBefore.Situacao, restored.Situacao); Assert.Equal(lotBefore.Revisao, restored.Revisao);
                Assert.Equal("2", (await Balance(client, m))!.Quantidade);
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($"DROP TRIGGER \"{trigger}\" ON \"ComandosPonteOvos\"; DROP FUNCTION \"{trigger}\"(); DROP SEQUENCE \"{reached}\";"));
            }
            var result = await Success<EggBridgeDetail>(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(preview.Token), key));
            Assert.Single(result.Tratamentos, x => x.Tipo == "Compensacao");
            var terminal = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{m.Lot.Id}"))!;
            Assert.Equal("Encerrado", terminal.Situacao); Assert.Equal(lotBefore.Revisao + 1, terminal.Revisao);
            Assert.Equal("0", (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_15_transfer_out_and_back_keeps_balance_but_permanently_blocks_compensation()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2); var scope = await Scope(client, m, user.UserId, eggs);
            var group = await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            var other = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Local de retorno sintético"), inventory: true));
            var outbound = new InventoryMovementCommand(m.Lot.Id, "1", "un", user.UserId, Evidence,
                new(m.Item.Revisao, m.Lot.Revisao, OrigemLocal: m.Local.Revisao, DestinoLocal: other.Revisao, OrigemPosicao: 1, DestinoPosicao: 0),
                OrigemLocalId: m.Local.Id, DestinoLocalId: other.Id, Evidencia: Evidence, Destino: Evidence);
            await Success<System.Text.Json.JsonElement>(await Post(client, "transferencias", outbound, inventory: true));
            var returned = outbound with { OrigemLocalId = other.Id, DestinoLocalId = m.Local.Id,
                VersoesEsperadas = new(m.Item.Revisao, m.Lot.Revisao, OrigemLocal: other.Revisao, DestinoLocal: m.Local.Revisao, OrigemPosicao: 1, DestinoPosicao: 2) };
            await Success<System.Text.Json.JsonElement>(await Post(client, "transferencias", returned, inventory: true));
            Assert.Equal("2", (await Balance(client, m))!.Quantidade);
            var preview = await Success<EggTreatmentPreview>(await Post(client, $"grupos/{group.Grupo.Id}/previas-tratamento", new EggTreatmentCommand("Compensacao", user.UserId, Evidence, Evidence, true)));
            Assert.False(preview.PodeConfirmar); Assert.NotEmpty(preview.Impedimentos);
            var before = await Snapshot();
            await Error(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
        }
    }
}
