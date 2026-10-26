using System.Net;
using System.Net.Http.Json;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task T12_compensation_keeps_original_null_references_after_the_source_lot_gets_a_new_conversion()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            var original = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(m => m.LoteId == f.Input.Lot.Id && m.TipoEvento == "ProducaoConsumo"));
            Assert.Null(original.ConversaoItemId); Assert.Null(original.PerfilNutricionalId);

            var conversion = await Formulation(s => s.CreateConversionAsync(f.Input.Item.Id,
                new("kg", "L", "2", Evidence, "Medição sintética posterior", GenSW.Domain.Inventory.InventoryRules.Today(TimeProvider.System), Evidence, "Amostra posterior", "Medido"), user.UserId, default));
            await Success<LoteView>(await Post(client, $"lotes/{f.Input.Lot.Id}/referencias",
                new InventoryStateCommand(f.Input.Lot.Revisao, "Referência atual não altera fatos passados", Evidence, ConversaoItemId: conversion.Id, Aplicabilidade: Evidence), inventory: true));

            order = await Success<OrderView>(await Post(client, $"{order.Id}/reversao", new ReverseCommand(order.Revisao, true, "Erro de registro e contagem coerente", Evidence)));
            Assert.Equal("Revertida", order.Estado); Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade);
            var compensation = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(m => m.LoteId == f.Input.Lot.Id && m.TipoEvento == "ReversaoProducaoConsumo"));
            Assert.Null(compensation.ConversaoItemId); Assert.Null(compensation.PerfilNutricionalId);
            Assert.Equal(original.Quantidade, compensation.Quantidade); Assert.Equal("100", compensation.QuantidadeDeclarada); Assert.Equal("0", compensation.Residuo);
            using var snapshot = System.Text.Json.JsonDocument.Parse(compensation.SnapshotJson);
            Assert.Equal(original.Id, snapshot.RootElement.GetProperty("compensacao").GetProperty("movimentoOriginalId").GetGuid());
        }
    }

    [Fact]
    public async Task T10_T11_exceptional_irregular_fact_with_zero_output_is_total_loss_and_retains_irregularity_evidence()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            await Success<LoteView>(await Post(client, $"lotes/{f.Input.Lot.Id}/bloqueio", new InventoryStateCommand(f.Input.Lot.Revisao, "Mudança durante execução já consumida", Evidence), inventory: true));
            var command = Confirm(order, f, user.UserId, output: "0", loss: "100") with
            { DataObservada = GenSW.Domain.Inventory.InventoryRules.Today(TimeProvider.System), AptidaoNoFato = "Irregular" };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao-ocorrencia", command));
            Assert.Equal("Confirmada", order.Estado); Assert.Equal("PerdaTotal", order.Execucao!.Resultado);
            Assert.Empty(order.Execucao.LotesSaida); ReservationBalances(order, 100, 0, 100, 0);
            using var snapshot = System.Text.Json.JsonDocument.Parse(order.Execucao.SnapshotJson);
            Assert.Equal("Irregular", snapshot.RootElement.GetProperty("realizado").GetProperty("aptidaoNoFato").GetString());
            var occurrences = await client.GetFromJsonAsync<ProductionPage<System.Text.Json.JsonElement>>($"/api/v1/ordens-producao/{order.Id}/ocorrencias");
            Assert.Contains(occurrences!.Items, row => row.GetProperty("tipo").GetString() == "ConsumoIrregular");
            Assert.Equal("0", (await Balance(client, f.Input))!.Quantidade);
            Assert.False(await Factory.ExecuteDbContextAsync(db => db.LotesMateriais.AnyAsync(l => l.ItemId == f.Output.Id)));
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task T20_malformed_preview_collections_are_bad_requests_without_any_mutation(string inputs)
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            using var parsed = System.Text.Json.JsonDocument.Parse(inputs);
            var body = new { operacao = "inicio", inicio = new { versaoEsperada = order.Revisao, responsavelId = user.UserId, entradas = parsed.RootElement, motivo = Evidence } };
            var before = await Snapshot();
            await Error(await Post(client, $"{order.Id}/previas", body), HttpStatusCode.BadRequest);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task T01_two_draft_edits_from_one_revision_preserve_only_one_winner_and_no_stock_effect()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId);
            var create = new OrderCommand(Guid.NewGuid(), Unique(), f.Version.Id, "100", "kg", user.UserId);
            var draft = await Success<OrderView>(await Post(client, "", create), HttpStatusCode.Created);
            var before = (await Balance(client, f.Input))!;
            async Task<HttpResponseMessage> Edit(string code)
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/ordens-producao/{draft.Id}")
                { Content = JsonContent.Create(create with { Codigo = code, VersaoEsperada = draft.Revisao }) };
                request.Headers.Add("Idempotency-Key", Unique()); return await client.SendAsync(request);
            }
            var codes = new[] { Unique(), Unique() };
            var replies = await Race(() => Edit(codes[0]), () => Edit(codes[1]));
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            var current = (await Get(client, draft.Id))!;
            Assert.Equal(draft.Revisao + 1, current.Revisao); Assert.Contains(current.Codigo, codes);
            Assert.Equal("Rascunho", current.Estado); Assert.Empty(current.Reservas); Assert.Null(current.Execucao);
            Assert.Equal(before.Quantidade, (await Balance(client, f.Input))!.Quantidade);
            Assert.Equal(before.Revisao, (await Balance(client, f.Input))!.Revisao);
            foreach (var reply in replies) reply.Dispose();
        }
    }

    [Fact]
    public async Task T09_replacing_the_input_position_releases_original_reservation_and_consumes_a_separate_allocation()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId);
            var local = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Posição alternativa sintética"), inventory: true), HttpStatusCode.Created);
            var lot = await Success<LoteView>(await Post(client, "lotes", new LoteCommand(f.Input.Item.Id, Unique(), "Fixture alternativa", Evidence, user.UserId, ItemVersaoEsperada: f.Input.Item.Revisao), inventory: true), HttpStatusCode.Created);
            var alternative = new Material(f.Input.Item, lot, local);
            await Success<System.Text.Json.JsonElement>(await Post(client, "entradas", Movement(alternative, user.UserId, "100", 0), inventory: true), HttpStatusCode.Created);
            var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var originalReservation = Assert.Single(order.Reservas).Id;
            var command = Confirm(order, f, user.UserId);
            command = command with { Entradas = [new(command.Entradas[0].LinhaId, [new(Guid.NewGuid(), lot.Id, local.Id, "100", "kg")])] };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", command));
            ReservationBalances(order, 200, 0, 100, 100);
            var original = order.Reservas.Single(r => r.Id == originalReservation);
            Assert.Equal("100", original.QuantidadeOriginal); Assert.Equal("0", original.QuantidadeConsumida); Assert.Equal("100", original.QuantidadeLiberada);
            var supplement = Assert.Single(order.Reservas, r => r.Id != originalReservation);
            Assert.Equal(lot.Id, supplement.LoteId); Assert.Equal("100", supplement.QuantidadeConsumida);
            Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade); Assert.Equal("0", (await Balance(client, alternative))!.Quantidade);
        }
    }

    [Fact]
    public async Task T12_failed_input_compensation_rolls_back_the_already_debited_output_and_retries_once()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            var before = await Snapshot(); var command = new ReverseCommand(order.Revisao, true, "Erro confirmado e contagem sintética coerente", Evidence); var key = Unique();
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION "ProductionTestRejectCompensation"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Synthetic compensation fault' USING ERRCODE='P0001'; END $$;
                CREATE TRIGGER "TR_ProductionTestRejectCompensation" BEFORE INSERT ON "MovimentosEstoque"
                  FOR EACH ROW WHEN (NEW."TipoEvento"='ReversaoProducaoConsumo') EXECUTE FUNCTION "ProductionTestRejectCompensation"();
                """));
            try
            {
                await Error(await Post(client, $"{order.Id}/reversao", command, key), HttpStatusCode.InternalServerError);
                Assert.Equal(before, await Snapshot()); Assert.Equal("Confirmada", (await Get(client, order.Id))!.Estado);
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
                    DROP TRIGGER "TR_ProductionTestRejectCompensation" ON "MovimentosEstoque";
                    DROP FUNCTION "ProductionTestRejectCompensation"();
                    """));
            }
            order = await Success<OrderView>(await Post(client, $"{order.Id}/reversao", command, key));
            Assert.Equal("Revertida", order.Estado); Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade);
            var after = await Snapshot(); await Success<OrderView>(await Post(client, $"{order.Id}/reversao", command, key)); Assert.Equal(after, await Snapshot());
        }
    }
}
