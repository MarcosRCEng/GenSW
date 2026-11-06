using System.Text.Json;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;
using static GenSW.Application.MaterialCosts.MaterialCostMapping;

namespace GenSW.Application.MaterialCosts;

public sealed partial class MaterialCostFlow
{
    private sealed record Resolution(ApuracaoCustoExecucao Original,IReadOnlyList<CostComponent> Components,IReadOnlyList<CostDestination> Destinations,
        IReadOnlyList<SaidaProducao> Outputs,IReadOnlyList<PosicaoCustoMaterial> Positions,IReadOnlyList<CamadaCustoMaterial> Layers,IReadOnlyList<LossCommand> Losses);
    private async Task<Resolution> PrepareResolution(Guid execution,MaterialResolutionCommand command,bool tracking,CancellationToken ct)
    {
        MaterialCostRules.Id(command.Id);Text(command.Motivo);Text(command.Evidencia,"Evidência da resolução presente");
        if(command.CustoMaterial is null||command.CustoMaterial.PendenteRateio)throw new ArgumentException("Resolução exige rateio explícito, completo e confirmado.");
        if(command.RevisaoEsperada!=1)throw Conflict("revisao_monetaria_obsoleta","Apuração original não corresponde à revisão esperada.");
        var original=(await repository.ListAsync<ApuracaoCustoExecucao>(x=>x.ExecucaoId==execution&&x.ApuracaoOriginalId==null,false,ct)).SingleOrDefault()??throw new MaterialCostException(404,"apuracao_nao_encontrada","Apuração pendente não encontrada.");
        if(original.Estado!="PendenteRateio"||(await repository.ListAsync<ApuracaoCustoExecucao>(x=>x.ApuracaoOriginalId==original.Id,false,ct)).Count>0)throw Conflict("apuracao_nao_pendente","Apuração não está pendente ou já recebeu resolução presente.");
        var physical=await repository.GetAsync<ExecucaoProducao>(execution,false,ct)??throw new MaterialCostException(404,"execucao_nao_encontrada","Execução não encontrada.");
        var order=await repository.GetAsync<OrdemProducao>(physical.OrdemId,tracking,ct)??throw new ArgumentException("Ordem inexistente.");
        if(order.Estado!="Confirmada")throw Conflict("execucao_nao_confirmada","Execução revertida não aceita distribuição presente.");
        using var actual=JsonDocument.Parse(physical.SnapshotJson);using var monetary=JsonDocument.Parse(original.SnapshotJson);
        var recorded=InventoryJson.Read<ConfirmCommand>(actual.RootElement.GetProperty("realizado").GetRawText());
        var losses=InventoryJson.Read<LossCommand[]>(monetary.RootElement.GetProperty("perdas").GetRawText());
        recorded=recorded with{Perdas=losses,CustoMaterial=command.CustoMaterial};
        var outputs=await repository.ListAsync<SaidaProducao>(x=>x.ExecucaoId==execution,false,ct);var positions=new List<PosicaoCustoMaterial>();var layers=new List<CamadaCustoMaterial>();var quantities=new List<MaterialOutputQuantity>();
        foreach(var output in outputs.OrderBy(x=>x.LoteId))
        {
            var lot=await inventory.LoteAsync(output.LoteId,tracking,ct)??throw new ArgumentException("Lote de saída inexistente.");
            if(lot.Revisao!=output.RevisaoLoteOriginal||lot.Situacao!=output.SituacaoOriginal||lot.Validade!=output.ValidadeOriginal)throw Conflict("resolucao_impedida","Saída alterada; registre anotação/diferença presente sem atualizar descendentes.");
            if((await repository.ListAsync<ReservaProducao>(x=>x.LoteId==lot.Id,false,ct)).Count>0||(await repository.ListAsync<MovimentoEstoque>(x=>x.LoteId==lot.Id&&x.EventoId!=output.EventoId,false,ct)).Count>0)
                throw Conflict("resolucao_impedida","Saída já movimentada ou reservada; a pendência original permanece preservada.");
            var layer=(await repository.ListAsync<CamadaCustoMaterial>(x=>x.SaidaProducaoId==output.Id,false,ct)).SingleOrDefault()??throw Conflict("camada_ausente","Camada original de saída não encontrada.");
            var position=(await repository.LayerPositionsAsync(lot.Id,tracking,ct)).SingleOrDefault(x=>x.CamadaId==layer.Id&&x.LocalId==output.LocalId);
            if(position is null||position.Quantidade!=output.Quantidade||position.Estado!="Desconhecido"||position.ValorConhecido!=0)throw Conflict("resolucao_impedida","Saída já avaliada ou alterada; não propagar custo ao passado.");
            layers.Add(layer);positions.Add(position);quantities.Add(new(output.Id,lot.Unidade,output.Quantidade));
        }
        foreach(var zero in recorded.Saidas.Where(x=>!outputs.Any(y=>y.Id==x.Id)))quantities.Add(new(zero.Id,MaterialCostRules.Quantity(zero.Quantidade,zero.Unidade).Unidade,0));
        return new(original,Components(original.ComponentesJson),Destinations(recorded,quantities),outputs,positions,layers,losses);
    }
    public async Task<MaterialProductionPreview> PreviewResolutionAsync(Guid execution,MaterialResolutionCommand command,CancellationToken ct)
    {
        await CheckCut(command.CustoMaterial.SequenciaEsperada,ct);var prepared=await PrepareResolution(execution,command,false,ct);var seq=await repository.LastSequenceAsync(ct);
        var allocation=MaterialCostRules.Allocate(prepared.Components,prepared.Destinations);
        return new("ResolucaoPresente",Summary(prepared.Components,0,seq,1),allocation.Destinos.Select(value=>
        {var destination=prepared.Destinations.Single(x=>x.Id==value.Id);return Destination(value,destination.Quantidade,destination.Unidade,command.Motivo,seq,value.Tipo=="Saida"&&destination.Quantidade>0?value.Id:null);}).ToArray(),Sequence(seq));
    }
    public async Task<Guid> ResolveAsync(Guid execution,MaterialResolutionCommand command,Guid actor,CancellationToken ct)
    {
        await CheckCut(command.CustoMaterial.SequenciaEsperada,ct);var prepared=await PrepareResolution(execution,command,true,ct);var allocation=MaterialCostRules.Allocate(prepared.Components,prepared.Destinations);
        var ev=await Event("ResolucaoRateioPresente",actor,actor,command.Motivo,command.Id.ToString("D"),new{Versao=1,Pedido=command,ApuracaoOriginalId=prepared.Original.Id,SemRecalculoHistorico=true},ct,execution:execution,id:command.Id);
        var calculationId=Guid.NewGuid();var calculation=new ApuracaoCustoExecucao(calculationId,execution,"Apurada",allocation.ValorConhecido,allocation.ValorTotal,allocation.Cobertura,
            InventoryJson.Write(prepared.Components),InventoryJson.Write(prepared.Components.SelectMany(x=>x.Lacunas).Distinct().ToArray()),await inventory.LastSequenceAsync(ct),ev.Sequencia,
            InventoryJson.Write(new{Versao=1,Componentes=prepared.Components,Rateio=command.CustoMaterial,Destinos=allocation.Destinos,Perdas=prepared.Losses,ApuracaoOriginalId=prepared.Original.Id,Algoritmo=MaterialCostRules.Algorithm}),actor,actor,Now,Today,command.Motivo,command.Id.ToString("D"),MaterialCostRules.Algorithm,prepared.Original.Id);
        repository.Add(calculation);await repository.SaveAsync(ct);
        foreach(var output in prepared.Outputs)
        {
            var oldLayer=prepared.Layers.Single(x=>x.SaidaProducaoId==output.Id);var position=prepared.Positions.Single(x=>x.CamadaId==oldLayer.Id&&x.LocalId==output.LocalId);
            repository.Add(new ParcelaCustoMaterial(Guid.NewGuid(),ev.Id,oldLayer.Id,output.LocalId,"Saida",position.Quantidade,position.ValorConhecido,position.ComponentesJson,position.LacunasJson));
            position.SetBalance(0,0,"[]","[]","Desconhecido",position.Revisao);
            var value=allocation.Destinos.Single(x=>x.Id==output.Id);var layerId=Guid.NewGuid();IReadOnlyList<CostComponent> components=value.Componentes;
            if(value.Percentual==0)components=[new(Guid.NewGuid(),layerId,oldLayer.ItemId,oldLayer.Unidade,output.Quantidade,0,[])];
            await CreateLayer(output.LoteId,oldLayer.ItemId,oldLayer.Unidade,output.Quantidade,components,"ApuracaoPresente",prepared.Original.CorteFisico,ev,output.LocalId,ct,previous:oldLayer.Id,id:layerId);
            repository.Add(new DestinoCustoExecucao(Guid.NewGuid(),calculationId,value.Id,value.Tipo,output.Quantidade,oldLayer.Unidade,value.Percentual,value.ValorConhecido,value.ValorTotal,value.UnitarioCompleto?.Valor,value.Cobertura,
                InventoryJson.Write(value.Componentes),InventoryJson.Write(value.Lacunas),value.Rateios.FirstOrDefault()?.Ordem??0,InventoryJson.Write(value.Rateios),command.Motivo,output.Id,null,layerId));
        }
        foreach(var value in allocation.Destinos.Where(x=>x.Tipo=="Perda"||prepared.Destinations.Single(y=>y.Id==x.Id).Quantidade==0))
        {
            var destination=prepared.Destinations.Single(x=>x.Id==value.Id);
            repository.Add(new DestinoCustoExecucao(Guid.NewGuid(),calculationId,value.Id,value.Tipo,destination.Quantidade,destination.Unidade,value.Percentual,value.ValorConhecido,value.ValorTotal,null,value.Cobertura,
                InventoryJson.Write(value.Componentes),InventoryJson.Write(value.Lacunas),value.Rateios.FirstOrDefault()?.Ordem??0,InventoryJson.Write(value.Rateios),command.Motivo,null,value.Tipo=="Perda"?value.Id:null));
        }
        await repository.SaveAsync(ct);return calculationId;
    }
}
