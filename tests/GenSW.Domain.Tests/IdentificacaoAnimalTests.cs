using GenSW.Domain.Animals;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class IdentificacaoAnimalTests
{
    private static readonly Guid AnimalId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TipoIdentificacaoAnimal.Anilha, 1)] [InlineData(TipoIdentificacaoAnimal.Brinco, 2)]
    [InlineData(TipoIdentificacaoAnimal.Microchip, 3)] [InlineData(TipoIdentificacaoAnimal.Tatuagem, 4)]
    [InlineData(TipoIdentificacaoAnimal.Marca, 5)] [InlineData(TipoIdentificacaoAnimal.Outro, 6)]
    public void Defined_types_have_expected_values(TipoIdentificacaoAnimal type, int value) => Assert.Equal(value, (int)type);

    [Fact] public void Undefined_type_is_rejected() => Assert.Throws<ArgumentException>(() => Create((TipoIdentificacaoAnimal)99));
    [Fact] public void Creation_trims_edges_only_and_applies_rules()
    {
        var i = Create(valor: "  A  01-0  ", tipo: TipoIdentificacaoAnimal.Outro, descricao: "  Custom  ");
        Assert.Equal("A  01-0", i.Valor); Assert.Equal("Custom", i.DescricaoTipo); Assert.True(i.Ativo);
    }
    [Fact] public void Creation_rejects_boundaries_and_description_rules()
    {
        Assert.ThrowsAny<ArgumentException>(() => Create(valor: " "));
        Assert.ThrowsAny<ArgumentException>(() => Create(valor: new string('x', 129)));
        Assert.ThrowsAny<ArgumentException>(() => Create(tipo: TipoIdentificacaoAnimal.Outro));
        Assert.ThrowsAny<ArgumentException>(() => Create(tipo: TipoIdentificacaoAnimal.Outro, descricao: new string('d', 101)));
        Assert.ThrowsAny<ArgumentException>(() => Create(descricao: "x", tipo: TipoIdentificacaoAnimal.Anilha));
        Assert.Null(Create().DescricaoTipo);
        Assert.Null(Create().Observacao);
        Assert.ThrowsAny<ArgumentException>(() => IdentificacaoAnimal.Criar(AnimalId, TipoIdentificacaoAnimal.Anilha, null, "A01", false, null, new string('o', 1001), Now));
    }
    [Fact] public void Lifecycle_and_metadata_preserve_timestamps_when_unchanged()
    {
        var i = Create();
        i.AlterarMetadados(true, new DateOnly(2026, 9, 1), true, " note ", Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), i.UpdatedAtUtc); Assert.Equal("note", i.Observacao);
        i.AlterarMetadados(false, null, false, null, Now.AddMinutes(2)); Assert.Equal(Now.AddMinutes(1), i.UpdatedAtUtc);
        i.DefinirPrincipal(Now.AddMinutes(3)); Assert.True(i.Principal); Assert.Equal(Now.AddMinutes(3), i.UpdatedAtUtc);
        i.DefinirPrincipal(Now.AddMinutes(4)); Assert.Equal(Now.AddMinutes(3), i.UpdatedAtUtc);
        i.RemoverPrincipal(Now.AddMinutes(5)); Assert.False(i.Principal); Assert.Equal(Now.AddMinutes(5), i.UpdatedAtUtc);
        i.RemoverPrincipal(Now.AddMinutes(6)); Assert.False(i.Principal); Assert.Equal(Now.AddMinutes(5), i.UpdatedAtUtc);
        i.Inativar(Now.AddMinutes(7)); Assert.False(i.Ativo); Assert.False(i.Principal); Assert.Equal(Now.AddMinutes(7), i.UpdatedAtUtc);
        i.Inativar(Now.AddMinutes(8)); Assert.Equal(Now.AddMinutes(7), i.UpdatedAtUtc);
        Assert.Throws<InvalidOperationException>(() => i.DefinirPrincipal(Now.AddMinutes(9)));
        i.Reativar(Now.AddMinutes(10)); Assert.True(i.Ativo); Assert.False(i.Principal); Assert.Equal(Now.AddMinutes(10), i.UpdatedAtUtc);
        i.Reativar(Now.AddMinutes(11)); Assert.Equal(Now.AddMinutes(10), i.UpdatedAtUtc);
    }
    [Fact] public void Identifier_properties_are_immutable() => Assert.DoesNotContain(typeof(IdentificacaoAnimal).GetProperties(), p => p.Name is "Tipo" or "DescricaoTipo" or "Valor" && p.SetMethod?.IsPublic == true);

    private static IdentificacaoAnimal Create(TipoIdentificacaoAnimal tipo = TipoIdentificacaoAnimal.Anilha,
        string? descricao = null, string valor = "A01", bool principal = false)
        => IdentificacaoAnimal.Criar(AnimalId, tipo, descricao, valor, principal, null, null, Now);
}
