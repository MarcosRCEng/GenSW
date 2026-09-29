using GenSW.Domain.Animals;
using Xunit;
namespace GenSW.Domain.Tests;
public sealed class FiliacaoAnimalTests
{
 [Fact] public void Criar_rejeita_auto_filiacao(){var id=Guid.NewGuid();Assert.Throws<ArgumentException>(()=>FiliacaoAnimal.Criar(id,id,TipoFiliacaoAnimal.Pai,null,DateTimeOffset.UtcNow));}
 [Fact] public void Inativar_preserva_historico(){var x=FiliacaoAnimal.Criar(Guid.NewGuid(),Guid.NewGuid(),TipoFiliacaoAnimal.Mae,new DateOnly(2026,1,1),DateTimeOffset.UtcNow);x.Inativar(new DateOnly(2026,1,2),DateTimeOffset.UtcNow);Assert.False(x.Ativa);Assert.Equal(new DateOnly(2026,1,2),x.DataFim);}
}
