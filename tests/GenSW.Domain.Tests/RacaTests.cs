using GenSW.Domain.Breeds;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class RacaTests
{
    private static readonly Guid EspecieId = Guid.Parse("981d637c-e88c-4720-a4f2-a05e992ab6d1");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_normaliza_nome_e_inicia_ativo()
    {
        var raca = Raca.Criar(EspecieId, "  Cão\t de   trabalho ", CreatedAt);

        Assert.NotEqual(Guid.Empty, raca.Id);
        Assert.Equal(EspecieId, raca.EspecieId);
        Assert.Equal("Cão de trabalho", raca.Nome);
        Assert.True(raca.Ativo);
        Assert.Equal(CreatedAt, raca.CreatedAtUtc);
        Assert.Equal(CreatedAt, raca.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_rejeita_nome_ausente(string? nome)
        => Assert.ThrowsAny<ArgumentException>(() => Raca.Criar(EspecieId, nome!, CreatedAt));

    [Fact]
    public void Criar_rejeita_nome_com_mais_de_200_caracteres()
        => Assert.Throws<ArgumentException>(() => Raca.Criar(EspecieId, new string('R', 201), CreatedAt));

    [Fact]
    public void Criar_aceita_nome_com_exatamente_200_caracteres()
    {
        var nome = new string('R', 200);

        var raca = Raca.Criar(EspecieId, nome, CreatedAt);

        Assert.Equal(nome, raca.Nome);
    }

    [Fact]
    public void AlterarCadastro_sem_mudanca_logica_nao_atualiza_timestamp()
    {
        var raca = Raca.Criar(EspecieId, "Cão de trabalho", CreatedAt);

        raca.AlterarCadastro(EspecieId, " Cão  de\t trabalho ", CreatedAt.AddMinutes(1));

        Assert.Equal(CreatedAt, raca.UpdatedAtUtc);
    }

    [Fact]
    public void AlterarCadastro_permite_editar_raca_inativa()
    {
        var raca = Raca.Criar(EspecieId, "Cão de trabalho", CreatedAt);
        raca.Inativar(CreatedAt.AddMinutes(1));

        raca.AlterarCadastro(EspecieId, " Cão de companhia ", CreatedAt.AddMinutes(2));

        Assert.False(raca.Ativo);
        Assert.Equal("Cão de companhia", raca.Nome);
        Assert.Equal(CreatedAt.AddMinutes(2), raca.UpdatedAtUtc);
    }

    [Fact]
    public void Inativar_e_reativar_sao_idempotentes()
    {
        var raca = Raca.Criar(EspecieId, "Cão de trabalho", CreatedAt);
        var inactivatedAt = CreatedAt.AddMinutes(1);
        raca.Inativar(inactivatedAt);
        raca.Inativar(inactivatedAt.AddMinutes(1));

        Assert.False(raca.Ativo);
        Assert.Equal(inactivatedAt, raca.UpdatedAtUtc);

        var reactivatedAt = CreatedAt.AddMinutes(3);
        raca.Reativar(reactivatedAt);
        raca.Reativar(reactivatedAt.AddMinutes(1));

        Assert.True(raca.Ativo);
        Assert.Equal(reactivatedAt, raca.UpdatedAtUtc);
    }
}
