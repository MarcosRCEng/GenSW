using GenSW.Domain.People;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class PessoaTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_creates_a_valid_physical_person_with_generated_identity_and_initial_state()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, "  Maria Silva  ", null, CreatedAt);

        Assert.NotEqual(Guid.Empty, pessoa.Id);
        Assert.Equal(TipoPessoa.Fisica, pessoa.TipoPessoa);
        Assert.Equal("Maria Silva", pessoa.Nome);
        Assert.Null(pessoa.NomeFantasia);
        Assert.True(pessoa.Ativo);
        Assert.Equal(CreatedAt, pessoa.CreatedAtUtc);
        Assert.Equal(CreatedAt, pessoa.UpdatedAtUtc);
    }

    [Fact]
    public void Criar_creates_a_valid_legal_person_and_trims_trade_name()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Juridica, " GenSW Ltda. ", "  GenSW  ", CreatedAt);

        Assert.Equal(TipoPessoa.Juridica, pessoa.TipoPessoa);
        Assert.Equal("GenSW Ltda.", pessoa.Nome);
        Assert.Equal("GenSW", pessoa.NomeFantasia);
    }

    [Fact]
    public void Criar_normalizes_whitespace_only_legal_trade_name_to_null()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Juridica, "GenSW Ltda.", "   ", CreatedAt);

        Assert.Null(pessoa.NomeFantasia);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    [InlineData(" ")]
    public void Criar_rejects_names_shorter_than_two_characters(string nome)
    {
        Assert.Throws<ArgumentException>(() => Pessoa.Criar(TipoPessoa.Fisica, nome, null, CreatedAt));
    }

    [Fact]
    public void Criar_rejects_names_longer_than_200_characters()
    {
        Assert.Throws<ArgumentException>(() => Pessoa.Criar(TipoPessoa.Fisica, new string('A', 201), null, CreatedAt));
    }

    [Fact]
    public void Criar_rejects_effective_trade_name_for_physical_person()
    {
        Assert.Throws<ArgumentException>(() => Pessoa.Criar(TipoPessoa.Fisica, "Maria Silva", "Mari", CreatedAt));
    }

    [Fact]
    public void Criar_rejects_invalid_person_type()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pessoa.Criar((TipoPessoa)99, "Maria Silva", null, CreatedAt));
    }

    [Fact]
    public void TipoPessoa_cannot_be_changed_after_creation()
    {
        var setter = typeof(Pessoa).GetProperty(nameof(Pessoa.TipoPessoa))!.SetMethod;

        Assert.True(setter is null || setter.IsPrivate);
    }

    [Fact]
    public void AlterarCadastro_changes_registration_and_updates_timestamp()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Juridica, "GenSW Ltda.", null, CreatedAt);
        var changedAt = CreatedAt.AddMinutes(10);

        pessoa.AlterarCadastro(" GenSW Tecnologia Ltda. ", "  GenSW Tech ", changedAt);

        Assert.Equal("GenSW Tecnologia Ltda.", pessoa.Nome);
        Assert.Equal("GenSW Tech", pessoa.NomeFantasia);
        Assert.Equal(CreatedAt, pessoa.CreatedAtUtc);
        Assert.Equal(changedAt, pessoa.UpdatedAtUtc);
    }

    [Fact]
    public void AlterarCadastro_rejects_inactive_person()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, "Maria Silva", null, CreatedAt);
        pessoa.Inativar(CreatedAt.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            pessoa.AlterarCadastro("Maria Souza", null, CreatedAt.AddMinutes(2)));
    }

    [Fact]
    public void AlterarCadastro_with_no_logical_change_does_not_update_timestamp()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Juridica, "GenSW Ltda.", "GenSW", CreatedAt);

        pessoa.AlterarCadastro(" GenSW Ltda. ", " GenSW ", CreatedAt.AddMinutes(1));

        Assert.Equal(CreatedAt, pessoa.UpdatedAtUtc);
    }

    [Fact]
    public void Inativar_changes_state_and_timestamp_and_is_idempotent()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, "Maria Silva", null, CreatedAt);
        var inactivatedAt = CreatedAt.AddMinutes(1);

        pessoa.Inativar(inactivatedAt);

        Assert.False(pessoa.Ativo);
        Assert.Equal(inactivatedAt, pessoa.UpdatedAtUtc);

        pessoa.Inativar(inactivatedAt.AddMinutes(1));

        Assert.Equal(inactivatedAt, pessoa.UpdatedAtUtc);
    }

    [Fact]
    public void Reativar_changes_state_and_timestamp_and_is_idempotent()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, "Maria Silva", null, CreatedAt);
        pessoa.Inativar(CreatedAt.AddMinutes(1));
        var reactivatedAt = CreatedAt.AddMinutes(2);

        pessoa.Reativar(reactivatedAt);

        Assert.True(pessoa.Ativo);
        Assert.Equal(reactivatedAt, pessoa.UpdatedAtUtc);

        pessoa.Reativar(reactivatedAt.AddMinutes(1));

        Assert.Equal(reactivatedAt, pessoa.UpdatedAtUtc);
    }

    [Fact]
    public void Pessoa_belongs_to_Domain_without_Identity_or_Infrastructure_dependencies()
    {
        var domainAssembly = typeof(Pessoa).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        Assert.Equal(typeof(AssemblyReference).Assembly, domainAssembly);
        Assert.DoesNotContain(
            referencedAssemblies,
            assembly => assembly.Name is not null &&
                (assembly.Name.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) ||
                 assembly.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                 assembly.Name.StartsWith("GenSW.Infrastructure", StringComparison.Ordinal)));
    }
}
