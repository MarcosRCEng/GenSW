using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Inventory;
using GenSW.Application.MaterialCosts;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task C20_output_use_racing_reversal_cannot_create_cost_or_stock_twice()
    {
        var (client, user) = await Session(true);
        using var session = client;
        var scenario = await CostScenario(client, user.UserId, "10", "20", "10");
        var confirmed = await CostConfirmed(client, user.UserId, scenario, "10", "10");
        var output = Assert.Single(confirmed.Execucao!.LotesSaida);
        var lot = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{output.LoteId}"))!;
        var material = new Material(scenario.Output, lot, scenario.Destination);
        var balance = (await Balance(client, material))!;
        var consumption = Movement(material, user.UserId, "1", balance.Revisao) with
        {
            VersoesEsperadas = new(balance.ItemRevisao, lot.Revisao, balance.LocalRevisao, balance.Revisao),
        };
        var reversal = new ReverseCommand(confirmed.Revisao, true, Evidence, Evidence);

        var results = await Race(
            () => Post(client, "consumos-internos", consumption, inventory: true),
            () => Post(client, $"{confirmed.Id}/reversao", reversal));
        using var consumeResponse = results[0];
        using var reverseResponse = results[1];
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var inputCost = (await CostLot(client, scenario.Input.Lot.Id))!.Custo;
        var outputCost = (await CostLot(client, lot.Id))!.Custo;
        if (consumeResponse.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal("9", (await Balance(client, material))!.Quantidade);
            Assert.Equal("0.00", inputCost.ValorConhecido);
            Assert.Equal("18.00", outputCost.ValorTotal);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
            Assert.Equal("0", (await Balance(client, material))!.Quantidade);
            Assert.Equal("20.00", inputCost.ValorTotal);
            Assert.Equal("0.00", outputCost.ValorConhecido);
        }
    }

    [Fact]
    public async Task C30_timed_out_read_is_reported_and_never_replaces_the_recorded_cost()
    {
        var (client, user) = await Session(true);
        using var session = client;
        var material = await CostStock(client, user.UserId, "10", "20");
        var before = (await CostLot(client, material.Lot.Id))!;
        var reconciliation = $"/api/v1/custos-materiais/reconciliacao?loteId={material.Lot.Id}";
        var beforeReconciliation = await client.GetStringAsync(reconciliation);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            await using var blocker = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"ParcelasCustoMateriais\" IN ACCESS EXCLUSIVE MODE");
            foreach (var path in new[] { $"/api/v1/custos-materiais/lotes/{material.Lot.Id}", reconciliation })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("consulta_nao_concluida", problem.RootElement.GetProperty("code").GetString());
                Assert.False(problem.RootElement.TryGetProperty("custo", out _));
                Assert.False(problem.RootElement.TryGetProperty("items", out _));
            }
            await blocker.RollbackAsync();
            return 0;
        });

        var after = (await CostLot(client, material.Lot.Id))!;
        Assert.Equal(InventoryJson.Write(before), InventoryJson.Write(after));
        Assert.Equal(beforeReconciliation, await client.GetStringAsync(reconciliation));

        await Factory.ExecuteDbContextAsync(async db =>
        {
            await using var blocker = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413,421)");
            var write = Post(client, "consumos-internos", Movement(material, user.UserId, "1", 1), inventory: true);
            try
            {
                var deadline = DateTimeOffset.UtcNow.AddSeconds(4);
                bool waiting;
                do
                {
                    waiting = await db.Database.SqlQueryRaw<bool>("""
                        SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND classid=413 AND objid=421 AND NOT granted) AS "Value"
                        """).SingleAsync();
                    if (!waiting) await Task.Delay(20);
                } while (!waiting && !write.IsCompleted && DateTimeOffset.UtcNow < deadline);
                Assert.True(waiting, "O escritor concorrente deve aguardar a trava de estoque.");
                Assert.Equal(InventoryJson.Write(before), InventoryJson.Write(await CostLot(client, material.Lot.Id)));
                Assert.Equal(beforeReconciliation, await client.GetStringAsync(reconciliation));
            }
            finally { await blocker.RollbackAsync(); }
            using var response = await write;
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        });
        var updated = (await CostLot(client, material.Lot.Id))!;
        Assert.Equal("18.00", updated.Custo.ValorTotal);
        Assert.Equal("9", Assert.Single(Assert.Single(updated.Camadas).Posicoes).Quantidade);
        Assert.Equal(beforeReconciliation, await client.GetStringAsync(reconciliation));
    }

    [Fact]
    public async Task C32_inverse_conversion_rounds_only_final_cents_and_freezes_one_estimate_per_identity()
    {
        var (client, user) = await Session(true);
        using var session = client;
        var material = await CostStock(client, user.UserId, "10", "20");
        var today = InventoryRules.Today(TimeProvider.System);
        var conversion = await Formulation(service => service.CreateConversionAsync(material.Item.Id,
            new("L", "kg", "3", Evidence, "Medição sintética", today, Evidence, "Amostra sintética", "Medido"), user.UserId, default));
        var reference = new MaterialReferenceCommand(Guid.NewGuid(), material.Item.Id, "fonte", "doc", today,
            "BRL", "L", "1", "10", today, null, "contexto", Evidence);
        await Success<MaterialReferenceView>(await CostPost(client, "referencias", reference), HttpStatusCode.Created);
        var estimate = new MaterialEstimateCommand(Guid.NewGuid(), Evidence,
            [new(Guid.NewGuid(), material.Item.Id, "1", "kg", reference.Id, conversion.Id)]);
        var key = Unique();
        var first = await Success<JsonElement>(await CostPost(client, "estimativas", estimate, key), HttpStatusCode.Created);
        Assert.Equal("3.33", first.GetProperty("valorEstimadoTotal").GetString());
        Assert.True(first.GetProperty("linhas")[0].GetProperty("arredondado").GetBoolean());
        var replay = await Success<JsonElement>(await CostPost(client, "estimativas", estimate, key), HttpStatusCode.Created);
        Assert.Equal(first.GetRawText(), replay.GetRawText());
        await Error(await CostPost(client, "estimativas", estimate), HttpStatusCode.Conflict);

        var expired = reference with { Id = Guid.NewGuid(), VigenteDesde = today.AddDays(-10), VigenteAte = today.AddDays(-1) };
        await Success<MaterialReferenceView>(await CostPost(client, "referencias", expired), HttpStatusCode.Created);
        var historical = estimate with { Id = Guid.NewGuid(), Linhas = [estimate.Linhas[0] with { ReferenciaId = expired.Id }] };
        var result = await Success<JsonElement>(await CostPost(client, "estimativas", historical), HttpStatusCode.Created);
        Assert.Equal("3.33", result.GetProperty("valorEstimadoConhecido").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("valorEstimadoTotal").ValueKind);
        Assert.Equal("20.00", (await CostLot(client, material.Lot.Id))!.Custo.ValorTotal);
    }
}
