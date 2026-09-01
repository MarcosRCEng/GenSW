using GenSW.Domain.Species;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class EspecieTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_normalizes_whitespace_preserves_case_and_defaults_to_active()
    {
        var especie = Especie.Criar("  Cão\t  doméstico  ", "  Canis\n familiaris ", CreatedAt);

        Assert.NotEqual(Guid.Empty, especie.Id);
        Assert.Equal("Cão doméstico", especie.NomeComum);
        Assert.Equal("Canis familiaris", especie.NomeCientifico);
        Assert.True(especie.Ativo);
        Assert.Equal(CreatedAt, especie.CreatedAtUtc);
        Assert.Equal(CreatedAt, especie.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_rejects_missing_common_name(string? nomeComum)
        => Assert.ThrowsAny<ArgumentException>(() => Especie.Criar(nomeComum!, null, CreatedAt));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_accepts_missing_scientific_name_as_null(string? nomeCientifico)
        => Assert.Null(Especie.Criar("Cão", nomeCientifico, CreatedAt).NomeCientifico);

    [Fact]
    public void Criar_accepts_common_and_scientific_names_with_exactly_200_characters()
    {
        var commonName = new string('C', 200);
        var scientificName = new string('S', 200);

        var especie = Especie.Criar(commonName, scientificName, CreatedAt);

        Assert.Equal(commonName, especie.NomeComum);
        Assert.Equal(scientificName, especie.NomeCientifico);
    }

    [Fact]
    public void Criar_rejects_common_name_longer_than_200_characters()
        => Assert.Throws<ArgumentException>(() => Especie.Criar(new string('C', 201), null, CreatedAt));

    [Fact]
    public void Criar_rejects_scientific_name_longer_than_200_characters()
        => Assert.Throws<ArgumentException>(() => Especie.Criar("Cão", new string('S', 201), CreatedAt));

    [Fact]
    public void AlterarCadastro_allows_inactive_species_to_be_edited()
    {
        var especie = Especie.Criar("Cão", null, CreatedAt);
        especie.Inativar(CreatedAt.AddMinutes(1));

        especie.AlterarCadastro("  Cão doméstico ", " Canis familiaris ", CreatedAt.AddMinutes(2));

        Assert.False(especie.Ativo);
        Assert.Equal("Cão doméstico", especie.NomeComum);
        Assert.Equal("Canis familiaris", especie.NomeCientifico);
        Assert.Equal(CreatedAt.AddMinutes(2), especie.UpdatedAtUtc);
    }

    [Fact]
    public void AlterarCadastro_with_no_logical_change_does_not_update_timestamp()
    {
        var especie = Especie.Criar("Cão doméstico", "Canis familiaris", CreatedAt);

        especie.AlterarCadastro(" Cão  doméstico ", " Canis\t familiaris ", CreatedAt.AddMinutes(1));

        Assert.Equal(CreatedAt, especie.UpdatedAtUtc);
    }

    [Fact]
    public void Inativar_changes_state_and_timestamp()
    {
        var especie = Especie.Criar("Cão", null, CreatedAt);
        var inactivatedAt = CreatedAt.AddMinutes(1);

        especie.Inativar(inactivatedAt);

        Assert.False(especie.Ativo);
        Assert.Equal(inactivatedAt, especie.UpdatedAtUtc);
    }

    [Fact]
    public void Inativar_is_idempotent()
    {
        var especie = Especie.Criar("Cão", null, CreatedAt);
        var inactivatedAt = CreatedAt.AddMinutes(1);
        especie.Inativar(inactivatedAt);

        especie.Inativar(inactivatedAt.AddMinutes(1));

        Assert.Equal(inactivatedAt, especie.UpdatedAtUtc);
    }

    [Fact]
    public void Reativar_changes_state_and_timestamp()
    {
        var especie = Especie.Criar("Cão", null, CreatedAt);
        especie.Inativar(CreatedAt.AddMinutes(1));
        var reactivatedAt = CreatedAt.AddMinutes(2);

        especie.Reativar(reactivatedAt);

        Assert.True(especie.Ativo);
        Assert.Equal(reactivatedAt, especie.UpdatedAtUtc);
    }

    [Fact]
    public void Reativar_is_idempotent()
    {
        var especie = Especie.Criar("Cão", null, CreatedAt);
        especie.Inativar(CreatedAt.AddMinutes(1));
        var reactivatedAt = CreatedAt.AddMinutes(2);
        especie.Reativar(reactivatedAt);

        especie.Reativar(reactivatedAt.AddMinutes(1));

        Assert.Equal(reactivatedAt, especie.UpdatedAtUtc);
    }
}
