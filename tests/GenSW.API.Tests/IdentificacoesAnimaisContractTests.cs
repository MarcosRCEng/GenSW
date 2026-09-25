using System.Text.Json;
using GenSW.API.Contracts.AnimalIdentifications;
using GenSW.Domain.Animals;
using Xunit;

namespace GenSW.API.Tests;

public sealed class IdentificacoesAnimaisContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Metadata_patch_tracks_omitted_value_and_explicit_null_independently()
    {
        var omitted = JsonSerializer.Deserialize<UpdateIdentificacaoAnimalMetadataRequest>("{}", WebJson)!;
        var dateValue = JsonSerializer.Deserialize<UpdateIdentificacaoAnimalMetadataRequest>(
            "{\"dataAplicacao\":\"2026-09-12\"}", WebJson)!;
        var dateNull = JsonSerializer.Deserialize<UpdateIdentificacaoAnimalMetadataRequest>(
            "{\"dataAplicacao\":null}", WebJson)!;
        var observationValue = JsonSerializer.Deserialize<UpdateIdentificacaoAnimalMetadataRequest>(
            "{\"observacao\":\"nota\"}", WebJson)!;
        var observationNull = JsonSerializer.Deserialize<UpdateIdentificacaoAnimalMetadataRequest>(
            "{\"observacao\":null}", WebJson)!;

        Assert.False(omitted.HasDataAplicacao);
        Assert.False(omitted.HasObservacao);
        Assert.True(dateValue.HasDataAplicacao);
        Assert.False(dateValue.HasObservacao);
        Assert.Equal(new DateOnly(2026, 9, 12), dateValue.DataAplicacao);
        Assert.True(dateNull.HasDataAplicacao);
        Assert.False(dateNull.HasObservacao);
        Assert.Null(dateNull.DataAplicacao);
        Assert.True(observationValue.HasObservacao);
        Assert.False(observationValue.HasDataAplicacao);
        Assert.Equal("nota", observationValue.Observacao);
        Assert.True(observationNull.HasObservacao);
        Assert.False(observationNull.HasDataAplicacao);
        Assert.Null(observationNull.Observacao);
    }

    [Fact]
    public void Responses_use_stable_camel_case_shapes_and_integer_enum()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var animalId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var result = new IdentificacaoAnimalResponse(id, animalId, TipoIdentificacaoAnimal.Anilha,
            null, "A-1", true, new DateOnly(2026, 9, 12), "nota", true,
            DateTimeOffset.Parse("2026-09-12T10:00:00Z"), DateTimeOffset.Parse("2026-09-12T10:00:00Z"));
        var global = new IdentificacaoAnimalGlobalResponse(result,
            new IdentificacaoAnimalAnimalResumoResponse(animalId, "AN-1", "Bela"));
        var page = new IdentificacoesAnimalListResponse([result], 2, 25, 26, 2);
        var globalPage = new IdentificacoesAnimalGlobalListResponse([global], 2, 25, 26, 2);
        var json = JsonSerializer.Serialize(global, WebJson);
        var pageJson = JsonSerializer.Serialize(page, WebJson);
        var globalPageJson = JsonSerializer.Serialize(globalPage, WebJson);
        using var document = JsonDocument.Parse(globalPageJson);
        var root = document.RootElement;
        var item = root.GetProperty("items")[0];

        using var responseDocument = JsonDocument.Parse(json);
        Assert.Equal(1, responseDocument.RootElement.GetProperty("identificacao").GetProperty("tipo").GetInt32());
        Assert.Equal("AN-1", responseDocument.RootElement.GetProperty("animal").GetProperty("codigoInterno").GetString());
        Assert.Equal("Bela", responseDocument.RootElement.GetProperty("animal").GetProperty("nome").GetString());
        using var listDocument = JsonDocument.Parse(pageJson);
        Assert.Equal(2, listDocument.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(25, listDocument.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(26, listDocument.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, listDocument.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal("AN-1", item.GetProperty("animal").GetProperty("codigoInterno").GetString());
        Assert.Equal(2, root.GetProperty("page").GetInt32());
    }
}
