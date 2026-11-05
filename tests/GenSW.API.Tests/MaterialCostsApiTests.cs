using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.MaterialCosts;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    private static readonly Guid CostA=Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CostB=Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid CostC=Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static async Task<HttpResponseMessage> CostPost(HttpClient client,string path,object body,string? key=null)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/custos-materiais/"+path){Content=JsonContent.Create(body)};
        request.Headers.Add("Idempotency-Key",key??Unique());return await client.SendAsync(request);
    }
    private static MaterialEvidenceCommand CostEvidence(string total,string document,IReadOnlyList<string>? gaps=null)=>new("Documento sintético atribuível",document,InventoryRules.Today(TimeProvider.System),"BRL",total,["Materiais discriminados"],gaps??[]);
    private async Task<Material> CostStock(HttpClient client,Guid actor,string quantity,string total,string unit="kg",IReadOnlyList<string>? gaps=null)
    {
        var material=await Stock(client,actor,"0",unit);
        var cost=new MaterialReceiptCommand(1,"Conhecido",Evidence,CostEvidence(total,material.Lot.Codigo,gaps));
        await Success<JsonElement>(await Post(client,"entradas",Movement(material,actor,quantity,0) with{CustoMaterial=cost},inventory:true),HttpStatusCode.Created);
        return material;
    }
    private static Task<MaterialLotView?> CostLot(HttpClient client,Guid lot,string? cut=null)=>client.GetFromJsonAsync<MaterialLotView>($"/api/v1/custos-materiais/lotes/{lot}"+(cut is null?"":"?ateSequencia="+cut));
    private static Task<MaterialExecutionView?> CostExecution(HttpClient client,Guid execution)=>client.GetFromJsonAsync<MaterialExecutionView>($"/api/v1/custos-materiais/execucoes/{execution}");
    private async Task<Scenario> CostScenario(HttpClient client,Guid actor,string input,string total,string produced)
    {
        var stock=await CostStock(client,actor,input,total);var output=await Formulation(s=>s.CreateItemAsync(new(Unique(),"Produto sintético F04",null,null,"Alimentar","kg",true,true,true,false),actor,default));
        var destination=await Success<LocalView>(await Post(client,"locais",new LocalCommand(Unique(),"Destino sintético F04"),inventory:true),HttpStatusCode.Created);
        return new(stock,output,destination,await RecipeAsync(actor,stock.Item.Id,output.Id,input,produced));
    }
    private static ConfirmCommand Apportion(ConfirmCommand command,IReadOnlyList<MaterialShareCommand> shares)=>command with
    {CustoMaterial=new(1,false,"Rateio manual explícito e conferido",shares),Perdas=command.Perdas.Select((loss,index)=>loss with{Id=shares.Where(x=>x.Tipo=="Perda").Select(x=>x.DestinoId).ElementAtOrDefault(index) is var id&&id!=Guid.Empty?id:loss.Id??Guid.NewGuid()}).ToArray()};
    [Fact]
    public async Task C01_reference_never_replaces_documented_receipt_or_writes_cash()
    {
        var(client,user)=await Session(true);using(client)
        {
            var material=await Stock(client,user.UserId,"0");var cash=await Factory.ExecuteDbContextAsync(db=>db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM \"LancamentosCaixa\"").SingleAsync());
            var reference=new MaterialReferenceCommand(Guid.NewGuid(),material.Item.Id,"Cotação sintética","opaco-cotacao",InventoryRules.Today(TimeProvider.System),"BRL","kg","1","9",new(2026,1,1),null,"Contexto sintético",Evidence);
            await Success<MaterialReferenceView>(await CostPost(client,"referencias",reference),HttpStatusCode.Created);
            await Success<JsonElement>(await Post(client,"entradas",Movement(material,user.UserId,"10",0) with{CustoMaterial=new(1,"Conhecido",Evidence,CostEvidence("20.00",material.Lot.Codigo))},inventory:true),HttpStatusCode.Created);
            var cost=(await CostLot(client,material.Lot.Id))!;Assert.Equal("20.00",cost.Custo.ValorTotal);Assert.Equal("2.000000",cost.Custo.CustoUnitarioCompleto);Assert.Single(cost.Camadas);
            Assert.Equal(cash,await Factory.ExecuteDbContextAsync(db=>db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM \"LancamentosCaixa\"").SingleAsync()));
        }
    }
    [Fact]
    public async Task C02_unknown_receipt_keeps_null_complete_total_and_read_does_not_create_layers()
    {
        var(client,user)=await Session();using(client)
        {
            var material=await Stock(client,user.UserId,"10");var cost=(await CostLot(client,material.Lot.Id))!;
            Assert.Equal("Desconhecido",cost.Custo.Estado);Assert.Null(cost.Custo.ValorTotal);Assert.Null(cost.Custo.CustoUnitarioCompleto);Assert.NotEmpty(cost.Custo.Lacunas);
            var before=await Factory.ExecuteDbContextAsync(db=>db.Set<CamadaCustoMaterial>().CountAsync());
            _=await CostLot(client,material.Lot.Id);Assert.Equal(before,await Factory.ExecuteDbContextAsync(db=>db.Set<CamadaCustoMaterial>().CountAsync()));
        }
    }
    [Fact]
    public async Task C03_incompatible_receipt_rolls_back_and_lots_of_same_item_keep_distinct_costs()
    {
        var(client,user)=await Session();using(client)
        {
            var one=await CostStock(client,user.UserId,"10","20");
            var twoLot=await Success<LoteView>(await Post(client,"lotes",new LoteCommand(one.Item.Id,Unique(),one.Lot.Origem,one.Lot.Fonte,user.UserId,ItemVersaoEsperada:one.Item.Revisao),inventory:true),HttpStatusCode.Created);
            var two=new Material(one.Item,twoLot,one.Local);
            await Success<JsonElement>(await Post(client,"entradas",Movement(two,user.UserId,"10",0) with{CustoMaterial=new(1,"Conhecido",Evidence,CostEvidence("30",two.Lot.Codigo))},inventory:true),HttpStatusCode.Created);
            var before=(await CostLot(client,one.Lot.Id))!;
            await Error(await Post(client,"entradas",Movement(one,user.UserId,"10",1) with{CustoMaterial=new(1,"Conhecido",Evidence,CostEvidence("30",one.Lot.Codigo))},inventory:true),HttpStatusCode.Conflict);
            Assert.Equal("10",(await Balance(client,one))!.Quantidade);Assert.Equal(before.Custo.ValorTotal,(await CostLot(client,one.Lot.Id))!.Custo.ValorTotal);
            await Success<JsonElement>(await Post(client,"consumos-internos",Movement(one,user.UserId,"1",1),inventory:true),HttpStatusCode.Created);
            await Success<JsonElement>(await Post(client,"consumos-internos",Movement(two,user.UserId,"1",1),inventory:true),HttpStatusCode.Created);
            Assert.Equal("18.00",(await CostLot(client,one.Lot.Id))!.Custo.ValorTotal);Assert.Equal("27.00",(await CostLot(client,two.Lot.Id))!.Custo.ValorTotal);
        }
    }
    [Fact]
    public async Task C04_transfer_and_return_preserve_layer_and_all_twenty_reais()
    {
        var(client,user)=await Session();using(client)
        {
            var material=await CostStock(client,user.UserId,"10","20");var target=await Success<LocalView>(await Post(client,"locais",new LocalCommand(Unique(),"Segundo local sintético F04"),inventory:true),HttpStatusCode.Created);
            var transfer=new InventoryMovementCommand(material.Lot.Id,"3","kg",user.UserId,Evidence,new(material.Item.Revisao,material.Lot.Revisao,OrigemLocal:material.Local.Revisao,DestinoLocal:target.Revisao,OrigemPosicao:1),OrigemLocalId:material.Local.Id,DestinoLocalId:target.Id);
            await Success<JsonElement>(await Post(client,"transferencias",transfer,inventory:true),HttpStatusCode.Created);
            var cost=(await CostLot(client,material.Lot.Id))!;var layer=Assert.Single(cost.Camadas);Assert.Equal("20.00",cost.Custo.ValorTotal);
            Assert.Equal("14.00",layer.Posicoes.Single(x=>x.LocalId==material.Local.Id).Custo.ValorTotal);Assert.Equal("6.00",layer.Posicoes.Single(x=>x.LocalId==target.Id).Custo.ValorTotal);
            transfer=transfer with{OrigemLocalId=target.Id,DestinoLocalId=material.Local.Id,VersoesEsperadas=new(material.Item.Revisao,material.Lot.Revisao,OrigemLocal:target.Revisao,DestinoLocal:material.Local.Revisao,OrigemPosicao:1,DestinoPosicao:2)};
            await Success<JsonElement>(await Post(client,"transferencias",transfer,inventory:true),HttpStatusCode.Created);
            Assert.Equal(layer.Id,Assert.Single((await CostLot(client,material.Lot.Id))!.Camadas).Id);Assert.Equal("20.00",(await CostLot(client,material.Lot.Id))!.Custo.ValorTotal);
        }
    }
    [Fact]
    public async Task C05_three_counted_units_release_cent_only_on_last_withdrawal()
    {
        var(client,user)=await Session();using(client)
        {
            var material=await CostStock(client,user.UserId,"3","0.01","un");
            for(var i=1;i<=3;i++)await Success<JsonElement>(await Post(client,"consumos-internos",Movement(material,user.UserId,"1",i),inventory:true),HttpStatusCode.Created);
            var parts=await Factory.ExecuteDbContextAsync(async db=>await(from p in db.Set<ParcelaCustoMaterial>() join l in db.Set<CamadaCustoMaterial>() on p.CamadaId equals l.Id join e in db.Set<EventoCustoMaterial>() on p.EventoCustoId equals e.Id where l.LoteId==material.Lot.Id&&p.Sentido=="Saida" orderby e.Sequencia select p.ValorConhecido).ToArrayAsync());
            Assert.Equal(new[]{0m,0m,.01m},parts);Assert.Equal("0",(await Balance(client,material))!.Quantidade);
        }
    }
    [Fact]
    public async Task C10_C11_two_output_parcels_and_loss_have_exact_server_allocation()
    {
        var(client,user)=await Session();using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","10.01","5");var order=await Order(client,user.UserId,scenario);
            order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var command=Confirm(order,scenario,user.UserId,"10","5","5");var template=command.Saidas[0];
            command=Apportion(command with{Saidas=[template with{Id=CostA,Quantidade="2"},template with{Id=CostB,Quantidade="3",NovoCodigoLote=Unique()}]},[new(CostA,"Saida","50"),new(CostB,"Saida","30"),new(CostC,"Perda","20")]);
            var preview=await Success<ProductionPreview>(await Post(client,$"{order.Id}/previas",new ProductionPreviewCommand("confirmacao",Confirmacao:command)));
            Assert.NotNull(preview.CustoMaterial);Assert.Equal("5.01",preview.CustoMaterial.Destinos.Single(x=>x.DestinoId==CostA).Custo.ValorTotal);
            command=command with{CustoMaterial=command.CustoMaterial! with{SequenciaEsperada=preview.CustoMaterial.SequenciaEsperada}};
            order=await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",command));Assert.Equal(2,order.Execucao!.LotesSaida.Count);
            var cost=(await CostExecution(client,order.Execucao.Id))!;Assert.Equal("10.01",cost.Custo.ValorTotal);
            Assert.Equal("5.01",cost.Destinos.Single(x=>x.DestinoId==CostA).Custo.ValorTotal);Assert.Equal("3.00",cost.Destinos.Single(x=>x.DestinoId==CostB).Custo.ValorTotal);Assert.Equal("2.00",cost.Destinos.Single(x=>x.DestinoId==CostC).Custo.ValorTotal);
            Assert.Equal("2.505000",cost.Destinos.Single(x=>x.DestinoId==CostA).Custo.CustoUnitarioCompleto);Assert.Null(cost.Destinos.Single(x=>x.Tipo=="Perda").Custo.CustoUnitarioCompleto);
            Assert.Equal("0",(await Balance(client,scenario.Input))!.Quantidade);
        }
    }
    [Theory]
    [InlineData("99.999999")][InlineData("100.000001")][InlineData("-1")][InlineData("100.0000001")]
    public async Task C15_invalid_shares_leave_physical_monetary_and_reservations_unchanged(string percentage)
    {
        var(client,user)=await Session();using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","20","10");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var command=Confirm(order,scenario,user.UserId,"10","10","0");command=command with{CustoMaterial=new(1,false,Evidence,[new(command.Saidas[0].Id,"Saida",percentage)])};
            var before=(await CostLot(client,scenario.Input.Lot.Id))!;
            await Error(await Post(client,$"{order.Id}/confirmacao",command),HttpStatusCode.BadRequest);
            Assert.Equal("Iniciada",(await Get(client,order.Id))!.Estado);Assert.Equal("10",(await Balance(client,scenario.Input))!.Quantidade);Assert.Equal(before.Custo.ValorTotal,(await CostLot(client,scenario.Input.Lot.Id))!.Custo.ValorTotal);
            Assert.Empty(await Factory.ExecuteDbContextAsync(async db=>
            {var execution=await db.Set<GenSW.Domain.Production.ExecucaoProducao>().Where(e=>e.OrdemId==order.Id).Select(e=>e.Id).FirstOrDefaultAsync();return await db.Set<ApuracaoCustoExecucao>().Where(x=>x.ExecucaoId==execution).ToArrayAsync();}));
        }
    }
    [Fact]
    public async Task C21_present_evaluation_changes_only_identified_remaining_quantity()
    {
        var(client,user)=await Session(true);using(client)
        {
            var material=await Stock(client,user.UserId,"10");await Success<JsonElement>(await Post(client,"consumos-internos",Movement(material,user.UserId,"2",1),inventory:true),HttpStatusCode.Created);
            var before=(await CostLot(client,material.Lot.Id))!;
            await Success<MaterialHistoryView>(await CostPost(client,"avaliacoes",new MaterialEvaluationCommand(Guid.NewGuid(),material.Lot.Id,material.Local.Id,"8",before.Custo.Revisao,Evidence,CostEvidence("16",material.Lot.Codigo))),HttpStatusCode.Created);
            var after=(await CostLot(client,material.Lot.Id))!;Assert.Equal("16.00",after.Custo.ValorTotal);Assert.Equal("8",(await Balance(client,material))!.Quantidade);
            Assert.Equal("Desconhecido",(await CostLot(client,material.Lot.Id,before.CorteMonetario))!.Custo.Estado);
        }
    }
    [Fact]
    public async Task C25_pending_allocation_is_explicit_and_present_resolution_preserves_original()
    {
        var(client,user)=await Session(true);using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","20","10");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var command=Confirm(order,scenario,user.UserId,"10","10","0");order=await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",command));
            var pending=(await CostExecution(client,order.Execucao!.Id))!;Assert.Equal("PendenteRateio",pending.Estado);Assert.Null(pending.Custo.ValorTotal);Assert.Equal("20.00",pending.Custo.ValorConhecido);
            var output=order.Execucao.LotesSaida.Single();Assert.Null((await CostLot(client,output.LoteId))!.Custo.ValorTotal);
            var body=new MaterialResolutionCommand(Guid.NewGuid(),1,new(1,false,Evidence,[new(output.Id,"Saida","100")]),Evidence,Evidence);
            var preview=await Success<MaterialProductionPreview>(await CostPost(client,$"execucoes/{order.Execucao.Id}/previas-resolucao",body));
            body=body with{CustoMaterial=body.CustoMaterial with{SequenciaEsperada=preview.SequenciaEsperada}};
            await Success<JsonElement>(await CostPost(client,$"execucoes/{order.Execucao.Id}/resolucoes",body),HttpStatusCode.Created);
            var current=(await CostExecution(client,order.Execucao.Id))!;Assert.Equal("20.00",current.Custo.ValorTotal);Assert.Equal(pending.Id,current.ApuracaoOriginalId);Assert.Equal("20.00",(await CostLot(client,output.LoteId))!.Custo.ValorTotal);
            var original=await Factory.ExecuteDbContextAsync(db=>db.Set<ApuracaoCustoExecucao>().AsNoTracking().SingleAsync(x=>x.Id==pending.Id));Assert.Equal("PendenteRateio",original.Estado);Assert.Null(original.ValorTotal);
        }
    }
}
