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
                var persisted = await postgreFactory.ExecuteDbContextAsync(context => context.Pessoas.AsNoTracking().SingleAsync(pessoa => pessoa.Id == created.Id));
                Assert.Equal("Pessoa PostgreSQL Atualizada", persisted.Nome);
                Assert.False(persisted.Ativo);
            }
        }
        finally { await ((IAsyncLifetime)postgreFactory).DisposeAsync(); }
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
}
