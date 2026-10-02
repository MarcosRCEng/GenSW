using GenSW.Domain.Animals;
using Xunit;
namespace GenSW.Domain.Tests;
public sealed class ProleTests
{
 [Fact] public void Criar_individual_aceita_sexo_desconhecido_e_peso_ausente(){var item=Prole.Criar(Guid.NewGuid(),TipoRegistroProle.Individual,1,TipoOrigemProle.Nascimento,new(2026,1,2),null,SexoAnimal.Indeterminado,"Vivo",null,null,DateTimeOffset.UtcNow);Assert.Equal(SexoAnimal.Indeterminado,item.Sexo);Assert.Null(item.PesoGramas);}
 [Fact] public void Criar_rejeita_peso_nao_positivo()=>Assert.Throws<ArgumentException>(()=>Prole.Criar(Guid.NewGuid(),TipoRegistroProle.Individual,1,TipoOrigemProle.Nascimento,new(2026,1,2),0,SexoAnimal.Indeterminado,"Vivo",null,null,DateTimeOffset.UtcNow));
 [Fact] public void Lote_precisa_ser_desdobrado_antes_da_conversao(){var item=Prole.Criar(Guid.NewGuid(),TipoRegistroProle.Lote,2,TipoOrigemProle.Eclosao,new(2026,1,2),null,SexoAnimal.Indeterminado,"Vivo",null,null,DateTimeOffset.UtcNow);Assert.Throws<InvalidOperationException>(()=>item.VincularAnimal(Guid.NewGuid(),DateTimeOffset.UtcNow));}
 [Fact] public void Individual_nao_permite_segunda_conversao(){var item=Prole.Criar(Guid.NewGuid(),TipoRegistroProle.Individual,1,TipoOrigemProle.Nascimento,new(2026,1,2),null,SexoAnimal.Indeterminado,"Vivo",null,null,DateTimeOffset.UtcNow);item.VincularAnimal(Guid.NewGuid(),DateTimeOffset.UtcNow);Assert.Throws<InvalidOperationException>(()=>item.VincularAnimal(Guid.NewGuid(),DateTimeOffset.UtcNow));}
}
