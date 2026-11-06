using System.Globalization;
using System.Numerics;
using System.Text.Json;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;
using static GenSW.Application.MaterialCosts.MaterialCostMapping;

namespace GenSW.Application.MaterialCosts;

public sealed partial class MaterialCostService
{
    private sealed record Balance(Guid Layer,Guid Local,decimal Quantity,IReadOnlyList<CostComponent> Components,int Revision);
    private static IReadOnlyList<CostComponent> ComponentsAtCut(IEnumerable<ParcelaCustoMaterial> parts)
    {
        var expanded=parts.SelectMany(part=>Components(part.ComponentesJson).Select(component=>(Component:component,Sign:part.Sentido=="Entrada"?1:-1)));
        return expanded.GroupBy(x=>x.Component.Id).Select(group=>
        {
            var first=group.First().Component;var numerator=BigInteger.Zero;var denominator=BigInteger.One;var cents=BigInteger.Zero;
            foreach(var value in group)
            {
                var n=BigInteger.Parse(value.Component.FracaoNumerador,CultureInfo.InvariantCulture);var d=BigInteger.Parse(value.Component.FracaoDenominador,CultureInfo.InvariantCulture);
                numerator=numerator*d+value.Sign*n*denominator;denominator*=d;var gcd=BigInteger.GreatestCommonDivisor(numerator,denominator);numerator/=gcd;denominator/=gcd;
                if(value.Component.ValorConhecido is {} known)cents+=value.Sign*MaterialCostRules.Cents(known);
            }
            if(numerator<0||numerator>denominator||cents<0||numerator==0&&cents!=0)throw new MaterialCostException(409,"livro_custo_divergente","Livro monetário não conserva cobertura/centavos.");
            return first with{FracaoNumerador=numerator.ToString(CultureInfo.InvariantCulture),FracaoDenominador=denominator.ToString(CultureInfo.InvariantCulture),
                ValorConhecido=first.ValorConhecido is null?null:MaterialCostRules.FromCents(cents)};
        }).Where(x=>x.FracaoNumerador!="0").OrderBy(x=>x.Id.ToString("D"),StringComparer.Ordinal).ToArray();
    }
    private async Task<(long Monetary,long Physical,IReadOnlyList<EventoCustoMaterial> Events)> Cut(MaterialCostQuery query,CancellationToken ct)
    {
        query.Validate();var last=await repository.LastSequenceAsync(ct);var cut=query.AteSequencia is null?last:long.Parse(query.AteSequencia,CultureInfo.InvariantCulture);
        Conflict(cut>last,"corte_futuro","Corte monetário ainda não registrado.");
        var events=await repository.ListAsync<EventoCustoMaterial>(x=>x.Sequencia<=cut,false,ct);
        if(query.AteSequencia is not null&&last>0&&events.Count==0)throw new MaterialCostException(404,"corte_nao_registrado","Não há corte monetário registrado nesse ponto.");
        if(query.AteSequencia is not null&&events.Count>0)
        {
            var boundary=events.MaxBy(x=>x.Sequencia)!;
            if(boundary.Sequencia!=cut)throw new MaterialCostException(404,"corte_nao_registrado","Selecione uma sequência monetária registrada.");
            using var snapshot=JsonDocument.Parse(boundary.SnapshotJson);
            if(!snapshot.RootElement.TryGetProperty("transacaoId",out var transaction))throw new MaterialCostException(409,"corte_incompleto","Corte sem identidade transacional.");
            var identity=transaction.GetString();
            foreach(var later in await repository.ListAsync<EventoCustoMaterial>(x=>x.Sequencia>cut,false,ct))
            {using var next=JsonDocument.Parse(later.SnapshotJson);if(next.RootElement.TryGetProperty("transacaoId",out var nextIdentity)&&nextIdentity.GetString()==identity)throw new MaterialCostException(409,"corte_intermediario","A sequência está dentro de uma operação atômica. Consulte seu corte monetário final.");}
        }
        var physical=await inventory.LastSequenceAsync(ct);
        if(query.AteSequencia is not null&&events.Count>0)
        {
            physical=0;
            foreach(var ev in events)
            {
                using var snapshot=JsonDocument.Parse(ev.SnapshotJson);
                if(!snapshot.RootElement.TryGetProperty("corteFisico",out var field)||!long.TryParse(field.GetString(),NumberStyles.None,CultureInfo.InvariantCulture,out var value))throw new MaterialCostException(409,"corte_incompleto","Evento não identifica corte físico; preserve a evidência.");
                physical=Math.Max(physical,value);
            }
        }
        return(cut,physical,events);
    }
    private async Task<IReadOnlyList<Balance>> Balances(Guid lot,IReadOnlyList<EventoCustoMaterial> events,CancellationToken ct)
    {
        var layers=await repository.ListAsync<CamadaCustoMaterial>(x=>x.LoteId==lot,false,ct);var layerIds=layers.Select(x=>x.Id).ToArray();
        var eventIds=events.Select(x=>x.Id).ToHashSet();
        var parts=await repository.ListAsync<ParcelaCustoMaterial>(x=>layerIds.Contains(x.CamadaId),false,ct);
        return parts.Where(x=>eventIds.Contains(x.EventoCustoId)).GroupBy(x=>new{x.CamadaId,x.LocalId}).Select(group=>
        {
            var quantity=group.Sum(x=>x.Sentido=="Entrada"?x.Quantidade:-x.Quantidade);if(quantity<0)throw new MaterialCostException(409,"livro_custo_divergente","Livro monetário com quantidade negativa.");
            return new Balance(group.Key.CamadaId,group.Key.LocalId,quantity,ComponentsAtCut(group),group.Count());
        }).ToArray();
    }
    private async Task<MaterialLotView> Lot(Guid id,MaterialCostQuery query,CancellationToken ct)
    {
        var lot=await inventory.LoteAsync(id,false,ct)??throw Missing();var item=await inventory.ItemAsync(lot.ItemId,false,ct)??throw Missing();
        var cut=await Cut(query,ct);var balances=await Balances(id,cut.Events,ct);var layers=await repository.ListAsync<CamadaCustoMaterial>(x=>x.LoteId==id,false,ct);
        var stockMovements=await repository.ListAsync<MovimentoEstoque>(x=>x.LoteId==id,false,ct);var physicalByLocal=new Dictionary<Guid,decimal>();
        foreach(var movement in stockMovements)
        {
            var physicalEvent=await repository.GetAsync<EventoEstoque>(movement.EventoId,false,ct)??throw Missing();if(physicalEvent.Sequencia>cut.Physical)continue;
            physicalByLocal[movement.LocalId]=physicalByLocal.GetValueOrDefault(movement.LocalId)+(movement.Sentido=="Entrada"?movement.Quantidade:-movement.Quantidade);
        }
        var components=Merge(balances.SelectMany(x=>x.Components));var gaps=new List<string>();
        foreach(var pair in physicalByLocal)
        {
            var tracked=balances.Where(x=>x.Local==pair.Key).Sum(x=>x.Quantity);Conflict(tracked>pair.Value,"cobertura_divergente","Cobertura excede quantidade física no corte.");
            if(tracked<pair.Value)gaps.Add($"Saldo sem camada monetária: {Number(pair.Value-tracked)} {lot.Unidade}, local {pair.Key:D}");
        }
        var current=await repository.LayerPositionsAsync(id,false,ct);var revision=query.AteSequencia is null?current.Sum(x=>x.Revisao):balances.Sum(x=>x.Revision);
        var summary=Summary(components,physicalByLocal.Values.Sum(),cut.Monetary,revision,gaps);
        var views=layers.Where(layer=>balances.Any(x=>x.Layer==layer.Id)).OrderBy(x=>x.SequenciaOrigem).ThenBy(x=>x.Id.ToString("D"),StringComparer.Ordinal).Select(layer=>new MaterialLayerView(layer.Id,layer.LoteId,layer.ItemId,layer.Unidade,layer.TipoOrigem,
            Number(layer.QuantidadeOriginal),Money(layer.ValorConhecido),layer.Estado,Sequence(layer.SequenciaOrigem),layer.MovimentoOrigemId,layer.SaidaProducaoId,layer.EvidenciaId,layer.CamadaReferenciaId,layer.CreatedAtUtc,
            balances.Where(x=>x.Layer==layer.Id).OrderBy(x=>x.Local).Select(pos=>new MaterialPositionView(pos.Layer,pos.Local,layer.Unidade,Number(pos.Quantity),Summary(pos.Components,pos.Quantity,cut.Monetary,pos.Revision),pos.Revision)).ToArray())).ToArray();
        return new(id,lot.Codigo,item.Id,item.Codigo,item.Nome,lot.Unidade,summary,views,Sequence(cut.Physical),Sequence(cut.Monetary));
    }
    public Task<MaterialLotView> LotAsync(Guid id,MaterialCostQuery query,Guid actor,CancellationToken ct)=>Read(actor,false,()=>Lot(id,query,ct),ct);
    public Task<MaterialCostPage<MaterialHistoryView>> LotHistoryAsync(Guid id,MaterialCostQuery query,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {
        _=await inventory.LoteAsync(id,false,ct)??throw Missing();var cut=await Cut(query,ct);var layers=(await repository.ListAsync<CamadaCustoMaterial>(x=>x.LoteId==id,false,ct)).Select(x=>x.Id).ToArray();
        var events=(await repository.ListAsync<ParcelaCustoMaterial>(x=>layers.Contains(x.CamadaId),false,ct)).Select(x=>x.EventoCustoId).ToHashSet();
        return Page(cut.Events.Where(x=>events.Contains(x.Id)).OrderByDescending(x=>x.Sequencia).Select(History),query);
    },ct);
    public Task<MaterialExecutionView> ExecutionAsync(Guid id,MaterialCostQuery query,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {
        var physical=await repository.GetAsync<ExecucaoProducao>(id,false,ct)??throw Missing();var cut=await Cut(query,ct);
        var physicalOutputs=await repository.ListAsync<SaidaProducao>(x=>x.ExecucaoId==id,false,ct);var outputViews=new List<MaterialExecutionOutput>();
        foreach(var output in physicalOutputs){var lot=await inventory.LoteAsync(output.LoteId,false,ct)??throw Missing();outputViews.Add(new(output.Id,output.LoteId,output.LocalId,Number(output.Quantidade),lot.Unidade));}
        var value=(await repository.ListAsync<ApuracaoCustoExecucao>(x=>x.ExecucaoId==id&&x.CorteMonetario<=cut.Monetary,false,ct)).OrderByDescending(x=>x.CorteMonetario).ThenBy(x=>x.Id).FirstOrDefault();
        if(value is null)return new MaterialExecutionView(Guid.Empty,id,"SemApuracaoHistorica",Summary([],0,cut.Monetary,0,["Execução anterior sem apuração de custos; histórico não recalculado"]),[],Sequence(cut.Physical),Sequence(cut.Monetary),"Sem evidência monetária histórica",DateTimeOffset.UnixEpoch,"{}",null,physical.OrdemId,outputViews);
        var components=Components(value.ComponentesJson);var summary=Summary(components,0,value.CorteMonetario,1,Gaps(value.LacunasJson));
        var destinations=await repository.ListAsync<DestinoCustoExecucao>(x=>x.ApuracaoId==value.Id,false,ct);
        var views=destinations.OrderBy(x=>x.DestinoId.ToString("D"),StringComparer.Ordinal).Select(destination=>
        {
            var inherited=Components(destination.ComponentesJson);var cost=Summary(inherited,destination.Tipo=="Saida"?destination.Quantidade:0,value.CorteMonetario,1,Gaps(destination.LacunasJson));
            if (destination.Tipo == "Saida" && destination.Quantidade == 0)
                cost = cost with { Estado = "NaoAplicavel", ValorTotal = null, CustoUnitarioCompleto = null };
            else if (destination.Percentual == 0)
                cost = cost with { Estado = "Completo", ValorTotal = "0.00", CustoUnitarioCompleto = destination.Tipo == "Saida" ? "0.000000" : null };
            var unit=destination.Tipo=="Saida"&&destination.Quantidade>0?MaterialCostRules.UnitCost(destination.ValorConhecido,destination.Quantidade):null;
            return new MaterialDestinationView(destination.Id,destination.DestinoId,destination.Tipo,destination.SaidaProducaoId,destination.PerdaId,Number(destination.Quantidade),destination.Unidade,Number(destination.Percentual),cost,
                unit?.Valor.ToString("0.000000",CultureInfo.InvariantCulture),unit?.Arredondado??false,unit?.ResiduoNumerador,unit?.ResiduoDenominador,destination.Motivo,physicalOutputs.SingleOrDefault(x=>x.Id==destination.SaidaProducaoId)?.LoteId);
        }).ToArray();
        var originEvents=cut.Events.Where(x=>x.ExecucaoId==id&&x.Tipo=="Producao").Select(x=>x.Id).ToArray();
        var originalParts=await repository.ListAsync<ParcelaCustoMaterial>(x=>originEvents.Contains(x.EventoCustoId),false,ct);
        var originalIds=originalParts.Select(x=>x.Id).ToArray();var cutEventIds=cut.Events.Select(x=>x.Id).ToArray();
        var compensated=await repository.ListAsync<ParcelaCustoMaterial>(x=>x.ParcelaOriginalId.HasValue&&originalIds.Contains(x.ParcelaOriginalId.Value)&&cutEventIds.Contains(x.EventoCustoId),false,ct);
        var state=originalParts.Count>0&&compensated.Count==originalParts.Count?"Compensada":value.Estado;
        return new MaterialExecutionView(value.Id,id,state,summary,views,Sequence(value.CorteFisico),Sequence(value.CorteMonetario),value.Motivo,value.CreatedAtUtc,value.SnapshotJson,value.ApuracaoOriginalId,physical.OrdemId,outputViews);
    },ct);
    public Task<MaterialCostPage<MaterialReconciliationView>> ReconcileAsync(MaterialCostQuery query,Guid actor,CancellationToken ct)=>Read(actor,true,async()=>
    {
        var cut=await Cut(query,ct);var lots=await repository.ListAsync<LoteMaterial>(x=>(!query.LoteId.HasValue||x.Id==query.LoteId)&&(!query.ItemId.HasValue||x.ItemId==query.ItemId),false,ct);
        var rows=new List<MaterialReconciliationView>();
        foreach(var lot in lots)
        {
            var balances=await Balances(lot.Id,cut.Events,ct);var positions=query.AteSequencia is null?await repository.LayerPositionsAsync(lot.Id,false,ct):[];
            var physical=new Dictionary<Guid,decimal>();
            foreach(var movement in await repository.ListAsync<MovimentoEstoque>(x=>x.LoteId==lot.Id,false,ct))
            {var physicalEvent=await repository.GetAsync<EventoEstoque>(movement.EventoId,false,ct)??throw Missing();if(physicalEvent.Sequencia<=cut.Physical)physical[movement.LocalId]=physical.GetValueOrDefault(movement.LocalId)+(movement.Sentido=="Entrada"?movement.Quantidade:-movement.Quantidade);}
            foreach(var local in balances.Select(x=>x.Local).Concat(physical.Keys).Distinct().Where(x=>!query.LocalId.HasValue||x==query.LocalId))
            {
                var errors=new List<string>();var traced=balances.Where(x=>x.Local==local).ToArray();
                if(query.AteSequencia is null)
                {
                    foreach(var balance in traced)
                    {
                        var projection=positions.SingleOrDefault(x=>x.CamadaId==balance.Layer&&x.LocalId==local);
                        if(projection is null||projection.Quantidade!=balance.Quantity||projection.ValorConhecido!=Known(balance.Components)||InventoryJson.Write(Merge(Components(projection.ComponentesJson)))!=InventoryJson.Write(Merge(balance.Components)))errors.Add($"Livro/projeção divergentes na camada {balance.Layer:D}");
                    }
                }
                if(traced.Sum(x=>x.Quantity)>physical.GetValueOrDefault(local))errors.Add("Cobertura excede saldo físico no corte.");
                rows.Add(new(lot.Id,local,errors.Count==0,errors));
            }
        }
        return Page(rows.OrderBy(x=>x.LoteId).ThenBy(x=>x.LocalId),query);
    },ct);
}
