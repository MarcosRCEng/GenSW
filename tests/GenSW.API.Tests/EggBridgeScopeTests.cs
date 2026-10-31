using System.Net;
using GenSW.Application.EggBridge;
using GenSW.Application.Inventory;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_01_10_new_pending_decision_invalidates_same_source_preview_but_not_disjoint_source()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2);
            var original = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, original.Id, user.UserId, eggs[0]));
            await Scope(client, m, user.UserId, [eggs[0]], "Pendente", false, enroll: false);
            var before = await Snapshot();
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
            var superseded = await Preview(client, Entry(m, original.Id, user.UserId, eggs[0]));
            Assert.False(superseded.PodeConfirmar); Assert.NotNull(Assert.Single(superseded.Origens).Impedimento);
            var disjoint = await Preview(client, Entry(m, original.Id, user.UserId, eggs[1]));
            Assert.True(disjoint.PodeConfirmar);
            Assert.Equal(eggs[1].Id, Assert.Single((await Confirm(client, disjoint)).Pontes).ProducaoOvoId);
            Assert.False(await Factory.ExecuteDbContextAsync(db => db.Set<PonteProducaoOvo>().AnyAsync(x => x.ProducaoOvoId == eggs[0].Id)));
        }
    }

    [Fact]
    public async Task F03_18_sql_cannot_use_superseded_individual_evidence_from_an_older_scope()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId, "2"); var eggs = await Eggs(2);
            var move = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            var original = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(move.Id, "2", user.UserId, Evidence, Evidence)));
            var template = await Confirm(client, await Preview(client, Association(original.Id, user.UserId, proof.Id, [eggs[1]], 1)));
            await Scope(client, m, user.UserId, [eggs[0]], "Pendente", false, enroll: false);
            var before = await Snapshot();
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => Factory.ExecuteDbContextAsync(async db =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                var source = await db.ProducoesOvos.AsNoTracking().SingleAsync(x => x.Id == eggs[0].Id);
                var group = template.Grupo with { Id = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow };
                var bridge = new PonteProducaoOvo(Guid.NewGuid(), source.Id, group.Id, EggBridgeService.SourceSnapshot(source), EggBridgeService.SourceHash(source), source.UpdatedAtUtc, EggBridgeService.Algorithm);
                db.AddRange(group, bridge, new AlocacaoProvaOvos(Guid.NewGuid(), bridge.Id, proof.Id, move.Id, 2),
                    new ComandoPonteOvos(Guid.NewGuid(), "Associacao", Unique(), new string('b', 64), user.UserId, group.Id, true, 201, Root + "grupos/" + group.Id, InventoryJson.Write(new { Grupo = group }), DateTimeOffset.UtcNow));
                await db.SaveChangesAsync(); await tx.CommitAsync();
            }));
            Assert.Equal("23514", PostgresFailure(failure)?.SqlState);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task F03_06_17_original_un_receipt_remains_counted_when_conversion_metadata_was_not_applied()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1);
            var conversion = await Formulation(s => s.CreateConversionAsync(m.Item.Id,
                new("un", "kg", "0.01", Evidence, "Medição independente sintética", InventoryRules.Today(TimeProvider.System), Evidence, "Lote sintético", "Medido"), user.UserId, default));
            var linked = await Success<LoteView>(await Post(client, $"lotes/{m.Lot.Id}/referencias", new InventoryStateCommand(m.Lot.Revisao, Evidence, Evidence, ConversaoItemId: conversion.Id, Aplicabilidade: Evidence), inventory: true));
            m = m with { Lot = linked };
            await Success<System.Text.Json.JsonElement>(await Post(client, "entradas", Movement(m, user.UserId, "5", 0), inventory: true));
            var move = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            Assert.Equal(conversion.Id, move.ConversaoItemId); Assert.Equal("un", move.UnidadeDeclarada); Assert.Equal("5", move.QuantidadeDeclarada);
            var scope = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(move.Id, "5", user.UserId, Evidence, Evidence)));
            var preview = await Preview(client, Association(scope.Id, user.UserId, proof.Id, eggs, 1));
            Assert.True(preview.PodeConfirmar); Assert.Equal("0", preview.DeltaEstoque);
            await Confirm(client, preview); Assert.Equal("5", (await Balance(client, m))!.Quantidade);
        }
    }
}
