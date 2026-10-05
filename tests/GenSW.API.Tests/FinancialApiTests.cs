using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Domain.Animals;
using GenSW.Domain.Financial;
using GenSW.Domain.Species;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed class FinancialApiTests
{
    [Fact]
    public async Task Category_details_require_authentication_include_inactive_and_execute_with_database_read_only()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync();
        await using var setup = new AuthWebApplicationFactory(pg.ConnectionString);
        await setup.InitializeAsync();
        await setup.SeedUserAsync("category-reader");
        using var loginClient = setup.CreateHttpsClient();
        using var login = await loginClient.LoginAsync("category-reader", AuthWebApplicationFactory.ValidPassword);
        var token = (await login.ReadAccessTokenAsync()).AccessToken;
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var active = new CategoriaFinanceira(Guid.NewGuid(), "Categoria ativa", "CATEGORIA ATIVA", NaturezaFinanceira.Receita, null, true, 1, now, now);
        var inactive = active with { Id = Guid.NewGuid(), Nome = "Categoria inativa", NomeNormalizado = "CATEGORIA INATIVA", Ativa = false, Versao = 2 };
        await setup.ExecuteDbContextAsync(async db =>
        {
            db.AddRange(active, inactive);
            await db.SaveChangesAsync();
        });

        // Every connection serving these GETs is read-only. An accidental database
        // mutation fails the request, including writes outside the category table.
        await using var factory = new AuthWebApplicationFactory(pg.ConnectionString + ";Options=-c default_transaction_read_only=on");
        using var anonymous = factory.CreateHttpsClient();
        using var reader = factory.CreateHttpsClient();
        reader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        const string root = "/api/v1/financeiro/categorias/";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(root + active.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(root + Guid.NewGuid())).StatusCode);
        using var missing = await reader.GetAsync(root + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("nao_encontrado", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        foreach (var expected in new[] { active, inactive })
        {
            using var response = await reader.GetAsync(root + expected.Id);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var item = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(expected.Id, item.GetProperty("id").GetGuid());
            Assert.Equal(expected.Nome, item.GetProperty("nome").GetString());
            Assert.Equal((int)expected.Natureza, item.GetProperty("natureza").GetInt32());
            Assert.Equal(expected.Ativa, item.GetProperty("ativa").GetBoolean());
            Assert.Equal(expected.Versao, item.GetProperty("versao").GetInt32());
            Assert.False(item.TryGetProperty("nomeNormalizado", out _));
        }
        await setup.ExecuteDbContextAsync(async db =>
        {
            Assert.Equal(active, await db.Set<CategoriaFinanceira>().AsNoTracking().SingleAsync(x => x.Id == active.Id));
            Assert.Equal(inactive, await db.Set<CategoriaFinanceira>().AsNoTracking().SingleAsync(x => x.Id == inactive.Id));
        });
    }

    [Fact]
    public async Task Jwt_admin_decimal_pagination_conflicts_and_animal_no_side_effects()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync();
        await using var factory = new AuthWebApplicationFactory(pg.ConnectionString); await factory.InitializeAsync();
        await factory.SeedUserAsync("finance-admin", roles:["Admin"]); await factory.SeedUserAsync("finance-user");
        using var anonymous = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); using var user = factory.CreateHttpsClient();
        async Task Login(HttpClient client, string name) { using var r=await client.LoginAsync(name,AuthWebApplicationFactory.ValidPassword); client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",(await r.ReadAccessTokenAsync()).AccessToken); }
        await Login(admin,"finance-admin"); await Login(user,"finance-user");
        const string root="/api/v1/financeiro";
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(root+"/categorias")).StatusCode);
        Assert.Equal("null",await admin.GetStringAsync(root+"/configuracao"));
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync(root+"/configuracao",new{dataInicio="2026-08-01"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync(root+"/configuracao",new{saldoInicial="0.00"})).StatusCode);
        var config=new{dataInicio="2026-08-01",saldoInicial="100.00"};
        Assert.Equal(HttpStatusCode.Forbidden,(await user.PostAsJsonAsync(root+"/configuracao",config)).StatusCode);
        var configured=await admin.PostAsJsonAsync(root+"/configuracao",config); Assert.True(configured.StatusCode==HttpStatusCode.OK,await configured.Content.ReadAsStringAsync());
        Guid animalId=default;
        await factory.ExecuteDbContextAsync(async db=>{var now=DateTimeOffset.UtcNow; var species=Especie.Criar("Financeiro",null,now); var animal=Animal.Criar("FIN-API","Animal vendido",species.Id,null,null,SexoAnimal.Macho,null,EscopoAnimal.Operacional,new(2026,8,1),now);db.AddRange(species,animal);await db.SaveChangesAsync();animalId=animal.Id;});
        var category=Guid.Parse("37600000-0000-0000-0000-000000000006");
        object Entry(string value="250.00")=>new{tipo=1,dataMovimento="2026-08-01",valor=value,descricao="Venda recebida",categoriaId=category,formaPagamento=2,animalId};
        user.DefaultRequestHeaders.Add("Idempotency-Key","finance-http");
        var created=await user.PostAsJsonAsync(root+"/lancamentos",Entry()); Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var entry=await created.Content.ReadFromJsonAsync<JsonElement>(); var id=entry.GetProperty("id").GetGuid(); Assert.Equal(JsonValueKind.String,entry.GetProperty("valor").ValueKind);
        var repeat=await user.PostAsJsonAsync(root+"/lancamentos",Entry()); Assert.Equal(id,(await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var conflict=await user.PostAsJsonAsync(root+"/lancamentos",Entry("300.00")); Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode); Assert.Equal("conflito_caixa",(await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        user.DefaultRequestHeaders.Remove("Idempotency-Key");
        Assert.Equal(HttpStatusCode.BadRequest,(await user.PostAsJsonAsync(root+"/lancamentos",Entry())).StatusCode);
        user.DefaultRequestHeaders.Add("Idempotency-Key","invalid"); Assert.Equal(HttpStatusCode.BadRequest,(await user.PostAsJsonAsync(root+"/lancamentos",Entry("1.001"))).StatusCode);
        var list=await user.GetFromJsonAsync<JsonElement>(root+"/lancamentos?ano=2026&mes=8&pageSize=1"); Assert.Equal(1,list.GetProperty("items").GetArrayLength()); Assert.Equal(1,list.GetProperty("totalItems").GetInt32());
        var defaultList=await user.GetFromJsonAsync<JsonElement>(root+"/lancamentos"); Assert.Equal(25,defaultList.GetProperty("pageSize").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest,(await user.GetAsync(root+"/lancamentos?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await user.GetAsync(root+"/lancamentos/"+Guid.NewGuid())).StatusCode);
        var summary=await user.GetFromJsonAsync<JsonElement>(root+"/meses/2026/8"); var closing=new{versaoEsperada=summary.GetProperty("versao").GetInt32(),observacao="Conferido"};
        Assert.Equal(HttpStatusCode.Forbidden,(await user.PostAsJsonAsync(root+"/meses/2026/8/fechamento",closing)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.PostAsJsonAsync(root+"/meses/2026/8/fechamento",closing)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await user.PostAsJsonAsync(root+$"/lancamentos/{id}/cancelamento",new{versaoEsperada=1,motivo="Incorreto"})).StatusCode);
        user.DefaultRequestHeaders.Remove("Idempotency-Key"); user.DefaultRequestHeaders.Add("Idempotency-Key","annotation");
        Assert.Equal(HttpStatusCode.OK,(await user.PostAsJsonAsync(root+$"/lancamentos/{id}/ajuste",new{versaoEsperada=1,motivo="Descrição corrigida por anotação",somenteAnotacao=true})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await admin.PostAsJsonAsync(root+"/meses/2026/8/reabertura",new{})).StatusCode);
        await factory.ExecuteDbContextAsync(async db=>{var animal=await db.Animais.FindAsync(animalId);Assert.True(animal!.Ativo);Assert.Equal("Animal vendido",animal.Nome);});
    }
}
