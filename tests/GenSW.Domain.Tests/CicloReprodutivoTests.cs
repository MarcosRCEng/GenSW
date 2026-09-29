using GenSW.Domain.Animals;
using Xunit;
namespace GenSW.Domain.Tests;
public sealed class CicloReprodutivoTests
{
 [Fact] public void Criar_oviparo_aceita_resultado_coerente(){var x=CicloReprodutivo.Criar(Guid.NewGuid(),TipoCicloReprodutivo.Oviparo,StatusCicloReprodutivo.Concluido,new(2026,1,1),new(2026,1,3),new(2026,1,20),10,8,8,7,1,12.5m,null,null,null,null,null,null,null," lote ",DateTimeOffset.UtcNow);Assert.Equal(7,x.OvosEclodidos);Assert.Equal("lote",x.Observacao);}
 [Fact] public void Criar_rejeita_quantidades_oviparas_incoerentes()=>Assert.Throws<ArgumentException>(()=>CicloReprodutivo.Criar(Guid.NewGuid(),TipoCicloReprodutivo.Oviparo,StatusCicloReprodutivo.EmAndamento,null,null,null,5,6,null,null,null,null,null,null,null,null,null,null,null,null,DateTimeOffset.UtcNow));
 [Fact] public void Criar_rejeita_dados_de_outro_fluxo()=>Assert.Throws<ArgumentException>(()=>CicloReprodutivo.Criar(Guid.NewGuid(),TipoCicloReprodutivo.Gestacional,StatusCicloReprodutivo.EmAndamento,null,null,null,1,null,null,null,null,null,new(2026,1,1),null,null,null,null,null,null,null,DateTimeOffset.UtcNow));
}
