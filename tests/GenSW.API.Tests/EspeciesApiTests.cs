using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class EspeciesApiTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Especies_require_authentication_and_support_the_full_lifecycle_including_inactive_edits()
    {
        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/especies")).StatusCode);

        var client = await CreateAuthenticatedClientAsync(factory, "especies_lifecycle");
        using (client)
        {
            var created = await CreateAsync(client, "  Cão   doméstico ", " Canis familiaris ");
            Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
            Assert.EndsWith($"/api/v1/especies/{created.Id}", created.Response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Cão doméstico", created.Body.GetProperty("nomeComum").GetString());
            Assert.Equal("Canis familiaris", created.Body.GetProperty("nomeCientifico").GetString());
            Assert.True(created.Body.GetProperty("ativo").GetBoolean());
            Assert.NotEqual(JsonValueKind.Null, created.Body.GetProperty("createdAtUtc").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, created.Body.GetProperty("updatedAtUtc").ValueKind);

            using var list = await client.GetAsync("/api/v1/especies?page=1&pageSize=25&ativo=true&sortBy=nomeComum&sortDirection=asc");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Contains(created.Id, (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));

            using var get = await client.GetAsync($"/api/v1/especies/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);

            using var update = await PutAsync(client, created.Id, "Cão atualizado", "Canis lupus familiaris");
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            Assert.Equal("Cão atualizado", (await ReadJsonAsync(update)).GetProperty("nomeComum").GetString());

            using var deactivate = await PatchAsync(client, created.Id, false);
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
            Assert.False((await ReadJsonAsync(deactivate)).GetProperty("ativo").GetBoolean());

            using var getInactive = await client.GetAsync($"/api/v1/especies/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, getInactive.StatusCode);
            Assert.False((await ReadJsonAsync(getInactive)).GetProperty("ativo").GetBoolean());

            using var updateInactive = await PutAsync(client, created.Id, "Cão inativo editado", null);
            Assert.Equal(HttpStatusCode.OK, updateInactive.StatusCode);
            Assert.Equal("Cão inativo editado", (await ReadJsonAsync(updateInactive)).GetProperty("nomeComum").GetString());

            using var activate = await PatchAsync(client, created.Id, true);
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
            Assert.True((await ReadJsonAsync(activate)).GetProperty("ativo").GetBoolean());
        }
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?sortBy=other")]
    [InlineData("?sortBy=NOMECOMUM")]
    [InlineData("?sortDirection=sideways")]
    [InlineData("?sortDirection=ASC")]
    public async Task List_rejects_invalid_query_values(string query)
    {
        var client = await CreateAuthenticatedClientAsync(factory, "especies_invalid_query");
        using (client)
        using (var response = await client.GetAsync($"/api/v1/especies{query}"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Mutations_validate_data_and_report_missing_species()
    {
        var client = await CreateAuthenticatedClientAsync(factory, "especies_acceptance");
        using (client)
        {
            using var invalid = await client.PostAsJsonAsync("/api/v1/especies", new { nomeComum = " ", nomeCientifico = (string?)null });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            var missing = Guid.NewGuid();
            using var getMissing = await client.GetAsync($"/api/v1/especies/{missing}");
            using var putMissing = await PutAsync(client, missing, "Inexistente");
            using var patchMissing = await PatchAsync(client, missing, false);
            Assert.Equal(HttpStatusCode.NotFound, getMissing.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, putMissing.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, patchMissing.StatusCode);
        }
    }

    [Fact]
    public async Task Create_rejects_common_and_scientific_name_duplicates_while_accepting_multiple_null_scientific_names()
    {
        var client = await CreateAuthenticatedClientAsync(factory, "especies_duplicates");
        using (client)
        {
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(client, "Lobo", "Canis lupus")).Response.StatusCode);
            using var commonDuplicate = await client.PostAsJsonAsync("/api/v1/especies", new { nomeComum = "  lOBO  ", nomeCientifico = "Outro científico" });
            using var scientificDuplicate = await client.PostAsJsonAsync("/api/v1/especies", new { nomeComum = "Outro lobo", nomeCientifico = "  CANIS   LUPUS " });
            Assert.Equal(HttpStatusCode.Conflict, commonDuplicate.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, scientificDuplicate.StatusCode);
            Assert.Equal("Species already exists", (await ReadJsonAsync(commonDuplicate)).GetProperty("title").GetString());
            Assert.Equal("Species already exists", (await ReadJsonAsync(scientificDuplicate)).GetProperty("title").GetString());

            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(client, "Espécie sem científico 1", null)).Response.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(client, "Espécie sem científico 2", null)).Response.StatusCode);
        }
    }

    [Fact]
    public async Task Delete_is_not_an_allowed_species_operation()
    {
        var client = await CreateAuthenticatedClientAsync(factory, "especies_delete");
        using (client)
        {
            using var response = await client.DeleteAsync($"/api/v1/especies/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
    }

    [Fact]
    public async Task PostgreSql_flow_applies_migrations_and_persists_species_changes()
    {
        await using var postgreSql = await EphemeralPostgreSql.StartAsync();
        var postgreFactory = new AuthWebApplicationFactory(postgreSql.ConnectionString);
        await postgreFactory.InitializeAsync();
        try
        {
            var client = await CreateAuthenticatedClientAsync(postgreFactory, "especies_pg");
            using (client)
            {
                var created = await CreateAsync(client, "Espécie PostgreSQL", "Species postgresql");
                Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
                using var update = await PutAsync(client, created.Id, "Espécie PostgreSQL atualizada", "Species postgresql updated");
                Assert.Equal(HttpStatusCode.OK, update.StatusCode);
                using var deactivate = await PatchAsync(client, created.Id, false);
                Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

                var persisted = await postgreFactory.ExecuteDbContextAsync(context => context.Especies.AsNoTracking().SingleAsync(especie => especie.Id == created.Id));
                Assert.Equal("Espécie PostgreSQL atualizada", persisted.NomeComum);
                Assert.Equal("Species postgresql updated", persisted.NomeCientifico);
                Assert.False(persisted.Ativo);
            }
        }
        finally { await ((IAsyncLifetime)postgreFactory).DisposeAsync(); }
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

    private static async Task<(HttpResponseMessage Response, Guid Id, JsonElement Body)> CreateAsync(HttpClient client, string nomeComum, string? nomeCientifico)
    {
        var response = await client.PostAsJsonAsync("/api/v1/especies", new { nomeComum, nomeCientifico });
        var body = await ReadJsonAsync(response);
        return (response, body.GetProperty("id").GetGuid(), body);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string nomeComum, string? nomeCientifico = null)
        => client.PutAsJsonAsync($"/api/v1/especies/{id}", new { nomeComum, nomeCientifico });

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, Guid id, bool ativo)
        => client.PatchAsJsonAsync($"/api/v1/especies/{id}/ativo", new { ativo });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
