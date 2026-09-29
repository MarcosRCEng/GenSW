using GenSW.Domain.Animals;
using Xunit;
namespace GenSW.Domain.Tests;
public sealed class CruzamentoTests
{
 [Fact] public void Criar_rejeita_mesmo_animal_nos_dois_papeis(){var id=Guid.NewGuid();Assert.Throws<ArgumentException>(()=>Cruzamento.Criar(id,id,StatusCruzamento.Planejado,null,null,null,null,DateTimeOffset.UtcNow));}
 [Fact] public void Criar_rejeita_data_final_anterior_a_inicial()=>Assert.Throws<ArgumentException>(()=>Cruzamento.Criar(Guid.NewGuid(),Guid.NewGuid(),StatusCruzamento.Planejado,new(2026,9,1),new(2026,8,31),null,null,DateTimeOffset.UtcNow));
 [Fact] public void Atualizar_status_e_campos_preserva_timestamps_e_normaliza_texto(){var now=new DateTimeOffset(2026,9,29,12,0,0,TimeSpan.Zero);var x=Cruzamento.Criar(Guid.NewGuid(),Guid.NewGuid(),StatusCruzamento.Planejado,null,null," objetivo "," nota ",now);var changed=now.AddHours(1);x.AlterarStatus(StatusCruzamento.EmAndamento,changed);Assert.Equal(StatusCruzamento.EmAndamento,x.Status);Assert.Equal("objetivo",x.Objetivo);Assert.Equal("nota",x.Observacao);Assert.Equal(now,x.CreatedAtUtc);Assert.Equal(changed,x.UpdatedAtUtc);}
}
