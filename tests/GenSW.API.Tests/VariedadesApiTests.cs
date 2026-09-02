using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Domain.Species;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class VariedadesApiTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Variedades_require_authentication_and_support_lifecycle_and_historical_inactive_species_links()
    {
        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/variedades")).StatusCode);

        var species = await SeedSpeciesAsync(factory);
        var client = await CreateAuthenticatedClientAsync(factory, "variedades_lifecycle");
        using (client)
        {
            using var inactiveCreate = await client.PostAsJsonAsync("/api/v1/variedades", new { especieId = species.Inactive.Id, nome = "Variedade inválida" });
            Assert.Equal(HttpStatusCode.BadRequest, inactiveCreate.StatusCode);

            var created = await CreateAsync(client, species.Active.Id, "  Pelo   curto ");
            Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
            Assert.EndsWith($"/api/v1/variedades/{created.Id}", created.Response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Pelo curto", created.Body.GetProperty("nome").GetString());
            Assert.Equal(species.Active.Id, created.Body.GetProperty("especie").GetProperty("id").GetGuid());

            using var list = await client.GetAsync($"/api/v1/variedades?page=1&pageSize=25&search=CURTO&especieId={species.Active.Id}&ativo=true&sortBy=ativo&sortDirection=asc");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Contains(created.Id, (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));

            using var invalidMove = await PutAsync(client, created.Id, species.Inactive.Id, "Variedade inválida");
            Assert.Equal(HttpStatusCode.BadRequest, invalidMove.StatusCode);

            await SetSpeciesActiveAsync(factory, species.Historical.Id, false);
            var historical = await CreateVariedadeDirectlyAsync(factory, species.Historical.Id, "Variedade histórica");

            using var keepInactiveLink = await PutAsync(client, historical, species.Historical.Id, "Variedade histórica editada");
            Assert.Equal(HttpStatusCode.OK, keepInactiveLink.StatusCode);
            Assert.False((await ReadJsonAsync(keepInactiveLink)).GetProperty("especie").GetProperty("ativo").GetBoolean());

            using var changeStatusWithInactiveLink = await PatchAsync(client, historical, false);
            Assert.Equal(HttpStatusCode.OK, changeStatusWithInactiveLink.StatusCode);

            using var moveToActive = await PutAsync(client, historical, species.Active.Id, "Variedade migrada");
            Assert.Equal(HttpStatusCode.OK, moveToActive.StatusCode);
            Assert.Equal(species.Active.Id, (await ReadJsonAsync(moveToActive)).GetProperty("especieId").GetGuid());
        }
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?sortBy=other")]
    [InlineData("?sortDirection=sideways")]
    public async Task Variedades_list_rejects_invalid_query_values(string query)
    {
        var client = await CreateAuthenticatedClientAsync(factory, "variedades_invalid_query");
        using (client)
        using (var response = await client.GetAsync($"/api/v1/variedades{query}"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Variedades_validate_input_and_report_missing_resources_and_duplicates()
    {
        var species = await SeedSpeciesAsync(factory);
        var client = await CreateAuthenticatedClientAsync(factory, "variedades_validation");
        using (client)
        {
            using var empty = await client.PostAsJsonAsync("/api/v1/variedades", new { especieId = species.Active.Id, nome = " " });
            using var tooLong = await client.PostAsJsonAsync("/api/v1/variedades", new { especieId = species.Active.Id, nome = new string('x', 201) });
            using var missingSpecies = await client.PostAsJsonAsync("/api/v1/variedades", new { especieId = Guid.NewGuid(), nome = "Variedade" });
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingSpecies.StatusCode);

            var created = await CreateAsync(client, species.Active.Id, "Liso");
            using var duplicate = await client.PostAsJsonAsync("/api/v1/variedades", new { especieId = species.Active.Id, nome = "  lISO " });
            using var missingGet = await client.GetAsync($"/api/v1/variedades/{Guid.NewGuid()}");
            using var missingUpdate = await PutAsync(client, Guid.NewGuid(), species.Active.Id, "Inexistente");
            using var missingStatus = await PatchAsync(client, Guid.NewGuid(), false);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingGet.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingStatus.StatusCode);

            using var deactivate = await PatchAsync(client, created.Id, false);
            using var reactivate = await PatchAsync(client, created.Id, true);
            Assert.False((await ReadJsonAsync(deactivate)).GetProperty("ativo").GetBoolean());
            Assert.True((await ReadJsonAsync(reactivate)).GetProperty("ativo").GetBoolean());
        }
    }

    [Fact]
    public async Task Delete_is_not_an_allowed_variedade_operation()
    {
        var client = await CreateAuthenticatedClientAsync(factory, "variedades_delete");
        using (client)
        using (var response = await client.DeleteAsync($"/api/v1/variedades/{Guid.NewGuid()}"))
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
    }

    private static async Task<(Especie Active, Especie Historical, Especie Inactive)> SeedSpeciesAsync(AuthWebApplicationFactory webFactory)
    {
        var now = DateTimeOffset.UtcNow;
        var active = Especie.Criar("Ativa " + Guid.NewGuid().ToString("N"), null, now);
        var historical = Especie.Criar("Histórica " + Guid.NewGuid().ToString("N"), null, now);
        var inactive = Especie.Criar("Inativa " + Guid.NewGuid().ToString("N"), null, now);
        inactive.Inativar(now.AddMinutes(1));
        await webFactory.ExecuteDbContextAsync(async context =>
        {
            context.Especies.AddRange(active, historical, inactive);
            await context.SaveChangesAsync();
        });
        return (active, historical, inactive);
    }

    private static Task SetSpeciesActiveAsync(AuthWebApplicationFactory webFactory, Guid especieId, bool ativo) => webFactory.ExecuteDbContextAsync(async context =>
    {
        var especie = await context.Especies.SingleAsync(item => item.Id == especieId);
        if (ativo) especie.Reativar(DateTimeOffset.UtcNow); else especie.Inativar(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
    });

    private static async Task<Guid> CreateVariedadeDirectlyAsync(AuthWebApplicationFactory webFactory, Guid especieId, string nome)
    {
        var variedade = GenSW.Domain.Varieties.Variedade.Criar(especieId, nome, DateTimeOffset.UtcNow);
        await webFactory.ExecuteDbContextAsync(async context =>
        {
            context.Variedades.Add(variedade);
            await context.SaveChangesAsync();
        });
        return variedade.Id;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(AuthWebApplicationFactory webFactory, string prefix)
    {
        var user = await webFactory.SeedUserAsync($"{prefix}_{Guid.NewGuid():N}");
        var client = webFactory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var (token, _) = await login.ReadAccessTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<(HttpResponseMessage Response, Guid Id, JsonElement Body)> CreateAsync(HttpClient client, Guid especieId, string nome)
    {
        var response = await client.PostAsJsonAsync("/api/v1/variedades", new { especieId, nome });
        var body = await ReadJsonAsync(response);
        return (response, body.GetProperty("id").GetGuid(), body);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, Guid especieId, string nome)
        => client.PutAsJsonAsync($"/api/v1/variedades/{id}", new { especieId, nome });

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, Guid id, bool ativo)
        => client.PatchAsJsonAsync($"/api/v1/variedades/{id}/ativo", new { ativo });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
