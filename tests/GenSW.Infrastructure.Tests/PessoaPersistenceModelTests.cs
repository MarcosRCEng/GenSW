using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class PessoaPersistenceModelTests : IDisposable
{
    private readonly GenSWDbContext context;

    public PessoaPersistenceModelTests()
    {
        var options = new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql("Host=localhost;Database=gensw_model_tests")
            .Options;

        context = new GenSWDbContext(options);
    }

    [Fact]
    public void Pessoa_has_the_expected_master_data_mapping()
    {
        var pessoa = GetEntity<Pessoa>();
        var tipoPessoa = pessoa.FindProperty(nameof(Pessoa.TipoPessoa))!;
        var nome = pessoa.FindProperty(nameof(Pessoa.Nome))!;
        var nomeFantasia = pessoa.FindProperty(nameof(Pessoa.NomeFantasia))!;
        var ativo = pessoa.FindProperty(nameof(Pessoa.Ativo))!;

        Assert.Equal("Pessoas", pessoa.GetTableName());
        Assert.Equal(typeof(TipoPessoa), tipoPessoa.ClrType);
        Assert.False(tipoPessoa.IsNullable);
        Assert.Equal("integer", tipoPessoa.GetColumnType());
        Assert.False(nome.IsNullable);
        Assert.Equal(200, nome.GetMaxLength());
        Assert.Equal(200, nomeFantasia.GetMaxLength());
        Assert.True(nomeFantasia.IsNullable);
        Assert.False(ativo.IsNullable);
        Assert.False(pessoa.FindProperty(nameof(Pessoa.CreatedAtUtc))!.IsNullable);
        Assert.False(pessoa.FindProperty(nameof(Pessoa.UpdatedAtUtc))!.IsNullable);
        var designPessoa = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Pessoa))!;
        Assert.Contains(
            designPessoa.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Pessoas_TipoPessoa" &&
                constraint.Sql == "\"TipoPessoa\" IN (1, 2)");
    }

    [Fact]
    public void Pessoa_keeps_the_required_unique_restricted_ApplicationUser_relationship()
    {
        var user = GetEntity<ApplicationUser>();
        var pessoa = GetEntity<Pessoa>();
        var pessoaId = user.FindProperty(nameof(ApplicationUser.PessoaId))!;
        var relationship = user.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Count == 1 && foreignKey.Properties[0] == pessoaId);

        Assert.False(pessoaId.IsNullable);
        Assert.Equal(pessoa, relationship.PrincipalEntityType);
        Assert.True(relationship.IsRequired);
        Assert.True(relationship.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        Assert.Contains(
            user.GetIndexes(),
            index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0] == pessoaId);
    }

    public void Dispose() => context.Dispose();

    private IEntityType GetEntity<TEntity>()
    {
        return context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} is missing from the model.");
    }
}
