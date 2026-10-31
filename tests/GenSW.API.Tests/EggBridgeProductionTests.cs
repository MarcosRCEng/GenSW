using System.Net.Http.Json;
using GenSW.Application.EggBridge;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    private static async Task<HttpResponseMessage> Production(HttpClient client, string path, object command)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ordens-producao/" + path) { Content = JsonContent.Create(command) };
        request.Headers.Add("Idempotency-Key", Unique()); return await client.SendAsync(request);
    }
    private Task<T> Formulation<T>(Func<FormulationService, Task<T>> action) => Factory.ExecuteScopeAsync(s => action(s.GetRequiredService<FormulationService>()));
    private async Task<(OrderView Order, Guid InputLine, Guid OutputLine, Guid OutputItem, Guid Destination)> PlannedOrder(HttpClient client, Guid actor, Material input, bool mass, Guid? conversion = null)
    {
        var output = await Formulation(s => s.CreateItemAsync(new(Unique(), "Saída F02 sintética de F03", null, null, "Alimentar", mass ? "kg" : "un", true, true, true, false), actor, default));
        var local = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Destino F02 de ovos sintéticos"), inventory: true));
        var recipe = await Formulation(s => s.CreateRecipeAsync(new(Unique(), "Processo F02 sintético de ovos", Evidence), actor, default));
        var data = new RecipeData("Processamento", "Quantidade", "1", "un",
            [new(Guid.NewGuid(), input.Item.Id, "Alimentar", "1", "un", "Variavel", ConversaoId: conversion)],
            [new(Guid.NewGuid(), output.Id, "Saída medida", mass ? "0.008" : "1", mass ? "kg" : "un", "Variavel", true)], [], [], Evidence, []);
        var version = await Formulation(s => s.CreateVersionAsync(recipe.Id, data, actor, default));
        version = await Formulation(s => s.VersionStateAsync(version.Id, version.Revisao, true, actor, default));
        var draft = await Success<OrderView>(await Production(client, "", new OrderCommand(Guid.NewGuid(), Unique(), version.Id, "1", "un", actor)));
        var planned = await Success<OrderView>(await Production(client, $"{draft.Id}/planejamento", new ProductionStateCommand(draft.Revisao, Evidence)));
        return (planned, data.Entradas[0].Id, data.Saidas[0].Id, output.Id, local.Id);
    }

    [Fact]
    public async Task F03_15_even_released_f02_reservation_blocks_compensation_of_intact_physical_quantity()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2); var scope = await Scope(client, m, user.UserId, eggs);
            var group = await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            var plan = await PlannedOrder(client, user.UserId, m, false);
            var start = new StartCommand(plan.Order.Revisao, user.UserId,
                [new(plan.InputLine, [new(Guid.NewGuid(), m.Lot.Id, m.Local.Id, "1", "un")])], Evidence);
            var order = await Success<OrderView>(await Production(client, $"{plan.Order.Id}/inicio", start));
            Assert.Equal("1", (await Balance(client, m))!.Reservado);
            order = await Success<OrderView>(await Production(client, $"{order.Id}/aborto-sem-consumo", new AbortCommand(order.Revisao, true, Evidence, Evidence)));
            Assert.Equal("0", (await Balance(client, m))!.Reservado); Assert.Equal("2", (await Balance(client, m))!.Quantidade);
            var treatment = await Success<EggTreatmentPreview>(await Post(client, $"grupos/{group.Grupo.Id}/previas-tratamento", new EggTreatmentCommand("Compensacao", user.UserId, Evidence, Evidence, true)));
            Assert.False(treatment.PodeConfirmar); Assert.NotEmpty(treatment.Impedimentos);
            var before = await Snapshot();
            await Error(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(treatment.Token)), System.Net.HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task F03_17_23_f02_consumes_bridge_lot_with_explicit_conversion_and_later_egg_edit_never_recalculates_execution()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(2); var scope = await Scope(client, m, user.UserId, eggs);
            var group = await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            // This measured conversion is an explicit independent document;
            // it is never computed from the egg snapshot or species default.
            var conversion = await Formulation(s => s.CreateConversionAsync(m.Item.Id,
                new("un", "kg", "0.01", Evidence, "Pesagem sintética independente", InventoryRules.Today(TimeProvider.System), Evidence, "Lote da fixture", "Medido"), user.UserId, default));
            var linked = await Success<LoteView>(await Post(client, $"lotes/{m.Lot.Id}/referencias",
                new InventoryStateCommand(m.Lot.Revisao, Evidence, Evidence, ConversaoItemId: conversion.Id, Aplicabilidade: Evidence), inventory: true));
            m = m with { Lot = linked };
            var plan = await PlannedOrder(client, user.UserId, m, true, conversion.Id);
            var input = new InputCommand(plan.InputLine, [new(Guid.NewGuid(), m.Lot.Id, m.Local.Id, "1", "un", conversion.Id)]);
            var order = await Success<OrderView>(await Production(client, $"{plan.Order.Id}/inicio", new StartCommand(plan.Order.Revisao, user.UserId, [input], Evidence)));
            var confirm = new ConfirmCommand(order.Revisao, user.UserId, [input],
                [new(Guid.NewGuid(), plan.OutputLine, "0.008", "kg", Unique(), plan.Destination)],
                [new("0.002", "kg", Evidence)], Evidence, Observacao: Evidence, Evidencia: Evidence);
            order = await Success<OrderView>(await Production(client, $"{order.Id}/confirmacao", confirm));
            var execution = order.Execucao!.SnapshotJson;
            Assert.Equal("1", (await Balance(client, m))!.Quantidade); Assert.Equal("0", (await Balance(client, m))!.Reservado);
            await Factory.ExecuteDbContextAsync(async db =>
            {
                var source = await db.ProducoesOvos.SingleAsync(x => x.Id == eggs[0].Id);
                source.Atualizar(source.DataPostura, 999m, "Edição posterior sem conversão", DateTimeOffset.UtcNow); await db.SaveChangesAsync();
            });
            var after = (await client.GetFromJsonAsync<OrderView>($"/api/v1/ordens-producao/{order.Id}"))!;
            Assert.Equal(execution, after.Execucao!.SnapshotJson); Assert.Equal("1", (await Balance(client, m))!.Quantidade);
            var bridge = (await client.GetFromJsonAsync<EggBridgeDetail>(Root + "grupos/" + group.Grupo.Id))!;
            Assert.Equal(group.Pontes.Count, bridge.Pontes.Count); Assert.Contains(bridge.OrigensAtuais, x => x.Impedimento is not null);
        }
    }
}
