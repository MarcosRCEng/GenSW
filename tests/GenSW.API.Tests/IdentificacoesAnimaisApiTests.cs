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
    public async Task Minimal_anilha_creation_persists_defaults_when_all_optional_fields_are_omitted()
    {
        var animalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "minimal_anilha");
        using var create = await client.PostAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes",
            new { tipo = 1, valor = "HOM-MINIMAL" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await ReadJsonAsync(create)).GetProperty("id").GetGuid();

        var persisted = await GetIdentificationAsync(client, animalId, id);
        Assert.Equal(1, persisted.GetProperty("tipo").GetInt32());
        Assert.Equal("HOM-MINIMAL", persisted.GetProperty("valor").GetString());
        Assert.True(persisted.GetProperty("ativo").GetBoolean());
        Assert.False(persisted.GetProperty("principal").GetBoolean());
        foreach (var field in new[] { "descricaoTipo", "dataAplicacao", "observacao" })
            Assert.Equal(JsonValueKind.Null, persisted.GetProperty(field).ValueKind);
    }

    [Fact]
    public async Task Creating_a_microchip_keeps_the_existing_anilha_active_without_requiring_a_principal()
    {
        var animalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "simultaneous_markers");
        var anilha = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "HOM-SIMULTANEOUS");
        var microchip = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Microchip, "985141000000002");

        using var list = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { anilha, microchip }.OrderBy(id => id), items.Select(item => item.GetProperty("id").GetGuid()).OrderBy(id => id));
        Assert.All(items, item =>
        {
            Assert.True(item.GetProperty("ativo").GetBoolean());
            Assert.False(item.GetProperty("principal").GetBoolean());
        });
    }

    [Fact]
    public async Task Case_insensitive_anilha_duplicate_between_animals_returns_a_safe_409()
    {
        var animalId = await SeedAnimalAsync(factory);
        var otherAnimalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "case_duplicate");
        await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "HOM-A001");
        using var duplicate = await client.PostAsJsonAsync($"/api/v1/animais/{otherAnimalId}/identificacoes",
            new { tipo = 1, valor = "hom-a001" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        AssertSafeProblem(await ReadJsonAsync(duplicate), HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removing_or_inactivating_the_principal_never_promotes_another_active_marker(bool inactivate)
    {
        var animalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "no_promotion");
        var first = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, $"HOM-A-{inactivate}", principal: true);
        var second = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Microchip, $"HOM-B-{inactivate}");
        var beforeFirst = await GetIdentificationAsync(client, animalId, first);
        var beforeSecond = await GetIdentificationAsync(client, animalId, second);
        Assert.True(beforeFirst.GetProperty("ativo").GetBoolean());
        Assert.True(beforeFirst.GetProperty("principal").GetBoolean());
        Assert.True(beforeSecond.GetProperty("ativo").GetBoolean());
        Assert.False(beforeSecond.GetProperty("principal").GetBoolean());

        var operation = inactivate ? "ativo" : "principal";
        using var response = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{first}/{operation}",
            new Dictionary<string, bool> { [operation] = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var afterFirst = await GetIdentificationAsync(client, animalId, first);
        var afterSecond = await GetIdentificationAsync(client, animalId, second);
        Assert.Equal(!inactivate, afterFirst.GetProperty("ativo").GetBoolean());
        Assert.False(afterFirst.GetProperty("principal").GetBoolean());
        Assert.True(afterSecond.GetProperty("ativo").GetBoolean());
        Assert.False(afterSecond.GetProperty("principal").GetBoolean());
    }

    [Fact]
    public async Task Metadata_value_absence_and_explicit_null_survive_new_requests()
    {
        var animalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "metadata_readback");
        var id = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "HOM-METADATA");
        var route = $"/api/v1/animais/{animalId}/identificacoes/{id}";
        using var setValues = await client.PatchAsJsonAsync(route, new { dataAplicacao = "2026-09-11", observacao = "Conferida" });
        Assert.Equal(HttpStatusCode.OK, setValues.StatusCode);
        var withValues = await GetIdentificationAsync(client, animalId, id);
        Assert.Equal("2026-09-11", withValues.GetProperty("dataAplicacao").GetString());
        Assert.Equal("Conferida", withValues.GetProperty("observacao").GetString());

        using var clearObservation = await client.PatchAsJsonAsync(route, new { observacao = (string?)null });
        Assert.Equal(HttpStatusCode.OK, clearObservation.StatusCode);
        var withoutObservation = await GetIdentificationAsync(client, animalId, id);
        Assert.Equal(JsonValueKind.Null, withoutObservation.GetProperty("observacao").ValueKind);
        Assert.Equal("2026-09-11", withoutObservation.GetProperty("dataAplicacao").GetString());

        using var clearDate = await client.PatchAsJsonAsync(route, new { dataAplicacao = (string?)null });
        Assert.Equal(HttpStatusCode.OK, clearDate.StatusCode);
        var withoutDate = await GetIdentificationAsync(client, animalId, id);
        Assert.Equal(JsonValueKind.Null, withoutDate.GetProperty("dataAplicacao").ValueKind);
        Assert.Equal(JsonValueKind.Null, withoutDate.GetProperty("observacao").ValueKind);
    }

    [Fact]
    public async Task Each_nested_filter_selects_only_matching_markers_in_the_animal_scope()
    {
        var animalId = await SeedAnimalAsync(factory);
        var otherAnimalId = await SeedAnimalAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "independent_filters");
        var anilha = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "HOM-FILTER-RING");
        var principal = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Microchip, "HOM-FILTER-CHIP", principal: true);
        var historical = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "HOM-FILTER-OLD");
        await CreateIdentificationAsync(client, otherAnimalId, TipoIdentificacaoAnimal.Anilha, "HOM-FILTER-OTHER");
        using var inactivate = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{historical}/ativo", new { ativo = false });
        Assert.Equal(HttpStatusCode.OK, inactivate.StatusCode);

        var cases = new (string Query, Guid[] Expected)[]
        {
            ("tipo=1", [anilha, historical]),
            ("valor=filter-ring", [anilha]),
            ("ativo=true", [anilha, principal]),
            ("ativo=false", [historical]),
            ("principal=true", [principal]),
            ("principal=false", [anilha, historical]),
        };
        foreach (var (query, expected) in cases)
        {
            using var response = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes?{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await ReadJsonAsync(response);
            Assert.Equal(expected.Length, body.GetProperty("totalItems").GetInt32());
            Assert.Equal(expected.OrderBy(id => id), body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid()).OrderBy(id => id));
        }
    }

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
        AssertSafeProblem(await ReadJsonAsync(invalidEnum), HttpStatusCode.BadRequest);
        AssertSafeProblem(await ReadJsonAsync(emptyValue), HttpStatusCode.BadRequest);

        var id = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Brinco, "B-API-1");
        using var noMetadata = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}", new { });
        using var inactivate = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}/ativo", new { ativo = false });
        using var inactivePrincipal = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{id}/principal", new { principal = true });
        using var duplicate = await client.PostAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes", new { tipo = (int)TipoIdentificacaoAnimal.Brinco, valor = "B-API-1", principal = false });
        Assert.Equal(HttpStatusCode.BadRequest, noMetadata.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inactivate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inactivePrincipal.StatusCode);
        AssertSafeProblem(await ReadJsonAsync(noMetadata), HttpStatusCode.BadRequest);
        AssertSafeProblem(await ReadJsonAsync(inactivePrincipal), HttpStatusCode.BadRequest);
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
        AssertSafeProblem(await ReadJsonAsync(missingAnimal), HttpStatusCode.NotFound);
        AssertSafeProblem(await ReadJsonAsync(missingRow), HttpStatusCode.NotFound);
        AssertSafeProblem(await ReadJsonAsync(mismatchedRow), HttpStatusCode.NotFound);
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
        var principal = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Brinco, "Global-B", principal: true);
        var historical = await CreateIdentificationAsync(client, animalId, TipoIdentificacaoAnimal.Anilha, "Global-Old");
        using var inactivate = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/identificacoes/{historical}/ativo", new { ativo = false });
        Assert.Equal(HttpStatusCode.OK, inactivate.StatusCode);

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
        var filterCases = new (string Query, Guid[] Expected)[]
        {
            ("tipo=1", [first, historical]),
            ("ativo=true", [first, principal]),
            ("ativo=false", [historical]),
            ("principal=true", [principal]),
            ("principal=false", [first, historical]),
        };
        foreach (var (query, expected) in filterCases)
        {
            using var filtered = await client.GetAsync($"/api/v1/identificacoes-animal?valor=global&{query}");
            Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
            var filteredBody = await ReadJsonAsync(filtered);
            Assert.Equal(expected.Length, filteredBody.GetProperty("totalItems").GetInt32());
            Assert.Equal(expected.OrderBy(id => id), filteredBody.GetProperty("items").EnumerateArray()
                .Select(row => row.GetProperty("identificacao").GetProperty("id").GetGuid()).OrderBy(id => id));
        }
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

    private static async Task<JsonElement> GetIdentificationAsync(HttpClient client, Guid animalId, Guid id)
    {
        using var response = await client.GetAsync($"/api/v1/animais/{animalId}/identificacoes/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
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

    private static void AssertSafeProblem(JsonElement problem, HttpStatusCode expectedStatus = HttpStatusCode.Conflict)
    {
        Assert.Equal(JsonValueKind.Object, problem.ValueKind);
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        foreach (var property in problem.EnumerateObject())
        {
            // ASP.NET's correlation ID is not a diagnostic stack trace. Its value is still checked.
            if (property.Name == "traceId")
                Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$", property.Value.GetString()!);

            foreach (var forbidden in new[] { "Postgres", "PostgresException", "Npgsql", "SQL", "constraint", "stack", "trace", "DbUpdateException", "GenSWDbContext", "UX_IdentificacoesAnimal", "CK_IdentificacoesAnimal" })
            {
                if (property.Name != "traceId")
                    Assert.DoesNotContain(forbidden, property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(forbidden, property.Value.GetRawText(), StringComparison.OrdinalIgnoreCase);
            }
        }
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
