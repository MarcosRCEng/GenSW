using GenSW.Application.Production;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;
using static GenSW.Application.MaterialCosts.MaterialCostMapping;

namespace GenSW.Application.MaterialCosts;

public sealed partial class MaterialCostFlow
{
    private static IReadOnlyList<CostDestination> Destinations(ConfirmCommand command,IReadOnlyList<MaterialOutputQuantity> outputs)
    {
        var allocation=command.CustoMaterial;
        if(allocation is null||allocation.PendenteRateio)
        {
            if(allocation is not null&&(allocation.Versao!=1||allocation.Percentuais is null||allocation.Percentuais.Count!=0))throw new ArgumentException("Pendência explícita não deve declarar percentuais.");
            if(allocation is not null)Text(allocation.Motivo);
            return [];
        }
        if(allocation.Versao!=1||allocation.Percentuais is null||allocation.Percentuais.Count>1000)throw new ArgumentException("Envelope de rateio inválido.");
        Text(allocation.Motivo);
        if(command.Perdas.Any(x=>!x.Id.HasValue||x.Id==Guid.Empty)||command.Perdas.Select(x=>x.Id).Distinct().Count()!=command.Perdas.Count)throw new ArgumentException("Cada perda deve ter ID estável no envelope F04.");
        var eligible=outputs.Select(x=>new CostDestination(x.Id,"Saida",x.Quantidade,x.Unidade,0)).Concat(command.Perdas.Select(loss=>
        {var quantity=MaterialCostRules.Quantity(loss.Quantidade,loss.Unidade);return new CostDestination(loss.Id!.Value,"Perda",quantity.Quantidade,quantity.Unidade,0);})).ToDictionary(x=>x.Id);
        if(allocation.Percentuais.Select(x=>x.DestinoId).Distinct().Count()!=allocation.Percentuais.Count)throw new ArgumentException("Destinos monetários duplicados.");
        var result=new List<CostDestination>();
        foreach(var share in allocation.Percentuais)
        {
            if(!eligible.TryGetValue(share.DestinoId,out var destination)||destination.Tipo!=share.Tipo)throw new ArgumentException("Destino monetário não corresponde à saída/parcela ou perda declarada.");
            var percentage=MaterialCostRules.Percent(share.Percentual);
            if(destination.Quantidade==0&&percentage!=0)throw new ArgumentException("Saída zero exige percentual zero.");
            result.Add(destination with{Percentual=percentage});
        }
        foreach(var destination in eligible.Values.Where(x=>x.Quantidade>0||x.Tipo=="Perda"))if(!result.Any(x=>x.Id==destination.Id))throw new ArgumentException("Informe participação explícita de cada saída positiva e perda, inclusive zero com motivo.");
        foreach(var destination in eligible.Values.Where(x=>x.Quantidade==0&&!result.Any(y=>y.Id==x.Id)))result.Add(destination);
        _=MaterialCostRules.Distribute(0,result.Select(x=>new CostShare(x.Id,x.Percentual)).ToArray());
        return result;
    }
    public async Task<MaterialProductionPreview> PreviewProductionAsync(ConfirmCommand command,IReadOnlyList<MovimentoEstoque> syntheticMovements,IReadOnlyList<MaterialOutputQuantity> outputs,CancellationToken ct)
    {
        await CheckCut(command.CustoMaterial?.SequenciaEsperada,ct);
        var sequence=await repository.LastSequenceAsync(ct);var components=await PreviewTakes(syntheticMovements,ct);var destinations=Destinations(command,outputs);
        if(destinations.Count==0)return new("PendenteRateio",Summary(components,0,sequence,0,["Rateio pendente; conhecido não distribuído, fora do valor em estoque"]),[],Sequence(sequence));
        var allocation=MaterialCostRules.Allocate(components,destinations);
        return new("Apurada",Summary(components,0,sequence,0),allocation.Destinos.Select(value=>
        {var destination=destinations.Single(x=>x.Id==value.Id);return Destination(value,destination.Quantidade,destination.Unidade,command.CustoMaterial!.Motivo,sequence,value.Tipo=="Saida"&&destination.Quantidade>0?value.Id:null);}).ToArray(),Sequence(sequence));
    }
    private async Task<IReadOnlyList<MovimentoEstoque>> PhysicalOrder(IReadOnlyList<MovimentoEstoque> movements,CancellationToken ct)
    {
        var sequences=new Dictionary<Guid,long>();
        foreach(var id in movements.Select(x=>x.EventoId).Distinct())sequences[id]=(await repository.GetAsync<EventoEstoque>(id,false,ct)??throw Conflict("origem_fisica_ausente","Evento físico não encontrado na transação.")).Sequencia;
        return movements.OrderBy(x=>sequences[x.EventoId]).ThenBy(x=>x.Ordinal).ToArray();
    }
    public async Task ApplyProductionAsync(ExecucaoProducao execution,IReadOnlyList<MovimentoEstoque> movements,IReadOnlyList<SaidaProducao> outputs,ConfirmCommand command,CancellationToken ct)
    {
        await CheckCut(command.CustoMaterial?.SequenciaEsperada,ct);
        if((await repository.ListAsync<ApuracaoCustoExecucao>(x=>x.ExecucaoId==execution.Id,false,ct)).Count>0)throw Conflict("origem_ja_apurada","A execução já possui apuração monetária.");
        var ordered=await PhysicalOrder(movements,ct);var debits=ordered.Where(x=>x.TipoEvento=="ProducaoConsumo").ToArray();
        foreach(var movement in debits.GroupBy(x=>new{x.LoteId,x.LocalId}).Select(x=>x.First()))await EnsureLegacy(movement,execution.AutorId,execution.ResponsavelId,command.Motivo,ct);
        var outputQuantities=new List<MaterialOutputQuantity>();
        foreach(var outputCommand in command.Saidas)
        {
            var output=outputs.SingleOrDefault(x=>x.Id==outputCommand.Id);
            var movement=output is null?null:ordered.Single(x=>x.EventoId==output.EventoId&&x.Sentido=="Entrada");
            outputQuantities.Add(new(outputCommand.Id,movement?.Unidade??MaterialCostRules.Quantity(outputCommand.Quantidade,outputCommand.Unidade).Unidade,output?.Quantidade??0));
        }
        var destinations=Destinations(command,outputQuantities);var pending=destinations.Count==0;
        var ev=await Event("Producao",execution.AutorId,execution.ResponsavelId,command.CustoMaterial?.Motivo??command.Motivo,execution.Id.ToString("D"),new{Versao=1,ExecucaoId=execution.Id,Rateio=command.CustoMaterial,PendenteRateio=pending},ct,execution:execution.Id);
        var consumed=new List<CostComponent>();foreach(var debit in debits)consumed.AddRange(await Take(debit,ev,ct));var components=Merge(consumed);
        var allocated=pending?null:MaterialCostRules.Allocate(components,destinations);var gaps=components.SelectMany(x=>x.Lacunas).Distinct(StringComparer.Ordinal).ToList();
        if(pending)gaps.Add("Rateio pendente; conhecido não distribuído, fora do valor em estoque");
        var known=Known(components);var coverage=MaterialCostRules.Coverage(components);if(pending&&coverage=="Completo")coverage="Parcial";
        var calculationId=Guid.NewGuid();
        var costLosses=command.Perdas.Select(loss=>loss with{Id=loss.Id??Guid.NewGuid()}).ToArray();
        var calculation=new ApuracaoCustoExecucao(calculationId,execution.Id,pending?"PendenteRateio":"Apurada",known,!pending&&coverage=="Completo"?known:null,coverage,
            InventoryJson.Write(components),InventoryJson.Write(gaps),await inventory.LastSequenceAsync(ct),ev.Sequencia,
            InventoryJson.Write(new{Versao=1,Componentes=components,Rateio=command.CustoMaterial,Destinos=allocated?.Destinos,Perdas=costLosses,Saidas=command.Saidas,Algoritmo=MaterialCostRules.Algorithm}),
            execution.AutorId,execution.ResponsavelId,Now,Today,command.CustoMaterial?.Motivo??command.Motivo,execution.Id.ToString("D"),MaterialCostRules.Algorithm);
        repository.Add(calculation);await repository.SaveAsync(ct);
        foreach(var output in outputs)
        {
            var movement=ordered.Single(x=>x.EventoId==output.EventoId&&x.Sentido=="Entrada");var layerId=Guid.NewGuid();
            var value=allocated?.Destinos.Single(x=>x.Id==output.Id);
            IReadOnlyList<CostComponent> inherited=value?.Componentes??[new(Guid.NewGuid(),layerId,movement.ItemId,movement.Unidade,output.Quantidade,null,[$"Apuração {calculationId:D} pendente de rateio"] )];
            if(value is not null&&value.Percentual==0)inherited=[new(Guid.NewGuid(),layerId,movement.ItemId,movement.Unidade,output.Quantidade,0m,[])];
            await CreateLayer(output.LoteId,movement.ItemId,movement.Unidade,output.Quantidade,inherited,"Producao",(await repository.GetAsync<EventoEstoque>(movement.EventoId,false,ct))!.Sequencia,ev,output.LocalId,ct,movement.Id,output.Id,id:layerId);
            if(value is not null)
            {
                var destination=destinations.Single(x=>x.Id==value.Id);
                repository.Add(new DestinoCustoExecucao(Guid.NewGuid(),calculationId,value.Id,"Saida",output.Quantidade,movement.Unidade,value.Percentual,value.ValorConhecido,value.ValorTotal,
                    value.UnitarioCompleto?.Valor,value.Cobertura,InventoryJson.Write(value.Componentes),InventoryJson.Write(value.Lacunas),value.Rateios.FirstOrDefault()?.Ordem??0,
                    InventoryJson.Write(value.Rateios),command.CustoMaterial!.Motivo,output.Id,null,layerId));
                _=destination;
            }
        }
        if(allocated is not null)
        {
            foreach(var value in allocated.Destinos.Where(x=>x.Tipo=="Perda"||destinations.Single(y=>y.Id==x.Id).Quantidade==0))
            {
                var destination=destinations.Single(x=>x.Id==value.Id);
                repository.Add(new DestinoCustoExecucao(Guid.NewGuid(),calculationId,value.Id,value.Tipo,destination.Quantidade,destination.Unidade,value.Percentual,value.ValorConhecido,value.ValorTotal,
                    null,value.Cobertura,InventoryJson.Write(value.Componentes),InventoryJson.Write(value.Lacunas),value.Rateios.FirstOrDefault()?.Ordem??0,InventoryJson.Write(value.Rateios),command.CustoMaterial!.Motivo,
                    null,value.Tipo=="Perda"?value.Id:null));
            }
        }
        await repository.SaveAsync(ct);
    }
    public async Task ReversePhysicalAsync(EventoEstoque stockEvent,IReadOnlyList<MovimentoEstoque> compensations,CancellationToken ct)
    {
        foreach(var movement in compensations.Where(x=>x.Sentido=="Saida"))
        {
            using var snapshot=System.Text.Json.JsonDocument.Parse(movement.SnapshotJson);
            if(snapshot.RootElement.TryGetProperty("compensacao",out var c)&&c.TryGetProperty("movimentoOriginalId",out var field)&&Guid.TryParse(field.GetString(),out var original)&&
                (await repository.ListAsync<ParcelaCustoMaterial>(x=>x.MovimentoEstoqueId==original,false,ct)).Count==0)
                await EnsureLegacy(movement,stockEvent.AutorId,stockEvent.ResponsavelId,stockEvent.Motivo,ct);
        }
        var ev=await Event("CompensacaoFisica",stockEvent.AutorId,stockEvent.ResponsavelId,stockEvent.Motivo,stockEvent.Id.ToString("D"),new{Versao=1,stockEvent.EventoReferenciaId},ct,stockEvent.Id);
        foreach(var movement in compensations)
        {
            using var snapshot=System.Text.Json.JsonDocument.Parse(movement.SnapshotJson);
            if(!snapshot.RootElement.TryGetProperty("compensacao",out var compensation)||!compensation.TryGetProperty("movimentoOriginalId",out var originalProperty)||!Guid.TryParse(originalProperty.GetString(),out var originalId))throw new ArgumentException("Compensação exige referência física original exata.");
            var originalParts=await repository.ListAsync<ParcelaCustoMaterial>(x=>x.MovimentoEstoqueId==originalId,false,ct);
            if(originalParts.Count==0)
            {
                // A physical fact from before F04 has no historical monetary event.
                // The new compensation is unknown in the present, never backfilled.
                if(movement.Sentido=="Saida")
                {
                    await Take(movement,ev,ct);
                }
                else
                {
                    var layer=Guid.NewGuid();CostComponent[] unknown=[new(Guid.NewGuid(),layer,movement.ItemId,movement.Unidade,movement.Quantidade,null,["Compensação de fato anterior sem evidência monetária"] )];
                    await CreateLayer(movement.LoteId,movement.ItemId,movement.Unidade,movement.Quantidade,unknown,"CompensacaoLegada",stockEvent.Sequencia,ev,movement.LocalId,ct,movement.Id,id:layer);
                }
                continue;
            }
            if(originalParts.Sum(x=>x.Quantidade)!=movement.Quantidade)throw Conflict("compensacao_monetaria_incompativel","Parcelas não correspondem à quantidade física original.");
            foreach(var part in originalParts)
            {
                if((await repository.ListAsync<ParcelaCustoMaterial>(x=>x.ParcelaOriginalId==part.Id,false,ct)).Count>0)throw Conflict("parcela_ja_compensada","Parcela já compensada.");
                var positions=await repository.LayerPositionsAsync(movement.LoteId,true,ct);var position=positions.SingleOrDefault(x=>x.CamadaId==part.CamadaId&&x.LocalId==movement.LocalId);
                if(position is null){position=PosicaoCustoMaterial.Create(part.CamadaId,movement.LocalId,movement.Unidade);repository.Add(position);}
                var credit=part.Sentido=="Saida";IReadOnlyList<CostComponent> components;
                if(credit)
                {
                    var origin=await repository.GetAsync<EventoCustoMaterial>(part.EventoCustoId,false,ct)??throw Conflict("evento_ausente","Evento original ausente.");
                    foreach(var correction in await repository.ListAsync<ParcelaCustoMaterial>(x=>x.CamadaId==part.CamadaId&&x.Sentido=="Saida"&&!x.MovimentoEstoqueId.HasValue,false,ct))
                    {
                        var corrected=await repository.GetAsync<EventoCustoMaterial>(correction.EventoCustoId,false,ct);
                        if(corrected is not null&&corrected.Sequencia>origin.Sequencia&&corrected.Tipo=="AvaliacaoPresente"&&
                           (await repository.ListAsync<EventoCustoMaterial>(x=>x.EventoOriginalId==corrected.Id&&x.Tipo=="CompensacaoAvaliacao",false,ct)).Count==0)
                            throw Conflict("reversao_custo_impedida","Fonte recebeu avaliação presente posterior; registre diferença presente sem recompor custo incompatível.");
                    }
                    components=Merge(Components(position.ComponentesJson).Concat(Components(part.ComponentesJson)));
                }
                else
                {
                    if(position.Quantidade!=part.Quantidade||position.ValorConhecido!=part.ValorConhecido||!Components(position.ComponentesJson).Select(InventoryJson.Write).SequenceEqual(Components(part.ComponentesJson).Select(InventoryJson.Write)))
                        throw Conflict("reversao_custo_impedida","Camada de saída sofreu movimento ou avaliação; a compensação automática é incompatível.");
                    components=[];
                }
                var quantity=position.Quantidade+(credit?part.Quantidade:-part.Quantidade);if(quantity<0)throw Conflict("reversao_custo_impedida","Camada não permanece íntegra.");
                position.SetBalance(quantity,Known(components),InventoryJson.Write(components),InventoryJson.Write(components.SelectMany(x=>x.Lacunas).Distinct().ToArray()),MaterialCostRules.Coverage(components),position.Revisao);
                repository.Add(new ParcelaCustoMaterial(Guid.NewGuid(),ev.Id,part.CamadaId,movement.LocalId,credit?"Entrada":"Saida",part.Quantidade,part.ValorConhecido,part.ComponentesJson,part.LacunasJson,movement.Id,part.Id));
            }
            await repository.SaveAsync(ct);
        }
    }
}
