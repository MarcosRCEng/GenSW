using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class IdentificacaoAnimalPersistenceModelTests : IDisposable
{
    private readonly GenSWDbContext context = new(
        new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql("Host=localhost;Database=gensw_model_tests")
            .Options);

    [Fact]
    public void IdentificacaoAnimal_mapping_preserves_the_physical_identification_contract()
    {
        var identificacao = context.Model.FindEntityType(typeof(IdentificacaoAnimal));
        Assert.NotNull(identificacao);

        Assert.Equal("IdentificacoesAnimal", identificacao.GetTableName());
        Assert.Equal(typeof(Guid), identificacao.FindProperty(nameof(IdentificacaoAnimal.Id))!.ClrType);
        Assert.Equal(typeof(Guid), identificacao.FindProperty(nameof(IdentificacaoAnimal.AnimalId))!.ClrType);
        Assert.Equal("integer", identificacao.FindProperty(nameof(IdentificacaoAnimal.Tipo))!.GetColumnType());
        Assert.Equal(typeof(DateOnly?), identificacao.FindProperty(nameof(IdentificacaoAnimal.DataAplicacao))!.ClrType);
        Assert.Equal("date", identificacao.FindProperty(nameof(IdentificacaoAnimal.DataAplicacao))!.GetColumnType());
        Assert.Equal(128, identificacao.FindProperty(nameof(IdentificacaoAnimal.Valor))!.GetMaxLength());
        Assert.Equal(100, identificacao.FindProperty(nameof(IdentificacaoAnimal.DescricaoTipo))!.GetMaxLength());
        Assert.Equal(1000, identificacao.FindProperty(nameof(IdentificacaoAnimal.Observacao))!.GetMaxLength());
        Assert.False(identificacao.FindProperty(nameof(IdentificacaoAnimal.Principal))!.IsNullable);
        Assert.False(identificacao.FindProperty(nameof(IdentificacaoAnimal.Ativo))!.IsNullable);
        Assert.False(identificacao.FindProperty(nameof(IdentificacaoAnimal.CreatedAtUtc))!.IsNullable);
        Assert.False(identificacao.FindProperty(nameof(IdentificacaoAnimal.UpdatedAtUtc))!.IsNullable);
        Assert.Contains(identificacao.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(Animal) && fk.DeleteBehavior == DeleteBehavior.Restrict &&
            fk.Properties.Select(property => property.Name).SequenceEqual([nameof(IdentificacaoAnimal.AnimalId)]));

        var designIdentificacao = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(IdentificacaoAnimal))!;
        Assert.Contains(designIdentificacao.GetCheckConstraints(), check => check.Name == "CK_IdentificacoesAnimal_Tipo" && check.Sql == "\"Tipo\" IN (1, 2, 3, 4, 5, 6)");
        Assert.Contains(designIdentificacao.GetCheckConstraints(), check => check.Name == "CK_IdentificacoesAnimal_Valor_Canonical");
        Assert.Contains(designIdentificacao.GetCheckConstraints(), check => check.Name == "CK_IdentificacoesAnimal_DescricaoTipo_Semantics");
    }

    public void Dispose() => context.Dispose();
}
