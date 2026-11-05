using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.EggBridge;
using GenSW.Application.MaterialCosts;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;
public sealed partial class EggBridgeApiTests
{
    private static MaterialReceiptCommand DocumentedEggs(string total)=>new(1,"Conhecido",Evidence,new("Comprovante sintético","opaco-ovos",InventoryRules.Today(TimeProvider.System),"BRL",total,["Material ovos"],[]));
    private static Task<MaterialLotView?> EggCost(HttpClient client,Guid lot)=>client.GetFromJsonAsync<MaterialLotView>($"/api/v1/custos-materiais/lotes/{lot}");

    [Fact]
    public async Task C02_individually_identified_eggs_without_monetary_evidence_keep_unknown_cost()
    {
        var (client, user) = await Session(true);
        using var session = client;
        var material = await Stock(client, user.UserId);
        var eggs = await Eggs(2);
        var scope = await Scope(client, material, user.UserId, eggs);
        var preview = await Preview(client, Entry(material, scope.Id, user.UserId, eggs));
        Assert.Equal("Desconhecido", preview.CustoMaterial!.Custo.Estado);
        Assert.Null(preview.CustoMaterial.Custo.ValorTotal);

        await Confirm(client, preview);
        var lot = (await EggCost(client, material.Lot.Id))!;
        Assert.Equal("2", (await Balance(client, material))!.Quantidade);
        Assert.Equal("Desconhecido", lot.Custo.Estado);
        Assert.Null(lot.Custo.ValorTotal);
        Assert.Null(lot.Custo.CustoUnitarioCompleto);
        Assert.Null(Assert.Single(lot.Custo.Componentes).ValorConhecido);
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task C17_association_has_zero_monetary_delta_for_known_or_unknown_original_receipt(bool known)
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await Stock(client,user.UserId);var command=Movement(m,user.UserId,"5",0);if(known)command=command with{CustoMaterial=DocumentedEggs("10")};
            await Success<JsonElement>(await Post(client,"entradas",command,inventory:true));var eggs=await Eggs(2);var movement=await Factory.ExecuteDbContextAsync(db=>db.MovimentosEstoque.SingleAsync(x=>x.LoteId==m.Lot.Id));var scope=await Scope(client,m,user.UserId,eggs,"RecebidoComEquivalencia");var proof=await Success<ProvaEntradaOvos>(await Post(client,"provas",new EggProofCommand(movement.Id,"5",user.UserId,Evidence,Evidence)));
            var before=(await EggCost(client,m.Lot.Id))!;var monetaryEvents=await Factory.ExecuteDbContextAsync(db=>db.Set<EventoCustoMaterial>().CountAsync());var preview=await Preview(client,Association(scope.Id,user.UserId,proof.Id,eggs,1));Assert.Null(preview.CustoMaterial);await Confirm(client,preview);
            var after=(await EggCost(client,m.Lot.Id))!;Assert.Equal(InventoryJson.Write(before.Custo),InventoryJson.Write(after.Custo));Assert.Equal(before.Camadas.Select(x=>x.Id),after.Camadas.Select(x=>x.Id));Assert.Equal(monetaryEvents,await Factory.ExecuteDbContextAsync(db=>db.Set<EventoCustoMaterial>().CountAsync()));Assert.Equal("5",(await Balance(client,m))!.Quantidade);
        }
    }
    [Fact]
    public async Task C18_compensation_uses_exact_original_cost_once_and_documentary_invalidation_writes_no_cost()
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await Stock(client,user.UserId);var eggs=await Eggs(3);var scope=await Scope(client,m,user.UserId,eggs);var preview=await Preview(client,Entry(m,scope.Id,user.UserId,eggs) with{CustoMaterial=DocumentedEggs("0.01")});Assert.Equal("0.01",preview.CustoMaterial!.Custo.ValorTotal);
            var group=await Confirm(client,preview);var before=(await EggCost(client,m.Lot.Id))!;Assert.Equal("0.01",before.Custo.ValorTotal);
            var treatment=await Success<EggTreatmentPreview>(await Post(client,$"grupos/{group.Grupo.Id}/previas-tratamento",new EggTreatmentCommand("Compensacao",user.UserId,Evidence,Evidence,true)));Assert.True(treatment.PodeConfirmar);var key=Unique();await Success<EggBridgeDetail>(await Post(client,$"grupos/{group.Grupo.Id}/tratamentos",new EggConfirmCommand(treatment.Token),key));await Success<EggBridgeDetail>(await Post(client,$"grupos/{group.Grupo.Id}/tratamentos",new EggConfirmCommand(treatment.Token),key));
            Assert.Equal("0",(await Balance(client,m))!.Quantidade);Assert.Equal("0.00",(await EggCost(client,m.Lot.Id))!.Custo.ValorConhecido);var original=await Factory.ExecuteDbContextAsync(db=>db.Set<ParcelaCustoMaterial>().SingleAsync(x=>x.MovimentoEstoqueId==group.Grupo.MovimentoEstoqueId));var compensation=await Factory.ExecuteDbContextAsync(db=>db.Set<ParcelaCustoMaterial>().SingleAsync(x=>x.ParcelaOriginalId==original.Id));Assert.Equal(original.ValorConhecido,compensation.ValorConhecido);Assert.Equal(original.ComponentesJson,compensation.ComponentesJson);
            var second=await Stock(client,user.UserId,"1");var secondEgg=await Eggs(1);var secondScope=await Scope(client,second,user.UserId,secondEgg,"RecebidoComEquivalencia");var secondMovement=await Factory.ExecuteDbContextAsync(db=>db.MovimentosEstoque.SingleAsync(x=>x.LoteId==second.Lot.Id));var secondProof=await Success<ProvaEntradaOvos>(await Post(client,"provas",new EggProofCommand(secondMovement.Id,"1",user.UserId,Evidence,Evidence)));var secondGroup=await Confirm(client,await Preview(client,Association(secondScope.Id,user.UserId,secondProof.Id,secondEgg,1)));var events=await Factory.ExecuteDbContextAsync(db=>db.Set<EventoCustoMaterial>().CountAsync());var invalidation=await Success<EggTreatmentPreview>(await Post(client,$"grupos/{secondGroup.Grupo.Id}/previas-tratamento",new EggTreatmentCommand("Invalidacao",user.UserId,Evidence,Evidence)));await Success<EggBridgeDetail>(await Post(client,$"grupos/{secondGroup.Grupo.Id}/tratamentos",new EggConfirmCommand(invalidation.Token)));Assert.Equal(events,await Factory.ExecuteDbContextAsync(db=>db.Set<EventoCustoMaterial>().CountAsync()));Assert.Equal("1",(await Balance(client,second))!.Quantidade);Assert.Null((await EggCost(client,second.Lot.Id))!.Custo.ValorTotal);
        }
    }
}
