using GenSW.Domain.Species;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class EspeciePersistenceModelTests : IDisposable
{
    private readonly GenSWDbContext context = new(
        new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql("Host=localhost;Database=gensw_model_tests")
            .Options);

    [Fact]
    public void Especie_has_the_expected_master_data_mapping_and_constraints()
    {
        var especie = context.Model.FindEntityType(typeof(Especie))!;

        Assert.Equal("Especies", especie.GetTableName());
        Assert.False(especie.FindProperty(nameof(Especie.NomeComum))!.IsNullable);
        Assert.Equal(200, especie.FindProperty(nameof(Especie.NomeComum))!.GetMaxLength());
        Assert.True(especie.FindProperty(nameof(Especie.NomeCientifico))!.IsNullable);
        Assert.Equal(200, especie.FindProperty(nameof(Especie.NomeCientifico))!.GetMaxLength());
        Assert.Equal(true, especie.FindProperty(nameof(Especie.Ativo))!.GetDefaultValue());

        var designEspecie = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Especie))!;
        Assert.Contains(designEspecie.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Especies_NomeComum_Canonical" &&
            constraint.Sql == "\"NomeComum\" <> '' AND \"NomeComum\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"NomeComum\" !~ '(^ | $|  )'");
        Assert.Contains(designEspecie.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Especies_NomeCientifico_Canonical" &&
            constraint.Sql == "\"NomeCientifico\" IS NULL OR (\"NomeCientifico\" <> '' AND \"NomeCientifico\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"NomeCientifico\" !~ '(^ | $|  )')");
    }

    public void Dispose() => context.Dispose();
}
