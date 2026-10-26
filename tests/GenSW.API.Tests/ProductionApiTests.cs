using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

// Separate, newly initialized PostgreSQL cluster. No appsettings connection or
// operational database is ever used by these tests, including failure injection.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProductionPostgreSqlCollection : ICollectionFixture<AnimalApiPostgreSqlFixture>
{ public const string Name = "Isolated production PostgreSQL"; }

[Collection(ProductionPostgreSqlCollection.Name)]
#pragma warning disable EF1002 // Identifiers in fixture-only trigger SQL are generated UUIDs.
public sealed partial class ProductionApiTests(AnimalApiPostgreSqlFixture fixture)
{
    private AuthWebApplicationFactory Factory => fixture.Factory;
    private const string Evidence = "Fixture sintética isolada; nenhum material ou saldo operacional";
    private static string Unique() => Guid.NewGuid().ToString("N");
    private Task<T> Formulation<T>(Func<FormulationService, Task<T>> action) =>
        Factory.ExecuteScopeAsync(s => action(s.GetRequiredService<FormulationService>()));
    private async Task<(HttpClient Client, SeededUser User)> Session(bool admin = false)
    {
        var user = await Factory.SeedUserAsync("production_" + Unique(), roles: admin ? ["Admin"] : []);
        var client = Factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.ReadAccessTokenAsync()).AccessToken);
        return (client, user);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object command, string? key = null, bool inventory = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, (inventory ? "/api/v1/estoque/" : "/api/v1/ordens-producao/") + path)
        { Content = JsonContent.Create(command) };
        request.Headers.Add("Idempotency-Key", key ?? Unique());
        return await client.SendAsync(request);
    }
    private static async Task<T> Success<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            if (expected == HttpStatusCode.Created) Assert.NotNull(response.Headers.Location);
            return InventoryJson.Read<T>(await response.Content.ReadAsStringAsync());
        }
    }
    private static async Task<string> Error(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response)
        {
            var json = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {json}");
            return json;
        }
    }
    private sealed record Material(Item Item, LoteView Lot, LocalView Local);
    private sealed record Scenario(Material Input, Item Output, LocalView Destination, RecipeVersionView Version);
    private async Task<Material> Stock(HttpClient client, Guid actor, string quantity = "100", string unit = "kg", DateOnly? expiry = null)
    {
        var item = await Formulation(s => s.CreateItemAsync(new(Unique(), "Material sintético F02", null, null, "Alimentar", unit, true, true, true, false), actor, default));
        var local = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Local sintético F02"), inventory: true), HttpStatusCode.Created);
        var lot = await Success<LoteView>(await Post(client, "lotes", new LoteCommand(item.Id, Unique(), "Inventário sintético", Evidence, actor,
            Validade: expiry, FonteValidade: expiry is null ? null : Evidence, ResponsavelValidadeId: expiry is null ? null : actor, ItemVersaoEsperada: item.Revisao), inventory: true), HttpStatusCode.Created);
        if (quantity != "0") await Success<JsonElement>(await Post(client, "entradas", Movement(new(item, lot, local), actor, quantity, 0), inventory: true), HttpStatusCode.Created);
        return new(item, lot, local);
    }
    private static InventoryMovementCommand Movement(Material m, Guid actor, string quantity, int version) => new(m.Lot.Id, quantity, m.Lot.Unidade, actor,
        "Movimento sintético conferido", new(m.Item.Revisao, m.Lot.Revisao, m.Local.Revisao, version), LocalId: m.Local.Id,
        DataObservada: InventoryRules.Today(TimeProvider.System), Origem: m.Lot.Origem, Fonte: m.Lot.Fonte, Destino: "Destino sintético conferido", Evidencia: Evidence);
    private async Task<Scenario> ScenarioAsync(HttpClient client, Guid actor, string stock = "100", string planned = "100", string output = "98", string unit = "kg", DateOnly? expiry = null)
    {
        var input = await Stock(client, actor, stock, unit, expiry);
        var result = await Formulation(s => s.CreateItemAsync(new(Unique(), "Produto sintético F02", null, null, "Alimentar", unit, true, true, true, false), actor, default));
        var destination = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Destino sintético F02"), inventory: true), HttpStatusCode.Created);
        var version = await RecipeAsync(actor, input.Item.Id, result.Id, planned, output, unit);
        return new(input, result, destination, version);
    }
    private async Task<RecipeVersionView> RecipeAsync(Guid actor, Guid input, Guid output, string planned = "100", string produced = "98", string unit = "kg", RecipeData? data = null)
    {
        var recipe = await Formulation(s => s.CreateRecipeAsync(new(Unique(), "Processamento sintético F02", Evidence), actor, default));
        var content = data ?? new RecipeData("Processamento", "Quantidade", planned, unit,
            [new(Guid.NewGuid(), input, "Alimentar", planned, unit, "Variavel")],
            [new(Guid.NewGuid(), output, "Produto sintético", produced, unit, "Variavel", true)], [], [], Evidence, []);
        var version = await Formulation(s => s.CreateVersionAsync(recipe.Id, content, actor, default));
        return await Formulation(s => s.VersionStateAsync(version.Id, version.Revisao, true, actor, default));
    }
    private static Task<OrderView?> Get(HttpClient client, Guid id) => client.GetFromJsonAsync<OrderView>($"/api/v1/ordens-producao/{id}");
    private static Task<SaldoView?> Balance(HttpClient client, Material m) => client.GetFromJsonAsync<SaldoView>($"/api/v1/estoque/saldos/{m.Lot.Id}/{m.Local.Id}");
    private async Task<OrderView> Order(HttpClient client, Guid actor, Scenario f, string? size = null)
    {
        var draft = await Success<OrderView>(await Post(client, "", new OrderCommand(Guid.NewGuid(), Unique(), f.Version.Id, size ?? f.Version.Conteudo.TamanhoReferencia, f.Version.Conteudo.UnidadeReferencia, actor)), HttpStatusCode.Created);
        return await Success<OrderView>(await Post(client, $"{draft.Id}/planejamento", new ProductionStateCommand(draft.Revisao, "Plano sintético conferido")));
    }
    private static StartCommand Start(OrderView order, Scenario f, Guid actor, string? amount = null, Guid? allocation = null) => new(order.Revisao, actor,
        [new(f.Version.Conteudo.Entradas[0].Id, [new(allocation ?? Guid.NewGuid(), f.Input.Lot.Id, f.Input.Local.Id, amount ?? order.Planejamento!.Escalonada.Entradas[0].Quantidade, f.Input.Lot.Unidade)])], "Reserva integral sintética");
    private static ConfirmCommand Confirm(OrderView order, Scenario f, Guid actor, string input = "100", string output = "98", string loss = "2") => new(order.Revisao, actor,
        [new(f.Version.Conteudo.Entradas[0].Id, [new(order.Reservas[0].Id, f.Input.Lot.Id, f.Input.Local.Id, input, f.Input.Lot.Unidade)])],
        [new(Guid.NewGuid(), f.Version.Conteudo.Saidas[0].Id, output, f.Output.Unidade, Unique(), f.Destination.Id)],
        loss == "0" ? [] : [new(loss, f.Input.Lot.Unidade, "Perda de processo medida na fixture")], "Confirmação física sintética", Evidence, Evidencia: Evidence);
    private static void ReservationBalances(OrderView order, decimal original, decimal active, decimal consumed, decimal released)
    {
        static decimal Sum(OrderView o, Func<ReservationView, string> value) => o.Reservas.Sum(r => decimal.Parse(value(r), CultureInfo.InvariantCulture));
        Assert.Equal(original, Sum(order, r => r.QuantidadeOriginal)); Assert.Equal(active, Sum(order, r => r.QuantidadeAtiva));
        Assert.Equal(consumed, Sum(order, r => r.QuantidadeConsumida)); Assert.Equal(released, Sum(order, r => r.QuantidadeLiberada));
        Assert.All(order.Reservas, r => Assert.Equal(decimal.Parse(r.QuantidadeOriginal, CultureInfo.InvariantCulture), decimal.Parse(r.QuantidadeAtiva, CultureInfo.InvariantCulture) + decimal.Parse(r.QuantidadeConsumida, CultureInfo.InvariantCulture) + decimal.Parse(r.QuantidadeLiberada, CultureInfo.InvariantCulture)));
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
            var deadline = DateTimeOffset.UtcNow.AddSeconds(4); var blocked = false;
            while (DateTimeOffset.UtcNow < deadline)
            {
                var count = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND query LIKE '%413%421%'").SingleAsync());
                if (count >= 2) { blocked = true; break; }
                if (one.IsCompleted || two.IsCompleted) break;
                await Task.Delay(20);
            }
            Assert.True(blocked, "Both independent PostgreSQL writers must be waiting before releasing the deterministic barrier.");
        }
        finally { release.TrySetResult(); await holder; }
        return await Task.WhenAll(one!, two!).WaitAsync(TimeSpan.FromSeconds(15));
    }
}
