using GenSW.Domain.Animals;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class RegistroAnimalTests
{
    [Fact]
    public void Criar_normaliza_numero_e_mantem_registro_ativo()
    {
        var item = RegistroAnimal.Criar(Guid.NewGuid(), TipoRegistroAnimal.SISBOV, " SIS-001 ", new DateOnly(2026, 9, 1), DateTimeOffset.UtcNow);
        Assert.Equal("SIS-001", item.NumeroRegistro); Assert.True(item.Ativo); Assert.Null(item.DataFim);
    }

    [Fact]
    public void Inativar_preserva_historico_e_exige_data_valida()
    {
        var item = RegistroAnimal.Criar(Guid.NewGuid(), TipoRegistroAnimal.UELN, "UELN-01", new DateOnly(2026, 9, 1), DateTimeOffset.UtcNow);
        item.Inativar(new DateOnly(2026, 9, 2), DateTimeOffset.UtcNow);
        Assert.False(item.Ativo); Assert.Equal(new DateOnly(2026, 9, 2), item.DataFim);
        Assert.Throws<ArgumentException>(() => RegistroAnimal.Criar(Guid.NewGuid(), TipoRegistroAnimal.UELN, "UELN-02", new DateOnly(2026, 9, 2), DateTimeOffset.UtcNow).Inativar(new DateOnly(2026, 9, 1), DateTimeOffset.UtcNow));
    }
}
