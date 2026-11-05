using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Application.Authentication;
using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using static GenSW.Application.MaterialCosts.MaterialCostMapping;

namespace GenSW.Application.MaterialCosts;

public sealed partial class MaterialCostService(IMaterialCostRepository repository,IInventoryRepository inventory,
    IMaterialCostFlow flow,IAuthenticationSessionService authentication,TimeProvider clock)
{
    private DateTimeOffset Now=>clock.GetUtcNow();
    private DateOnly Today=>InventoryRules.Today(clock);
    private static MaterialCostException Missing()=>new(404,"custo_nao_encontrado","Registro monetário não encontrado.");
    private static void Conflict(bool condition,string code,string message){if(condition)throw new MaterialCostException(409,code,message);}
    private static string Text(string? value,string label="Motivo")=>CatalogRules.Text(value,2000,label);
    private async Task Authorize(Guid actor,bool admin,CancellationToken ct)
    {
        var user=await authentication.GetCurrentUserAsync(actor,ct)??throw new MaterialCostException(401,"sessao_invalida","Entre novamente; sessão não está ativa.");
        if(admin&&!user.Roles.Contains("Admin",StringComparer.Ordinal))throw new MaterialCostException(403,"admin_necessario","Operação monetária exige papel Admin atual.");
    }
    private async Task<T> Read<T>(Guid actor,bool admin,Func<Task<T>> action,CancellationToken ct)
    {await using var scope=await repository.BeginReadAsync(ct);await Authorize(actor,admin,ct);var result=await action();await scope.CommitAsync(ct);return result;}
    private sealed record Payload(object Body,int Status=200,string? Location=null,Guid? EventoId=null,Guid? RegistroId=null);
    public static string CanonicalHash<T>(T command)
    {
        using var document=JsonDocument.Parse(InventoryJson.Write(command));
        string Canonical(JsonElement value,string? field=null)=>value.ValueKind switch{
            JsonValueKind.Object=>"{"+string.Join(",",value.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>JsonSerializer.Serialize(x.Name)+":"+Canonical(x.Value,x.Name)))+"}",
            JsonValueKind.Array=>"["+string.Join(",",value.EnumerateArray().Select(x=>Canonical(x)))+"]",
            JsonValueKind.String when field is "totalDocumentado" or "valorUnitario" or "quantidade" or "quantidadeBase" or "percentual"=>decimal.TryParse(value.GetString(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number)?JsonSerializer.Serialize(Number(number)):value.GetRawText(),
            _=>value.GetRawText()};
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(document.RootElement))));
    }
    private async Task<MaterialMutationResult> Mutate<T>(Guid resource,string operation,T command,string key,Guid actor,bool admin,Func<Task<Payload>> action,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);MaterialCostRules.Id(resource);key=CatalogRules.Text(key,100,"Idempotency-Key");var hash=CanonicalHash(command);
        await using var scope=await repository.BeginMutationAsync(ct);
        try
        {
            await Authorize(actor,admin,ct);
            var old=(await repository.ListAsync<ComandoCustoMaterial>(x=>x.RecursoId==resource&&x.Operacao==operation&&x.Chave==key,false,ct)).SingleOrDefault();
            if(old is not null)
            {
                await Authorize(actor,old.ExigeAdmin,ct);Conflict(old.HashPayload!=hash,"idempotencia_divergente","Chave utilizada com outro pedido; preserve a chave original para recuperar.");
                await scope.CommitAsync(ct);return new(old.StatusHttp,old.Location,old.RespostaJson,true);
            }
            var payload=await action();var body=InventoryJson.Write(payload.Body);
            repository.Add(new ComandoCustoMaterial(Guid.NewGuid(),operation,resource,key,hash,actor,admin,payload.Status,payload.Location,body,Now,payload.EventoId,payload.RegistroId));
            await repository.SaveAsync(ct);await scope.CommitAsync(ct);return new(payload.Status,payload.Location,body,false);
        }
        catch{repository.ClearTracking();throw;}
    }
    public Task<MaterialMutationResult> CommandAsync(Guid resource,string operation,string key,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {
        var command=(await repository.ListAsync<ComandoCustoMaterial>(x=>x.RecursoId==resource&&x.Operacao==operation&&x.Chave==key,false,ct)).SingleOrDefault()??throw Missing();
        await Authorize(actor,command.ExigeAdmin,ct);return new MaterialMutationResult(command.StatusHttp,command.Location,command.RespostaJson,true);
    },ct);
    private static MaterialCostPage<T> Page<T>(IEnumerable<T> values,MaterialCostQuery query)
    {
        query.Validate();var rows=values.ToArray();return new(rows.Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToArray(),query.Page,query.PageSize,rows.Length,(int)Math.Ceiling(rows.Length/(double)query.PageSize));
    }
    private async Task<MaterialReferenceView> Reference(PrecoReferenciaMaterial value,CancellationToken ct)
    {
        var item=await inventory.ItemAsync(value.ItemId,false,ct)??throw Missing();
        var replaced=(await repository.ListAsync<PrecoReferenciaMaterial>(x=>x.SubstituiId==value.Id,false,ct)).Count>0;
        return new(value.Id,value.ItemId,item.Codigo,item.Nome,value.Fonte,value.ReferenciaProtegida,value.DataFonte,value.Moeda,value.Unidade,Number(value.QuantidadeBase),
            value.ValorUnitario.ToString("0.000000",CultureInfo.InvariantCulture),value.VigenciaInicio,value.VigenciaFim,value.Aplicabilidade,value.Revogada?"Revogada":replaced?"Substituida":"Publicada",1,value.SubstituiId,value.CreatedAtUtc);
    }
    public Task<MaterialCostPage<MaterialReferenceView>> ReferencesAsync(MaterialCostQuery query,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {
        query.Validate();var values=await repository.ListAsync<PrecoReferenciaMaterial>(x=>!query.ItemId.HasValue||x.ItemId==query.ItemId,false,ct);
        var views=new List<MaterialReferenceView>();foreach(var value in values.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id))
        {
            var view=await Reference(value,ct);
            if(query.Estado is not null&&view.Estado!=query.Estado)continue;
            if(query.Search is {} term&&!string.Join(' ',view.ItemCodigo,view.ItemNome,view.Fonte,view.Aplicabilidade).Contains(term.Trim(),StringComparison.OrdinalIgnoreCase))continue;
            if(query.Data is {} date&&(date<view.VigenteDesde||view.VigenteAte is {} until&&date>=until))continue;
            views.Add(view);
        }
        return Page(views,query);
    },ct);
    public Task<MaterialReferenceView> ReferenceAsync(Guid id,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>await Reference(await repository.GetAsync<PrecoReferenciaMaterial>(id,false,ct)??throw Missing(),ct),ct);
    public Task<MaterialCostPage<MaterialReferenceView>> ReferenceHistoryAsync(Guid id,MaterialCostQuery query,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {
        query.Validate();var selected=await repository.GetAsync<PrecoReferenciaMaterial>(id,false,ct)??throw Missing();var values=await repository.ListAsync<PrecoReferenciaMaterial>(x=>x.ItemId==selected.ItemId,false,ct);
        var root=selected;var seen=new HashSet<Guid>();
        while(root.SubstituiId is {} previous){if(!seen.Add(root.Id))throw new MaterialCostException(409,"referencia_ciclica","Cadeia documental inválida.");root=values.SingleOrDefault(x=>x.Id==previous)??throw Missing();}
        seen.Clear();var related=new List<PrecoReferenciaMaterial>{root};
        while(values.SingleOrDefault(x=>x.SubstituiId==root.Id) is {} next){if(!seen.Add(root.Id))throw new MaterialCostException(409,"referencia_ciclica","Cadeia documental inválida.");related.Add(next);root=next;}
        var result=new List<MaterialReferenceView>();foreach(var value in related)result.Add(await Reference(value,ct));return Page(result,query);
    },ct);
    public Task<MaterialEvidenceView> EvidenceAsync(Guid id,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {var value=await repository.GetAsync<EvidenciaCustoMaterial>(id,false,ct)??throw Missing();return new MaterialEvidenceView(value.Id,value.ItemId,value.Fonte,value.ReferenciaProtegida,value.DataFonte,value.Moeda,value.Unidade,Number(value.Quantidade),Money(value.Total),Gaps(value.ComponentesJson),Gaps(value.LacunasJson),value.Metodo,value.AutorId,value.ResponsavelId,value.CreatedAtUtc,value.Motivo,value.HashDocumento);},ct);
    public Task<MaterialMutationResult> PublishReferenceAsync(MaterialReferenceCommand command,string key,Guid actor,CancellationToken ct,Guid? original=null)=>Mutate(command.Id,original is null?"referencia":"substituicao",new{OriginalId=original,Comando=command},key,actor,true,async()=>
    {
        MaterialCostRules.Id(command.ItemId);var item=await inventory.ItemAsync(command.ItemId,true,ct)??throw Missing();
        var quantity=MaterialCostRules.Quantity(command.QuantidadeBase,command.Unidade);if(quantity.Quantidade<=0)throw new ArgumentException("Quantidade-base deve ser positiva.");
        MaterialCostRules.Currency(command.Moeda);var unitPrice=MaterialCostRules.ReferenceUnitPrice(command.ValorUnitario);
        if(command.DataFonte==default||command.VigenteDesde==default)throw new ArgumentException("Datas da fonte e da vigência são obrigatórias.");InventoryRules.Date(command.DataFonte,Today);
        if(command.VigenteAte is {} until&&until<=command.VigenteDesde)throw new ArgumentException("Vigência usa início inclusivo e fim exclusivo.");
        if(original is {} parent)
        {
            var previous=await repository.GetAsync<PrecoReferenciaMaterial>(parent,false,ct)??throw Missing();
            if(previous.ItemId!=item.Id||parent==command.Id)throw new ArgumentException("Substituição deve preservar o item e possuir nova identidade.");
            Conflict((await repository.ListAsync<PrecoReferenciaMaterial>(x=>x.SubstituiId==parent,false,ct)).Count>0,"referencia_ja_substituida","Referência já substituída; selecione a versão atual.");
        }
        else if(command.Revogada)throw new ArgumentException("Revogação deve referenciar publicação anterior.");
        var value=new PrecoReferenciaMaterial(command.Id,item.Id,Text(command.Fonte,"Fonte"),Text(command.ReferenciaDocumento,"Referência documental protegida"),command.DataFonte,"BRL",quantity.Unidade,quantity.Quantidade,unitPrice,
            command.VigenteDesde,command.VigenteAte,Text(command.Aplicabilidade,"Aplicabilidade"),"CotacaoDocumental",actor,actor,Now,Today,Text(command.Motivo),command.Id.ToString("D"),MaterialCostRules.Algorithm,original,command.Revogada);
        repository.Add(value);var ev=new EventoCustoMaterial(Guid.NewGuid(),await repository.NextSequenceAsync(ct),"ReferenciaDocumental",actor,actor,Now,Today,command.Motivo,command.Id.ToString("D"),MaterialCostRules.Algorithm,
            InventoryJson.Write(new{Versao=1,ReferenciaId=value.Id,SubstituiId=original,CorteFisico=Sequence(await inventory.LastSequenceAsync(ct)),TransacaoId=repository.TransactionIdentity,SemEfeitoComprovado=true}));repository.Add(ev);
        await repository.SaveAsync(ct);return new(await Reference(value,ct),201,$"/api/v1/custos-materiais/referencias/{value.Id}",ev.Id,value.Id);
    },ct);
    public Task<MaterialMutationResult> EvaluateAsync(MaterialEvaluationCommand command,string key,Guid actor,CancellationToken ct)=>Mutate(command.Id,"avaliacao",command,key,actor,true,async()=>
    {
        var id=await flow.EvaluateAsync(command,actor,ct);var ev=await repository.GetAsync<EventoCustoMaterial>(id,false,ct)??throw Missing();
        return new(History(ev),201,$"/api/v1/custos-materiais/avaliacoes/{id}",id,id);
    },ct);
    public Task<MaterialMutationResult> CompensateAsync(Guid original,MaterialCompensationCommand command,string key,Guid actor,CancellationToken ct)=>Mutate(command.Id,"compensacao",new{OriginalId=original,Comando=command},key,actor,true,async()=>
    {
        var id=await flow.CompensateEvaluationAsync(original,command,actor,ct);return new(History(await repository.GetAsync<EventoCustoMaterial>(id,false,ct)??throw Missing()),201,null,id,id);
    },ct);
    public Task<MaterialProductionPreview> PreviewResolutionAsync(Guid execution,MaterialResolutionCommand command,Guid actor,CancellationToken ct)=>Read(actor,true,()=>flow.PreviewResolutionAsync(execution,command,ct),ct);
    public Task<MaterialMutationResult> ResolveAsync(Guid execution,MaterialResolutionCommand command,string key,Guid actor,CancellationToken ct)=>Mutate(command.Id,"resolucao",new{ExecucaoId=execution,Comando=command},key,actor,true,async()=>
    {
        var id=await flow.ResolveAsync(execution,command,actor,ct);
        return new(new{command.Id,ApuracaoId=id,ExecucaoId=execution,Estado="ResolucaoPresente",OriginalPreservado=true},201,$"/api/v1/custos-materiais/execucoes/{execution}",command.Id,id);
    },ct);
    private static MaterialHistoryView History(EventoCustoMaterial value)=>new(value.Id,value.Tipo,Sequence(value.Sequencia),value.Motivo,value.CreatedAtUtc,value.SnapshotJson,value.EventoOriginalId);
    public Task<MaterialHistoryView> EvaluationAsync(Guid id,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>History(await repository.GetAsync<EventoCustoMaterial>(id,false,ct)??throw Missing()),ct);
    public Task<MaterialCostPage<MaterialHistoryView>> EvaluationsAsync(MaterialCostQuery query,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {query.Validate();return Page((await repository.ListAsync<EventoCustoMaterial>(x=>x.Tipo=="AvaliacaoPresente"||x.Tipo=="CompensacaoAvaliacao",false,ct)).OrderByDescending(x=>x.Sequencia).Select(History),query);},ct);
    public Task<MaterialMutationResult> EstimateAsync(MaterialEstimateCommand command,string key,Guid actor,CancellationToken ct)=>Mutate(command.Id,"estimativa",command,key,actor,false,async()=>
    {
        var existing = await repository.ListAsync<ComandoCustoMaterial>(x => x.RecursoId == command.Id && x.Operacao == "estimativa", false, ct);
        Conflict(existing.Count > 0, "estimativa_existente", "Esta estimativa já foi registrada. Consulte a identidade e a chave originais.");
        Text(command.Motivo);if(command.Linhas is null||command.Linhas.Count is <1 or >500||command.Linhas.Select(x=>x.Id).Distinct().Count()!=command.Linhas.Count)throw new ArgumentException("Linhas de estimativa inválidas.");
        var lines=new List<object>();var total=BigInteger.Zero;var allGaps=new List<string>();
        foreach(var line in command.Linhas)
        {
            MaterialCostRules.Id(line.Id);var item=await inventory.ItemAsync(line.ItemId,false,ct)??throw Missing();var quantity=MaterialCostRules.Quantity(line.Quantidade,line.Unidade);var gaps=new List<string>();
            PrecoReferenciaMaterial? reference=null;decimal? amount=null;bool rounded=false;
            if(line.ReferenciaId is {} selected)
            {
                reference=await repository.GetAsync<PrecoReferenciaMaterial>(selected,false,ct)??throw Missing();if(reference.ItemId!=item.Id)throw new ArgumentException("Referência de outro item.");
                if(reference.Revogada||(await repository.ListAsync<PrecoReferenciaMaterial>(x=>x.SubstituiId==reference.Id,false,ct)).Count>0)gaps.Add("Versão documental revogada/substituída selecionada explicitamente");
                if(Today<reference.VigenciaInicio||reference.VigenciaFim is {} until&&Today>=until)gaps.Add("Referência fora da vigência atual");
                var ratio = (Numerator: BigInteger.One, Denominator: BigInteger.One);
                var comparable = true;
                if (quantity.Unidade != reference.Unidade)
                {
                    var conversion = line.ConversaoId is { } id ? await inventory.ConversionAsync(id, ct) : null;
                    var documentedRatio = conversion is { } document && document.ItemId == item.Id
                        ? EstimateConversion(quantity.Unidade, reference.Unidade, document)
                        : null;
                    comparable = documentedRatio.HasValue;
                    if (documentedRatio is { } value) ratio = value;
                    else gaps.Add("Conversão documental selecionada ausente ou incompatível com as dimensões da linha");
                }
                if (comparable)
                {
                    var quantityNumerator = MaterialCostRules.ScaledQuantity(quantity.Quantidade, quantity.Unidade) * ratio.Numerator;
                    if (reference.Unidade == "un" && quantityNumerator % (ratio.Denominator * 1000000) != 0)
                        throw new ArgumentException("A conversão selecionada produz contagem fracionária em un.");
                    // A conversion is a ratio, not a new physical quantity. Round only the final cents.
                    var numerator = new BigInteger(reference.ValorUnitario * 1000000m) * quantityNumerator * 100;
                    var denominator = MaterialCostRules.ScaledQuantity(reference.QuantidadeBase, reference.Unidade) * ratio.Denominator * 1000000;
                    var cents=BigInteger.DivRem(numerator,denominator,out var remainder);if(remainder*2>denominator||remainder*2==denominator&&!cents.IsEven)cents++;
                    amount=MaterialCostRules.FromCents(cents);rounded=remainder!=0;total+=cents;
                }
            }
            else gaps.Add("Referência não selecionada; nenhum preço implícito");
            allGaps.AddRange(gaps);lines.Add(new{line.Id,line.ItemId,ItemCodigo=item.Codigo,ItemNome=item.Nome,line.Quantidade,line.Unidade,Referencia=reference is null?null:await Reference(reference,ct),line.ConversaoId,ValorEstimadoConhecido=amount is {} known?Money(known):null,Lacunas=gaps,Arredondado=rounded});
        }
        var knownTotal=MaterialCostRules.FromCents(total);
        return new(new{command.Id,Tipo="EstimativaPorReferencia",Rotulo="Estimado por referências documentais; não é custo comprovado",Linhas=lines,ValorEstimadoConhecido=Money(knownTotal),ValorEstimadoTotal=allGaps.Count==0?Money(knownTotal):null,Lacunas=allGaps.Distinct().ToArray(),CreatedAtUtc=Now,DataOperacional=Today,Algoritmo=MaterialCostRules.Algorithm,Motivo=command.Motivo},201,$"/api/v1/custos-materiais/estimativas/{command.Id}",RegistroId:command.Id);
    },ct);

    private static (BigInteger Numerator, BigInteger Denominator)? EstimateConversion(
        string quantityUnit, string referenceUnit, ConversaoItem conversion)
    {
        var source = CatalogRules.Dimension(conversion.Origem);
        var target = CatalogRules.Dimension(conversion.Destino);
        var quantityDimension = CatalogRules.Dimension(quantityUnit);
        var referenceDimension = CatalogRules.Dimension(referenceUnit);
        var numerator = new BigInteger(conversion.Fator * 1000000m) * (conversion.Destino is "g" or "mL" ? 1 : 1000);
        var denominator = new BigInteger(1000000) * (conversion.Origem is "g" or "mL" ? 1 : 1000);
        if (quantityDimension == source && referenceDimension == target) return (numerator, denominator);
        if (quantityDimension == target && referenceDimension == source) return (denominator, numerator);
        return null;
    }

    public Task<string> EstimateAsync(Guid id,Guid actor,CancellationToken ct)=>Read(actor,false,async()=>
    {var command=(await repository.ListAsync<ComandoCustoMaterial>(x=>x.RecursoId==id&&x.Operacao=="estimativa",false,ct)).SingleOrDefault()??throw Missing();return command.RespostaJson;},ct);
}
