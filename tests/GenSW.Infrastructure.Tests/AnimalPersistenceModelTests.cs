using GenSW.Domain.Animals;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class AnimalPersistenceModelTests : IDisposable
{
    private readonly GenSWDbContext context = new(
        new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql("Host=localhost;Database=gensw_model_tests")
            .Options);

    [Fact]
    public void Animal_mapping_defines_all_required_columns_constraints_indexes_and_structural_relationships()
    {
        Assert.NotNull(context.Model.FindEntityType(typeof(Animal)));
        var animal = context.Model.FindEntityType(typeof(Animal))!;

        Assert.Equal("Animais", animal.GetTableName());
        Assert.NotNull(animal.FindPrimaryKey());
        Assert.Equal(64, animal.FindProperty(nameof(Animal.CodigoInterno))!.GetMaxLength());
        Assert.False(animal.FindProperty(nameof(Animal.CodigoInterno))!.IsNullable);
        Assert.Equal(200, animal.FindProperty(nameof(Animal.Nome))!.GetMaxLength());
        Assert.True(animal.FindProperty(nameof(Animal.Nome))!.IsNullable);
        Assert.False(animal.FindProperty(nameof(Animal.EspecieId))!.IsNullable);
        Assert.True(animal.FindProperty(nameof(Animal.RacaId))!.IsNullable);
        Assert.True(animal.FindProperty(nameof(Animal.VariedadeId))!.IsNullable);
        Assert.Equal("integer", animal.FindProperty(nameof(Animal.Sexo))!.GetColumnType());
        Assert.Equal("integer", animal.FindProperty(nameof(Animal.Escopo))!.GetColumnType());
        Assert.Equal(typeof(DateOnly?), animal.FindProperty(nameof(Animal.DataNascimento))!.ClrType);
        Assert.Equal(true, animal.FindProperty(nameof(Animal.Ativo))!.GetDefaultValue());
        Assert.False(animal.FindProperty(nameof(Animal.CreatedAtUtc))!.IsNullable);
        Assert.False(animal.FindProperty(nameof(Animal.UpdatedAtUtc))!.IsNullable);

        Assert.Contains(animal.GetIndexes(), index => index.GetDatabaseName() == "UX_Animais_CodigoInterno_CaseInsensitive" && index.IsUnique);
        Assert.Contains(animal.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Animal.EspecieId)]));
        Assert.Contains(animal.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Animal.RacaId), nameof(Animal.EspecieId)]));
        Assert.Contains(animal.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Animal.VariedadeId), nameof(Animal.EspecieId)]));

        Assert.NotNull(context.Model.FindEntityType(typeof(Raca)));
        var breed = context.Model.FindEntityType(typeof(Raca))!;
        Assert.NotNull(context.Model.FindEntityType(typeof(Variedade)));
        var variety = context.Model.FindEntityType(typeof(Variedade))!;
        Assert.Contains(breed.GetKeys(), key => key.GetName() == "AK_Racas_Id_EspecieId");
        Assert.Contains(variety.GetKeys(), key => key.GetName() == "AK_Variedades_Id_EspecieId");

        Assert.Contains(animal.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Especie) && foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Animal.EspecieId)]));
        Assert.Contains(animal.GetForeignKeys(), foreignKey =>
            foreignKey.GetConstraintName() == "FK_Animais_Racas_RacaId_EspecieId" &&
            foreignKey.PrincipalEntityType.ClrType == typeof(Raca) && foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Animal.RacaId), nameof(Animal.EspecieId)]));
        Assert.Contains(animal.GetForeignKeys(), foreignKey =>
            foreignKey.GetConstraintName() == "FK_Animais_Variedades_VariedadeId_EspecieId" &&
            foreignKey.PrincipalEntityType.ClrType == typeof(Variedade) && foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Animal.VariedadeId), nameof(Animal.EspecieId)]));

        Assert.NotNull(context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Animal)));
        var designAnimal = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Animal))!;
        Assert.Contains(designAnimal.GetCheckConstraints(), check => check.Name == "CK_Animais_CodigoInterno_Canonical");
        Assert.Contains(designAnimal.GetCheckConstraints(), check => check.Name == "CK_Animais_Nome_Canonical");
        Assert.Contains(designAnimal.GetCheckConstraints(), check => check.Name == "CK_Animais_Sexo" && check.Sql == "\"Sexo\" IN (1, 2, 3)");
        Assert.Contains(designAnimal.GetCheckConstraints(), check => check.Name == "CK_Animais_Escopo" && check.Sql == "\"Escopo\" IN (1, 2)");
    }

    public void Dispose() => context.Dispose();
}
