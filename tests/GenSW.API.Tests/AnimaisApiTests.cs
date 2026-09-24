using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GenSW.API.Tests;

public sealed class AnimaisApiTests(AnimalApiPostgreSqlFixture fixture) : IClassFixture<AnimalApiPostgreSqlFixture>
{
    private readonly AuthWebApplicationFactory factory = fixture.Factory;
    [Fact]
    public async Task Animais_require_authentication_and_support_manual_lifecycle_with_a_full_snapshot()
    {
        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/animais")).StatusCode);

        var classifications = await SeedClassificationsAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_lifecycle");
        var created = await CreateAsync(client, " AN-API-001 ", "  Bela   Animal ", classifications);

        Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
        Assert.EndsWith($"/api/v1/animais/{created.Id}", created.Response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("AN-API-001", created.Body.GetProperty("codigoInterno").GetString());
        Assert.Equal("Bela Animal", created.Body.GetProperty("nome").GetString());
        Assert.Equal(classifications.Especie.Id, created.Body.GetProperty("especie").GetProperty("id").GetGuid());
        Assert.Equal(classifications.Raca.Id, created.Body.GetProperty("raca").GetProperty("id").GetGuid());
        Assert.Equal(classifications.Variedade.Id, created.Body.GetProperty("variedade").GetProperty("id").GetGuid());

        using var get = await client.GetAsync($"/api/v1/animais/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        using var update = await client.PutAsJsonAsync($"/api/v1/animais/{created.Id}", new
        {
            codigoInterno = "AN-API-002",
            nome = "Bela atualizada",
            especieId = classifications.Especie.Id,
            racaId = (Guid?)null,
            variedadeId = (Guid?)null,
            sexo = 1,
            dataNascimento = "2020-01-02",
            escopo = 2,
        });
        var updated = await ReadJsonAsync(update);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("AN-API-002", updated.GetProperty("codigoInterno").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("raca").ValueKind);
        Assert.Equal(1, updated.GetProperty("sexo").GetInt32());
        Assert.Equal(2, updated.GetProperty("escopo").GetInt32());

        using var deactivate = await client.PatchAsJsonAsync($"/api/v1/animais/{created.Id}/ativo", new { ativo = false });
        using var deactivateAgain = await client.PatchAsJsonAsync($"/api/v1/animais/{created.Id}/ativo", new { ativo = false });
        Assert.False((await ReadJsonAsync(deactivate)).GetProperty("ativo").GetBoolean());
        Assert.False((await ReadJsonAsync(deactivateAgain)).GetProperty("ativo").GetBoolean());

        using var activate = await client.PatchAsJsonAsync($"/api/v1/animais/{created.Id}/ativo", new { ativo = true });
        Assert.True((await ReadJsonAsync(activate)).GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task Animal_base_lifecycle_remains_independent_when_no_identifiers_exist()
    {
        var classifications = await SeedClassificationsAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_without_identifications");

        using var create = await client.PostAsJsonAsync("/api/v1/animais", new
        {
            codigoInterno = "AN-NO-IDENTIFICATION",
            nome = "Animal sem identificação",
            especieId = classifications.Especie.Id,
            racaId = (Guid?)null,
            variedadeId = (Guid?)null,
            sexo = 1,
            dataNascimento = "2020-01-02",
            escopo = 1,
        });
        var created = await ReadJsonAsync(create);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var animalId = created.GetProperty("id").GetGuid();
        Assert.Equal(JsonValueKind.Null, created.GetProperty("raca").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("variedade").ValueKind);

        using var update = await client.PutAsJsonAsync($"/api/v1/animais/{animalId}", new
        {
            codigoInterno = "AN-NO-IDENTIFICATION-EDITED",
            nome = "Animal editado",
            especieId = classifications.Especie.Id,
            racaId = (Guid?)null,
            variedadeId = (Guid?)null,
            sexo = 2,
            dataNascimento = "2021-02-03",
            escopo = 2,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var deactivate = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/ativo", new { ativo = false });
        using var activate = await client.PatchAsJsonAsync($"/api/v1/animais/{animalId}/ativo", new { ativo = true });
        Assert.False((await ReadJsonAsync(deactivate)).GetProperty("ativo").GetBoolean());
        Assert.True((await ReadJsonAsync(activate)).GetProperty("ativo").GetBoolean());
        Assert.Equal("Animal editado", (await ReadJsonAsync(activate)).GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Animais_list_applies_defaults_filters_case_insensitive_search_sorting_and_offset_pages()
    {
        var classifications = await SeedClassificationsAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_list");
        var first = await CreateAsync(client, "AN-LIST-A", "Zeta", classifications, sexo: 1, escopo: 1);
        var second = await CreateAsync(client, "AN-LIST-B", "Alpha", classifications, sexo: 2, escopo: 2);
        var third = await CreateAsync(client, "AN-LIST-C", "Busca Nome", classifications, sexo: 3, escopo: 1);
        using var inactive = await client.PatchAsJsonAsync($"/api/v1/animais/{third.Id}/ativo", new { ativo = false });
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);

        using var defaults = await client.GetAsync($"/api/v1/animais?especieId={classifications.Especie.Id}");
        var defaultBody = await ReadJsonAsync(defaults);
        Assert.Equal(HttpStatusCode.OK, defaults.StatusCode);
        Assert.Equal(1, defaultBody.GetProperty("page").GetInt32());
        Assert.Equal(25, defaultBody.GetProperty("pageSize").GetInt32());

        var expectedAll = new[] { first.Id, second.Id, third.Id };
        var filters = new[]
        {
            (Query: $"especieId={classifications.Especie.Id}", Expected: expectedAll),
            (Query: $"racaId={classifications.Raca.Id}", Expected: expectedAll),
            (Query: $"variedadeId={classifications.Variedade.Id}", Expected: expectedAll),
            (Query: $"especieId={classifications.Especie.Id}&sexo=1", Expected: new[] { first.Id }),
            (Query: $"especieId={classifications.Especie.Id}&escopo=2", Expected: new[] { second.Id }),
            (Query: $"especieId={classifications.Especie.Id}&ativo=false", Expected: new[] { third.Id }),
        };
        foreach (var filter in filters)
        {
            using var response = await client.GetAsync($"/api/v1/animais?{filter.Query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertResponseItems(await ReadJsonAsync(response), filter.Expected);
        }

        var expectedIdsBySort = new Dictionary<string, Guid[]>
        {
            ["codigoInterno"] = [first.Id, second.Id, third.Id],
            ["nome"] = [second.Id, third.Id, first.Id],
            ["sexo"] = [first.Id, second.Id, third.Id],
        };
        foreach (var (sortBy, expectedAscending) in expectedIdsBySort)
        {
            await AssertOrderedListAsync(client, classifications.Especie.Id, sortBy, "asc", expectedAscending);
            await AssertOrderedListAsync(client, classifications.Especie.Id, sortBy, "desc", expectedAscending.Reverse().ToArray());
        }

        await AssertOrderedValuesAsync(client, classifications.Especie.Id, "escopo", "asc", "escopo", [1, 1, 2]);
        await AssertOrderedValuesAsync(client, classifications.Especie.Id, "escopo", "desc", "escopo", [2, 1, 1]);
        await AssertOrderedValuesAsync(client, classifications.Especie.Id, "ativo", "asc", "ativo", [0, 1, 1]);
        await AssertOrderedValuesAsync(client, classifications.Especie.Id, "ativo", "desc", "ativo", [1, 1, 0]);
        await AssertCreatedAtOrderAsync(client, classifications.Especie.Id, "asc");
        await AssertCreatedAtOrderAsync(client, classifications.Especie.Id, "desc");

        using var codeSearch = await client.GetAsync($"/api/v1/animais?search=an-list-a");
        using var nameSearch = await client.GetAsync($"/api/v1/animais?search=busca%20nome");
        using var offset = await client.GetAsync($"/api/v1/animais?especieId={classifications.Especie.Id}&page=2&pageSize=1&sortBy=codigoInterno&sortDirection=asc");
        AssertResponseItems(await ReadJsonAsync(codeSearch), [first.Id]);
        AssertResponseItems(await ReadJsonAsync(nameSearch), [third.Id]);
        var offsetBody = await ReadJsonAsync(offset);
        Assert.Equal(2, offsetBody.GetProperty("page").GetInt32());
        Assert.Equal(1, offsetBody.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, offsetBody.GetProperty("totalItems").GetInt32());
        Assert.Equal(3, offsetBody.GetProperty("totalPages").GetInt32());
        AssertResponseItems(offsetBody, [second.Id]);
    }

    [Fact]
    public async Task Automatic_creation_uses_the_postgresql_sequence()
    {
        var classifications = await SeedClassificationsAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_automatic");
        using var response = await client.PostAsJsonAsync("/api/v1/animais", new
        {
            codigoInterno = (string?)null,
            nome = "Automático",
            especieId = classifications.Especie.Id,
            racaId = classifications.Raca.Id,
            variedadeId = classifications.Variedade.Id,
            sexo = 2,
            dataNascimento = "2020-01-02",
            escopo = 1,
        });
        var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Matches("^AN-[0-9]{6}$", body.GetProperty("codigoInterno").GetString());
    }

    [Fact]
    public async Task Animais_report_validation_missing_and_duplicate_failures_without_database_details()
    {
        var classifications = await SeedClassificationsAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_failures");
        var created = await CreateAsync(client, "AN-FAIL-001", "Falha", classifications);

        using var invalidPolicy = await client.PostAsJsonAsync("/api/v1/animais", new { codigoInterno = "AN-FUTURE", especieId = classifications.Especie.Id, sexo = 1, dataNascimento = "9999-01-01", escopo = 1 });
        using var missingSpecies = await client.PostAsJsonAsync("/api/v1/animais", new { codigoInterno = "AN-MISSING-SPECIES", especieId = Guid.NewGuid(), sexo = 1, escopo = 1 });
        using var missingBreed = await client.PostAsJsonAsync("/api/v1/animais", new { codigoInterno = "AN-MISSING-BREED", especieId = classifications.Especie.Id, racaId = Guid.NewGuid(), sexo = 1, escopo = 1 });
        using var missingVariety = await client.PostAsJsonAsync("/api/v1/animais", new { codigoInterno = "AN-MISSING-VARIETY", especieId = classifications.Especie.Id, variedadeId = Guid.NewGuid(), sexo = 1, escopo = 1 });
        using var missingGet = await client.GetAsync($"/api/v1/animais/{Guid.NewGuid()}");
        using var missingPut = await client.PutAsJsonAsync($"/api/v1/animais/{Guid.NewGuid()}", new { codigoInterno = "AN-MISSING", especieId = classifications.Especie.Id, sexo = 1, escopo = 1 });
        using var missingPatch = await client.PatchAsJsonAsync($"/api/v1/animais/{Guid.NewGuid()}/ativo", new { ativo = false });
        using var duplicate = await client.PostAsJsonAsync("/api/v1/animais", new { codigoInterno = "  an-fail-001 ", especieId = classifications.Especie.Id, sexo = 1, escopo = 1 });
        using var invalidQuery = await client.GetAsync("/api/v1/animais?page=0&sortBy=invalid&sortDirection=sideways&sexo=99");

        Assert.Equal(HttpStatusCode.BadRequest, invalidPolicy.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingSpecies.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingBreed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingVariety.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingPut.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingPatch.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidQuery.StatusCode);
        AssertSafeConflict(await ReadJsonAsync(duplicate), "The animal code is already in use.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task Create_rejects_each_invalid_codigo_interno_in_isolation(string codigoInterno)
    {
        var classifications = await SeedClassificationsAsync(factory);
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_invalid_code");
        using var response = await client.PostAsJsonAsync("/api/v1/animais", new
        {
            codigoInterno,
            especieId = classifications.Especie.Id,
            sexo = 1,
            escopo = 1,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_an_empty_species_id_in_isolation()
    {
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_empty_species");
        using var response = await client.PostAsJsonAsync("/api/v1/animais", new
        {
            codigoInterno = "AN-EMPTY-SPECIES",
            especieId = Guid.Empty,
            sexo = 1,
            escopo = 1,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("sexo=99")]
    [InlineData("escopo=99")]
    public async Task List_rejects_each_invalid_enum_query_value_in_isolation(string query)
    {
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_invalid_enum_query");
        using var response = await client.GetAsync($"/api/v1/animais?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_inactive_and_incompatible_destinations_without_changing_the_animal()
    {
        var classifications = await SeedClassificationsAsync(factory);
        var incompatible = await SeedClassificationsAsync(factory);
        var inactiveBreed = Raca.Criar(classifications.Especie.Id, "Raça inativa " + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        inactiveBreed.Inativar(DateTimeOffset.UtcNow);
        await factory.ExecuteDbContextAsync(async context =>
        {
            context.Racas.Add(inactiveBreed);
            await context.SaveChangesAsync();
        });
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_update_destinations");
        var created = await CreateAsync(client, "AN-DEST-001", "Original", classifications);

        using var inactiveResponse = await client.PutAsJsonAsync($"/api/v1/animais/{created.Id}", new
        {
            codigoInterno = "AN-DEST-CHANGED",
            nome = "Alteração inválida",
            especieId = classifications.Especie.Id,
            racaId = inactiveBreed.Id,
            variedadeId = classifications.Variedade.Id,
            sexo = 1,
            escopo = 1,
        });
        Assert.Equal(HttpStatusCode.BadRequest, inactiveResponse.StatusCode);
        await AssertAnimalUnchangedAsync(client, created.Id, "AN-DEST-001", "Original", classifications.Especie.Id, classifications.Raca.Id);

        using var incompatibleResponse = await client.PutAsJsonAsync($"/api/v1/animais/{created.Id}", new
        {
            codigoInterno = "AN-DEST-CHANGED-2",
            nome = "Outra alteração inválida",
            especieId = classifications.Especie.Id,
            racaId = incompatible.Raca.Id,
            variedadeId = classifications.Variedade.Id,
            sexo = 1,
            escopo = 1,
        });
        Assert.Equal(HttpStatusCode.BadRequest, incompatibleResponse.StatusCode);
        await AssertAnimalUnchangedAsync(client, created.Id, "AN-DEST-001", "Original", classifications.Especie.Id, classifications.Raca.Id);
    }

    [Fact]
    public async Task Automatic_creation_returns_a_safe_conflict_after_five_deterministic_collisions()
    {
        var classifications = await SeedClassificationsAsync(factory);
        var candidates = Enumerable.Range(1, 5).Select(index => $"AN-COLLISION-{index:D2}").ToArray();
        await SeedAnimalsAsync(factory, classifications, candidates);
        var scenario = new AnimalCodeAllocationScenario(candidates);
        using var scenarioFactory = CreateFactoryWithAllocatorScenario(scenario);
        var user = await factory.SeedUserAsync($"animais_collisions_{Guid.NewGuid():N}");
        using var client = await CreateAuthenticatedClientAsync(scenarioFactory, user);

        using var response = await client.PostAsJsonAsync("/api/v1/animais", CreateAutomaticRequest(classifications));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(5, scenario.AllocationCount);
        AssertSafeConflict(await ReadJsonAsync(response), "The automatic animal code allocation limit was reached.");
    }

    [Fact]
    public async Task Automatic_creation_returns_a_safe_conflict_when_the_sequence_is_exhausted()
    {
        var classifications = await SeedClassificationsAsync(factory);
        var scenario = new AnimalCodeAllocationScenario([], new AnimalCodeSequenceExhaustedException());
        using var scenarioFactory = CreateFactoryWithAllocatorScenario(scenario);
        var user = await factory.SeedUserAsync($"animais_exhausted_{Guid.NewGuid():N}");
        using var client = await CreateAuthenticatedClientAsync(scenarioFactory, user);

        using var response = await client.PostAsJsonAsync("/api/v1/animais", CreateAutomaticRequest(classifications));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, scenario.AllocationCount);
        AssertSafeConflict(await ReadJsonAsync(response), "No more automatic animal codes are available.");
    }

    [Fact]
    public async Task Invalid_mutation_and_list_query_responses_do_not_expose_exception_details()
    {
        const string sensitiveMessage = "SQLSTATE 23505; PostgreSQL Detail; constraint; stack trace";
        using var scenarioFactory = CreateFactoryWithAnimalService(new ThrowingAnimalService(new ArgumentException(sensitiveMessage)));
        var user = await factory.SeedUserAsync($"animais_safe_bad_request_{Guid.NewGuid():N}");
        using var client = await CreateAuthenticatedClientAsync(scenarioFactory, user);

        using var mutation = await client.PostAsJsonAsync("/api/v1/animais", new
        {
            codigoInterno = "AN-SAFE-400",
            especieId = Guid.NewGuid(),
            sexo = 1,
            escopo = 1,
        });
        using var list = await client.GetAsync("/api/v1/animais?page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.BadRequest, mutation.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
        AssertSafeBadRequest(await ReadJsonAsync(mutation), "The submitted animal data is invalid.");
        AssertSafeBadRequest(await ReadJsonAsync(list), "The supplied list query is invalid.");
    }

    [Fact]
    public async Task Delete_is_not_an_allowed_animal_operation()
    {
        using var client = await CreateAuthenticatedClientAsync(factory, "animais_delete");
        using var response = await client.DeleteAsync($"/api/v1/animais/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static async Task<(Especie Especie, Raca Raca, Variedade Variedade)> SeedClassificationsAsync(AuthWebApplicationFactory webFactory, bool active = true)
    {
        var now = DateTimeOffset.UtcNow;
        var especie = Especie.Criar("Espécie " + Guid.NewGuid().ToString("N"), null, now);
        var raca = Raca.Criar(especie.Id, "Raça " + Guid.NewGuid().ToString("N"), now);
        var variedade = Variedade.Criar(especie.Id, "Variedade " + Guid.NewGuid().ToString("N"), now);
        if (!active)
        {
            raca.Inativar(now.AddMinutes(1));
            variedade.Inativar(now.AddMinutes(1));
        }

        await webFactory.ExecuteDbContextAsync(async context =>
        {
            context.Especies.Add(especie);
            context.Racas.Add(raca);
            context.Variedades.Add(variedade);
            await context.SaveChangesAsync();
        });
        return (especie, raca, variedade);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(AuthWebApplicationFactory webFactory, string prefix)
    {
        var user = await webFactory.SeedUserAsync($"{prefix}_{Guid.NewGuid():N}");
        return await CreateAuthenticatedClientAsync(webFactory, user);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> webFactory, SeededUser user)
    {
        var client = webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var (token, _) = await login.ReadAccessTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<(HttpResponseMessage Response, Guid Id, JsonElement Body)> CreateAsync(
        HttpClient client,
        string codigoInterno,
        string? nome,
        (Especie Especie, Raca Raca, Variedade Variedade) classifications,
        int sexo = 2,
        int escopo = 1)
    {
        var response = await client.PostAsJsonAsync("/api/v1/animais", new
        {
            codigoInterno,
            nome,
            especieId = classifications.Especie.Id,
            racaId = classifications.Raca.Id,
            variedadeId = classifications.Variedade.Id,
            sexo,
            dataNascimento = "2020-01-02",
            escopo,
        });
        var body = await ReadJsonAsync(response);
        return (response, body.GetProperty("id").GetGuid(), body);
    }

    private static object CreateAutomaticRequest((Especie Especie, Raca Raca, Variedade Variedade) classifications) => new
    {
        codigoInterno = (string?)null,
        nome = "Automático",
        especieId = classifications.Especie.Id,
        racaId = classifications.Raca.Id,
        variedadeId = classifications.Variedade.Id,
        sexo = 2,
        dataNascimento = "2020-01-02",
        escopo = 1,
    };

    private WebApplicationFactory<Program> CreateFactoryWithAllocatorScenario(AnimalCodeAllocationScenario scenario) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAnimalCodeAllocator>();
            services.AddSingleton(scenario);
            services.AddScoped<IAnimalCodeAllocator, ScenarioAnimalCodeAllocator>();
        }));

    private WebApplicationFactory<Program> CreateFactoryWithAnimalService(IAnimalService animalService) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAnimalService>();
            services.AddScoped<IAnimalService>(_ => animalService);
        }));

    private static async Task SeedAnimalsAsync(
        AuthWebApplicationFactory webFactory,
        (Especie Especie, Raca Raca, Variedade Variedade) classifications,
        IReadOnlyCollection<string> codes)
    {
        var now = DateTimeOffset.UtcNow;
        await webFactory.ExecuteDbContextAsync(async context =>
        {
            foreach (var code in codes)
            {
                context.Animais.Add(Animal.Criar(
                    code,
                    null,
                    classifications.Especie.Id,
                    classifications.Raca.Id,
                    classifications.Variedade.Id,
                    SexoAnimal.Femea,
                    new DateOnly(2020, 1, 2),
                    EscopoAnimal.Operacional,
                    DateOnly.FromDateTime(now.UtcDateTime),
                    now));
            }

            await context.SaveChangesAsync();
        });
    }

    private static void AssertResponseItems(JsonElement body, IReadOnlyCollection<Guid> expectedIds)
    {
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(expectedIds.Count, items.Length);
        Assert.Equal(expectedIds, items.Select(item => item.GetProperty("id").GetGuid()).ToArray());
    }

    private static async Task AssertOrderedListAsync(HttpClient client, Guid especieId, string sortBy, string direction, IReadOnlyCollection<Guid> expectedIds)
    {
        using var response = await client.GetAsync($"/api/v1/animais?especieId={especieId}&sortBy={sortBy}&sortDirection={direction}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertResponseItems(await ReadJsonAsync(response), expectedIds);
    }

    private static async Task AssertOrderedValuesAsync(HttpClient client, Guid especieId, string sortBy, string direction, string propertyName, IReadOnlyCollection<int> expectedValues)
    {
        using var response = await client.GetAsync($"/api/v1/animais?especieId={especieId}&sortBy={sortBy}&sortDirection={direction}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var values = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()
            .Select(item => propertyName == "ativo" ? item.GetProperty(propertyName).GetBoolean() ? 1 : 0 : item.GetProperty(propertyName).GetInt32())
            .ToArray();
        var comparableExpected = propertyName == "ativo"
            ? expectedValues.Select(value => value == 1 ? 1 : 0).ToArray()
            : expectedValues.ToArray();
        Assert.Equal(comparableExpected, values);
        Assert.Equal(3, values.Length);
    }

    private static async Task AssertCreatedAtOrderAsync(HttpClient client, Guid especieId, string direction)
    {
        using var response = await client.GetAsync($"/api/v1/animais?especieId={especieId}&sortBy=createdAtUtc&sortDirection={direction}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var timestamps = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("createdAtUtc").GetDateTimeOffset())
            .ToArray();
        Assert.Equal(3, timestamps.Length);
        var ordered = direction == "asc"
            ? timestamps.OrderBy(value => value).ToArray()
            : timestamps.OrderByDescending(value => value).ToArray();
        Assert.Equal(ordered, timestamps);
    }

    private static async Task AssertAnimalUnchangedAsync(HttpClient client, Guid id, string expectedCode, string expectedName, Guid expectedSpeciesId, Guid expectedBreedId)
    {
        using var response = await client.GetAsync($"/api/v1/animais/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var animal = await ReadJsonAsync(response);
        Assert.Equal(expectedCode, animal.GetProperty("codigoInterno").GetString());
        Assert.Equal(expectedName, animal.GetProperty("nome").GetString());
        Assert.Equal(expectedSpeciesId, animal.GetProperty("especieId").GetGuid());
        Assert.Equal(expectedBreedId, animal.GetProperty("racaId").GetGuid());
    }

    private static void AssertSafeConflict(JsonElement body, string expectedDetail)
    {
        Assert.Equal(expectedDetail, body.GetProperty("detail").GetString());
        var serialized = body.GetRawText();
        foreach (var forbidden in new[] { "SQLSTATE", "PostgreSQL Detail", "constraint", "stack trace" })
        {
            Assert.DoesNotContain(forbidden, serialized, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertSafeBadRequest(JsonElement body, string expectedDetail)
    {
        Assert.Equal(expectedDetail, body.GetProperty("detail").GetString());
        var serialized = body.GetRawText();
        foreach (var forbidden in new[] { "SQLSTATE", "PostgreSQL Detail", "constraint", "stack trace" })
        {
            Assert.DoesNotContain(forbidden, serialized, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}

internal sealed class AnimalCodeAllocationScenario(IReadOnlyList<string> codes, Exception? allocationFailure = null)
{
    private int nextCodeIndex;

    public int AllocationCount { get; private set; }

    public string Allocate()
    {
        AllocationCount++;
        if (allocationFailure is not null)
        {
            throw allocationFailure;
        }

        return codes[nextCodeIndex++];
    }
}

internal sealed class ScenarioAnimalCodeAllocator(GenSWDbContext context, AnimalCodeAllocationScenario scenario) : IAnimalCodeAllocator
{
    public Task<IAnimalAutomaticCodeAttempt> BeginAttemptAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IAnimalAutomaticCodeAttempt>(new ScenarioAnimalCodeAttempt(context, scenario));
}

internal sealed class ScenarioAnimalCodeAttempt(GenSWDbContext context, AnimalCodeAllocationScenario scenario) : IAnimalAutomaticCodeAttempt
{
    public Task<string> AllocateNextCodigoInternoAsync(CancellationToken cancellationToken = default) => Task.FromResult(scenario.Allocate());

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAndDetachAsync(Animal failedAnimal, CancellationToken cancellationToken = default)
    {
        context.Entry(failedAnimal).State = EntityState.Detached;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class ThrowingAnimalService(Exception exception) : IAnimalService
{
    public Task<AnimalResult> CreateAsync(CreateAnimalCommand command, CancellationToken cancellationToken = default) =>
        Task.FromException<AnimalResult>(exception);

    public Task<AnimalResult?> GetByIdAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        Task.FromException<AnimalResult?>(exception);

    public Task<PagedAnimalResult> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default) =>
        Task.FromException<PagedAnimalResult>(exception);

    public Task<AnimalResult> UpdateAsync(Guid animalId, UpdateAnimalCommand command, CancellationToken cancellationToken = default) =>
        Task.FromException<AnimalResult>(exception);

    public Task<AnimalResult> SetActiveAsync(Guid animalId, bool ativo, CancellationToken cancellationToken = default) =>
        Task.FromException<AnimalResult>(exception);
}

public sealed class AnimalApiPostgreSqlFixture : IAsyncLifetime
{
    private EphemeralPostgreSql? postgreSql;

    public AuthWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        postgreSql = await EphemeralPostgreSql.StartAsync();
        Factory = new AuthWebApplicationFactory(postgreSql.ConnectionString);
        await Factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await ((IAsyncLifetime)Factory).DisposeAsync();
        }

        if (postgreSql is not null)
        {
            await postgreSql.DisposeAsync();
        }
    }
}
