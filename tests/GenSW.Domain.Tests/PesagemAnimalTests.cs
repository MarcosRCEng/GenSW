using GenSW.Domain.Animals;
using Xunit;
namespace GenSW.Domain.Tests;

public sealed class PesagemAnimalTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    [Theory]
    [InlineData(TipoMarcoPesagem.Livre, null, null)]
    [InlineData(TipoMarcoPesagem.Nascimento, null, null)]
    [InlineData(TipoMarcoPesagem.IdadeEmDias, 30, null)]
    [InlineData(TipoMarcoPesagem.PrimeiraPostura, null, null)]
    [InlineData(TipoMarcoPesagem.Abate, null, null)]
    [InlineData(TipoMarcoPesagem.Outro, null, "Revisão")]
    public void Supports_milestones_without_inventing_birth(TipoMarcoPesagem type, int? target, string? description)
    {
        var item = PesagemAnimal.Criar(Guid.NewGuid(), new(2026, 10, 1), 12.34m, type, description, target, null, null, Now);
        Assert.Equal(target, item.IdadeReferenciaDias);
        Assert.Equal(new DateOnly(2026, 10, 1), item.DataMedicao);
    }
    [Theory]
    [InlineData("0")][InlineData("-1")][InlineData("0.001")][InlineData("100000000")][InlineData("1.234")]
    public void Rejects_invalid_precision_and_range(string value) => Assert.Throws<ArgumentException>(() =>
        PesagemAnimal.Criar(Guid.NewGuid(), new(2026, 10, 1), decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), TipoMarcoPesagem.Livre, null, null, null, null, Now));
    [Fact]
    public void Rejects_dates_and_milestone_contradictions()
    {
        void Invalid(DateOnly date, TipoMarcoPesagem type, int? target, DateOnly? birth) => Assert.Throws<ArgumentException>(() =>
            PesagemAnimal.Criar(Guid.NewGuid(), date, 1, type, null, target, null, birth, Now));
        Invalid(new(2026,10,3), TipoMarcoPesagem.Livre, null, null);
        Invalid(new(2026,9,1), TipoMarcoPesagem.Livre, null, new(2026,9,2));
        Invalid(new(2026,9,3), TipoMarcoPesagem.Nascimento, null, new(2026,9,2));
        Invalid(new(2026,9,3), TipoMarcoPesagem.Outro, null, null);
        Invalid(new(2026,9,3), TipoMarcoPesagem.IdadeEmDias, null, null);
        Invalid(new(2026,9,3), TipoMarcoPesagem.Livre, 30, null);
        Invalid(new(2026,9,3), (TipoMarcoPesagem)99, null, null);
    }
}
