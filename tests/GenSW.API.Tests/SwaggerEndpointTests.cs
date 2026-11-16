using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GenSW.API.Tests;

public sealed class SwaggerEndpointTests
{
    [Fact]
    public async Task Development_document_references_distinct_species_summary_schemas()
    {
        await using var factory = new AuthWebApplicationFactory();
        await using var developmentFactory = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Development));
        using var client = developmentFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var breedSpecies = SpeciesSchemaReference(schemas.GetProperty("RacaResponse"));
        var varietySpecies = SpeciesSchemaReference(schemas.GetProperty("VariedadeResponse"));

        Assert.NotEqual(breedSpecies, varietySpecies);
        const string schemaPrefix = "#/components/schemas/";
        Assert.StartsWith(schemaPrefix, breedSpecies);
        Assert.StartsWith(schemaPrefix, varietySpecies);
        Assert.True(schemas.TryGetProperty(breedSpecies[schemaPrefix.Length..], out _));
        Assert.True(schemas.TryGetProperty(varietySpecies[schemaPrefix.Length..], out _));
    }

    private static string SpeciesSchemaReference(JsonElement schema) =>
        schema.GetProperty("properties").GetProperty("especie").GetProperty("$ref").GetString()!;
}
