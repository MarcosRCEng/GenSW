using System.Globalization;
using System.Numerics;
using System.Text.Json;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;
using static GenSW.Application.MaterialCosts.MaterialCostMapping;

namespace GenSW.Application.MaterialCosts;

// Hooks participate in the caller's physical transaction. They never open a
// second transaction, issue public inventory commands, or write to Caixa.
public sealed partial class MaterialCostFlow(IMaterialCostRepository repository, IInventoryRepository inventory, TimeProvider clock) : IMaterialCostFlow
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private DateOnly Today => InventoryRules.Today(clock);
    private static MaterialCostException Conflict(string code,string message) => new(409,code,message);
    private static string Text(string? value,string label="Motivo") => CatalogRules.Text(value,2000,label);
    private async Task CheckCut(string? expected,CancellationToken ct)
    {
        if(expected is null)return;
        if(!long.TryParse(expected,NumberStyles.None,CultureInfo.InvariantCulture,out var cut)||cut<0)throw new ArgumentException("Corte monetário inválido.");
        if(cut!=await repository.LastSequenceAsync(ct))throw Conflict("previa_monetaria_obsoleta","O livro de custos mudou; confira nova prévia.");
    }
    private EvidenciaCustoMaterial Evidence(MaterialEvidenceCommand command,Guid item,string unit,decimal quantity,Guid actor,Guid responsible,string reason,string correlation)
    {
        ArgumentNullException.ThrowIfNull(command);if(command.DataFonte==default)throw new ArgumentException("Data da fonte é obrigatória.");InventoryRules.Date(command.DataFonte,Today);
        MaterialCostRules.Currency(command.Moeda);var total=MaterialCostRules.Amount(command.TotalDocumentado,command.Moeda);
        _=MaterialCostRules.UnitCost(total,quantity);
        if(command.Componentes is null||command.Componentes.Count is <1 or >100||command.Lacunas is null||command.Lacunas.Count>100)throw new ArgumentException("Discrimine os componentes e lacunas da evidência.");
        var included=command.Componentes.Select(x=>Text(x,"Componente documentado")).Distinct(StringComparer.Ordinal).ToArray();
        var gaps=command.Lacunas.Select(x=>Text(x,"Lacuna")).Distinct(StringComparer.Ordinal).ToArray();
        var hash=CatalogRules.Optional(command.Hash,128);
        return new(Guid.NewGuid(),item,Text(command.Fonte,"Fonte"),Text(command.ReferenciaDocumento,"Referência documental protegida"),command.DataFonte,
            "BRL",unit,quantity,total,InventoryJson.Write(included),InventoryJson.Write(gaps),"DocumentoAtribuivel",actor,responsible,Now,Today,Text(reason),correlation,MaterialCostRules.Algorithm,hash);
    }
    private async Task<EventoCustoMaterial> Event(string type,Guid actor,Guid responsible,string reason,string correlation,object snapshot,CancellationToken ct,Guid? physical=null,Guid? execution=null,Guid? original=null,Guid? id=null)
    {
        using var document=JsonDocument.Parse(InventoryJson.Write(snapshot));
        var envelope=document.RootElement.EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.Clone(),StringComparer.Ordinal);
        envelope["corteFisico"]=JsonSerializer.SerializeToElement(Sequence(await inventory.LastSequenceAsync(ct)));
        envelope["transacaoId"]=JsonSerializer.SerializeToElement(repository.TransactionIdentity);
        var ev=new EventoCustoMaterial(id??Guid.NewGuid(),await repository.NextSequenceAsync(ct),type,actor,responsible,Now,Today,Text(reason),correlation,
            MaterialCostRules.Algorithm,InventoryJson.Write(envelope),physical,execution,original);
        repository.Add(ev);return ev;
    }
    private async Task<CamadaCustoMaterial> CreateLayer(Guid lot,Guid item,string unit,decimal quantity,IReadOnlyList<CostComponent> components,
        string origin,long originSequence,EventoCustoMaterial ev,Guid local,CancellationToken ct,Guid? movement=null,Guid? output=null,EvidenciaCustoMaterial? evidence=null,Guid? previous=null,Guid? id=null)
    {
        var layerId=id??Guid.NewGuid();var known=Known(components);var state=MaterialCostRules.Coverage(components);
        var layer=new CamadaCustoMaterial(layerId,lot,item,unit,quantity,known,state,origin,originSequence,InventoryJson.Write(components),
            InventoryJson.Write(components.SelectMany(x=>x.Lacunas).Distinct(StringComparer.Ordinal).ToArray()),ev.AutorId,ev.ResponsavelId,ev.CreatedAtUtc,ev.DataOperacional,
            ev.Motivo,ev.Correlacao,MaterialCostRules.Algorithm,movement,output,evidence?.Id,previous);
        if(evidence is not null)repository.Add(evidence);repository.Add(layer);
        var position=PosicaoCustoMaterial.Create(layer.Id,local,unit);position.SetBalance(quantity,known,layer.ComponentesJson,layer.LacunasJson,state,0);repository.Add(position);
        repository.Add(new ParcelaCustoMaterial(Guid.NewGuid(),ev.Id,layer.Id,local,"Entrada",quantity,known,layer.ComponentesJson,layer.LacunasJson,movement));
        await repository.SaveAsync(ct);return layer;
    }
    private async Task EnsureLegacy(MovimentoEstoque movement,Guid actor,Guid responsible,string reason,CancellationToken ct)
    {
        var positions=(await repository.LayerPositionsAsync(movement.LoteId,true,ct)).Where(x=>x.LocalId==movement.LocalId).ToArray();
        var tracked=positions.Sum(x=>x.Quantidade);var missing=movement.SaldoAnterior-tracked;
        if(missing<0)throw Conflict("cobertura_divergente","Cobertura monetária excede a posição física anterior; preserve a evidência e diagnostique.");
        if(missing==0)return;
        var layer=Guid.NewGuid();CostComponent[] components=[new(Guid.NewGuid(),layer,movement.ItemId,movement.Unidade,missing,null,["Saldo legado sem evidência monetária"] )];
        var ev=await Event("ReconhecimentoLegado",actor,responsible,reason,movement.Id.ToString("D"),new{movement.LoteId,movement.LocalId,Quantidade=Number(missing),SemBackfill=true},ct);
        await CreateLayer(movement.LoteId,movement.ItemId,movement.Unidade,missing,components,"Legado",0,ev,movement.LocalId,ct,id:layer);
    }
    private async Task<IReadOnlyList<CostComponent>> Take(MovimentoEstoque movement,EventoCustoMaterial ev,CancellationToken ct)
    {
        var layers=await repository.ListAsync<CamadaCustoMaterial>(x=>x.LoteId==movement.LoteId,false,ct);
        var ordered=layers.OrderBy(x=>x.SequenciaOrigem).ThenBy(x=>x.Id.ToString("D"),StringComparer.Ordinal).Select(x=>x.Id).ToArray();
        var positions=(await repository.LayerPositionsAsync(movement.LoteId,true,ct)).Where(x=>x.LocalId==movement.LocalId&&x.Quantidade>0).ToDictionary(x=>x.CamadaId);
        var remaining=movement.Quantidade;var taken=new List<CostComponent>();
        foreach(var layer in ordered)
        {
            if(remaining==0)break;if(!positions.TryGetValue(layer,out var pos))continue;
            var amount=Math.Min(pos.Quantidade,remaining);var split=Split(Components(pos.ComponentesJson),pos.Quantidade,amount);
            var gaps=split.Taken.SelectMany(x=>x.Lacunas).Distinct(StringComparer.Ordinal).ToArray();
            repository.Add(new ParcelaCustoMaterial(Guid.NewGuid(),ev.Id,layer,movement.LocalId,"Saida",amount,Known(split.Taken),InventoryJson.Write(split.Taken),InventoryJson.Write(gaps),movement.Id));
            pos.SetBalance(pos.Quantidade-amount,Known(split.Remaining),InventoryJson.Write(split.Remaining),InventoryJson.Write(split.Remaining.SelectMany(x=>x.Lacunas).Distinct().ToArray()),MaterialCostRules.Coverage(split.Remaining),pos.Revisao);
            taken.AddRange(split.Taken);remaining-=amount;
        }
        if(remaining!=0)throw Conflict("cobertura_insuficiente","Quantidade física não tem cobertura monetária identificada.");
        await repository.SaveAsync(ct);return Merge(taken);
    }
    private async Task CompatibleReceipt(Guid lot,decimal quantity,MaterialReceiptCommand? command,CancellationToken ct)
    {
        if(command is not null)
        {
            if(command.Versao!=1||command.Estado is not ("Conhecido" or "Desconhecido"))throw new ArgumentException("Envelope de custo inválido.");
            Text(command.Motivo);if(command.Estado=="Conhecido"&&command.Evidencia is null)throw new ArgumentException("Custo conhecido exige evidência documental.");
            if(command.Estado=="Desconhecido"&&command.Evidencia is not null)throw new ArgumentException("Documento monetário exige escolha explícita de custo conhecido.");
        }
        var layers=await repository.ListAsync<CamadaCustoMaterial>(x=>x.LoteId==lot,false,ct);
        if(layers.Count==0)return;
        var known=command?.Estado=="Conhecido";var incoming=known?MaterialCostRules.Amount(command!.Evidencia!.TotalDocumentado,command.Evidencia.Moeda):0;
        foreach(var layer in layers)
        {
            if(known!=(layer.Estado!="Desconhecido"))throw Conflict("custo_lote_incompativel","Estado de custo distinto exige outro lote interno; não completar legado por recebimento.");
            if(!known)continue;
            if(layer.TipoOrigem!="Recebimento"||layer.EvidenciaId is null||MaterialCostRules.Cents(layer.ValorConhecido)*MaterialCostRules.ScaledQuantity(quantity,layer.Unidade)!=MaterialCostRules.Cents(incoming)*MaterialCostRules.ScaledQuantity(layer.QuantidadeOriginal,layer.Unidade))
                throw Conflict("custo_lote_incompativel","Razão exata de custo distinta exige outro lote interno.");
            var evidence=await repository.GetAsync<EvidenciaCustoMaterial>(layer.EvidenciaId.Value,false,ct)??throw Conflict("evidencia_ausente","Evidência da camada está ausente.");
            var supplied=command!.Evidencia!;
            if(evidence.Fonte!=supplied.Fonte.Trim()||evidence.ReferenciaProtegida!=supplied.ReferenciaDocumento.Trim()||evidence.DataFonte!=supplied.DataFonte||
                !Gaps(evidence.ComponentesJson).Order(StringComparer.Ordinal).SequenceEqual(supplied.Componentes.Order(StringComparer.Ordinal))||!Gaps(evidence.LacunasJson).Order(StringComparer.Ordinal).SequenceEqual(supplied.Lacunas.Order(StringComparer.Ordinal)))
                throw Conflict("proveniencia_lote_incompativel","Proveniência/componentes distintos exigem outro lote interno.");
        }
    }
    private sealed record VirtualPosition(Guid Layer,long Origin,Guid Local,decimal Quantity,IReadOnlyList<CostComponent> Components);
    private async Task<IReadOnlyList<CostComponent>> PreviewTakes(IReadOnlyList<MovimentoEstoque> movements,CancellationToken ct)
    {
        var state=new Dictionary<Guid,List<VirtualPosition>>();var taken=new List<CostComponent>();
        foreach(var movement in movements.Where(x=>x.Sentido=="Saida"))
        {
            if(!state.TryGetValue(movement.LoteId,out var positions))
            {
                var layers=(await repository.ListAsync<CamadaCustoMaterial>(x=>x.LoteId==movement.LoteId,false,ct)).ToDictionary(x=>x.Id);
                positions=(await repository.LayerPositionsAsync(movement.LoteId,false,ct)).Select(x=>new VirtualPosition(x.CamadaId,layers[x.CamadaId].SequenciaOrigem,x.LocalId,x.Quantidade,Components(x.ComponentesJson))).ToList();state[movement.LoteId]=positions;
            }
            var missing=movement.SaldoAnterior-positions.Where(x=>x.Local==movement.LocalId).Sum(x=>x.Quantity);
            if(missing<0)throw Conflict("cobertura_divergente","Cobertura monetária diverge da posição física.");
            if(missing>0)
            {
                var id=Guid.NewGuid();positions.Add(new(id,0,movement.LocalId,missing,[new(id,id,movement.ItemId,movement.Unidade,missing,null,["Saldo legado sem evidência monetária"])]));
            }
            var remaining=movement.Quantidade;
            foreach(var position in positions.Where(x=>x.Local==movement.LocalId&&x.Quantity>0).OrderBy(x=>x.Origin).ThenBy(x=>x.Layer.ToString("D"),StringComparer.Ordinal).ToArray())
            {
                if(remaining==0)break;var amount=Math.Min(position.Quantity,remaining);var split=Split(position.Components,position.Quantity,amount);
                taken.AddRange(split.Taken);positions[positions.IndexOf(position)]=position with{Quantity=position.Quantity-amount,Components=split.Remaining};remaining-=amount;
            }
            if(remaining!=0)throw Conflict("cobertura_insuficiente","Quantidade não disponível para a prévia monetária.");
        }
        return Merge(taken);
    }
    public async Task<MaterialInventoryPreview> PreviewInventoryAsync(string operation,InventoryMovementCommand command,InventoryPreview preview,CancellationToken ct)
    {
        var sequence=await repository.LastSequenceAsync(ct);await CheckCut(command.CustoMaterial?.SequenciaEsperada,ct);
        var amount=CatalogRules.Decimal(preview.Quantidade.Normalizada);IReadOnlyList<CostComponent> components;
        if(operation is "Entrada" or "Abertura")
        {
            await CompatibleReceipt(command.LoteId,amount,command.CustoMaterial,ct);
            var lot=await inventory.LoteAsync(command.LoteId,false,ct)??throw new ArgumentException("Lote inexistente.");
            var id=Guid.NewGuid();var evidence=command.CustoMaterial?.Estado=="Conhecido"?Evidence(command.CustoMaterial.Evidencia!,lot.ItemId,lot.Unidade,amount,Guid.Empty,Guid.Empty,command.CustoMaterial.Motivo,"previa"):null;
            components=[new(id,id,lot.ItemId,lot.Unidade,amount,evidence?.Total,evidence is null?[command.CustoMaterial?.Motivo??"Recebimento sem evidência monetária"]:Gaps(evidence.LacunasJson),evidence?.Id)];
        }
        else
        {
            if(command.CustoMaterial is not null)throw new ArgumentException("Custo documental no recebimento; ajuste positivo usa avaliação presente separada.");
            var origin=preview.Posicoes.FirstOrDefault(x=>CatalogRules.Decimal(x.SaldoPosterior)<CatalogRules.Decimal(x.SaldoAnterior));
            if(origin is null)return new(Summary([],amount,sequence,0,["Acréscimo físico sem avaliação monetária"]),Sequence(sequence),[]);
            var lot=await inventory.LoteAsync(command.LoteId,false,ct)??throw new ArgumentException("Lote inexistente.");
            var fake=new MovimentoEstoque(Guid.Empty,Guid.Empty,1,lot.Id,origin.LocalId,lot.ItemId,lot.Unidade,"Saida",CatalogRules.Decimal(origin.SaldoAnterior)-CatalogRules.Decimal(origin.SaldoPosterior),"0",lot.Unidade,"0",CatalogRules.Decimal(origin.SaldoAnterior),CatalogRules.Decimal(origin.SaldoPosterior),null,null,"{}",operation);
            components=await PreviewTakes([fake],ct);
        }
        return new(Summary(components,amount,sequence,0),Sequence(sequence),["Referência/cotação não preenche custo comprovado; total completo depende da cobertura documental."]);
    }
    public async Task ApplyInventoryAsync(EventoEstoque stockEvent,IReadOnlyList<MovimentoEstoque> movements,MaterialReceiptCommand? cost,CancellationToken ct)
    {
        await CheckCut(cost?.SequenciaEsperada,ct);
        if(stockEvent.Tipo is "Entrada" or "Abertura")
        {
            foreach(var movement in movements)
            {
                if(movement.Sentido!="Entrada")throw new ArgumentException("Recebimento deve creditar material.");
                await CompatibleReceipt(movement.LoteId,movement.Quantidade,cost,ct);
                // Existing untracked quantity is recognized in the present, never backfilled.
                await EnsureLegacy(movement,stockEvent.AutorId,stockEvent.ResponsavelId,stockEvent.Motivo,ct);
                var evidence=cost?.Estado=="Conhecido"?Evidence(cost.Evidencia!,movement.ItemId,movement.Unidade,movement.Quantidade,stockEvent.AutorId,stockEvent.ResponsavelId,cost.Motivo,stockEvent.Id.ToString("D")):null;
                var layer=Guid.NewGuid();CostComponent[] components=[new(Guid.NewGuid(),layer,movement.ItemId,movement.Unidade,movement.Quantidade,evidence?.Total,
                    evidence is null?[cost?.Motivo??"Recebimento sem evidência monetária"]:Gaps(evidence.LacunasJson),evidence?.Id)];
                var ev=await Event("Recebimento",stockEvent.AutorId,stockEvent.ResponsavelId,cost?.Motivo??stockEvent.Motivo,stockEvent.Id.ToString("D"),new{Versao=1,Pedido=cost,MovimentoId=movement.Id},ct,stockEvent.Id);
                await CreateLayer(movement.LoteId,movement.ItemId,movement.Unidade,movement.Quantidade,components,"Recebimento",stockEvent.Sequencia,ev,movement.LocalId,ct,movement.Id,evidence:evidence,id:layer);
            }
            return;
        }
        if(cost is not null)throw new ArgumentException("Custo documental é próprio do recebimento; avaliação de ajuste é um fato presente separado.");
        var debits=movements.Where(x=>x.Sentido=="Saida").ToArray();var credits=movements.Where(x=>x.Sentido=="Entrada").ToArray();
        foreach(var movement in debits.GroupBy(x=>new{x.LoteId,x.LocalId}).Select(x=>x.First()))await EnsureLegacy(movement,stockEvent.AutorId,stockEvent.ResponsavelId,stockEvent.Motivo,ct);
        var evMove=await Event(stockEvent.Tipo,stockEvent.AutorId,stockEvent.ResponsavelId,stockEvent.Motivo,stockEvent.Id.ToString("D"),new{Versao=1,Movimentos=movements.Select(x=>x.Id).ToArray()},ct,stockEvent.Id);
        if(debits.Length==1&&credits.Length==1)
        {
            var taken=await Take(debits[0],evMove,ct);var parcels=await repository.ListAsync<ParcelaCustoMaterial>(x=>x.EventoCustoId==evMove.Id&&x.Sentido=="Saida",false,ct);
            foreach(var parcel in parcels)
            {
                var positions=await repository.LayerPositionsAsync(credits[0].LoteId,true,ct);var pos=positions.SingleOrDefault(x=>x.CamadaId==parcel.CamadaId&&x.LocalId==credits[0].LocalId);
                if(pos is null){pos=PosicaoCustoMaterial.Create(parcel.CamadaId,credits[0].LocalId,credits[0].Unidade);repository.Add(pos);}
                var merged=Merge(Components(pos.ComponentesJson).Concat(Components(parcel.ComponentesJson)));
                pos.SetBalance(pos.Quantidade+parcel.Quantidade,Known(merged),InventoryJson.Write(merged),InventoryJson.Write(merged.SelectMany(x=>x.Lacunas).Distinct().ToArray()),MaterialCostRules.Coverage(merged),pos.Revisao);
                repository.Add(new ParcelaCustoMaterial(Guid.NewGuid(),evMove.Id,parcel.CamadaId,credits[0].LocalId,"Entrada",parcel.Quantidade,parcel.ValorConhecido,parcel.ComponentesJson,parcel.LacunasJson,credits[0].Id));
            }
            _=taken;await repository.SaveAsync(ct);return;
        }
        foreach(var movement in debits)await Take(movement,evMove,ct);
        foreach(var movement in credits)
        {
            await EnsureLegacy(movement,stockEvent.AutorId,stockEvent.ResponsavelId,stockEvent.Motivo,ct);var layer=Guid.NewGuid();
            CostComponent[] components=[new(Guid.NewGuid(),layer,movement.ItemId,movement.Unidade,movement.Quantidade,null,["Acréscimo físico sem avaliação monetária"] )];
            await CreateLayer(movement.LoteId,movement.ItemId,movement.Unidade,movement.Quantidade,components,"AjustePositivo",stockEvent.Sequencia,evMove,movement.LocalId,ct,movement.Id,id:layer);
        }
    }
}
