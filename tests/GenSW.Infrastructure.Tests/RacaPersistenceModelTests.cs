using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class RacaPersistenceModelTests : IDisposable
{
    private readonly GenSWDbContext context = new(
        new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql("Host=localhost;Database=gensw_model_tests")
            .Options);

    [Fact]
    public void Raca_mapping_enforces_species_scoped_canonical_master_data()
    {
        var raca = context.Model.FindEntityType(typeof(Raca))!;
        var speciesForeignKey = Assert.Single(raca.GetForeignKeys());

        Assert.Equal("Racas", raca.GetTableName());
        Assert.NotNull(raca.FindPrimaryKey());
        Assert.False(raca.FindProperty(nameof(Raca.EspecieId))!.IsNullable);
        Assert.False(raca.FindProperty(nameof(Raca.Nome))!.IsNullable);
        Assert.Equal(200, raca.FindProperty(nameof(Raca.Nome))!.GetMaxLength());
        Assert.Equal(true, raca.FindProperty(nameof(Raca.Ativo))!.GetDefaultValue());
        Assert.False(raca.FindProperty(nameof(Raca.CreatedAtUtc))!.IsNullable);
        Assert.False(raca.FindProperty(nameof(Raca.UpdatedAtUtc))!.IsNullable);
        Assert.Equal(typeof(Especie), speciesForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, speciesForeignKey.DeleteBehavior);

        var designRaca = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Raca))!;
        Assert.Contains(designRaca.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Racas_Nome_Canonical" &&
            constraint.Sql == "\"Nome\" <> '' AND \"Nome\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"Nome\" !~ '(^ | $|  )'");
    }

    public void Dispose() => context.Dispose();
}
