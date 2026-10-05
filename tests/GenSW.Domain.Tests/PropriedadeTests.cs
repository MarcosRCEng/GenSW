using GenSW.Domain.Properties;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class PropriedadeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cadastro_normaliza_campos_e_status_idempotente_preserva_auditoria()
    {
        var property = Propriedade.Criar("  Unidade\t  Norte ", "  Estrada rural  ", "  ", Now);
        Assert.Equal("Unidade Norte", property.Nome);
        Assert.Equal("UNIDADE NORTE", property.NomeNormalizado);
        Assert.Equal("Estrada rural", property.Localizacao);
        Assert.Null(property.Observacao);
        Assert.True(property.Ativo);
        property.AlterarStatus(true, Now.AddHours(1));
        Assert.Equal(Now, property.UpdatedAtUtc);
        property.AlterarStatus(false, Now.AddHours(2));
        Assert.False(property.Ativo);
        Assert.Equal(Now, property.CreatedAtUtc);
        Assert.Equal(Now.AddHours(2), property.UpdatedAtUtc);
        property.AlterarStatus(true, Now.AddHours(3));
        Assert.True(property.Ativo);
    }

    [Fact]
    public void Cadastro_rejeita_nome_vazio_e_campos_acima_do_limite_sem_mutacao_parcial()
    {
        Assert.Throws<ArgumentException>(() => Propriedade.Criar("  ", null, null, Now));
        Assert.Throws<ArgumentException>(() => Propriedade.Criar(new string('n', 201), null, null, Now));
        var property = Propriedade.Criar("Norte", null, null, Now);
        Assert.Throws<ArgumentException>(() => property.AlterarCadastro("Sul", new string('l', 501), null, Now));
        Assert.Throws<ArgumentException>(() => property.AlterarCadastro("Sul", null, new string('o', 2001), Now));
        Assert.Equal("Norte", property.Nome);
    }

    [Fact]
    public void Encerramento_preserva_vinculo_admite_mesmo_dia_e_nao_reabre_historico()
    {
        var start = new DateOnly(2026, 10, 1);
        var animal = Guid.NewGuid();
        var property = Guid.NewGuid();
        var link = VinculoAnimalPropriedade.Criar(animal, property, start, "  Recebido  ", Now);
        Assert.Null(link.DataFim);
        link.Encerrar(start, Now.AddHours(1));
        Assert.Equal(animal, link.AnimalId);
        Assert.Equal(property, link.PropriedadeId);
        Assert.Equal(start, link.DataInicio);
        Assert.Equal(start, link.DataFim);
        Assert.Equal("Recebido", link.Observacao);
        Assert.Equal(Now, link.CreatedAtUtc);
        Assert.Throws<ArgumentException>(() => link.Encerrar(start.AddDays(1), Now));
    }

    [Fact]
    public void Vinculo_rejeita_ids_vazios_datas_ausentes_futuras_e_fim_anterior()
    {
        var today = new DateOnly(2026, 10, 5);
        Assert.Throws<ArgumentException>(() => VinculoAnimalPropriedade.Criar(Guid.Empty, Guid.NewGuid(), today, null, Now));
        Assert.Throws<ArgumentException>(() => VinculoAnimalPropriedade.Criar(Guid.NewGuid(), Guid.Empty, today, null, Now));
        Assert.Throws<ArgumentException>(() => VinculoAnimalPropriedade.Criar(Guid.NewGuid(), Guid.NewGuid(), default, null, Now));
        Assert.Throws<ArgumentException>(() => VinculoAnimalPropriedade.Criar(Guid.NewGuid(), Guid.NewGuid(), today.AddDays(1), null, Now));
        var link = VinculoAnimalPropriedade.Criar(Guid.NewGuid(), Guid.NewGuid(), today, null, Now);
        Assert.Throws<ArgumentException>(() => link.Encerrar(today.AddDays(-1), Now));
        Assert.Throws<ArgumentException>(() => link.Encerrar(today.AddDays(1), Now));
        Assert.Null(link.DataFim);
    }
}
