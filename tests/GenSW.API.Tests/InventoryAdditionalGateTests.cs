using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class InventoryAdditionalGateTests(AnimalApiPostgreSqlFixture fixture)
{
    private AuthWebApplicationFactory Factory => fixture.Factory;
    private async Task<(HttpClient Client, Guid Actor)> Session()
    {
        var user=await Factory.SeedUserAsync("stock_gate_"+Guid.NewGuid().ToString("N"),roles:["Admin"]);
        var client=Factory.CreateHttpsClient(); using var login=await client.LoginAsync(user.UserName,user.Password);
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",(await login.ReadAccessTokenAsync()).AccessToken);
        return (client,user.UserId);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client,string path,object body)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/estoque/"+path) { Content=JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("N")); return await client.SendAsync(request);
    }
    private static async Task<T> Created<T>(Task<HttpResponseMessage> request)
    {
        using var response=await request; Assert.True(response.StatusCode==HttpStatusCode.Created,await response.Content.ReadAsStringAsync());
        return InventoryJson.Read<T>(await response.Content.ReadAsStringAsync());
    }
    private static async Task<(CatalogItem Item, ItemData Data)> Item(HttpClient client)
    {
        var data=new ItemData(Guid.NewGuid().ToString("N"),"Material sintético de gates",null,null,"Alimentar","kg",true,true,true,false);
        using var response=await client.PostAsJsonAsync("/api/v1/itens",data); Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<CatalogItem>())!,data);
    }
    private static LoteCommand Lot(CatalogItem item,Guid actor,string? code=null) => new(item.Id,code??Guid.NewGuid().ToString("N"),"Origem sintética","Fixture PostgreSQL isolada",actor,ItemVersaoEsperada:item.Revisao);
    private static InventoryMovementCommand Movement(CatalogItem item,LoteView lot,LocalView local,Guid actor,int position=0) =>
        new(lot.Id,"2","kg",actor,"Conferência sintética de gates",new(item.Revisao,lot.Revisao,local.Revisao,position),LocalId:local.Id,
            DataObservada:new(2026,1,1),Origem:lot.Origem,Fonte:lot.Fonte,Evidencia:"Contagem sintética, sem material real");
    private static async Task Code(HttpResponseMessage response,string expected)
    {
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode); using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expected,body.RootElement.GetProperty("code").GetString());
    }
    private async Task<HttpResponseMessage[]> Race(Func<Task<HttpResponseMessage>> catalogue,Func<Task<HttpResponseMessage>> physical)
    {
        var locked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder=Factory.ExecuteDbContextAsync(async db =>
        {
            await using var tx=await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413,421)");
            locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync();
        });
        Task<HttpResponseMessage>? one=null,two=null;
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10)); one=catalogue(); two=physical();
            var deadline=DateTimeOffset.UtcNow.AddSeconds(4); var bothBlocked=false;
            while (DateTimeOffset.UtcNow<deadline)
            {
                var count=await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND query LIKE '%413%421%'").SingleAsync());
                if (count>=2) { bothBlocked=true; break; }
                if (one.IsCompleted||two.IsCompleted) break; await Task.Delay(20);
            }
            Assert.True(bothBlocked,"Both real PostgreSQL writers must wait on the shared catalogue lock.");
        }
        finally { release.TrySetResult(); await holder; }
        return await Task.WhenAll(one!,two!).WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Codes_are_case_insensitive_and_opening_cannot_repeat_or_follow_an_ordinary_receipt()
    {
        var (client,actor)=await Session(); using (client)
        {
            var (item,_)=await Item(client); var localCode="Local-"+Guid.NewGuid().ToString("N");
            var a=await Created<LocalView>(Post(client,"locais",new LocalCommand(localCode,"Local sintético")));
            using var duplicateLocal=await Post(client,"locais",new LocalCommand(localCode.ToUpperInvariant(),"Duplicado sintético")); await Code(duplicateLocal,"registro_duplicado");
            var b=await Created<LocalView>(Post(client,"locais",new LocalCommand(Guid.NewGuid().ToString("N"),"Segundo local sintético")));
            var lotCode="Lote-"+Guid.NewGuid().ToString("N"); var lot=await Created<LoteView>(Post(client,"lotes",Lot(item,actor,lotCode)));
            using var duplicateLot=await Post(client,"lotes",Lot(item,actor,lotCode.ToUpperInvariant())); await Code(duplicateLot,"registro_duplicado");
            await Created<MovementReply>(Post(client,"aberturas",Movement(item,lot,a,actor)));
            using var repeated=await Post(client,"aberturas",Movement(item,lot,a,actor,1)); await Code(repeated,"posicao_ja_movimentada");
            await Created<MovementReply>(Post(client,"entradas",Movement(item,lot,b,actor)));
            using var late=await Post(client,"aberturas",Movement(item,lot,b,actor,1)); await Code(late,"posicao_ja_movimentada");
            Assert.Equal(1,await Factory.ExecuteDbContextAsync(db=>db.LocaisEstoque.CountAsync(x=>x.CodigoNormalizado==localCode.ToUpperInvariant())));
            Assert.Equal(1,await Factory.ExecuteDbContextAsync(db=>db.LotesMateriais.CountAsync(x=>x.CodigoNormalizado==lotCode.ToUpperInvariant())));
            var events=await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={lot.Id}");
            Assert.Equal(2,events!.TotalItems); Assert.Single(events.Items,x=>x.Tipo=="Abertura");
        }
    }

    [Fact]
    public async Task Entry_capability_change_and_receipt_serialize_and_current_capability_is_revalidated()
    {
        var (client,actor)=await Session(); using (client)
        {
            var (item,data)=await Item(client); var local=await Created<LocalView>(Post(client,"locais",new LocalCommand(Guid.NewGuid().ToString("N"),"Local sintético")));
            var lot=await Created<LoteView>(Post(client,"lotes",Lot(item,actor)));
            var responses=await Race(()=>client.PutAsJsonAsync($"/api/v1/itens/{item.Id}",new ItemCommand(data with { PodeEntrar=false },item.Revisao)),()=>Post(client,"entradas",Movement(item,lot,local,actor)));
            Assert.Equal(HttpStatusCode.OK,responses[0].StatusCode); Assert.Contains(responses[1].StatusCode,new[] { HttpStatusCode.Created,HttpStatusCode.Conflict });
            var received=responses[1].StatusCode==HttpStatusCode.Created; foreach (var response in responses) response.Dispose();
            var current=(await client.GetFromJsonAsync<CatalogItem>($"/api/v1/itens/{item.Id}"))!; Assert.False(current.PodeEntrar);
            var balance=(await client.GetFromJsonAsync<SaldoView>($"/api/v1/estoque/saldos/{lot.Id}/{local.Id}"))!; Assert.Equal(received?"2":"0",balance.Quantidade);
            using var after=await Post(client,"entradas",Movement(current,lot,local,actor,balance.Revisao)); await Code(after,"referencia_inativa");
            Assert.Equal(received?1:0,await Factory.ExecuteDbContextAsync(db=>db.MovimentosEstoque.CountAsync(x=>x.LoteId==lot.Id)));
        }
    }

    [Fact]
    public async Task Unit_change_and_first_physical_lot_serialize_without_mixed_units()
    {
        var (client,actor)=await Session(); using (client)
        {
            var (item,data)=await Item(client);
            var responses=await Race(()=>client.PutAsJsonAsync($"/api/v1/itens/{item.Id}",new ItemCommand(data with { Unidade="L" },item.Revisao)),()=>Post(client,"lotes",Lot(item,actor)));
            var changed=responses[0].StatusCode==HttpStatusCode.OK;
            Assert.Equal(changed?HttpStatusCode.Conflict:HttpStatusCode.Created,responses[1].StatusCode);
            if (!changed) await Code(responses[0],"unidade_fixada");
            foreach (var response in responses) response.Dispose();
            var current=(await client.GetFromJsonAsync<CatalogItem>($"/api/v1/itens/{item.Id}"))!;
            Assert.Equal(changed?"L":"kg",current.Unidade); Assert.Equal(!changed,current.UnidadeFixada);
            var lots=await client.GetFromJsonAsync<InventoryPage<LoteView>>($"/api/v1/estoque/lotes?itemId={item.Id}");
            Assert.Equal(changed?0:1,lots!.TotalItems); Assert.All(lots.Items,x=>Assert.Equal(current.Unidade,x.Unidade));
        }
    }

    [Fact]
    public async Task Profile_inactivation_and_new_physical_reference_serialize_and_future_binding_is_denied()
    {
        var (client,actor)=await Session(); using (client)
        {
            var (item,_)=await Item(client);
            var content=new ProfileData("Perfil sintético de gates","Fixture","Sintético","Fixture","Fixture","Amostra sintética",null,null,null,null,
                [new NutritionValue("PB","Conhecido","100","Medido","BN","g/kg","Sintético","Fixture")]);
            using var draftResponse=await client.PostAsJsonAsync($"/api/v1/itens/{item.Id}/perfis-nutricionais",content); Assert.Equal(HttpStatusCode.Created,draftResponse.StatusCode);
            var draft=(await draftResponse.Content.ReadFromJsonAsync<ProfileView>())!;
            using var publishedResponse=await client.PostAsJsonAsync($"/api/v1/perfis-nutricionais/{draft.Id}/publicacao",new VersionCommand(draft.Revisao)); Assert.Equal(HttpStatusCode.OK,publishedResponse.StatusCode);
            var profile=(await publishedResponse.Content.ReadFromJsonAsync<ProfileView>())!;
            var command=Lot(item,actor) with { PerfilNutricionalId=profile.Id,Aplicabilidade="Mesma amostra sintética" };
            var responses=await Race(()=>client.PostAsJsonAsync($"/api/v1/perfis-nutricionais/{profile.Id}/inativacao",new VersionCommand(profile.Revisao)),()=>Post(client,"lotes",command));
            Assert.Equal(HttpStatusCode.OK,responses[0].StatusCode); Assert.Contains(responses[1].StatusCode,new[] { HttpStatusCode.Created,HttpStatusCode.Conflict });
            var bound=responses[1].StatusCode==HttpStatusCode.Created;
            if (!bound) await Code(responses[1],"perfil_inativo"); foreach (var response in responses) response.Dispose();
            Assert.Equal("Inativo",(await client.GetFromJsonAsync<ProfileView>($"/api/v1/perfis-nutricionais/{profile.Id}"))!.Estado);
            var lots=await client.GetFromJsonAsync<InventoryPage<LoteView>>($"/api/v1/estoque/lotes?itemId={item.Id}");
            Assert.Equal(bound?1:0,lots!.TotalItems); Assert.All(lots.Items,x=>Assert.Equal(profile.Id,x.PerfilNutricionalId));
            using var later=await Post(client,"lotes",command with { Codigo=Guid.NewGuid().ToString("N") }); await Code(later,"perfil_inativo");
        }
    }

    private sealed record CatalogItem(Guid Id,int Revisao,bool UnidadeFixada,string Unidade,bool PodeEntrar);
    private sealed record MovementReply(EventoView Evento,InventoryPreview Previa);
}
