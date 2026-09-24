using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GenSW.API.Tests;

public sealed class IdentificacoesAnimaisApiTests(AnimalApiPostgreSqlFixture fixture) : IClassFixture<AnimalApiPostgreSqlFixture>
{
    private readonly AuthWebApplicationFactory factory = fixture.Factory;

    [Fact]
    public async Task Nested_identifications_require_authentication_and_support_the_full_lifecycle()
    {
        var animalId = await SeedAnimalAsync(factory);
        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/animais/{animalId}/identificacoes")).StatusCode);

        using var client = await CreateAuthenticatedClientAsync(factory, "identifications_lifecycle");
        using var create = await client.PostAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes", new
        {
            tipo = (int)TipoIdentificacaoAnimal.Anilha, valor = " AN-API-1 ", principal = false,
            dataAplicacao = "2026-09-01", observacao = " inicial "
        });
        var created = await ReadJsonAsync(create);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.EndsWith($"/api/v1/animais/{animalId}/identificacoes/{id}", create.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        using var list = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes");
        using var get = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes/{id}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(id, (await ReadJsonAsync(list)).GetProperty("items")[0].GetProperty("id").GetGuid());

        using var metadata = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}", new { dataAplicacao = (string?)null, observacao = " alterada " });
        using var active = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}/ativo", new { ativo = true });
        using var principal = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}/principal", new { principal = true });
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.Equal(HttpStatusCode.OK, principal.StatusCode);
        var principalBody = await ReadJsonAsync(principal);
        Assert.True(principalBody.GetProperty("principal").GetBoolean());
        Assert.Equal(JsonValueKind.Null, principalBody.GetProperty("dataAplicacao").ValueKind);
        Assert.Equal("alterada", principalBody.GetProperty("observacao").GetString());
    }

    [Fact]
    public async Task Nested_identifications_return_safe_bad_request_and_conflict_boundaries()
    {
        var animalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "identifications_boundaries");
        using var invalidEnum = await client.PostAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes", new { tipo = 99, valor = "X", principal = false });
        using var emptyValue = await client.PostAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes", new { tipo = 1, valor = " ", principal = false });
        Assert.Equal(HttpStatusCode.BadRequest, invalidEnum.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyValue.StatusCode);

        var id = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Brinco, "B-API-1");
        using var noMetadata = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}", new { });
        using var inactivate = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}/ativo", new { ativo = false });
        using var inactivePrincipal = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}/principal", new { principal = true });
        using var duplicate = await client.PostAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes", new { tipo = (int)TipoIdentificacaoAnimal.Brinco, valor = "B-API-1", principal = false });
        Assert.Equal(HttpStatusCode.BadRequest, noMetadata.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inactivate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inactivePrincipal.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        AssertSafeProblem(await ReadJsonAsync(duplicate));
    }

    [Fact]
    public async Task Nested_identifications_hide_cross_animal_rows_and_support_filters_and_pagination()
    {
        var animalId = await SeedAnimalAsync(factory);
        var otherAnimalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "identifications_query");
        var first = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "FILTER-A");
        var second = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Brinco, "FILTER-B", principal: true);
        var third = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "FILTER-C");
        var other = await CreateIdentificationAsync(client, otherAnimalId, TipoIdentificacaoAnimal.Anilha, "OTHER-A");

        using var missingAnimal = await client.GetAsync($"/api/v1/animais/{Guid.NewGuid()}/identificacoes");
        using var missingRow = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes/{Guid.NewGuid()}");
        using var mismatchedRow = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes/{other}");
        using var filtered = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes?tipo=1&valor=filter&ativo=true&principal=false&page=1&pageSize=1");
        using var secondPage = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes?tipo=1&valor=filter&ativo=true&principal=false&page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.NotFound, missingAnimal.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingRow.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mismatchedRow.StatusCode);
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var body = await ReadJsonAsync(filtered);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
        var firstPageId = body.GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.Contains(firstPageId, new[] { first, third });
        Assert.Equal(2, body.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
        var secondPageBody = await ReadJsonAsync(secondPage);
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        Assert.Equal(2, secondPageBody.GetProperty("page").GetInt32());
        Assert.Equal(1, secondPageBody.GetProperty("items").GetArrayLength());
        var secondPageId = secondPageBody.GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.NotEqual(firstPageId, secondPageId);
        Assert.Equal(new[] { first, third }.OrderBy(id => id), new[] { firstPageId, secondPageId }.OrderBy(id => id));
        Assert.Equal(2, secondPageBody.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, secondPageBody.GetProperty("totalPages").GetInt32());
        Assert.NotEqual(second, first);
        using var delete = await client.DeleteAsync($"/api/v1/animais/{animalId}/identificacoes/{first}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    [Fact]
    public async Task Global_identifications_are_authenticated_get_only_and_return_safe_paged_lookup_rows()
    {
        var animalId = await SeedAnimalAsync(factory);
        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/identificacoes-animal")).StatusCode);

        using var client = await CreateAuthenticatedClientAsync(factory, "identifications_global");
        using var invalidEnum = await client.GetAsync("/api/v1/identificacoes-animal?tipo=99");
        using var invalidPage = await client.GetAsync("/api/v1/identificacoes-animal?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidEnum.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

        var first = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "Global-A");
        await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Brinco, "Global-B", principal: true);

        using var response = await client.GetAsync("/api/v1/identificacoes-animal?valor=global-a&tipo=1&ativo=true&principal=false&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(1, body.GetProperty("totalItems").GetInt32());
        var item = body.GetProperty("items")[0];
        Assert.Equal(first, item.GetProperty("identificacao").GetProperty("id").GetGuid());
        var animal = item.GetProperty("animal");
        Assert.Equal(animalId, animal.GetProperty("id").GetGuid());
        Assert.True(animal.TryGetProperty("codigoInterno", out _));
        Assert.True(animal.TryGetProperty("nome", out _));
        Assert.Equal(3, animal.EnumerateObject().Count());
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Patch, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, "/api/v1/identificacoes-animal");
            using var methodResponse = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, methodResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Principal_persistence_conflict_returns_a_stable_safe_409_problem()
    {
        var user = await factory.SeedUserAsync($"identifications_principal_conflict_{Guid.NewGuid():N}");
        using var conflictFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentificacaoAnimalService>();
            services.AddScoped<IIdentificacaoAnimalService, PrincipalConflictIdentificacaoAnimalService>();
        }));
        using var client = await CreateAuthenticatedClientAsync(conflictFactory, user);
        using var response = await client.PatchAsJsonAsync($"/api/v1/animais/{Guid.NewGuid()}/identificacoes/{Guid.NewGuid()}/principal", new { principal = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal("The principal physical identification could not be updated.", problem.GetProperty("detail").GetString());
        AssertSafeProblem(problem);
    }

    private static async Task<Guid> CreateIdentificationAsync(HttpClient client, Guid animalId, TipoIdentificacaoAnimal tipo, string valor, bool principal = false)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes", new { tipo = (int)tipo, valor, principal });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> SeedAnimalAsync(AuthWebApplicationFactory webFactory)
    {
        var now = DateTimeOffset.UtcNow;
        var species = Especie.Criar("Espécie identificação " + Guid.NewGuid().ToString("N"), null, now);
        var animal = Animal.Criar("AN-ID-" + Guid.NewGuid().ToString("N"), null, species.Id, null, null,
            SexoAnimal.Femea, null, EscopoAnimal.Operacional, DateOnly.FromDateTime(now.UtcDateTime), now);
        await webFactory.ExecuteDbContextAsync(async context =>
        {
            context.Especies.Add(species);
            context.Animais.Add(animal);
            await context.SaveChangesAsync();
        });
        return animal.Id;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> webFactory, string prefix)
    {
        var user = await ((AuthWebApplicationFactory)webFactory).SeedUserAsync($"{prefix}_{Guid.NewGuid():N}");
        return await CreateAuthenticatedClientAsync(webFactory, user);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> webFactory, SeededUser user)
    {
        var client = webFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var (token, _) = await login.ReadAccessTokenAsync();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static void AssertSafeProblem(JsonElement problem)
    {
        var serialized = problem.GetRawText();
        foreach (var forbidden in new[] { "SQLSTATE", "PostgreSQL", "constraint", "stack trace" })
            Assert.DoesNotContain(forbidden, serialized, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class PrincipalConflictIdentificacaoAnimalService : IIdentificacaoAnimalService
{
    private static readonly Exception Exception = new IdentificacaoAnimalPrincipalConflictException(
        IdentificacaoAnimalPrincipalConflictSource.PersistedNamedPrincipalAtivaUniqueConstraint,
        new InvalidOperationException("SQLSTATE; constraint; stack trace"));

    public Task<IdentificacaoAnimalResult> CreateAsync(Guid animalId, CreateIdentificacaoAnimalCommand command, CancellationToken cancellationToken = default) => Task.FromException<IdentificacaoAnimalResult>(Exception);
    public Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId, IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default) => Task.FromException<PagedIdentificacaoAnimalResult>(Exception);
    public Task<IdentificacaoAnimalResult> GetByAnimalAsync(Guid animalId, Guid identificacaoId, CancellationToken cancellationToken = default) => Task.FromException<IdentificacaoAnimalResult>(Exception);
    public Task<IdentificacaoAnimalResult> UpdateMetadataAsync(Guid animalId, Guid identificacaoId, UpdateIdentificacaoAnimalMetadataCommand command, CancellationToken cancellationToken = default) => Task.FromException<IdentificacaoAnimalResult>(Exception);
    public Task<IdentificacaoAnimalResult> SetAtivoAsync(Guid animalId, Guid identificacaoId, SetIdentificacaoAnimalAtivoCommand command, CancellationToken cancellationToken = default) => Task.FromException<IdentificacaoAnimalResult>(Exception);
    public Task<IdentificacaoAnimalResult> SetPrincipalAsync(Guid animalId, Guid identificacaoId, SetIdentificacaoAnimalPrincipalCommand command, CancellationToken cancellationToken = default) => Task.FromException<IdentificacaoAnimalResult>(Exception);
    public Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default) => Task.FromException<PagedIdentificacaoAnimalGlobalResult>(Exception);
}
