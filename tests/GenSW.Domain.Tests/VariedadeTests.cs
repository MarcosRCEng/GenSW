using System.Reflection;
using GenSW.Domain.Varieties;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class VariedadeTests
{
    private static readonly Guid EspecieId = Guid.Parse("981d637c-e88c-4720-a4f2-a05e992ab6d1");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_normaliza_nome_e_inicia_ativo()
    {
        var variedade = Variedade.Criar(EspecieId, "  Pelo\t curto   inglês ", CreatedAt);

        Assert.NotEqual(Guid.Empty, variedade.Id);
        Assert.Equal(EspecieId, variedade.EspecieId);
        Assert.Equal("Pelo curto inglês", variedade.Nome);
        Assert.True(variedade.Ativo);
        Assert.Equal(CreatedAt, variedade.CreatedAtUtc);
        Assert.Equal(CreatedAt, variedade.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_rejeita_nome_ausente(string? nome)
        => Assert.ThrowsAny<ArgumentException>(() => Variedade.Criar(EspecieId, nome!, CreatedAt));

    [Fact]
    public void Criar_rejeita_nome_com_mais_de_200_caracteres()
        => Assert.Throws<ArgumentException>(() => Variedade.Criar(EspecieId, new string('V', 201), CreatedAt));

    [Fact]
    public void Criar_aceita_nome_com_exatamente_200_caracteres()
    {
        var nome = new string('V', 200);

        var variedade = Variedade.Criar(EspecieId, nome, CreatedAt);

        Assert.Equal(nome, variedade.Nome);
    }

    [Fact]
    public void AlterarCadastro_sem_mudanca_logica_nao_atualiza_timestamp()
    {
        var variedade = Variedade.Criar(EspecieId, "Pelo curto inglês", CreatedAt);

        variedade.AlterarCadastro(EspecieId, " Pelo  curto\t inglês ", CreatedAt.AddMinutes(1));

        Assert.Equal(CreatedAt, variedade.UpdatedAtUtc);
    }

    [Fact]
    public void AlterarCadastro_permite_editar_variedade_inativa()
    {
        var variedade = Variedade.Criar(EspecieId, "Pelo curto inglês", CreatedAt);
        variedade.Inativar(CreatedAt.AddMinutes(1));

        variedade.AlterarCadastro(EspecieId, " Pelo longo inglês ", CreatedAt.AddMinutes(2));

        Assert.False(variedade.Ativo);
        Assert.Equal("Pelo longo inglês", variedade.Nome);
        Assert.Equal(CreatedAt.AddMinutes(2), variedade.UpdatedAtUtc);
    }

    [Fact]
    public void Inativar_e_reativar_sao_idempotentes()
    {
        var variedade = Variedade.Criar(EspecieId, "Pelo curto inglês", CreatedAt);
        var inactivatedAt = CreatedAt.AddMinutes(1);
        variedade.Inativar(inactivatedAt);
        variedade.Inativar(inactivatedAt.AddMinutes(1));

        Assert.False(variedade.Ativo);
        Assert.Equal(inactivatedAt, variedade.UpdatedAtUtc);

        var reactivatedAt = CreatedAt.AddMinutes(3);
        variedade.Reativar(reactivatedAt);
        variedade.Reativar(reactivatedAt.AddMinutes(1));

        Assert.True(variedade.Ativo);
        Assert.Equal(reactivatedAt, variedade.UpdatedAtUtc);
    }

    [Fact]
    public void Entidade_nao_expoe_dependencia_de_breeds()
    {
        var tiposExpostos = typeof(Variedade)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(GetTiposExpostos);

        Assert.DoesNotContain(tiposExpostos, tipo => tipo.Namespace == "GenSW.Domain.Breeds");
    }

    private static IEnumerable<Type> GetTiposExpostos(MemberInfo member)
        => member switch
        {
            PropertyInfo property => [property.PropertyType],
            MethodInfo method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType),
            _ => []
        };
}
