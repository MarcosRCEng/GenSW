using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Application.Authentication;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Domain.Production;

namespace GenSW.Application.Production;

public sealed class ProductionService(IProductionRepository repository,IInventoryRepository inventory,
    IFormulationRepository formulation,IAuthenticationSessionService authentication,TimeProvider clock)
{
    public const string Algorithm="gensw-production/1.0";
    private DateTimeOffset Now=>clock.GetUtcNow();
    private DateOnly Today=>InventoryRules.Today(clock);
    private DateOnly? mutationDate;
    private DateOnly OperationalDate=>mutationDate??Today;
    private static ProductionException Missing()=>new(404,"registro_nao_encontrado","Registro não encontrado.");
    private static void Conflict(bool condition,string code,string message) { if(condition) throw new ProductionException(409,code,message); }
    private static void Admin(bool admin) { if(!admin) throw new ProductionException(403,"admin_necessario","Operação exige papel Admin atual."); }
    private static string Text(string? text,string label="Motivo")=>CatalogRules.Text(text,2000,label);
    private async Task<bool> Authorize(Guid actor,bool requireAdmin,CancellationToken ct)
    {
        var user=await authentication.GetCurrentUserAsync(actor,ct)??throw new ProductionException(401,"sessao_invalida","Usuário não está mais ativo. Entre novamente.");
        var admin=user.Roles.Contains("Admin",StringComparer.Ordinal);if(requireAdmin) Admin(admin);return admin;
    }
    private async Task Responsible(Guid id,CancellationToken ct)
    { InventoryRules.Id(id);var value=await inventory.ResponsibleAsync(id,true,ct)??throw Missing();Conflict(!value.Ativo,"responsavel_inativo","Escolha responsável ativo para o novo fato."); }
    private async Task<T> Read<T>(Guid actor,bool admin,Func<bool,Task<T>> action,CancellationToken ct)
    { await using var scope=await inventory.BeginReadAsync(ct);var isAdmin=await Authorize(actor,admin,ct);var result=await action(isAdmin);await scope.CommitAsync(ct);return result; }
    public static string CanonicalHash<T>(T command)
    {
        using var doc=JsonDocument.Parse(InventoryJson.Write(command));
        string Canonical(JsonElement e,string? field=null)=>e.ValueKind switch {
            JsonValueKind.Object=>"{"+string.Join(",",e.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>JsonSerializer.Serialize(x.Name)+":"+Canonical(x.Value,x.Name)))+"}",
            JsonValueKind.Array=>"["+string.Join(",",(field=="alocacoes"?e.EnumerateArray().OrderBy(x=>x.GetProperty("id").GetString(),StringComparer.Ordinal):e.EnumerateArray().AsEnumerable()).Select(x=>Canonical(x)))+"]",
            JsonValueKind.String when field is "quantidade" or "tamanho" or "calculado" or "normalizado" or "residuo"=>decimal.TryParse(e.GetString(),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number)?JsonSerializer.Serialize(CatalogRules.Format(number)):e.GetRawText(),
            _=>e.GetRawText() };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(doc.RootElement))));
    }
    private async Task<ProductionMutationResult> Mutate<T>(Guid id,string operation,T command,string key,Guid actor,bool admin,
        Func<bool,Task<OrdemProducao>> action,CancellationToken ct,int status=200)
    {
        ArgumentNullException.ThrowIfNull(command);ValidateShape(command);InventoryRules.Id(id);key=CatalogRules.Text(key,100,"Idempotency-Key");var hash=CanonicalHash(command);
        await using var scope=await repository.BeginMutationAsync(ct);
        try
        {
            var isAdmin=await Authorize(actor,admin,ct);
            mutationDate=Today;
            if(await repository.ReplayAsync(id,operation,key,ct) is {} replay)
            { if(replay.ExigeAdmin) Admin(isAdmin);Conflict(replay.HashPayload!=hash,"idempotencia_divergente","Chave já utilizada com conteúdo diferente.");await scope.CommitAsync(ct);return new(replay.StatusHttp,replay.Location,replay.RespostaJson,true); }
            await repository.EnsureOrderConsistentAsync(id,ct);
            var order=await action(isAdmin);await repository.SaveAsync(ct);
            var body=InventoryJson.Write(await View(order,isAdmin,ct));var location=status==201?$"/api/v1/ordens-producao/{id}":null;
            var dynamicAdmin=admin||(command is ConfirmCommand confirmation&&confirmation.Saidas.All(x=>CatalogRules.Decimal(x.Quantidade)==0));
            repository.Add(new ComandoProducao(Guid.NewGuid(),id,operation,key,hash,actor,status,location,body,dynamicAdmin,Now));
            await repository.SaveAsync(ct);Conflict(Today!=mutationDate,"conflito_transitorio","Data operacional mudou; confira nova prévia e tente novamente com a mesma chave.");await scope.CommitAsync(ct);return new(status,location,body,false);
        }
        catch { inventory.ClearTracking();throw; }
        finally { mutationDate=null; }
    }
    private static void ValidateShape<T>(T command)
    {
        void Acceptance(QuantizationAcceptance? a){if(a is null)return;InventoryRules.PreciseDecimal(a.Calculado);InventoryRules.PreciseDecimal(a.Normalizado);InventoryRules.PreciseDecimal(a.Residuo);Text(a.Motivo,"Motivo do aceite");}
        void Inputs(IReadOnlyList<InputCommand> inputs)
        {
            ArgumentNullException.ThrowIfNull(inputs);if(inputs.Count>500)throw new ArgumentException("Limite de linhas excedido.");
            foreach(var line in inputs){ArgumentNullException.ThrowIfNull(line);InventoryRules.Id(line.LinhaId);ArgumentNullException.ThrowIfNull(line.Alocacoes);if(line.Alocacoes.Count>500)throw new ArgumentException("Limite de alocações excedido.");foreach(var a in line.Alocacoes){ArgumentNullException.ThrowIfNull(a);InventoryRules.Id(a.Id);InventoryRules.Id(a.LoteId);InventoryRules.Id(a.LocalId);CatalogRules.Decimal(a.Quantidade);CatalogRules.Unit(a.Unidade);Acceptance(a.AceiteQuantizacao);}}
        }
        switch(command)
        {
            case OrderCommand c:InventoryRules.Id(c.Id);InventoryRules.Id(c.ReceitaVersaoId);InventoryRules.Id(c.ResponsavelId);CatalogRules.Text(c.Codigo,50,"Código");CatalogRules.Quantity(c.Tamanho,c.Unidade);Text(c.Motivo);break;
            case ProductionStateCommand c:Text(c.Motivo);break;
            case StartCommand c:InventoryRules.Id(c.ResponsavelId);Inputs(c.Entradas);Text(c.Motivo);break;
            case ConfirmCommand c:
                InventoryRules.Id(c.ResponsavelId);Inputs(c.Entradas);Text(c.Motivo);CatalogRules.Optional(c.Observacao);CatalogRules.Optional(c.Evidencia);ArgumentNullException.ThrowIfNull(c.Saidas);ArgumentNullException.ThrowIfNull(c.Perdas);
                if(c.Saidas.Count>500||c.Perdas.Count>100)throw new ArgumentException("Limite de saídas/perdas excedido.");
                foreach(var output in c.Saidas){ArgumentNullException.ThrowIfNull(output);InventoryRules.Id(output.Id);InventoryRules.Id(output.LinhaId);CatalogRules.Unit(output.Unidade);if(CatalogRules.Decimal(output.Quantidade)>0){InventoryRules.Id(output.LocalId);CatalogRules.Text(output.NovoCodigoLote,50,"Código do novo lote");}Acceptance(output.AceiteQuantizacao);}
                foreach(var loss in c.Perdas){ArgumentNullException.ThrowIfNull(loss);CatalogRules.Quantity(loss.Quantidade,loss.Unidade);Text(loss.Motivo);}
                break;
            case AbortCommand c:Text(c.Motivo);Text(c.Evidencia,"Evidência");break;
            case ReverseCommand c:Text(c.Motivo);Text(c.Evidencia,"Evidência");break;
            case OccurrenceCommand c:Text(c.Motivo);Text(c.Evidencia,"Evidência");break;
        }
    }
    private async Task<OrdemProducao> Order(Guid id,bool tracking,CancellationToken ct)=>await repository.OrderAsync(id,tracking,ct)??throw Missing();
    private void Audit(OrdemProducao order,string operation,Guid actor,string reason,object? before)
        =>repository.Add(new HistoricoProducao(Guid.NewGuid(),order.Id,operation,actor,Now,Text(reason),InventoryJson.Write(before),InventoryJson.Write(order)));
    private static PlanSnapshot Plan(OrdemProducao order)=>order.SnapshotPlanejadoJson is {} json?InventoryJson.Read<PlanSnapshot>(json):throw new ProductionException(409,"planejamento_ausente","Planeje a ordem antes desta ação.");
    private async Task<string?> Property(Guid? id,CancellationToken ct)
    { if(id is not {} property)return null;Conflict(!await inventory.PropertyActiveAsync(property,ct),"propriedade_inativa","Escolha propriedade ativa para o novo vínculo.");return await repository.PropertyLabelAsync(property,ct); }
    public Task<ProductionMutationResult> CreateAsync(OrderCommand c,string key,Guid actor,CancellationToken ct)=>Mutate(c.Id,"criacao",c,key,actor,false,async _=>
    {
        InventoryRules.Expected(0,c.VersaoEsperada);await Responsible(c.ResponsavelId,ct);var recipe=await formulation.VersionAsync(c.ReceitaVersaoId,false,ct);
        if(recipe is null)throw Missing();var order=OrdemProducao.Create(c.Id,c.Codigo,c.ReceitaVersaoId,c.Tamanho,c.Unidade,c.ResponsavelId,c.PropriedadeId,await Property(c.PropriedadeId,ct),actor,Now);
        repository.Add(order);Audit(order,"criacao",actor,c.Motivo,null);return order;
    },ct,201);
    public Task<ProductionMutationResult> UpdateAsync(Guid id,OrderCommand c,string key,Guid actor,CancellationToken ct)=>Mutate(id,"edicao",c,key,actor,false,async _=>
    {
        if(c.Id!=id)throw new ArgumentException("Identidade da ordem difere da rota.");var order=await Order(id,true,ct);var before=InventoryJson.Write(order);await Responsible(c.ResponsavelId,ct);
        if(await formulation.VersionAsync(c.ReceitaVersaoId,false,ct) is null)throw Missing();
        order.Update(c.Codigo,c.ReceitaVersaoId,c.Tamanho,c.Unidade,c.ResponsavelId,c.PropriedadeId,c.PropriedadeId==order.PropriedadeId?order.PropriedadeNome:await Property(c.PropriedadeId,ct),c.VersaoEsperada,Now);
        Audit(order,"edicao",actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;
    },ct);
    private static ConversionView Conversion(ConversaoItem c)=>new(c.Id,c.ItemId,c.Numero,c.Origem,c.Destino,CatalogRules.Format(c.Fator),c.Fonte,c.Metodo,c.DataFonte,c.Contexto,c.ReferenciaAmostra,c.Proveniencia);
    private async Task<PlanSnapshot> Snapshot(OrdemProducao order,CancellationToken ct)
    {
        var version=await formulation.VersionAsync(order.ReceitaVersaoId,false,ct)??throw Missing();var header=await formulation.RecipeAsync(version.ReceitaId,false,ct)??throw Missing();
        Conflict(!header.Ativo||version.Estado!="Publicado","receita_inativa","Planejamento exige receita ativa e versão publicada.");
        var data=version.Data();data.Validate(true);var scaled=data.Scale(order.Tamanho,order.Unidade);
        var items=new Dictionary<Guid,ItemSnapshot>();var conversions=new Dictionary<Guid,ConversionView>();var profiles=new Dictionary<Guid,ProfileView>();var dependencies=new Dictionary<Guid,RecipeVersionView>();var count=0;
        async Task Visit(RecipeVersion current,HashSet<Guid> path)
        {
            Conflict(!path.Add(current.ReceitaId),"ciclo_receita","Ciclo de receitas detectado.");if(path.Count>10)throw new ArgumentException("Profundidade máxima de sub-receitas: 10.");
            var content=current.Data();count+=content.Entradas.Count;if(count>500)throw new ArgumentException("Limite de linhas expandidas excedido.");
            foreach(var line in content.Entradas.Select(x=>(x.ItemId,x.PerfilId,x.ConversaoId,false)).Concat(content.Saidas.Select(x=>(x.ItemId,x.PerfilId,x.ConversaoId,true))))
            {
                var item=await inventory.ItemAsync(line.ItemId,false,ct)??throw Missing();Conflict(!item.Ativo||(!line.Item4&&!item.PodeEntrar)||(line.Item4&&!item.PodeProduzir),"item_inativo","Item não está apto ao novo planejamento.");items[item.Id]=new(item.Id,item.Codigo,item.Nome,item.Unidade,item.Classe,item.Revisao);
                if(line.PerfilId is {} profileId){var p=await inventory.ProfileAsync(profileId,ct)??throw Missing();Conflict(p.ItemId!=item.Id||p.Estado!="Publicado","perfil_inativo","Perfil incompatível ou não publicado.");profiles[p.Id]=new(p.Id,p.ItemId,p.Numero,p.Revisao,p.Estado,p.AutorId,p.CreatedAtUtc,p.PublishedAtUtc,p.Data());}
                if(line.ConversaoId is {} conversionId){var c=await inventory.ConversionAsync(conversionId,ct)??throw Missing();if(c.ItemId!=item.Id)throw new ArgumentException("Conversão pertence a outro item.");conversions[c.Id]=Conversion(c);}
            }
            foreach(var line in content.Entradas.Where(x=>x.SubReceitaVersaoId.HasValue))
            {
                var sub=await formulation.VersionAsync(line.SubReceitaVersaoId!.Value,false,ct)??throw Missing();var subHeader=await formulation.RecipeAsync(sub.ReceitaId,false,ct)??throw Missing();
                Conflict(sub.Estado!="Publicado"||!subHeader.Ativo,"receita_inativa","Sub-receita não está disponível para novo uso.");
                var output=sub.Data().Saidas.SingleOrDefault(x=>x.Id==line.SubSaidaId)??throw new ArgumentException("Saída da sub-receita inexistente.");if(output.ItemId!=line.ItemId)throw new ArgumentException("Sub-receita incompatível.");
                dependencies[sub.Id]=new(sub.Id,sub.ReceitaId,sub.Numero,sub.Revisao,sub.Estado,sub.AutorId,sub.CreatedAtUtc,sub.PublishedAtUtc,sub.Data());await Visit(sub,new(path));
            }
        }
        await Visit(version,[]);
        return new(new(version.Id,version.ReceitaId,version.Numero,version.Revisao,version.Estado,version.AutorId,version.CreatedAtUtc,version.PublishedAtUtc,data),new(header.Codigo,header.Nome,header.Finalidade),scaled,items.Values.OrderBy(x=>x.Id).ToArray(),conversions.Values.OrderBy(x=>x.Id).ToArray(),profiles.Values.OrderBy(x=>x.Id).ToArray(),dependencies.Values.OrderBy(x=>x.Id).ToArray(),Algorithm,await inventory.ResponsibleAsync(order.ResponsavelId,false,ct));
    }
    private async Task ValidatePlanUse(PlanSnapshot plan,CancellationToken ct)
    {
        foreach(var reference in new[]{plan.Receita}.Concat(plan.Dependencias))
        {var current=await formulation.VersionAsync(reference.Id,false,ct)??throw Missing();var header=await formulation.RecipeAsync(reference.ReceitaId,false,ct)??throw Missing();Conflict(current.Estado!="Publicado"||!header.Ativo,"receita_inativa","Receita/perfil inativado impede iniciar novo uso. Revise o plano.");}
        foreach(var reference in plan.Perfis)Conflict((await inventory.ProfileAsync(reference.Id,ct)??throw Missing()).Estado!="Publicado","perfil_inativo","Perfil inativado impede iniciar novo uso.");
    }
    public Task<ProductionMutationResult> PlanAsync(Guid id,ProductionStateCommand c,string key,Guid actor,CancellationToken ct)=>Mutate(id,"planejamento",c,key,actor,false,async _=>
    {var order=await Order(id,true,ct);order.Require("Rascunho");order.Expect(c.VersaoEsperada);var before=InventoryJson.Write(order);await Responsible(order.ResponsavelId,ct);var snapshot=await Snapshot(order,ct);var json=InventoryJson.Write(snapshot);order.Plan(json,CanonicalHash(snapshot),c.VersaoEsperada,Now);Audit(order,"planejamento",actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;},ct);
    public Task<ProductionMutationResult> StateAsync(Guid id,string operation,ProductionStateCommand c,string key,Guid actor,CancellationToken ct)
    {
        if(operation is not ("revisao-planejamento" or "cancelamento"))throw new ArgumentException("Ação inválida.");
        return Mutate(id,operation,c,key,actor,false,async _=>{var order=await Order(id,true,ct);var before=InventoryJson.Write(order);order.Transition(operation=="cancelamento"?"Cancelada":"Rascunho",c.VersaoEsperada,Now);Audit(order,operation,actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;},ct);
    }
    private sealed record InputPrepared(Guid LineId,AllocationCommand Command,Item Item,LoteMaterial Lot,LocalEstoque Local,PosicaoEstoque Position,ConversaoItem? Conversion,PhysicalQuantity Quantity,decimal Amount);
    private sealed record OutputPrepared(OutputCommand Command,Item Item,LocalEstoque Local,ConversaoItem? Conversion,PhysicalQuantity Quantity,decimal Amount);
    private sealed record Prepared(IReadOnlyList<InputPrepared> Inputs,IReadOnlyList<OutputPrepared> Outputs,IReadOnlyList<ProductionQuantityPreview> Quantities,IReadOnlyList<string> Warnings,bool Deviation);
    private static PhysicalQuantity Zero(string unit)=>new("0",unit,"0","0","0",unit,null,null,null,false);
    private static void Unique<T>(IEnumerable<T> ids,string message) {var values=ids.ToArray();if(values.Distinct().Count()!=values.Length)throw new ArgumentException(message);}
    private async Task<Prepared> Prepare(OrdemProducao order,IReadOnlyList<InputCommand> inputs,IReadOnlyList<OutputCommand>? outputs,
        bool starting,bool exceptional,bool tracking,bool accept,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inputs);if(inputs.Count>500||outputs?.Count>500)throw new ArgumentException("Limite de linhas excedido.");var plan=Plan(order);
        if(starting)await ValidatePlanUse(plan,ct);
        Unique(inputs.Select(x=>x.LinhaId),"Linha de entrada duplicada.");
        if(!inputs.Select(x=>x.LinhaId).Order().SequenceEqual(plan.Escalonada.Entradas.Select(x=>x.Id).Order()))throw new ArgumentException("Informe exatamente todas as linhas do plano; entrada não utilizada recebe alocações vazias.");
        if(inputs.Any(x=>x.Alocacoes is null||x.Alocacoes.Count>500)||inputs.Sum(x=>x.Alocacoes.Count)>1000)throw new ArgumentException("Alocações inválidas ou acima do limite.");
        var allocations=inputs.SelectMany(line=>line.Alocacoes.Select(a=>(line.LinhaId,Allocation:a))).ToArray();Unique(allocations.Select(x=>x.Allocation.Id),"Identificador de alocação duplicado.");Unique(allocations.Select(x=>(x.LinhaId,x.Allocation.LoteId,x.Allocation.LocalId)),"Alocação da mesma linha/lote/local duplicada.");
        foreach(var a in allocations) {InventoryRules.Id(a.Allocation.Id);InventoryRules.Id(a.Allocation.LoteId);InventoryRules.Id(a.Allocation.LocalId);}
        if(outputs is not null){Unique(outputs.Select(x=>x.Id),"Identificador de saída duplicado.");if(outputs.Any(x=>x.Id==Guid.Empty)||!outputs.Select(x=>x.LinhaId).Distinct().Order().SequenceEqual(plan.Escalonada.Saidas.Select(x=>x.Id).Order()))throw new ArgumentException("Informe todas e somente as linhas de saída do plano, inclusive saídas zero.");}
        var preliminary=new Dictionary<Guid,LoteMaterial>();foreach(var lotId in allocations.Select(x=>x.Allocation.LoteId).Distinct().Order())preliminary[lotId]=await inventory.LoteAsync(lotId,false,ct)??throw Missing();
        var items=new Dictionary<Guid,Item>();foreach(var itemId in preliminary.Values.Select(x=>x.ItemId).Concat(plan.Escalonada.Saidas.Select(x=>x.ItemId)).Distinct().Order())items[itemId]=await inventory.ItemAsync(itemId,tracking,ct)??throw Missing();
        var locals=new Dictionary<Guid,LocalEstoque>();foreach(var localId in allocations.Select(x=>x.Allocation.LocalId).Concat(outputs?.Where(x=>CatalogRules.Decimal(x.Quantidade)>0).Select(x=>x.LocalId)??[]).Distinct().Order())locals[localId]=await inventory.LocalAsync(localId,tracking,ct)??throw Missing();
        var lots=new Dictionary<Guid,LoteMaterial>();foreach(var lotId in preliminary.Keys.Order())lots[lotId]=await inventory.LoteAsync(lotId,tracking,ct)??throw Missing();
        var positions=new Dictionary<(Guid,Guid),PosicaoEstoque>();foreach(var pair in allocations.Select(x=>(x.Allocation.LoteId,x.Allocation.LocalId)).Distinct().OrderBy(x=>x.LoteId).ThenBy(x=>x.LocalId)){positions[pair]=await inventory.PositionAsync(pair.LoteId,pair.LocalId,tracking,ct)??PosicaoEstoque.Create(pair.LoteId,pair.LocalId,lots[pair.LoteId].Unidade);await repository.EnsurePositionConsistentAsync(pair.LoteId,pair.LocalId,ct);}
        var original=starting?[]:await repository.ReservationsAsync(order.Id,false,ct);var prepared=new List<InputPrepared>();var quantities=new List<ProductionQuantityPreview>();var warnings=new List<string>();var deviation=false;
        foreach(var allocation in allocations)
        {
            var c=allocation.Allocation;var line=plan.Escalonada.Entradas.Single(x=>x.Id==allocation.LinhaId);var lot=lots[c.LoteId];var item=items[lot.ItemId];var local=locals[c.LocalId];
            if(lot.ItemId!=line.ItemId)throw new ArgumentException("Alocação deve usar o Item da linha planejada.");Conflict(lot.Situacao=="Encerrado","lote_encerrado","Lote encerrado não admite movimento.");
            if(!exceptional)Conflict(InventoryRules.Unavailable(item,lot,local,Today).Count>0,"estoque_inelegivel","Entrada exige item/local/lote ativos, Ordinário, Liberado e não vencido.");
            var prior=original.SingleOrDefault(x=>x.Id==c.Id);
            if(prior is not null)
            {
                if(prior.LinhaId!=line.Id||prior.LoteId!=lot.Id||prior.LocalId!=local.Id)throw new ArgumentException("Troca de posição exige novo identificador de alocação.");
                using var priorJson=JsonDocument.Parse(prior.SnapshotJson);var priorCommand=InventoryJson.Read<AllocationCommand>(priorJson.RootElement.GetProperty("alocacao").GetRawText());
                Conflict(priorCommand.ConversaoItemId!=c.ConversaoItemId,"conversao_alterada","Confirmação deve preservar a conversão da alocação reservada.");
            }
            ConversaoItem? conversion=null;if(c.ConversaoItemId is {} conversionId){if(prior is null&&conversionId!=lot.ConversaoItemId)throw new ArgumentException("Conversão deve estar explicitamente vinculada ao lote.");conversion=await inventory.ConversionAsync(conversionId,ct)??throw Missing();if(conversion.ItemId!=item.Id)throw new ArgumentException("Conversão pertence a outro item.");}
            CatalogRules.Unit(c.Unidade);if(CatalogRules.Decimal(c.Quantidade)==0&&CatalogRules.Dimension(c.Unidade)!=CatalogRules.Dimension(lot.Unidade)&&
                (conversion is null||!((CatalogRules.Dimension(c.Unidade)==CatalogRules.Dimension(conversion.Origem)&&CatalogRules.Dimension(lot.Unidade)==CatalogRules.Dimension(conversion.Destino))||(CatalogRules.Dimension(c.Unidade)==CatalogRules.Dimension(conversion.Destino)&&CatalogRules.Dimension(lot.Unidade)==CatalogRules.Dimension(conversion.Origem)))))throw new ArgumentException("Unidade da alocação zero é incompatível com o lote.");
            var quantity=CatalogRules.Decimal(c.Quantidade)==0&&!starting?Zero(lot.Unidade):InventoryRules.Normalize(c.Quantidade,c.Unidade,lot.Unidade,conversion,item.Id);if(accept)InventoryRules.Accept(quantity,c.AceiteQuantizacao);
            var amount=CatalogRules.Decimal(quantity.Normalizada);var position=positions[(lot.Id,local.Id)];prepared.Add(new(line.Id,c,item,lot,local,position,conversion,quantity,amount));quantities.Add(new("Entrada",line.Id,c.Id,quantity));if(lot.Validade is null)warnings.Add($"Lote {lot.Codigo}: validade não informada.");
        }
        foreach(var group in prepared.GroupBy(x=>(x.Lot.Id,x.Local.Id)))
        {var first=group.First();var other=await repository.ReservedAsync(first.Lot.Id,first.Local.Id,starting?null:order.Id,ct);Conflict(group.Sum(x=>x.Amount)>first.Position.Quantidade-other,"saldo_reserva_insuficiente","Quantidade excede o físico disponível após reservas de outras ordens.");}
        foreach(var line in plan.Escalonada.Entradas)
        {
            var item=plan.Itens.Single(x=>x.Id==line.ItemId);var conversion=line.ConversaoId is {} c?await inventory.ConversionAsync(c,ct):null;var expected=InventoryRules.Normalize(line.Quantidade,line.Unidade,item.Unidade,conversion,item.Id);var actual=prepared.Where(x=>x.LineId==line.Id).Sum(x=>x.Amount);
            if(starting)Conflict(actual!=CatalogRules.Decimal(expected.Normalizada),"reserva_incompleta","Início exige a quantidade planejada integral de cada linha, inclusive embalagens/consumíveis.");else deviation|=actual!=CatalogRules.Decimal(expected.Normalizada);
        }
        var resultOutputs=new List<OutputPrepared>();
        foreach(var c in outputs??[])
        {
            var line=plan.Escalonada.Saidas.Single(x=>x.Id==c.LinhaId);var item=items[line.ItemId];CatalogRules.Unit(c.Unidade);var declared=CatalogRules.Decimal(c.Quantidade);if(declared==0){if(CatalogRules.Dimension(c.Unidade)!=CatalogRules.Dimension(item.Unidade))throw new ArgumentException("Unidade de saída zero é incompatível com o item.");if(c.AceiteQuantizacao is not null)throw new ArgumentException("Saída zero não admite aceite de resíduo.");quantities.Add(new("Saida",line.Id,c.Id,Zero(item.Unidade)));continue;}
            var local=locals[c.LocalId];Conflict(!local.Ativo,"local_inativo","Saída exige local ativo.");
            var blocked=exceptional||c.Bloqueado||InventoryRules.Expired(c.Validade,Today);
            if(exceptional)Conflict(local.Finalidade!="Segregacao","segregacao_necessaria","Registro de fato passado exige todas as saídas Bloqueadas em Segregação.");
            else {Conflict(!item.Ativo||!item.PodeProduzir,"item_inelegivel","Saída exige Item ativo com capacidade de produção.");Conflict(local.Finalidade!=(blocked?"Segregacao":"Ordinario"),"destino_invalido","Selecione destino Ordinário ou Segregação conforme a situação da saída.");}
            if(c.PerfilNutricionalId is {} profileId){var profile=await inventory.ProfileAsync(profileId,ct)??throw Missing();Conflict(profile.ItemId!=item.Id||profile.Estado!="Publicado","perfil_inativo","Selecione perfil publicado do item de saída.");}
            ConversaoItem? conversion=null;if(c.ConversaoItemId is {} conversionId){conversion=await inventory.ConversionAsync(conversionId,ct)??throw Missing();if(conversion.ItemId!=item.Id)throw new ArgumentException("Conversão da saída pertence a outro item.");}
            if(c.PerfilNutricionalId.HasValue||c.ConversaoItemId.HasValue)Text(c.Aplicabilidade,"Aplicabilidade das referências");if(c.Validade.HasValue)await Responsible(c.ResponsavelValidadeId??Guid.Empty,ct);
            var quantity=InventoryRules.Normalize(c.Quantidade,c.Unidade,item.Unidade,conversion,item.Id);if(accept)InventoryRules.Accept(quantity,c.AceiteQuantizacao);var amount=CatalogRules.Decimal(quantity.Normalizada);
            // Validate lot metadata in preview too; the generated identity is discarded until confirmation.
            _=LoteMaterial.Create(item.Id,c.NovoCodigoLote,item.Unidade,new("","Produção",order.Codigo,order.ResponsavelId,c.DataOrigem,c.Fabricacao,c.Coleta),c.Validade,c.FonteValidade,c.ResponsavelValidadeId,c.PerfilNutricionalId,c.ConversaoItemId,c.Aplicabilidade,blocked,Now,Today);
            resultOutputs.Add(new(c,item,local,conversion,quantity,amount));quantities.Add(new("Saida",line.Id,c.Id,quantity));if(c.Validade is null)warnings.Add($"Saída {line.Nome}: validade não informada.");
        }
        if(outputs is not null)foreach(var line in plan.Escalonada.Saidas)
        {var item=plan.Itens.Single(x=>x.Id==line.ItemId);var conversion=line.ConversaoId is {} conversionId?await inventory.ConversionAsync(conversionId,ct):null;var expected=InventoryRules.Normalize(line.Quantidade,line.Unidade,item.Unidade,conversion,item.Id);deviation|=resultOutputs.Where(x=>x.Command.LinhaId==line.Id).Sum(x=>x.Amount)!=CatalogRules.Decimal(expected.Normalizada);}
        warnings.Add("Durante a transformação, o físico contábil permanece intacto e as quantidades ficam reservadas até a confirmação integral.");
        return new(prepared,resultOutputs,quantities,warnings.Distinct().ToArray(),deviation);
    }
    public Task<ProductionPreview> PreviewAsync(Guid id,ProductionPreviewCommand c,Guid actor,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        if(c.Operacao=="inicio"&&c.Inicio is {} startShape)ValidateShape(startShape);
        else if(c.Operacao is "confirmacao" or "confirmacao-ocorrencia"&&c.Confirmacao is {} confirmShape)ValidateShape(confirmShape);
        else throw new ArgumentException("Prévia exige operação e comando correspondentes.");
        return Read(actor,c.Operacao=="confirmacao-ocorrencia",async _=>
    {
        var order=await Order(id,false,ct);Prepared prepared;
        if(c.Operacao=="inicio"&&c.Inicio is {} start){order.Require("Planejada");order.Expect(start.VersaoEsperada);prepared=await Prepare(order,start.Entradas,null,true,false,false,false,ct);}
        else if(c.Operacao is "confirmacao" or "confirmacao-ocorrencia"&&c.Confirmacao is {} confirm){order.Require("Iniciada");order.Expect(confirm.VersaoEsperada);prepared=await Prepare(order,confirm.Entradas,confirm.Saidas,false,c.Operacao=="confirmacao-ocorrencia",false,false,ct);}
        else throw new ArgumentException("Prévia exige operação e comando correspondentes.");return new ProductionPreview(prepared.Quantities,prepared.Warnings);
    },ct);
    }
    public Task<ProductionMutationResult> StartAsync(Guid id,StartCommand c,string key,Guid actor,CancellationToken ct)=>Mutate(id,"inicio",c,key,actor,false,async _=>
    {
        var order=await Order(id,true,ct);order.Require("Planejada");order.Expect(c.VersaoEsperada);Text(c.Motivo);await Responsible(c.ResponsavelId,ct);var before=InventoryJson.Write(order);
        var prepared=await Prepare(order,c.Entradas,null,true,false,true,true,ct);
        foreach(var input in prepared.Inputs)repository.Add(ReservaProducao.Create(input.Command.Id,id,input.LineId,input.Lot.Id,input.Local.Id,input.Lot.Unidade,input.Amount,InventoryJson.Write(new{Alocacao=input.Command,Quantidade=input.Quantity,Lote=input.Lot,Local=input.Local,Item=input.Item,Conversao=input.Conversion,Perfil=input.Lot.PerfilNutricionalId is {} p?await inventory.ProfileAsync(p,ct):null}),Now));
        order.Transition("Iniciada",c.VersaoEsperada,Now);Audit(order,"inicio",actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;
    },ct);
    private async Task<MovimentoEstoque> Movement(Guid execution,Guid line,Guid? allocation,Item item,LoteMaterial lot,LocalEstoque local,PosicaoEstoque position,
        decimal amount,bool credit,string operation,PhysicalQuantity quantity,QuantizationAcceptance? acceptance,Guid actor,Guid responsible,string reason,
        DateOnly? observed,CancellationToken ct,MovimentoEstoque? original=null,bool useSelectedConversion=false,Guid? selectedConversion=null)
    {
        var sequence=await inventory.NextSequenceAsync(ct);var eventId=Guid.NewGuid();var now=Now;var date=OperationalDate;
        var conversionId=original is not null?original.ConversaoItemId:useSelectedConversion?selectedConversion:lot.ConversaoItemId;
        var profileId=original is not null?original.PerfilNutricionalId:lot.PerfilNutricionalId;
        var conversion=original is null&&conversionId is {} conversionReference?await inventory.ConversionAsync(conversionReference,ct)??throw Missing():null;
        var profile=original is null&&profileId is {} profileReference?await inventory.ProfileAsync(profileReference,ct)??throw Missing():null;
        var snapshot=original is null?InventoryJson.Write(new{Previa=new{Quantidade=quantity},Pedido=new{AceiteQuantizacao=acceptance},Producao=new{ExecucaoId=execution,LinhaId=line},Item=item,Lote=lot,Local=local,Conversao=conversion,Perfil=profile}):
            InventoryJson.Write(new{Compensacao=new{MovimentoOriginalId=original.Id},Producao=new{ExecucaoId=execution,LinhaId=line}});
        var entity=new EventoEstoque(eventId,sequence,operation,actor,responsible,now,date,observed,Text(reason),null,original?.EventoId,Algorithm,snapshot);inventory.Add(entity);
        var before=position.Quantidade;var revision=position.Revisao;if(revision==0)inventory.Add(position);position.SetBalance(checked(before+(credit?amount:-amount)),revision);
        var movement=new MovimentoEstoque(Guid.NewGuid(),eventId,1,lot.Id,local.Id,item.Id,lot.Unidade,credit?"Entrada":"Saida",amount,original is null?quantity.Declarada:CatalogRules.Format(original.Quantidade),original is null?quantity.UnidadeDeclarada:lot.Unidade,original is null?quantity.Residuo:"0",before,position.Quantidade,conversionId,profileId,snapshot,operation);
        inventory.Add(movement);repository.Add(new ExecucaoMovimento(Guid.NewGuid(),execution,line,allocation,movement.Id,original is null?credit?"Saida":"Entrada":"Compensacao"));await repository.SaveAsync(ct);return movement;
    }
    public Task<ProductionMutationResult> ConfirmAsync(Guid id,ConfirmCommand c,string key,Guid actor,bool exceptional,CancellationToken ct)=>Mutate(id,exceptional?"confirmacao-ocorrencia":"confirmacao",c,key,actor,exceptional,async admin=>
    {
        var order=await Order(id,true,ct);order.Require("Iniciada");order.Expect(c.VersaoEsperada);Text(c.Motivo);InventoryRules.Date(c.DataObservada,Today);await Responsible(c.ResponsavelId,ct);
        if(exceptional){Text(c.Evidencia,"Evidência do fato ocorrido");if(c.DataObservada is null||c.AptidaoNoFato is not ("Apto" or "Irregular"))throw new ArgumentException("Informe data observada e aptidão Apto/Irregular comprovada no fato.");}
        var before=InventoryJson.Write(order);var prepared=await Prepare(order,c.Entradas,c.Saidas,false,exceptional,true,true,ct);var plan=Plan(order);
        if(prepared.Inputs.All(x=>x.Amount==0))throw new ArgumentException("Confirmação exige consumo positivo; sem consumo, utilize aborto sem consumo.");
        if(prepared.Outputs.Count==0){Admin(admin);Text(c.Evidencia,"Evidência da perda total");}
        ArgumentNullException.ThrowIfNull(c.Perdas);if(c.Perdas.Count>100)throw new ArgumentException("Limite de perdas excedido.");foreach(var loss in c.Perdas){CatalogRules.Quantity(loss.Quantidade,loss.Unidade);Text(loss.Motivo);if(loss.LinhaId is {} line&&!plan.Escalonada.Entradas.Any(x=>x.Id==line))throw new ArgumentException("Perda refere-se a linha inexistente.");}
        if(prepared.Deviation||c.Perdas.Count>0)Text(c.Observacao,"Observação explícita do desvio/perda");
        // Balance uses the selected documentary conversion and exact subunit factors, without quantizing a second physical fact.
        var balanceWarnings=new List<string>();
        decimal? Mass(Item item,decimal amount,ConversaoItem? conversion)
        {
            if(item.Classe is "Embalagem" or "Consumivel"||amount==0)return 0m;
            if(item.Unidade=="kg")return amount;
            if(conversion is not null)
            {
                try
                {
                    var sourceDimension=CatalogRules.Dimension(item.Unidade);
                    if(CatalogRules.Dimension(conversion.Origem)==sourceDimension&&CatalogRules.Dimension(conversion.Destino)=="massa")
                        return checked(amount/CatalogRules.Canonical(1m,conversion.Origem)*conversion.Fator*CatalogRules.Canonical(1m,conversion.Destino));
                    if(CatalogRules.Dimension(conversion.Destino)==sourceDimension&&CatalogRules.Dimension(conversion.Origem)=="massa")
                        return checked(amount/CatalogRules.Canonical(1m,conversion.Destino)/conversion.Fator*CatalogRules.Canonical(1m,conversion.Origem));
                }
                catch(OverflowException){balanceWarnings.Add($"Item {item.Codigo}: conversão excede a precisão decimal; balanço indeterminado.");return null;}
            }
            balanceWarnings.Add($"Item {item.Codigo}: não há conversão documental selecionada compatível com massa; balanço indeterminado.");return null;
        }
        var masses=prepared.Inputs.Select(x=>Mass(x.Item,x.Amount,x.Conversion)).ToArray();var outputMasses=prepared.Outputs.Select(x=>Mass(x.Item,x.Amount,x.Conversion)).ToArray();
        decimal? balance=null;
        if(c.Perdas.Any(x=>CatalogRules.Dimension(x.Unidade)!="massa"))balanceWarnings.Add("Há perdas sem conversão para massa; balanço indeterminado.");
        if(masses.All(x=>x.HasValue)&&outputMasses.All(x=>x.HasValue)&&c.Perdas.All(x=>CatalogRules.Dimension(x.Unidade)=="massa"))
        {
            try{balance=checked(masses.Sum(x=>x!.Value)-outputMasses.Sum(x=>x!.Value)-c.Perdas.Sum(x=>CatalogRules.Canonical(CatalogRules.Decimal(x.Quantidade),x.Unidade)));}
            catch(OverflowException){balanceWarnings.Add("Somatório excede a precisão decimal; balanço indeterminado.");}
        }
        if(balance is not null and not 0)Text(c.Observacao,"Observação da diferença de massa não explicada");
        var executionId=Guid.NewGuid();var exceptionKind=exceptional?c.AptidaoNoFato=="Apto"?"FatoApto":"ConsumoIrregular":null;
        var result=prepared.Outputs.Count==0?"PerdaTotal":exceptionKind??(prepared.Deviation||c.Perdas.Count>0?"ComDesvio":"Normal");
        repository.Add(new ExecucaoProducao(executionId,id,result,actor,c.ResponsavelId,Now,OperationalDate,c.DataObservada,InventoryJson.Write(new{Planejado=plan,Realizado=c,ClassificacaoOcorrencia=exceptionKind,Responsavel=await inventory.ResponsibleAsync(c.ResponsavelId,false,ct),Quantidades=prepared.Quantities,BalancoKg=balance is {} b?CatalogRules.Format(b):null,AvisosBalanco=balanceWarnings.Distinct().ToArray(),OcorrenciaProcessamento=plan.Escalonada.Tipo=="MisturaSimples"&&(c.Perdas.Count>0||balance is not null and not 0),AutorId=actor,Algoritmo=Algorithm}),Algorithm));
        var reservations=await repository.ReservationsAsync(id,true,ct);
        // Reserve supplement before the debit, so each reservation conserves its original quantity independently.
        foreach(var input in prepared.Inputs.Where(x=>x.Amount>0))
        {
            var remaining=input.Amount;var matching=reservations.Where(x=>x.LinhaId==input.LineId&&x.LoteId==input.Lot.Id&&x.LocalId==input.Local.Id&&x.QuantidadeAtiva>0).ToArray();
            foreach(var reservation in matching){var consumed=Math.Min(remaining,reservation.QuantidadeAtiva);reservation.Settle(consumed);remaining-=consumed;}
            if(remaining>0){var supplement=ReservaProducao.Create(Guid.NewGuid(),id,input.LineId,input.Lot.Id,input.Local.Id,input.Lot.Unidade,remaining,InventoryJson.Write(new{Alocacao=input.Command,Quantidade=input.Quantity,Lote=input.Lot,Local=input.Local,Item=input.Item,Conversao=input.Conversion,Perfil=input.Lot.PerfilNutricionalId is {} p?await inventory.ProfileAsync(p,ct):null,Suplementar=true}),Now);supplement.Settle(remaining);repository.Add(supplement);}
            await Movement(executionId,input.LineId,input.Command.Id,input.Item,input.Lot,input.Local,input.Position,input.Amount,false,"ProducaoConsumo",input.Quantity,input.Command.AceiteQuantizacao,actor,c.ResponsavelId,c.Motivo,c.DataObservada,ct,useSelectedConversion:true,selectedConversion:input.Command.ConversaoItemId);
        }
        foreach(var reservation in reservations.Where(x=>x.QuantidadeAtiva>0))reservation.Settle(0);
        var created=new List<SaidaProducao>();
        foreach(var output in prepared.Outputs)
        {
            var request=output.Command;var lot=LoteMaterial.Create(output.Item.Id,request.NovoCodigoLote,output.Item.Unidade,new("","Produção",order.Codigo,c.ResponsavelId,request.DataOrigem,request.Fabricacao,request.Coleta),request.Validade,request.FonteValidade,request.ResponsavelValidadeId,request.PerfilNutricionalId,request.ConversaoItemId,request.Aplicabilidade,exceptional||request.Bloqueado,Now,Today);
            output.Item.FixUnit();inventory.Add(lot);var position=PosicaoEstoque.Create(lot.Id,output.Local.Id,lot.Unidade);
            var movement=await Movement(executionId,request.LinhaId,request.Id,output.Item,lot,output.Local,position,output.Amount,true,"ProducaoSaida",output.Quantity,request.AceiteQuantizacao,actor,c.ResponsavelId,c.Motivo,c.DataObservada,ct);
            var link=new SaidaProducao(request.Id,executionId,request.LinhaId,lot.Id,output.Local.Id,output.Amount,lot.Revisao,lot.Situacao,lot.Validade,movement.EventoId);repository.Add(link);created.Add(link);await repository.SaveAsync(ct);
        }
        foreach(var input in prepared.Inputs.Where(x=>x.Amount>0).Select(x=>x.Lot.Id).Distinct())foreach(var output in created)repository.Add(new GenealogiaMaterial(Guid.NewGuid(),executionId,input,output.LoteId));
        if(exceptional||prepared.Deviation||c.Perdas.Count>0||balance is not null and not 0)repository.Add(new OcorrenciaProducao(Guid.NewGuid(),id,exceptional?exceptionKind!:plan.Escalonada.Tipo=="MisturaSimples"?"ProcessamentoRealizado":"Desvio",actor,Now,c.DataObservada,c.Motivo,c.Evidencia??c.Observacao??c.Motivo,true));
        order.Transition("Confirmada",c.VersaoEsperada,Now);Audit(order,exceptional?"confirmacao-ocorrencia":"confirmacao",actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;
    },ct);
    public Task<ProductionMutationResult> AbortAsync(Guid id,AbortCommand c,string key,Guid actor,CancellationToken ct)=>Mutate(id,"aborto-sem-consumo",c,key,actor,true,async _=>
    {var order=await Order(id,true,ct);order.Require("Iniciada");order.Expect(c.VersaoEsperada);Text(c.Motivo);Text(c.Evidencia,"Evidência da declaração");Conflict(!c.NenhumConsumoFisico||await repository.HasDeclaredConsumptionAsync(id,ct),"consumo_existente","Havendo consumo físico, registre todos os reais/perdas; aborto não pode restaurar saldo fictício.");var before=InventoryJson.Write(order);foreach(var reserve in await repository.ReservationsAsync(id,true,ct))if(reserve.QuantidadeAtiva>0)reserve.Settle(0);order.Transition("AbortadaSemConsumo",c.VersaoEsperada,Now);repository.Add(new OcorrenciaProducao(Guid.NewGuid(),id,"AbortoSemConsumo",actor,Now,null,c.Motivo,c.Evidencia,false));Audit(order,"aborto-sem-consumo",actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;},ct);
    public Task<ProductionMutationResult> OccurrenceAsync(Guid id,OccurrenceCommand c,string key,Guid actor,CancellationToken ct)=>Mutate(id,"ocorrencias",c,key,actor,false,async _=>
    {var order=await Order(id,true,ct);order.Expect(c.VersaoEsperada);Text(c.Motivo);Text(c.Evidencia,"Evidência da ocorrência");InventoryRules.Date(c.DataObservada,Today);if(c.Tipo is not ("Interrupcao" or "Desvio" or "Perda" or "Anotacao" or "Falta"))throw new ArgumentException("Tipo de ocorrência inválido.");if(c.Tipo!="Anotacao")order.Require("Iniciada","Planejada");if(c.HouveConsumoFisico)order.Require("Iniciada");var before=InventoryJson.Write(order);repository.Add(new OcorrenciaProducao(Guid.NewGuid(),id,c.Tipo,actor,Now,c.DataObservada,c.Motivo,c.Evidencia,c.HouveConsumoFisico));order.RecordOccurrence(c.VersaoEsperada,Now);Audit(order,"ocorrencias",actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;},ct);
    public Task<ProductionMutationResult> ReverseAsync(Guid id,ReverseCommand c,string key,Guid actor,CancellationToken ct)=>Mutate(id,"reversao",c,key,actor,true,async _=>
    {
        var order=await Order(id,true,ct);order.Require("Confirmada");order.Expect(c.VersaoEsperada);Text(c.Motivo);Text(c.Evidencia,"Evidência da coerência física");if(!c.CoerenciaFisicaConfirmada)throw new ArgumentException("Confirme a coerência física da compensação; perda real não pode ser recriada.");
        var execution=await repository.ExecutionAsync(id,ct)??throw Missing();var movements=await repository.StockMovementsAsync(execution.Id,ct);var outputs=await repository.OutputsAsync(execution.Id,ct);var links=await repository.MovementsAsync(execution.Id,ct);
        await Responsible(actor,ct);
        var lots=new Dictionary<Guid,LoteMaterial>();foreach(var lotId in movements.Select(x=>x.LoteId).Distinct().Order())lots[lotId]=await inventory.LoteAsync(lotId,false,ct)??throw Missing();
        var items=new Dictionary<Guid,Item>();foreach(var itemId in lots.Values.Select(x=>x.ItemId).Distinct().Order())items[itemId]=await inventory.ItemAsync(itemId,true,ct)??throw Missing();
        var locals=new Dictionary<Guid,LocalEstoque>();foreach(var localId in movements.Select(x=>x.LocalId).Distinct().Order())locals[localId]=await inventory.LocalAsync(localId,true,ct)??throw Missing();
        foreach(var lotId in lots.Keys.Order().ToArray())lots[lotId]=await inventory.LoteAsync(lotId,true,ct)??throw Missing();
        var positions=new Dictionary<(Guid,Guid),PosicaoEstoque>();foreach(var pos in movements.Select(x=>(x.LoteId,x.LocalId)).Distinct().OrderBy(x=>x.LoteId).ThenBy(x=>x.LocalId))positions[pos]=await inventory.PositionAsync(pos.LoteId,pos.LocalId,true,ct)??throw Missing();
        // Validate every source and every output before creating the first compensation.
        Conflict(movements.Where(x=>x.TipoEvento=="ProducaoConsumo").Any(x=>lots[x.LoteId].Situacao=="Encerrado"),"fonte_encerrada","Fonte encerrada impede reversão; registre ajuste presente vinculado.");
        foreach(var output in outputs)Conflict(await repository.OutputWasUsedAsync(output,ct)||positions[(output.LoteId,output.LocalId)].Quantidade!=output.Quantidade,"reversao_impedida","Lote de saída sofreu movimento, reserva, alteração ou não permanece íntegro na origem. Use anotação/ajuste presente.");
        var before=InventoryJson.Write(order);
        foreach(var original in movements.OrderBy(x=>x.Sentido=="Entrada"?0:1).ThenBy(x=>x.Id))
        {
            var lot=lots[original.LoteId];var link=links.Single(x=>x.MovimentoEstoqueId==original.Id);
            await Movement(execution.Id,link.LinhaId,link.AlocacaoId,items[lot.ItemId],lot,locals[original.LocalId],positions[(lot.Id,original.LocalId)],original.Quantidade,original.Sentido=="Saida",original.Sentido=="Entrada"?"ReversaoProducaoSaida":"ReversaoProducaoConsumo",Zero(lot.Unidade),null,actor,actor,c.Motivo,null,ct,original);
        }
        foreach(var output in outputs){var lot=lots[output.LoteId];lot.SetState("Encerrado",lot.Revisao,Now,Today);}
        repository.Add(new ReversaoProducao(Guid.NewGuid(),execution.Id,actor,Now,c.Motivo,c.Evidencia));order.Transition("Revertida",c.VersaoEsperada,Now);Audit(order,"reversao",actor,c.Motivo,JsonDocument.Parse(before).RootElement);return order;
    },ct);
    private static ReservationView Reservation(ReservaProducao reserve)
    {
        using var snapshot=JsonDocument.Parse(reserve.SnapshotJson);
        var allocation=InventoryJson.Read<AllocationCommand>(snapshot.RootElement.GetProperty("alocacao").GetRawText());
        return new(reserve.Id,reserve.LinhaId,reserve.LoteId,reserve.LocalId,reserve.Unidade,
            CatalogRules.Format(reserve.QuantidadeOriginal),CatalogRules.Format(reserve.QuantidadeAtiva),
            CatalogRules.Format(reserve.QuantidadeConsumida),CatalogRules.Format(reserve.QuantidadeLiberada),
            allocation.ConversaoItemId,allocation.Quantidade,allocation.Unidade,allocation.AceiteQuantizacao);
    }
    private async Task<OrderView> View(OrdemProducao order,bool admin,CancellationToken ct)
    {
        var reserves=(await repository.ReservationsAsync(order.Id,false,ct)).Select(Reservation).ToArray();
        var execution=await repository.ExecutionAsync(order.Id,ct);var adjustments=new List<EventoView>();if(execution is not null)foreach(var eventId in await repository.AdjustmentEventsAsync(execution.Id,ct))if(await inventory.EventoAsync(eventId,ct)is {} adjustment)adjustments.Add(adjustment);
        var outputViews=new List<SaidaProducaoView>();if(execution is not null)foreach(var output in await repository.OutputsAsync(execution.Id,ct)){var lot=await inventory.LoteAsync(output.LoteId,false,ct)??throw Missing();var local=await inventory.LocalAsync(output.LocalId,false,ct)??throw Missing();outputViews.Add(new(output.Id,output.ExecucaoId,output.LinhaId,output.LoteId,output.LocalId,CatalogRules.Format(output.Quantidade),lot.Unidade,lot.Codigo,local.Codigo,local.Nome));}
        ExecutionView? executionView=execution is null?null:new(execution.Id,order.Id,execution.Resultado,execution.AutorId,execution.ResponsavelId,execution.CreatedAtUtc,execution.DataOperacional,execution.DataObservada,execution.SnapshotJson,await repository.MovementsAsync(execution.Id,ct),outputViews,adjustments);
        var actions=new List<string>{"ocorrencias"};var blocks=new List<string>();
        if(order.Estado=="Rascunho")actions.AddRange(["edicao","planejamento","cancelamento"]);
        if(order.Estado=="Planejada")actions.AddRange(["revisao-planejamento","cancelamento","inicio"]);
        if(order.Estado=="Iniciada"){actions.Add("confirmacao");if(admin)actions.AddRange(["aborto-sem-consumo","confirmacao-ocorrencia"]);blocks.Add("O físico contábil permanece intacto e reservado até a confirmação integral; interrupções não liberam reservas.");}
        if(order.Estado=="Confirmada"&&admin){var impeditives=new List<string>();if(execution is not null){foreach(var output in await repository.OutputsAsync(execution.Id,ct))if(await repository.OutputWasUsedAsync(output,ct))impeditives.Add($"Saída {output.LoteId}: uso/alteração posterior impede reversão.");foreach(var source in (await repository.StockMovementsAsync(execution.Id,ct)).Where(x=>x.TipoEvento=="ProducaoConsumo").Select(x=>x.LoteId).Distinct())if((await inventory.LoteAsync(source,false,ct))?.Situacao=="Encerrado")impeditives.Add($"Fonte {source}: encerrada.");}if(impeditives.Count==0)actions.Add("reversao");blocks.AddRange(impeditives);}
        return new(order.Id,order.Codigo,order.Estado,order.Revisao,order.ReceitaVersaoId,order.Tamanho,order.Unidade,order.ResponsavelId,order.PropriedadeId,order.PropriedadeNome,order.CreatedAtUtc,order.PlanejadaEmUtc,order.IniciadaEmUtc,order.ConfirmadaEmUtc,order.SnapshotPlanejadoJson is null?null:Plan(order),reserves,executionView,actions,blocks);
    }
    public Task<OrderView> GetAsync(Guid id,Guid actor,CancellationToken ct)=>Read(actor,false,async admin=>await View(await Order(id,false,ct),admin,ct),ct);
    public Task<ProductionPage<OrderView>> ListAsync(ProductionQuery q,Guid actor,CancellationToken ct)=>Read(actor,false,async admin=>{var page=await repository.OrdersAsync(q,ct);var views=new List<OrderView>();foreach(var order in page.Items)views.Add(await View(order,admin,ct));return new ProductionPage<OrderView>(views,page.Page,page.PageSize,page.TotalItems,page.TotalPages);},ct);
    public Task<ProductionPage<HistoricoProducao>> HistoryAsync(Guid id,int page,int size,Guid actor,CancellationToken ct)=>Read(actor,false,async _=>{await Order(id,false,ct);return await repository.HistoryAsync(id,page,size,ct);},ct);
    public Task<ProductionPage<OcorrenciaProducao>> OccurrencesAsync(Guid id,int page,int size,Guid actor,CancellationToken ct)=>Read(actor,false,async _=>{await Order(id,false,ct);return await repository.OccurrencesAsync(id,page,size,ct);},ct);
    public Task<ProductionMutationResult> CommandAsync(Guid id,string operation,string key,Guid actor,CancellationToken ct)=>Read(actor,false,async admin=>{var replay=await repository.ReplayAsync(id,operation,key,ct)??throw Missing();if(replay.ExigeAdmin)Admin(admin);return new ProductionMutationResult(replay.StatusHttp,replay.Location,replay.RespostaJson,true);},ct);
    public Task<ProductionPage<GenealogyEdge>> GenealogyAsync(Guid lot,GenealogyQuery q,Guid actor,CancellationToken ct)=>Read(actor,false,async _=>{if(await inventory.LoteAsync(lot,false,ct)is null)throw Missing();return await repository.GenealogyAsync(lot,q,ct);},ct);
    public Task<ProductionPage<ProductionReconciliation>> ReconcileAsync(ProductionQuery q,Guid actor,CancellationToken ct)=>Read(actor,true,_=>repository.ReconcileAsync(q,ct),ct);
}
