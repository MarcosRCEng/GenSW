using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class EggProductionReadTests(AnimalApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Compact_pages_preserve_filtered_metrics_and_legacy_requests_keep_the_full_series()
    {
        using var client = await AuthenticateAsync();
        var animalId = await SeedAnimalAsync(50.01m,
            (new(2026, 9, 1), 65m),
            (new(2026, 9, 10), 50m),
            (new(2026, 9, 10), 50m),
            (new(2026, 9, 12), 50.01m),
            (new(2026, 9, 12), 50.01m),
            (new(2026, 9, 15), 70m));
        var path = $"/api/v1/animais/{animalId}/producoes-ovos";
        const string filter = "pageSize=2&dataInicial=2026-09-10&dataFinal=2026-09-12";

        var compact = await client.GetFromJsonAsync<JsonElement>($"{path}?{filter}&includeHistory=false");
        var legacy = await client.GetFromJsonAsync<JsonElement>($"{path}?{filter}");
        var history = await client.GetFromJsonAsync<JsonElement>($"{path}?{filter}&includeHistory=true");
        var secondPage = await client.GetFromJsonAsync<JsonElement>($"{path}?{filter}&page=2&includeHistory=false");
        var metrics = compact.GetProperty("metricas");
        var series = legacy.GetProperty("metricas").GetProperty("evolucao");

        Assert.Equal(4, compact.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, compact.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, compact.GetProperty("items").GetArrayLength());
        Assert.Equal(2, secondPage.GetProperty("items").GetArrayLength());
        Assert.All(compact.GetProperty("items").EnumerateArray(), item => Assert.Equal("2026-09-12", item.GetProperty("dataPostura").GetString()));
        Assert.All(secondPage.GetProperty("items").EnumerateArray(), item => Assert.Equal("2026-09-10", item.GetProperty("dataPostura").GetString()));
        AssertJsonEqual(legacy.GetProperty("items"), compact.GetProperty("items"));
        Assert.Equal(4, series.GetArrayLength());
        Assert.Equal(4, series.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Distinct().Count());
        AssertJsonEqual(legacy, history);

        Assert.False(metrics.TryGetProperty("evolucao", out _));
        AssertJsonEqual(metrics, secondPage.GetProperty("metricas"));
        foreach (var metric in metrics.EnumerateObject())
            AssertJsonEqual(legacy.GetProperty("metricas").GetProperty(metric.Name), metric.Value);
        Assert.Equal(4, metrics.GetProperty("totalLancamentos").GetInt32());
        Assert.Equal(50m, metrics.GetProperty("pesoMedioGramas").GetDecimal());
        Assert.Equal(50m, metrics.GetProperty("pesoMinimoGramas").GetDecimal());
        Assert.Equal(50.01m, metrics.GetProperty("pesoMaximoGramas").GetDecimal());
        Assert.Equal(50.01m, metrics.GetProperty("pesoPadraoGramas").GetDecimal());
        Assert.Equal(2, metrics.GetProperty("diasAtePesoPadrao").GetInt32());

        var empty = await client.GetFromJsonAsync<JsonElement>($"{path}?includeHistory=false&dataInicial=2026-09-16");
        Assert.Equal(0, empty.GetProperty("totalPages").GetInt32());
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("metricas").GetProperty("pesoMedioGramas").ValueKind);
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("metricas").GetProperty("diasAtePesoPadrao").ValueKind);
        Assert.Equal(50.01m, empty.GetProperty("metricas").GetProperty("pesoPadraoGramas").GetDecimal());
    }

    [Fact]
    public async Task Compact_query_handles_species_without_standard_and_requires_an_eligible_animal()
    {
        using var client = await AuthenticateAsync();
        var animalId = await SeedAnimalAsync(null, (new(2026, 9, 1), 45m));
        var result = await client.GetFromJsonAsync<JsonElement>($"/api/v1/animais/{animalId}/producoes-ovos?includeHistory=false");
        var metrics = result.GetProperty("metricas");
        Assert.Equal(45m, metrics.GetProperty("pesoMedioGramas").GetDecimal());
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("pesoPadraoGramas").ValueKind);
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("diasAtePesoPadrao").ValueKind);

        using var missing = await client.GetAsync($"/api/v1/animais/{Guid.NewGuid()}/producoes-ovos?includeHistory=false");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static void AssertJsonEqual(JsonElement expected, JsonElement actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()), JsonNode.Parse(actual.GetRawText())),
            $"Expected {expected.GetRawText()}, got {actual.GetRawText()}.");

    private async Task<HttpClient> AuthenticateAsync()
    {
        var user = await fixture.Factory.SeedUserAsync($"postura_{Guid.NewGuid():N}");
        var client = fixture.Factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.ReadAccessTokenAsync()).AccessToken);
        return client;
    }

    private Task<Guid> SeedAnimalAsync(decimal? standard, params (DateOnly Date, decimal Weight)[] entries) =>
        fixture.Factory.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            var species = Especie.Criar($"Espécie sintética {Guid.NewGuid():N}", null, true, standard, now);
            var animal = Animal.Criar(Guid.NewGuid().ToString("N"), "Fêmea sintética", species.Id, null, null, SexoAnimal.Femea,
                null, EscopoAnimal.Operacional, DateOnly.FromDateTime(now.UtcDateTime), now);
            db.AddRange(species, animal);
            db.AddRange(entries.Select(entry => ProducaoOvo.Criar(animal.Id, entry.Date, entry.Weight, null, now)));
            await db.SaveChangesAsync();
            return animal.Id;
        });
}
