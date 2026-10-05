using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class PropriedadesApiTests(AnimalApiPostgreSqlFixture fixture)
{
    private readonly AuthWebApplicationFactory factory = fixture.Factory;

    [Fact]
    public async Task All_property_and_animal_location_routes_require_authentication()
    {
        using var client = factory.CreateHttpsClient();
        var id = Guid.NewGuid();
        var requests = new (HttpMethod Method, string Route, object? Body)[]
        {
            (HttpMethod.Get, "/api/v1/propriedades", null),
            (HttpMethod.Post, "/api/v1/propriedades", new { nome = "Anônima" }),
            (HttpMethod.Get, $"/api/v1/propriedades/{id}", null),
            (HttpMethod.Put, $"/api/v1/propriedades/{id}", new { nome = "Anônima" }),
            (HttpMethod.Patch, $"/api/v1/propriedades/{id}/ativo", new { ativo = false }),
            (HttpMethod.Get, $"/api/v1/animais/{id}/propriedades", null),
            (HttpMethod.Post, $"/api/v1/animais/{id}/propriedade/transferir", new { propriedadeId = id, dataInicio = "2026-01-01" }),
            (HttpMethod.Post, $"/api/v1/animais/{id}/propriedade/desvincular", new { vinculoAtualIdEsperado = id, dataFim = "2026-01-01" }),
        };
        foreach (var (method, route, body) in requests)
        {
            using var request = new HttpRequestMessage(method, route);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Non_admin_can_create_view_edit_inactivate_and_reactivate_a_physical_property()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var unique = Guid.NewGuid().ToString("N");
        using var create = await client.PostAsJsonAsync("/api/v1/propriedades", new
        {
            nome = $"  Sítio   {unique}  ", localizacao = "  Estrada rural, km 2  ", observacao = "  Unidade de criação  ",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var body = await PropertyTestHttp.ReadAsync(create);
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.EndsWith($"/api/v1/propriedades/{id}", create.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"Sítio {unique}", body.GetProperty("nome").GetString());
        Assert.Equal("Estrada rural, km 2", body.GetProperty("localizacao").GetString());
        Assert.Equal("Unidade de criação", body.GetProperty("observacao").GetString());
        Assert.True(body.GetProperty("ativo").GetBoolean());
        Assert.True(body.GetProperty("createdAtUtc").GetDateTimeOffset() <= body.GetProperty("updatedAtUtc").GetDateTimeOffset());

        using var edit = await client.PutAsJsonAsync($"/api/v1/propriedades/{id}", new { nome = $"Unidade {unique}", localizacao = (string?)null, observacao = " " });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await PropertyTestHttp.ReadAsync(edit);
        Assert.Equal($"Unidade {unique}", edited.GetProperty("nome").GetString());
        Assert.Equal(JsonValueKind.Null, edited.GetProperty("localizacao").ValueKind);
        Assert.Equal(JsonValueKind.Null, edited.GetProperty("observacao").ValueKind);

        foreach (var active in new[] { false, false, true })
        {
            using var status = await client.PatchAsJsonAsync($"/api/v1/propriedades/{id}/ativo", new { ativo = active });
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            using var get = await client.GetAsync($"/api/v1/propriedades/{id}");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            Assert.Equal(active, (await PropertyTestHttp.ReadAsync(get)).GetProperty("ativo").GetBoolean());
        }
    }

    [Fact]
    public async Task Search_status_ordering_and_pagination_select_persisted_properties()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var prefix = $"Busca-{Guid.NewGuid():N}";
        var alpha = await PropertyTestHttp.CreatePropertyAsync(client, $"{prefix} Alpha");
        var beta = await PropertyTestHttp.CreatePropertyAsync(client, $"{prefix} Beta");
        var gamma = await PropertyTestHttp.CreatePropertyAsync(client, $"{prefix} Gamma");
        using var inactive = await client.PatchAsJsonAsync($"/api/v1/propriedades/{gamma}/ativo", new { ativo = false });
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);

        var first = await ListAsync(client, $"search={prefix.ToLowerInvariant()}&sortBy=nome&sortDirection=asc&page=1&pageSize=2");
        Assert.Equal(new[] { alpha, beta }, Ids(first));
        Assert.Equal(3, first.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, first.GetProperty("page").GetInt32());
        Assert.Equal(2, first.GetProperty("pageSize").GetInt32());
        Assert.Equal(new[] { gamma }, Ids(await ListAsync(client, $"search={prefix}&page=2&pageSize=2")));
        Assert.Equal(new[] { gamma, beta, alpha }, Ids(await ListAsync(client, $"search={prefix}&sortDirection=desc")));
        Assert.Equal(new[] { alpha, beta }, Ids(await ListAsync(client, $"search={prefix}&ativo=true")));
        Assert.Equal(new[] { gamma }, Ids(await ListAsync(client, $"search={prefix}&ativo=false")));
        Assert.Empty(Ids(await ListAsync(client, $"search={prefix}&page=99")));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("sortBy=unknown")]
    [InlineData("sortDirection=sideways")]
    [InlineData("ativo=unknown")]
    public async Task Invalid_search_queries_return_400(string query)
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        using var response = await client.GetAsync($"/api/v1/propriedades?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Validation_duplicates_and_missing_resources_have_explicit_status_and_preserve_existing_data()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var name = $"Única {Guid.NewGuid():N}";
        var id = await PropertyTestHttp.CreatePropertyAsync(client, name);
        var other = await PropertyTestHttp.CreatePropertyAsync(client, $"Outra {Guid.NewGuid():N}");
        foreach (var request in new object[]
        {
            new { nome = " " }, new { nome = new string('n', 201) },
            new { nome = name, localizacao = new string('l', 501) },
            new { nome = name, observacao = new string('o', 2001) },
        })
        {
            using var invalid = await client.PostAsJsonAsync("/api/v1/propriedades", request);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var duplicate = await client.PostAsJsonAsync("/api/v1/propriedades", new { nome = $"  {name.ToUpperInvariant()}  " });
        using var duplicateEdit = await client.PutAsJsonAsync($"/api/v1/propriedades/{other}", new { nome = name.ToLowerInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateEdit.StatusCode);
        var problem = await PropertyTestHttp.ReadAsync(duplicate);
        Assert.False(problem.ToString().Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
        Assert.False(problem.ToString().Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        using var invalidEdit = await client.PutAsJsonAsync($"/api/v1/propriedades/{id}", new { nome = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalidEdit.StatusCode);
        using var preserved = await client.GetAsync($"/api/v1/propriedades/{id}");
        Assert.Equal(name, (await PropertyTestHttp.ReadAsync(preserved)).GetProperty("nome").GetString());
        var missing = Guid.NewGuid();
        using var get = await client.GetAsync($"/api/v1/propriedades/{missing}");
        using var edit = await client.PutAsJsonAsync($"/api/v1/propriedades/{missing}", new { nome = "Ausente" });
        using var status = await client.PatchAsJsonAsync($"/api/v1/propriedades/{missing}/ativo", new { ativo = false });
        using var history = await client.GetAsync($"/api/v1/animais/{missing}/propriedades");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
    }

    [Theory]
    [InlineData(EscopoAnimal.Operacional)]
    [InlineData(EscopoAnimal.Referencia)]
    public async Task Creating_and_editing_an_animal_without_a_property_remains_valid(EscopoAnimal scope)
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var species = await PropertyTestHttp.SeedSpeciesAsync(factory);
        var code = $"SEM-{Guid.NewGuid():N}";
        var request = new { codigoInterno = code, nome = "Sem propriedade", especieId = species, sexo = 1, escopo = scope };
        using var create = await client.PostAsJsonAsync("/api/v1/animais", request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await PropertyTestHttp.ReadAsync(create)).GetProperty("id").GetGuid();
        using var edit = await client.PutAsJsonAsync($"/api/v1/animais/{id}", request);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var state = await PropertyTestHttp.StateAsync(client, id);
        Assert.Equal(JsonValueKind.Null, state.GetProperty("atual").ValueKind);
        Assert.Empty(state.GetProperty("historico").EnumerateArray());
    }

    [Fact]
    public async Task Transfer_detach_and_relink_preserve_history_and_filiation_as_the_source_of_pedigree()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var animal = await PropertyTestHttp.SeedAnimalAsync(factory);
        var parent = await PropertyTestHttp.SeedAnimalAsync(factory, speciesId: animal.SpeciesId, scope: EscopoAnimal.Referencia);
        using var filiation = await client.PostAsJsonAsync($"/api/v1/animais/{animal.Id}/filiacoes", new { progenitorId = parent.Id, tipoFiliacao = 1 });
        Assert.Equal(HttpStatusCode.Created, filiation.StatusCode);
        using var pedigreeBefore = await client.GetAsync($"/api/v1/animais/{animal.Id}/pedigree");
        Assert.Equal(HttpStatusCode.OK, pedigreeBefore.StatusCode);
        var originalPedigree = await pedigreeBefore.Content.ReadAsStringAsync();
        var firstProperty = await PropertyTestHttp.CreatePropertyAsync(client);
        var secondProperty = await PropertyTestHttp.CreatePropertyAsync(client);
        var first = await PropertyTestHttp.TransferAsync(client, animal.Id, firstProperty, "2026-01-01", null);
        var firstId = first.GetProperty("atual").GetProperty("id").GetGuid();
        var second = await PropertyTestHttp.TransferAsync(client, animal.Id, secondProperty, "2026-02-01", firstId, "Transferência física");
        var secondId = second.GetProperty("atual").GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        var entries = second.GetProperty("historico").EnumerateArray().ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal("2026-02-01", entries.Single(entry => entry.GetProperty("id").GetGuid() == firstId).GetProperty("dataFim").GetString());
        Assert.Equal("Transferência física", second.GetProperty("atual").GetProperty("observacao").GetString());
        Assert.Single(entries, entry => entry.GetProperty("dataFim").ValueKind == JsonValueKind.Null);

        using var detach = await client.PostAsJsonAsync($"/api/v1/animais/{animal.Id}/propriedade/desvincular", new { dataFim = "2026-03-01", vinculoAtualIdEsperado = secondId });
        Assert.Equal(HttpStatusCode.OK, detach.StatusCode);
        var detached = await PropertyTestHttp.ReadAsync(detach);
        Assert.Equal(JsonValueKind.Null, detached.GetProperty("atual").ValueKind);
        Assert.Equal(2, detached.GetProperty("historico").GetArrayLength());
        Assert.All(detached.GetProperty("historico").EnumerateArray(), entry => Assert.Equal(JsonValueKind.String, entry.GetProperty("dataFim").ValueKind));
        var relink = await PropertyTestHttp.TransferAsync(client, animal.Id, firstProperty, "2026-03-01", null);
        Assert.Equal(3, relink.GetProperty("historico").GetArrayLength());
        Assert.Equal(firstProperty, relink.GetProperty("atual").GetProperty("propriedadeId").GetGuid());
        using var pedigreeAfter = await client.GetAsync($"/api/v1/animais/{animal.Id}/pedigree");
        Assert.Equal(originalPedigree, await pedigreeAfter.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Inactivation_preserves_current_links_and_inactive_destinations_or_stale_commands_cannot_mutate_history()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var animal = await PropertyTestHttp.SeedAnimalAsync(factory);
        var current = await PropertyTestHttp.CreatePropertyAsync(client);
        var inactive = await PropertyTestHttp.CreatePropertyAsync(client);
        var state = await PropertyTestHttp.TransferAsync(client, animal.Id, current, "2026-01-01", null);
        var linkId = state.GetProperty("atual").GetProperty("id").GetGuid();
        foreach (var id in new[] { current, inactive })
        {
            using var status = await client.PatchAsJsonAsync($"/api/v1/propriedades/{id}/ativo", new { ativo = false });
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        }
        var stillLinked = await PropertyTestHttp.StateAsync(client, animal.Id);
        Assert.Equal(linkId, stillLinked.GetProperty("atual").GetProperty("id").GetGuid());
        Assert.False(stillLinked.GetProperty("atual").GetProperty("propriedadeAtiva").GetBoolean());
        using var inactiveDestination = await PropertyTestHttp.PostTransferAsync(client, animal.Id, inactive, "2026-02-01", linkId);
        using var stale = await PropertyTestHttp.PostTransferAsync(client, animal.Id, current, "2026-02-01", Guid.NewGuid());
        using var staleDetach = await client.PostAsJsonAsync($"/api/v1/animais/{animal.Id}/propriedade/desvincular", new { dataFim = "2026-02-01", vinculoAtualIdEsperado = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, inactiveDestination.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleDetach.StatusCode);
        Assert.Equal(stillLinked.ToString(), (await PropertyTestHttp.StateAsync(client, animal.Id)).ToString());
        using var detach = await client.PostAsJsonAsync($"/api/v1/animais/{animal.Id}/propriedade/desvincular", new { dataFim = "2026-02-01", vinculoAtualIdEsperado = linkId });
        Assert.Equal(HttpStatusCode.OK, detach.StatusCode);
    }

    [Fact]
    public async Task Invalid_dates_same_destination_and_missing_target_leave_the_current_link_intact()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var animal = await PropertyTestHttp.SeedAnimalAsync(factory);
        var current = await PropertyTestHttp.CreatePropertyAsync(client);
        var target = await PropertyTestHttp.CreatePropertyAsync(client);
        var before = await PropertyTestHttp.TransferAsync(client, animal.Id, current, "2026-02-01", null);
        var id = before.GetProperty("atual").GetProperty("id").GetGuid();
        using var backward = await PropertyTestHttp.PostTransferAsync(client, animal.Id, target, "2026-01-31", id);
        using var future = await PropertyTestHttp.PostTransferAsync(client, animal.Id, target, "9999-01-01", id);
        using var same = await PropertyTestHttp.PostTransferAsync(client, animal.Id, current, "2026-02-02", id);
        using var missing = await PropertyTestHttp.PostTransferAsync(client, animal.Id, Guid.NewGuid(), "2026-02-02", id);
        using var missingAnimal = await PropertyTestHttp.PostTransferAsync(client, Guid.NewGuid(), target, "2026-02-02", null);
        using var backwardDetach = await client.PostAsJsonAsync($"/api/v1/animais/{animal.Id}/propriedade/desvincular", new { dataFim = "2026-01-31", vinculoAtualIdEsperado = id });
        Assert.Equal(HttpStatusCode.BadRequest, backward.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingAnimal.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, backwardDetach.StatusCode);
        Assert.Equal(before.ToString(), (await PropertyTestHttp.StateAsync(client, animal.Id)).ToString());
    }

    private static Guid[] Ids(JsonElement list) => list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();

    private static async Task<JsonElement> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"/api/v1/propriedades?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await PropertyTestHttp.ReadAsync(response);
    }
}

internal static class PropertyTestHttp
{
    public static async Task<HttpClient> AuthenticateAsync(AuthWebApplicationFactory factory)
    {
        var user = await factory.SeedUserAsync($"prop_{Guid.NewGuid():N}");
        var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.ReadAccessTokenAsync()).AccessToken);
        return client;
    }

    public static async Task<Guid> CreatePropertyAsync(HttpClient client, string? name = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/propriedades", new { nome = name ?? $"Propriedade {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    public static Task<Guid> SeedSpeciesAsync(AuthWebApplicationFactory factory) => factory.ExecuteDbContextAsync(async db =>
    {
        var species = Especie.Criar($"Propriedade {Guid.NewGuid():N}", null, DateTimeOffset.UtcNow);
        db.Especies.Add(species);
        await db.SaveChangesAsync();
        return species.Id;
    });

    public static Task<(Guid Id, Guid SpeciesId)> SeedAnimalAsync(AuthWebApplicationFactory factory, Guid? speciesId = null, EscopoAnimal scope = EscopoAnimal.Operacional) => factory.ExecuteDbContextAsync(async db =>
    {
        var now = DateTimeOffset.UtcNow;
        if (speciesId is null)
        {
            var species = Especie.Criar($"Propriedade {Guid.NewGuid():N}", null, now);
            db.Especies.Add(species);
            speciesId = species.Id;
        }
        var animal = Animal.Criar($"PROP-{Guid.NewGuid():N}", "Animal da unidade", speciesId.Value, null, null, SexoAnimal.Macho, null, scope, DateOnly.FromDateTime(now.UtcDateTime), now);
        db.Animais.Add(animal);
        await db.SaveChangesAsync();
        return (animal.Id, speciesId.Value);
    });

    public static Task<HttpResponseMessage> PostTransferAsync(HttpClient client, Guid animalId, Guid propertyId, string start, Guid? expected, string? observation = null) =>
        client.PostAsJsonAsync($"/api/v1/animais/{animalId}/propriedade/transferir", new { propriedadeId = propertyId, dataInicio = start, vinculoAtualIdEsperado = expected, observacao = observation });

    public static async Task<JsonElement> TransferAsync(HttpClient client, Guid animalId, Guid propertyId, string start, Guid? expected, string? observation = null)
    {
        using var response = await PostTransferAsync(client, animalId, propertyId, start, expected, observation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    public static async Task<JsonElement> StateAsync(HttpClient client, Guid animalId)
    {
        using var response = await client.GetAsync($"/api/v1/animais/{animalId}/propriedades");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
