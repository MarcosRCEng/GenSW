using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class VariedadePersistenceModelTests : IDisposable
{
    private readonly GenSWDbContext context = new(
        new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql("Host=localhost;Database=gensw_model_tests")
            .Options);

    [Fact]
    public void Variedade_mapping_enforces_species_scoped_canonical_master_data()
    {
        var variedade = context.Model.FindEntityType(typeof(Variedade))!;
        var speciesForeignKey = Assert.Single(variedade.GetForeignKeys());

        Assert.Equal("Variedades", variedade.GetTableName());
        Assert.NotNull(variedade.FindPrimaryKey());
        Assert.False(variedade.FindProperty(nameof(Variedade.EspecieId))!.IsNullable);
        Assert.False(variedade.FindProperty(nameof(Variedade.Nome))!.IsNullable);
        Assert.Equal(200, variedade.FindProperty(nameof(Variedade.Nome))!.GetMaxLength());
        Assert.Equal(true, variedade.FindProperty(nameof(Variedade.Ativo))!.GetDefaultValue());
        Assert.False(variedade.FindProperty(nameof(Variedade.CreatedAtUtc))!.IsNullable);
        Assert.False(variedade.FindProperty(nameof(Variedade.UpdatedAtUtc))!.IsNullable);
        Assert.Equal(typeof(Especie), speciesForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, speciesForeignKey.DeleteBehavior);

        var designVariedade = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Variedade))!;
        Assert.Contains(designVariedade.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Variedades_Nome_Canonical" &&
            constraint.Sql == "\"Nome\" <> '' AND \"Nome\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"Nome\" !~ '(^ | $|  )'");
    }

    public void Dispose() => context.Dispose();
}
