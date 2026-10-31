using System.Text.Json.Nodes;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task F03_14_18_sql_compensation_rejects_incoherent_or_incomplete_quantity_snapshot(bool missingAcceptanceFlag)
    {
        var (client, user) = await Session(true); using (client)
        {
            var material = await Stock(client, user.UserId); var eggs = await Eggs(2);
            var scope = await Scope(client, material, user.UserId, eggs);
            var detail = await Confirm(client, await Preview(client, Entry(material, scope.Id, user.UserId, eggs)));
            var before = await Snapshot();
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => Factory.ExecuteDbContextAsync(async db =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                var original = await db.MovimentosEstoque.AsNoTracking().SingleAsync(x => x.Id == detail.Grupo.MovimentoEstoqueId);
                var position = await db.PosicoesEstoque.SingleAsync(x => x.LoteId == material.Lot.Id && x.LocalId == material.Local.Id);
                var ev = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
                var quantity = new PhysicalQuantity("2", "un", "2", "2", "0", "un", null, null, null, false);
                var snapshot = JsonNode.Parse(InventoryJson.Write(new
                {
                    Pedido = new EggTreatmentCommand("Compensacao", user.UserId, Evidence, Evidence, true),
                    F03 = new { GrupoId = detail.Grupo.Id },
                    Compensacao = new { MovimentoOriginalId = original.Id },
                    Previa = new { Quantidade = quantity }
                }))!;
                var physical = snapshot["previa"]!["quantidade"]!.AsObject();
                if (missingAcceptanceFlag) physical.Remove("exigeAceite");
                else physical["calculada"] = "3";
                var serialized = snapshot.ToJsonString();
                var sequence = await db.Database.SqlQueryRaw<long>("SELECT nextval('\"SequenciaEstoque\"') AS \"Value\"").SingleAsync();
                db.Add(new EventoEstoque(ev, sequence, "CompensacaoEntradaOvos", user.UserId, user.UserId, now,
                    InventoryRules.Today(TimeProvider.System), null, Evidence, Evidence, original.EventoId, EggBridgeService.Algorithm, serialized));
                position.SetBalance(0, position.Revisao);
                db.Add(new MovimentoEstoque(Guid.NewGuid(), ev, 1, original.LoteId, original.LocalId, original.ItemId,
                    "un", "Saida", 2, "2", "un", "0", 2, 0, original.ConversaoItemId, original.PerfilNutricionalId,
                    serialized, "CompensacaoEntradaOvos"));
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }));
            var postgres = PostgresFailure(failure);
            Assert.NotNull(postgres); Assert.Equal("23514", postgres!.SqlState);
            Assert.Contains("quantity snapshot", postgres.MessageText);
            Assert.Equal(before, await Snapshot()); Assert.Equal("2", (await Balance(client, material))!.Quantidade);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task F03_09_18_sql_decision_rejects_forged_hash_or_stale_observed_timestamp(bool staleTimestamp)
    {
        var (client, user) = await Session(true); using (client)
        {
            var material = await Stock(client, user.UserId); var eggs = await Eggs(1);
            var originalScope = await Scope(client, material, user.UserId, eggs);
            var before = await Snapshot();
            var scopeCount = await Factory.ExecuteDbContextAsync(db => db.Set<RecorteConciliacaoOvos>().CountAsync(x => x.ItemId == material.Item.Id));
            var decisionCount = await Factory.ExecuteDbContextAsync(db => db.Set<DecisaoConciliacaoOvo>().CountAsync(x => x.ProducaoOvoId == eggs[0].Id));
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => Factory.ExecuteDbContextAsync(async db =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                var source = await db.ProducoesOvos.AsNoTracking().SingleAsync(x => x.Id == eggs[0].Id);
                var scope = originalScope with { Id = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow, OrdemAprovacao = 0 };
                var hash = EggBridgeService.SourceHash(source);
                var forgedHash = (hash[0] == 'A' ? "B" : "A") + hash[1..];
                db.AddRange(scope, new DecisaoConciliacaoOvo(Guid.NewGuid(), scope.Id, source.Id, "Pendente", false,
                    Evidence, staleTimestamp ? hash : forgedHash, staleTimestamp ? source.UpdatedAtUtc.AddSeconds(-1) : source.UpdatedAtUtc));
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }));
            var postgres = PostgresFailure(failure);
            Assert.NotNull(postgres); Assert.Equal("23514", postgres!.SqlState);
            Assert.Contains("canonical observed source", postgres.MessageText);
            Assert.Equal(before, await Snapshot());
            Assert.Equal(scopeCount, await Factory.ExecuteDbContextAsync(db => db.Set<RecorteConciliacaoOvos>().CountAsync(x => x.ItemId == material.Item.Id)));
            Assert.Equal(decisionCount, await Factory.ExecuteDbContextAsync(db => db.Set<DecisaoConciliacaoOvo>().CountAsync(x => x.ProducaoOvoId == eggs[0].Id)));
        }
    }
}
