using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;
using static GenSW.Application.MaterialCosts.MaterialCostMapping;

namespace GenSW.Application.MaterialCosts;

public sealed partial class MaterialCostFlow
{
    public async Task<Guid> EvaluateAsync(MaterialEvaluationCommand command,Guid actor,CancellationToken ct)
    {
        MaterialCostRules.Id(command.Id);MaterialCostRules.Id(command.LoteId);MaterialCostRules.Id(command.LocalId);Text(command.Motivo);
        if(command.ApuracaoId is not null)throw new ArgumentException("Pendência de rateio exige resolução explícita por destino, não avaliação isolada.");
        var lot=await inventory.LoteAsync(command.LoteId,true,ct)??throw new MaterialCostException(404,"lote_nao_encontrado","Lote não encontrado.");
        var local=await inventory.LocalAsync(command.LocalId,true,ct)??throw new MaterialCostException(404,"local_nao_encontrado","Local não encontrado.");
        var physical=await inventory.PositionAsync(lot.Id,local.Id,true,ct)??throw new ArgumentException("Posição física inexistente.");
        var quantity=MaterialCostRules.Quantity(command.Quantidade,lot.Unidade).Quantidade;
        if(quantity<=0||quantity>physical.Quantidade)throw Conflict("saldo_nao_identificado","Avaliação presente limita-se à quantidade ainda identificada.");
        var positions=await repository.LayerPositionsAsync(lot.Id,true,ct);
        if(positions.Sum(x=>x.Revisao)!=command.RevisaoEsperada)throw Conflict("revisao_monetaria_obsoleta","Atualize a cobertura monetária antes de avaliar.");
        var fake=new MovimentoEstoque(Guid.NewGuid(),Guid.Empty,1,lot.Id,local.Id,lot.ItemId,lot.Unidade,"Saida",quantity,Number(quantity),lot.Unidade,"0",physical.Quantidade,physical.Quantidade,null,null,"{}","Avaliacao");
        await EnsureLegacy(fake,actor,actor,command.Motivo,ct);
        var evidence=Evidence(command.Evidencia,lot.ItemId,lot.Unidade,quantity,actor,actor,command.Motivo,command.Id.ToString("D"));
        var layerId=Guid.NewGuid();var cut=await inventory.LastSequenceAsync(ct);var reservations=await repository.ListAsync<ReservaProducao>(x=>x.LoteId==lot.Id,false,ct);
        var ev=await Event("AvaliacaoPresente",actor,actor,command.Motivo,command.Id.ToString("D"),new{Versao=1,Pedido=command,CamadaAtualId=layerId,CorteFisico=Sequence(cut),ReservasConhecidas=reservations.Select(x=>x.Id).ToArray(),SemPropagacaoHistorica=true},ct,id:command.Id);
        var layers=(await repository.ListAsync<CamadaCustoMaterial>(x=>x.LoteId==lot.Id,false,ct)).OrderBy(x=>x.SequenciaOrigem).ThenBy(x=>x.Id.ToString("D"),StringComparer.Ordinal).ToArray();
        positions=await repository.LayerPositionsAsync(lot.Id,true,ct);var remaining=quantity;
        foreach(var layer in layers)
        {
            if(remaining==0)break;var position=positions.SingleOrDefault(x=>x.CamadaId==layer.Id&&x.LocalId==local.Id&&x.Quantidade>0);if(position is null)continue;
            var amount=Math.Min(remaining,position.Quantidade);var split=Split(Components(position.ComponentesJson),position.Quantidade,amount);
            repository.Add(new ParcelaCustoMaterial(Guid.NewGuid(),ev.Id,layer.Id,local.Id,"Saida",amount,Known(split.Taken),InventoryJson.Write(split.Taken),InventoryJson.Write(split.Taken.SelectMany(x=>x.Lacunas).Distinct().ToArray())));
            position.SetBalance(position.Quantidade-amount,Known(split.Remaining),InventoryJson.Write(split.Remaining),InventoryJson.Write(split.Remaining.SelectMany(x=>x.Lacunas).Distinct().ToArray()),MaterialCostRules.Coverage(split.Remaining),position.Revisao);remaining-=amount;
        }
        if(remaining!=0)throw Conflict("cobertura_insuficiente","Quantidade identificada insuficiente para avaliação.");
        CostComponent[] components=[new(Guid.NewGuid(),layerId,lot.ItemId,lot.Unidade,quantity,evidence.Total,Gaps(evidence.LacunasJson),evidence.Id)];
        await CreateLayer(lot.Id,lot.ItemId,lot.Unidade,quantity,components,"Avaliacao",cut,ev,local.Id,ct,evidence:evidence,id:layerId);
        return ev.Id;
    }
    public async Task<Guid> CompensateEvaluationAsync(Guid evaluation,MaterialCompensationCommand command,Guid actor,CancellationToken ct)
    {
        MaterialCostRules.Id(command.Id);Text(command.Motivo);Text(command.Evidencia,"Evidência");
        var original=await repository.GetAsync<EventoCustoMaterial>(evaluation,false,ct)??throw new MaterialCostException(404,"avaliacao_nao_encontrada","Avaliação não encontrada.");
        if(original.Tipo!="AvaliacaoPresente")throw new ArgumentException("Compensação deve referenciar avaliação presente.");
        if((await repository.ListAsync<EventoCustoMaterial>(x=>x.EventoOriginalId==original.Id,false,ct)).Count>0)throw Conflict("avaliacao_ja_compensada","Avaliação já compensada.");
        var parts=await repository.ListAsync<ParcelaCustoMaterial>(x=>x.EventoCustoId==original.Id,false,ct);var credit=parts.Single(x=>x.Sentido=="Entrada");
        var layer=await repository.GetAsync<CamadaCustoMaterial>(credit.CamadaId,false,ct)??throw Conflict("camada_ausente","Camada não encontrada.");
        var positions=await repository.LayerPositionsAsync(layer.LoteId,true,ct);
        if(positions.Sum(x=>x.Revisao)!=command.RevisaoEsperada)throw Conflict("revisao_monetaria_obsoleta","Atualize a cobertura monetária.");
        var position=positions.Single(x=>x.CamadaId==credit.CamadaId&&x.LocalId==credit.LocalId);
        if(position.Quantidade!=credit.Quantidade||position.ValorConhecido!=credit.ValorConhecido||await repository.LayerWasUsedAsync(layer.Id,ct))throw Conflict("avaliacao_utilizada","Avaliação já utilizada; registre diferença presente sem recalcular o passado.");
        using var snapshot=System.Text.Json.JsonDocument.Parse(original.SnapshotJson);
        var cut=long.Parse(snapshot.RootElement.GetProperty("corteFisico").GetString()!,System.Globalization.CultureInfo.InvariantCulture);
        var oldReservations=snapshot.RootElement.GetProperty("reservasConhecidas").EnumerateArray().Select(x=>x.GetGuid()).ToHashSet();
        foreach(var movement in await repository.ListAsync<MovimentoEstoque>(x=>x.LoteId==layer.LoteId,false,ct))
            if((await repository.GetAsync<EventoEstoque>(movement.EventoId,false,ct))!.Sequencia>cut)throw Conflict("avaliacao_utilizada","Movimento posterior impede compensação automática.");
        if((await repository.ListAsync<ReservaProducao>(x=>x.LoteId==layer.LoteId,false,ct)).Any(x=>!oldReservations.Contains(x.Id)))throw Conflict("avaliacao_utilizada","Reserva posterior impede compensação automática.");
        var ev=await Event("CompensacaoAvaliacao",actor,actor,command.Motivo,command.Id.ToString("D"),new{Versao=1,Pedido=command,AvaliacaoOriginalId=evaluation},ct,original:original.Id,id:command.Id);
        foreach(var part in parts.OrderBy(x=>x.Sentido=="Entrada"?0:1))
        {
            var pos=positions.Single(x=>x.CamadaId==part.CamadaId&&x.LocalId==part.LocalId);var add=part.Sentido=="Saida";
            var components=add?Merge(Components(pos.ComponentesJson).Concat(Components(part.ComponentesJson))):[];
            pos.SetBalance(pos.Quantidade+(add?part.Quantidade:-part.Quantidade),Known(components),InventoryJson.Write(components),InventoryJson.Write(components.SelectMany(x=>x.Lacunas).Distinct().ToArray()),MaterialCostRules.Coverage(components),pos.Revisao);
            repository.Add(new ParcelaCustoMaterial(Guid.NewGuid(),ev.Id,part.CamadaId,part.LocalId,add?"Entrada":"Saida",part.Quantidade,part.ValorConhecido,part.ComponentesJson,part.LacunasJson,null,part.Id));
        }
        await repository.SaveAsync(ct);return ev.Id;
    }
}
