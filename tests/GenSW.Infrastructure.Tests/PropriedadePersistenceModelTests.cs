using GenSW.Domain.Animals;
using GenSW.Domain.Properties;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class PropriedadePersistenceModelTests
{
    private static GenSWDbContext Context() => new(new DbContextOptionsBuilder<GenSWDbContext>()
        .UseNpgsql("Host=localhost;Database=gensw_model_only").Options);

    [Fact]
    public void Snapshot_matches_the_runtime_postgresql_model()
    {
        using var context = Context();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Current_link_is_unique_and_references_are_restrictive_without_property_on_animal()
    {
        using var context = Context();
        var model = context.Model;
        var link = model.FindEntityType(typeof(VinculoAnimalPropriedade))!;
        var current = Assert.Single(link.GetIndexes(), index => index.GetDatabaseName() == "UX_VinculosAnimalPropriedade_Animal_Atual");
        Assert.True(current.IsUnique);
        Assert.Equal("\"DataFim\" IS NULL", current.GetFilter());
        Assert.Equal(nameof(VinculoAnimalPropriedade.AnimalId), Assert.Single(current.Properties).Name);
        Assert.All(link.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        var property = model.FindEntityType(typeof(Propriedade))!;
        Assert.Contains(property.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Propriedade.NomeNormalizado));
        Assert.DoesNotContain(model.FindEntityType(typeof(Animal))!.GetProperties(), field => field.Name.Contains("Propriedade", StringComparison.Ordinal));
    }
}
