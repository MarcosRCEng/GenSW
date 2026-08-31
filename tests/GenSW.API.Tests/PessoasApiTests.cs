using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Domain.People;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class PessoasApiTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Pessoas_require_authentication_but_allow_a_non_admin_user_to_create_and_manage_people()
    {
        using var anonymous = factory.CreateHttpsClient();
        using var unauthorized = await anonymous.GetAsync("/api/v1/pessoas");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var (client, user) = await CreateAuthenticatedClientAsync(factory, "pessoas_api");
        using (client)
        {
            var created = await CreateAsync(client, TipoPessoa.Fisica, "Pessoa HTTP");
            Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
            Assert.EndsWith($"/api/v1/pessoas/{created.Id}", created.Response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            using var get = await client.GetAsync($"/api/v1/pessoas/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            using var update = await PutAsync(client, created.Id, "Pessoa Atualizada");
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            using var deactivate = await PatchAsync(client, created.Id, false);
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
            using var inactiveUpdate = await PutAsync(client, created.Id, "Não Pode");
            Assert.Equal(HttpStatusCode.Conflict, inactiveUpdate.StatusCode);
            using var activate = await PatchAsync(client, created.Id, true);
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
            Assert.True((await ReadJsonAsync(activate)).GetProperty("ativo").GetBoolean());
            using var updateAfterActivation = await PutAsync(client, created.Id, "Pessoa Editada Após Reativação");
            Assert.Equal(HttpStatusCode.OK, updateAfterActivation.StatusCode);
            Assert.Equal("Pessoa Editada Após Reativação", (await ReadJsonAsync(updateAfterActivation)).GetProperty("nome").GetString());
            using var list = await client.GetAsync("/api/v1/pessoas?page=1&pageSize=25&tipoPessoa=Fisica&ativo=true&sortBy=nome&sortDirection=asc");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?sortBy=other")]
    [InlineData("?sortDirection=sideways")]
    [InlineData("?tipoPessoa=Other")]
    public async Task List_rejects_invalid_http_query_values(string query)
    {
        var (client, _) = await CreateAuthenticatedClientAsync(factory, "pessoas_invalid");
        using (client)
        {
            using var response = await client.GetAsync($"/api/v1/pessoas{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task PostgreSql_flow_persists_create_update_and_status()
    {
        await using var postgreSql = await EphemeralPostgreSql.StartAsync();
        var postgreFactory = new AuthWebApplicationFactory(postgreSql.ConnectionString);
        await postgreFactory.InitializeAsync();
        try
        {
            var (client, _) = await CreateAuthenticatedClientAsync(postgreFactory, "pessoas_pg");
            using (client)
            {
                var created = await CreateAsync(client, TipoPessoa.Juridica, "Pessoa PostgreSQL", "PG");
                Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
                using var update = await PutAsync(client, created.Id, "Pessoa PostgreSQL Atualizada", "PG");
                Assert.Equal(HttpStatusCode.OK, update.StatusCode);
                using var status = await PatchAsync(client, created.Id, false);
                Assert.Equal(HttpStatusCode.OK, status.StatusCode);
                using var get = await client.GetAsync($"/api/v1/pessoas/{created.Id}");
                Assert.Equal(HttpStatusCode.OK, get.StatusCode);
                using var list = await client.GetAsync("/api/v1/pessoas?search=PostgreSQL&page=2&pageSize=25&sortDirection=desc");
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);

                var alpha = await CreateAsync(client, TipoPessoa.Fisica, "R1 Alpha");
                var beta = await CreateAsync(client, TipoPessoa.Juridica, "R1 Beta", "R1 Comercial Beta");
                var gamma = await CreateAsync(client, TipoPessoa.Juridica, "R1 Gamma");
                using var deactivateGamma = await PatchAsync(client, gamma.Id, false);

                var juridicas = await GetListAsync(client, "?tipoPessoa=Juridica");
                Assert.All(juridicas.GetProperty("items").EnumerateArray(), item => Assert.Equal((int)TipoPessoa.Juridica, item.GetProperty("tipoPessoa").GetInt32()));
                Assert.Contains(juridicas.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == beta.Id);
                var active = await GetListAsync(client, "?ativo=true");
                Assert.All(active.GetProperty("items").EnumerateArray(), item => Assert.True(item.GetProperty("ativo").GetBoolean()));
                var inactive = await GetListAsync(client, "?ativo=false");
                Assert.All(inactive.GetProperty("items").EnumerateArray(), item => Assert.False(item.GetProperty("ativo").GetBoolean()));
                Assert.Contains(inactive.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == gamma.Id);
                Assert.DoesNotContain(inactive.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == alpha.Id || item.GetProperty("id").GetGuid() == beta.Id);
                var searched = await GetListAsync(client, "?search=Comercial");
                Assert.Contains(searched.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == beta.Id);
                var asc = await GetListAsync(client, "?search=R1&sortBy=nome&sortDirection=asc");
                var desc = await GetListAsync(client, "?search=R1&sortBy=nome&sortDirection=desc");
                Assert.Equal(new[] { "R1 Alpha", "R1 Beta", "R1 Gamma" }, asc.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("nome").GetString()!).ToArray());
                Assert.Equal(new[] { "R1 Gamma", "R1 Beta", "R1 Alpha" }, desc.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("nome").GetString()!).ToArray());
                var page1 = await GetListAsync(client, "?search=R1&page=1&pageSize=2&sortBy=nome&sortDirection=asc");
                var page2 = await GetListAsync(client, "?search=R1&page=2&pageSize=2&sortBy=nome&sortDirection=asc");
                Assert.Equal(2, page1.GetProperty("items").GetArrayLength());
                Assert.Equal(1, page1.GetProperty("page").GetInt32());
                Assert.Equal(2, page1.GetProperty("pageSize").GetInt32());
                Assert.Equal(3, page1.GetProperty("totalItems").GetInt32());
                Assert.Equal(2, page1.GetProperty("totalPages").GetInt32());
                Assert.Equal("R1 Gamma", page2.GetProperty("items")[0].GetProperty("nome").GetString());
                var beyond = await GetListAsync(client, "?search=R1&page=999&pageSize=25");
                Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
                Assert.Equal(999, beyond.GetProperty("page").GetInt32());
                var persisted = await postgreFactory.ExecuteDbContextAsync(context => context.Pessoas.AsNoTracking().SingleAsync(pessoa => pessoa.Id == created.Id));
                Assert.Equal("Pessoa PostgreSQL Atualizada", persisted.Nome);
                Assert.False(persisted.Ativo);
            }
        }
        finally { await ((IAsyncLifetime)postgreFactory).DisposeAsync(); }
    }

    [Fact]
    public async Task Acceptance_validation_not_found_and_response_bodies_are_explicit()
    {
        var (client, _) = await CreateAuthenticatedClientAsync(factory, "pessoas_acceptance");
        using (client)
        {
            var missing = Guid.NewGuid();
            using var getMissing = await client.GetAsync($"/api/v1/pessoas/{missing}");
            using var putMissing = await PutAsync(client, missing, "Pessoa inexistente");
            using var patchMissing = await PatchAsync(client, missing, false);
            Assert.Equal(HttpStatusCode.NotFound, getMissing.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, putMissing.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, patchMissing.StatusCode);

            using var invalidName = await client.PostAsJsonAsync("/api/v1/pessoas", new { tipoPessoa = TipoPessoa.Fisica, nome = " ", nomeFantasia = (string?)null });
            using var invalidFantasy = await client.PostAsJsonAsync("/api/v1/pessoas", new { tipoPessoa = TipoPessoa.Fisica, nome = "Pessoa Física", nomeFantasia = "Inválida" });
            Assert.Equal(HttpStatusCode.BadRequest, invalidName.StatusCode);
            Assert.NotEqual(HttpStatusCode.InternalServerError, invalidName.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalidFantasy.StatusCode);

            var pf = await CreateAsync(client, TipoPessoa.Fisica, "  Alpha  ");
            Assert.Equal(HttpStatusCode.Created, pf.Response.StatusCode);
            using (var pfBody = JsonDocument.Parse(await pf.Response.Content.ReadAsStringAsync()))
            {
                Assert.Equal(pf.Id, pfBody.RootElement.GetProperty("id").GetGuid());
                Assert.NotEqual(Guid.Empty, pfBody.RootElement.GetProperty("id").GetGuid());
                Assert.Equal((int)TipoPessoa.Fisica, pfBody.RootElement.GetProperty("tipoPessoa").GetInt32());
                Assert.Equal("Alpha", pfBody.RootElement.GetProperty("nome").GetString());
                Assert.True(pfBody.RootElement.GetProperty("ativo").GetBoolean());
                Assert.Equal(JsonValueKind.Null, pfBody.RootElement.GetProperty("nomeFantasia").ValueKind);
                Assert.NotEqual(JsonValueKind.Null, pfBody.RootElement.GetProperty("createdAtUtc").ValueKind);
                Assert.NotEqual(JsonValueKind.Null, pfBody.RootElement.GetProperty("updatedAtUtc").ValueKind);
            }
            var pj = await CreateAsync(client, TipoPessoa.Juridica, "Beta", "Comercial Beta");
            using (var pjBody = JsonDocument.Parse(await pj.Response.Content.ReadAsStringAsync()))
            {
                Assert.Equal((int)TipoPessoa.Juridica, pjBody.RootElement.GetProperty("tipoPessoa").GetInt32());
                Assert.Equal("Comercial Beta", pjBody.RootElement.GetProperty("nomeFantasia").GetString());
            }
            var gamma = await CreateAsync(client, TipoPessoa.Juridica, "Gamma");
            using var deactivate = await PatchAsync(client, gamma.Id, false);
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
            using var invalidUpdate = await PutAsync(client, pf.Id, " ");
            Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
            using var get = await client.GetAsync($"/api/v1/pessoas/{pf.Id}");
            using (var getBody = JsonDocument.Parse(await get.Content.ReadAsStringAsync()))
            {
                Assert.Equal(pf.Id, getBody.RootElement.GetProperty("id").GetGuid());
                Assert.Equal((int)TipoPessoa.Fisica, getBody.RootElement.GetProperty("tipoPessoa").GetInt32());
                Assert.Equal("Alpha", getBody.RootElement.GetProperty("nome").GetString());
                Assert.Equal(JsonValueKind.Null, getBody.RootElement.GetProperty("nomeFantasia").ValueKind);
                Assert.True(getBody.RootElement.GetProperty("ativo").GetBoolean());
            }
            using var pageZero = await client.GetAsync("/api/v1/pessoas?pageSize=0");
            Assert.Equal(HttpStatusCode.BadRequest, pageZero.StatusCode);
        }
    }

    private static async Task<(HttpClient Client, SeededUser User)> CreateAuthenticatedClientAsync(AuthWebApplicationFactory webFactory, string prefix)
    {
        var user = await webFactory.SeedUserAsync($"{prefix}_{Guid.NewGuid():N}");
        var client = webFactory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var (token, _) = await login.ReadAccessTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user);
    }

    private static async Task<(HttpResponseMessage Response, Guid Id)> CreateAsync(HttpClient client, TipoPessoa tipoPessoa, string nome, string? nomeFantasia = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/pessoas", new { tipoPessoa, nome, nomeFantasia });
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (response, body.RootElement.GetProperty("id").GetGuid());
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string nome, string? nomeFantasia = null)
        => client.PutAsJsonAsync($"/api/v1/pessoas/{id}", new { nome, nomeFantasia });

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, Guid id, bool ativo)
        => client.PatchAsJsonAsync($"/api/v1/pessoas/{id}/ativo", new { ativo });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> GetListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"/api/v1/pessoas{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }
}
