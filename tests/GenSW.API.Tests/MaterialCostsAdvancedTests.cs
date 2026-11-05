using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.MaterialCosts;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    private static (Guid A,Guid B,Guid C) CostIds() { var prefix=Guid.NewGuid().ToString("D")[..24];return(Guid.Parse(prefix+"000000000001"),Guid.Parse(prefix+"000000000002"),Guid.Parse(prefix+"000000000003")); }
    private async Task<OrderView> CostConfirmed(HttpClient client,Guid actor,Scenario scenario,string consumed,string produced,string loss="0")
    {
        var order=await Order(client,actor,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,actor)));
        var c=Confirm(order,scenario,actor,consumed,produced,loss);
        c=Apportion(c,loss=="0"?[new(c.Saidas[0].Id,"Saida","100")]:[new(c.Saidas[0].Id,"Saida","80"),new(CostC,"Perda","20")]);
        return await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",c));
    }
    [Theory]
    [InlineData("0.01","50","50","0","0.01","0.00","0.00")]
    [InlineData("0.01","33.333333","33.333333","33.333334","0.00","0.00","0.01")]
    [InlineData("0.02","50","25","25","0.01","0.01","0.00")]
    public async Task C06_C08_largest_remainder_uses_stable_destination_ids_in_postgresql(string total,string a,string b,string c,string expectedA,string expectedB,string expectedC)
    {
        var(CostA,CostB,CostC)=CostIds();
        var(client,user)=await Session();using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"3",total,"3");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var command=Confirm(order,scenario,user.UserId,"3","3","0");var template=command.Saidas[0];
            command=Apportion(command with{Saidas=[template with{Id=CostC,Quantidade="1"},template with{Id=CostB,Quantidade="1",NovoCodigoLote=Unique()},template with{Id=CostA,Quantidade="1",NovoCodigoLote=Unique()}]},[new(CostC,"Saida",c),new(CostB,"Saida",b),new(CostA,"Saida",a)]);
            var one=await Success<ProductionPreview>(await Post(client,$"{order.Id}/previas",new ProductionPreviewCommand("confirmacao",Confirmacao:command)));
            var reordered=command with{Saidas=command.Saidas.Reverse().ToArray(),CustoMaterial=command.CustoMaterial! with{Percentuais=command.CustoMaterial.Percentuais.Reverse().ToArray()}};
            var two=await Success<ProductionPreview>(await Post(client,$"{order.Id}/previas",new ProductionPreviewCommand("confirmacao",Confirmacao:reordered)));
            Assert.Equal(one.CustoMaterial!.Destinos.OrderBy(x=>x.DestinoId).Select(x=>x.Custo.ValorTotal),two.CustoMaterial!.Destinos.OrderBy(x=>x.DestinoId).Select(x=>x.Custo.ValorTotal));
            order=await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",reordered));var result=(await CostExecution(client,order.Execucao!.Id))!;
            Assert.Equal(expectedA,result.Destinos.Single(x=>x.DestinoId==CostA).Custo.ValorTotal);Assert.Equal(expectedB,result.Destinos.Single(x=>x.DestinoId==CostB).Custo.ValorTotal);Assert.Equal(expectedC,result.Destinos.Single(x=>x.DestinoId==CostC).Custo.ValorTotal);
        }
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task C09_C12_components_are_allocated_individually_and_unknown_coverage_is_inherited(bool known)
    {
        var(CostA,CostB,CostC)=CostIds();
        var(client,user)=await Session();using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"2",known?"0.01":"4","3");var other=known?await CostStock(client,user.UserId,"1","0.01"):await Stock(client,user.UserId,"1");
            var data=scenario.Version.Conteudo with{Entradas=[scenario.Version.Conteudo.Entradas[0],new(Guid.NewGuid(),other.Item.Id,"Alimentar","1","kg","Variavel")]};scenario=scenario with{Version=await RecipeAsync(user.UserId,scenario.Input.Item.Id,scenario.Output.Id,data:data)};
            var order=await Order(client,user.UserId,scenario);var start=Start(order,scenario,user.UserId);start=start with{Entradas=[start.Entradas[0],new(data.Entradas[1].Id,[new(Guid.NewGuid(),other.Lot.Id,other.Local.Id,"1","kg")])]};order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",start));
            var command=Confirm(order,scenario,user.UserId,"2","3","0");var output=command.Saidas[0];command=command with{Entradas=[command.Entradas[0] with{Alocacoes=[command.Entradas[0].Alocacoes[0] with{Id=order.Reservas.Single(r=>r.LoteId==scenario.Input.Lot.Id).Id}]},new(data.Entradas[1].Id,[new(order.Reservas.Single(r=>r.LoteId==other.Lot.Id).Id,other.Lot.Id,other.Local.Id,"1","kg")])],Saidas=[output with{Id=CostB,Quantidade="1"},output with{Id=CostA,Quantidade="2",NovoCodigoLote=Unique()}]};command=Apportion(command,[new(CostA,"Saida","50"),new(CostB,"Saida","50")]);
            order=await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",command));var result=(await CostExecution(client,order.Execucao!.Id))!;var a=result.Destinos.Single(x=>x.DestinoId==CostA).Custo;var b=result.Destinos.Single(x=>x.DestinoId==CostB).Custo;
            Assert.Equal(known?"0.02":"2.00",a.ValorConhecido);Assert.Equal(known?"0.00":"2.00",b.ValorConhecido);Assert.Equal(2,a.Componentes.Count);Assert.Equal(2,b.Componentes.Count);
            if(!known){Assert.Equal("Parcial",a.Estado);Assert.Null(a.ValorTotal);Assert.Null(b.CustoUnitarioCompleto);Assert.Contains(a.Componentes,x=>x.ValorConhecido is null&&x.Quantidade=="1");Assert.NotEmpty(a.Lacunas);}
        }
    }
    [Fact]
    public async Task C13_total_loss_has_one_explicit_destination_no_output_lot_and_no_unit_cost()
    {
        var(CostA,CostB,CostC)=CostIds();
        var(client,user)=await Session(true);using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","4","10");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var c=Confirm(order,scenario,user.UserId,"10","0","10");c=Apportion(c,[new(c.Saidas[0].Id,"Saida","0"),new(CostC,"Perda","100")]);
            order=await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",c));Assert.Empty(order.Execucao!.LotesSaida);var result=(await CostExecution(client,order.Execucao.Id))!;var loss=Assert.Single(result.Destinos,x=>x.Tipo=="Perda");Assert.Equal("4.00",loss.Custo.ValorConhecido);Assert.Null(loss.Custo.CustoUnitarioCompleto);Assert.Null(result.Destinos.Single(x=>x.Tipo=="Saida").LoteId);
        }
    }
    [Fact]
    public async Task C14_zero_output_cannot_receive_positive_share_and_does_not_create_a_lot()
    {
        var(CostA,CostB,CostC)=CostIds();
        var(client,user)=await Session();using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","4","10");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var c=Confirm(order,scenario,user.UserId,"10","10","0");var o=c.Saidas[0];c=Apportion(c with{Saidas=[o with{Id=CostA,Quantidade="0"},o with{Id=CostB,NovoCodigoLote=Unique()}]},[new(CostA,"Saida","10"),new(CostB,"Saida","90")]);
            await Error(await Post(client,$"{order.Id}/confirmacao",c),HttpStatusCode.BadRequest);Assert.Equal("10",(await Balance(client,scenario.Input))!.Quantidade);
            c=c with{CustoMaterial=c.CustoMaterial! with{Percentuais=[new(CostA,"Saida","0"),new(CostB,"Saida","100")]}};order=await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",c));Assert.Single(order.Execucao!.LotesSaida);var result=(await CostExecution(client,order.Execucao.Id))!;Assert.Null(result.Destinos.Single(x=>x.DestinoId==CostA).Custo.CustoUnitarioCompleto);Assert.Equal("4.00",result.Destinos.Single(x=>x.DestinoId==CostB).Custo.ValorTotal);
        }
    }
    [Theory]
    [InlineData("USD","0.00")][InlineData("BRL","0.001")][InlineData("BRL","")]
    public async Task C16_currency_excess_precision_and_blank_amount_fail_without_physical_effect(string currency,string amount)
    {
        var(client,user)=await Session();using(client)
        {var m=await Stock(client,user.UserId,"0");var c=Movement(m,user.UserId,"1",0) with{CustoMaterial=new(1,"Conhecido",Evidence,CostEvidence(amount,m.Lot.Codigo) with{Moeda=currency})};await Error(await Post(client,"entradas",c,inventory:true),HttpStatusCode.BadRequest);Assert.Equal("0",(await Balance(client,m))!.Quantidade);Assert.Empty((await CostLot(client,m.Lot.Id))!.Camadas);}
    }
    [Fact]
    public async Task C16_documented_zero_is_complete_and_distinct_from_unknown()
    {
        var(client,user)=await Session();using(client){var m=await CostStock(client,user.UserId,"1","0");var result=(await CostLot(client,m.Lot.Id))!;Assert.Equal("Completo",result.Custo.Estado);Assert.Equal("0.00",result.Custo.ValorTotal);Assert.NotNull(result.Camadas.Single().EvidenciaId);}
    }
    [Fact]
    public async Task C19_reversal_restores_original_cents_once_and_preserves_frozen_calculation()
    {
        var(client,user)=await Session(true);using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","10.01","8");var order=await CostConfirmed(client,user.UserId,scenario,"10","8","2");var before=(await CostExecution(client,order.Execucao!.Id))!;var reverse=new ReverseCommand(order.Revisao,true,Evidence,Evidence);var key=Unique();
            var first=await Post(client,$"{order.Id}/reversao",reverse,key);var bytes=await first.Content.ReadAsStringAsync();await Success<OrderView>(first);Assert.Equal("10.01",(await CostLot(client,scenario.Input.Lot.Id))!.Custo.ValorTotal);Assert.Equal("10",(await Balance(client,scenario.Input))!.Quantidade);
            var after=(await CostExecution(client,order.Execucao.Id))!;Assert.Equal("Compensada",after.Estado);Assert.Equal(before.SnapshotJson,after.SnapshotJson);Assert.Equal(before.Custo.ValorTotal,after.Custo.ValorTotal);using var replay=await Post(client,$"{order.Id}/reversao",reverse,key);Assert.Equal(bytes,await replay.Content.ReadAsStringAsync());await Error(await Post(client,$"{order.Id}/reversao",reverse with{VersaoEsperada=order.Revisao+1}),HttpStatusCode.Conflict);
        }
    }
    [Fact]
    public async Task C22_late_correction_of_used_source_cannot_reprice_output_or_reverse_incompatibly()
    {
        var(client,user)=await Session(true);using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","20","8");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var c=Confirm(order,scenario,user.UserId,"8","8","0");c=Apportion(c,[new(c.Saidas[0].Id,"Saida","100")]);order=await Success<OrderView>(await Post(client,$"{order.Id}/confirmacao",c));var output=Assert.Single(order.Execucao!.LotesSaida);var frozen=(await CostExecution(client,order.Execucao.Id))!;var source=(await CostLot(client,scenario.Input.Lot.Id))!;
            await Success<MaterialHistoryView>(await CostPost(client,"avaliacoes",new MaterialEvaluationCommand(Guid.NewGuid(),source.LoteId,scenario.Input.Local.Id,"2",source.Custo.Revisao,Evidence,CostEvidence("6","novo-documento"))),HttpStatusCode.Created);
            Assert.Equal("16.00",(await CostLot(client,output.LoteId))!.Custo.ValorTotal);Assert.Equal(frozen.SnapshotJson,(await CostExecution(client,order.Execucao.Id))!.SnapshotJson);await Error(await Post(client,$"{order.Id}/reversao",new ReverseCommand(order.Revisao,true,Evidence,Evidence)),HttpStatusCode.Conflict);Assert.Equal("2",(await Balance(client,scenario.Input))!.Quantidade);
        }
    }
    [Fact]
    public async Task C23_intermediate_inherits_component_once_instead_of_adding_ancestor_cost_again()
    {
        var(client,user)=await Session();using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"3","3","3");var first=await CostConfirmed(client,user.UserId,scenario,"3","3");var output=Assert.Single(first.Execucao!.LotesSaida);
            var lot=(await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{output.LoteId}"))!;var middle=new Material(scenario.Output,lot,scenario.Destination);var result=await Formulation(s=>s.CreateItemAsync(new(Unique(),"Segundo produto F04",null,null,"Alimentar","kg",true,true,true,false),user.UserId,default));var secondScenario=new Scenario(middle,result,scenario.Destination,await RecipeAsync(user.UserId,middle.Item.Id,result.Id,"3","3"));
            var second=await CostConfirmed(client,user.UserId,secondScenario,"3","3");var cost=(await CostExecution(client,second.Execucao!.Id))!;Assert.Equal("3.00",cost.Custo.ValorTotal);Assert.Single(cost.Custo.Componentes);Assert.Equal("3.00",(await CostLot(client,second.Execucao.LotesSaida.Single().LoteId))!.Custo.ValorTotal);
        }
    }
    [Fact]
    public async Task C26_failure_after_physical_write_rolls_back_and_exact_retry_is_durable_across_author()
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await CostStock(client,user.UserId,"10","20");var before=(await CostLot(client,m.Lot.Id))!;var command=new MaterialEvaluationCommand(Guid.NewGuid(),m.Lot.Id,m.Local.Id,"10",before.Custo.Revisao,Evidence,CostEvidence("30","reavaliacao"));var key=Unique();
            await Factory.ExecuteDbContextAsync(db=>db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION "F04TestRejectCommand"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic failure before durable reply'; END $$;
                CREATE TRIGGER "F04TestRejectCommand" BEFORE INSERT ON "ComandosCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "F04TestRejectCommand"();
                """));
            try {await Error(await CostPost(client,"avaliacoes",command,key),HttpStatusCode.InternalServerError);Assert.Equal(before.Custo.ValorTotal,(await CostLot(client,m.Lot.Id))!.Custo.ValorTotal);Assert.Equal(before.Custo.Revisao,(await CostLot(client,m.Lot.Id))!.Custo.Revisao);}
            finally {await Factory.ExecuteDbContextAsync(db=>db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"F04TestRejectCommand\" ON \"ComandosCustoMateriais\"; DROP FUNCTION \"F04TestRejectCommand\"();"));}
            using var first=await CostPost(client,"avaliacoes",command,key);Assert.Equal(HttpStatusCode.Created,first.StatusCode);var bytes=await first.Content.ReadAsStringAsync();var(other,_)=await Session(true);using(other){using var replay=await CostPost(other,"avaliacoes",command,key);Assert.Equal(bytes,await replay.Content.ReadAsStringAsync());using var lookup=await other.GetAsync($"/api/v1/custos-materiais/comandos/{command.Id}/avaliacao/{key}");Assert.Equal(bytes,await lookup.Content.ReadAsStringAsync());}
            await Error(await CostPost(client,"avaliacoes",command with{Motivo="outro"},key),HttpStatusCode.Conflict);Assert.Equal("30.00",(await CostLot(client,m.Lot.Id))!.Custo.ValorTotal);
        }
    }
    [Fact]
    public async Task C27_two_evaluators_at_same_revision_have_exactly_one_winner_behind_barrier()
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await CostStock(client,user.UserId,"10","20");var cost=(await CostLot(client,m.Lot.Id))!;var one=new MaterialEvaluationCommand(Guid.NewGuid(),m.Lot.Id,m.Local.Id,"10",cost.Custo.Revisao,Evidence,CostEvidence("30","um"));var two=one with{Id=Guid.NewGuid(),Evidencia=CostEvidence("40","dois")};
            var responses=await Race(()=>CostPost(client,"avaliacoes",one),()=>CostPost(client,"avaliacoes",two));Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);foreach(var response in responses)response.Dispose();var final=(await CostLot(client,m.Lot.Id))!;Assert.Contains(final.Custo.ValorTotal,new[]{"30.00","40.00"});Assert.Equal("10",(await Balance(client,m))!.Quantidade);
        }
    }
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)][InlineData(IsolationLevel.RepeatableRead)]
    public async Task C28_direct_sql_cannot_mutate_facts_or_projection_under_either_isolation(IsolationLevel isolation)
    {
        var(client,user)=await Session();using(client)
        {
            var m=await CostStock(client,user.UserId,"3","0.01","un");var before=(await CostLot(client,m.Lot.Id))!;var layer=before.Camadas.Single();
            foreach(var sql in new[]{"UPDATE \"CamadasCustoMateriais\" SET \"ValorConhecido\"=9 WHERE \"Id\"=@id","DELETE FROM \"CamadasCustoMateriais\" WHERE \"Id\"=@id","UPDATE \"PosicoesCustoMateriais\" SET \"ValorConhecido\"=9,\"Revisao\"=\"Revisao\"+1 WHERE \"CamadaId\"=@id","UPDATE \"PosicoesCustoMateriais\" SET \"ComponentesJson\"=jsonb_set(\"ComponentesJson\",'{0,fracaoNumerador}','\"0\"'),\"Revisao\"=\"Revisao\"+1 WHERE \"CamadaId\"=@id"})
            {
                var connectionString=await Factory.ExecuteDbContextAsync(db=>Task.FromResult(db.Database.GetConnectionString()!));await using var db=new NpgsqlConnection(connectionString);await db.OpenAsync();await using var tx=await db.BeginTransactionAsync(isolation);
                var ex=await Assert.ThrowsAsync<PostgresException>(async()=>{await using var cmd=new NpgsqlCommand(sql,db,tx);cmd.Parameters.AddWithValue("id",layer.Id);await cmd.ExecuteNonQueryAsync();await tx.CommitAsync();});Assert.Equal("23514",ex.SqlState);
            }
            Assert.Equal(before.Custo.ValorTotal,(await CostLot(client,m.Lot.Id))!.Custo.ValorTotal);
        }
    }
    [Fact]
    public async Task C31_admin_revoked_before_replay_is_denied_and_inactive_account_cannot_read()
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await Stock(client,user.UserId,"0");var command=new MaterialReferenceCommand(Guid.NewGuid(),m.Item.Id,"fonte","doc",InventoryRules.Today(TimeProvider.System),"BRL","kg","1","9",new(2026,1,1),null,"contexto",Evidence);var key=Unique();await Success<MaterialReferenceView>(await CostPost(client,"referencias",command,key),HttpStatusCode.Created);
            await Factory.ExecuteScopeAsync(async s=>{var users=s.GetRequiredService<UserManager<ApplicationUser>>();var current=(await users.FindByIdAsync(user.UserId.ToString()))!;Assert.True((await users.RemoveFromRoleAsync(current,"Admin")).Succeeded);return 0;});await Error(await CostPost(client,"referencias",command,key),HttpStatusCode.Forbidden);using var recovery=await client.GetAsync($"/api/v1/custos-materiais/comandos/{command.Id}/referencia/{key}");Assert.Equal(HttpStatusCode.Forbidden,recovery.StatusCode);
            await Factory.SetUserActiveAsync(user.UserId,false);using var read=await client.GetAsync($"/api/v1/custos-materiais/lotes/{m.Lot.Id}");Assert.Equal(HttpStatusCode.Unauthorized,read.StatusCode);
        }
    }
    [Fact]
    public async Task C32_reference_supersession_never_changes_an_existing_estimate_or_proven_stock()
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await CostStock(client,user.UserId,"10","20");var reference=new MaterialReferenceCommand(Guid.NewGuid(),m.Item.Id,"fonte","doc",InventoryRules.Today(TimeProvider.System),"BRL","kg","1","9",new(2026,1,1),null,"contexto",Evidence);await Success<MaterialReferenceView>(await CostPost(client,"referencias",reference),HttpStatusCode.Created);
            var estimate=new MaterialEstimateCommand(Guid.NewGuid(),Evidence,[new(Guid.NewGuid(),m.Item.Id,"10","kg",reference.Id)]);using var first=await CostPost(client,"estimativas",estimate);Assert.Equal(HttpStatusCode.Created,first.StatusCode);var bytes=await first.Content.ReadAsStringAsync();var replacement=reference with{Id=Guid.NewGuid(),ValorUnitario="11"};await Success<MaterialReferenceView>(await CostPost(client,$"referencias/{reference.Id}/substituicoes",replacement),HttpStatusCode.Created);
            using var old=await client.GetAsync($"/api/v1/custos-materiais/estimativas/{estimate.Id}");Assert.Equal(bytes,await old.Content.ReadAsStringAsync());var chain=(await client.GetFromJsonAsync<MaterialCostPage<MaterialReferenceView>>($"/api/v1/custos-materiais/referencias/{replacement.Id}/historico"))!;Assert.Equal(2,chain.TotalItems);Assert.Equal("20.00",(await CostLot(client,m.Lot.Id))!.Custo.ValorTotal);
            var absent=new MaterialEstimateCommand(Guid.NewGuid(),Evidence,[new(Guid.NewGuid(),m.Item.Id,"1","L")]);using var missing=await CostPost(client,"estimativas",absent);Assert.Equal(HttpStatusCode.Created,missing.StatusCode);using var json=JsonDocument.Parse(await missing.Content.ReadAsStringAsync());Assert.Equal(JsonValueKind.Null,json.RootElement.GetProperty("valorEstimadoTotal").ValueKind);Assert.NotEmpty(json.RootElement.GetProperty("lacunas").EnumerateArray());
        }
    }
    [Fact]
    public async Task C33_maximum_amount_and_canonical_grams_remain_exact_in_postgresql()
    {
        var(client,user)=await Session();using(client)
        {
            var m=await Stock(client,user.UserId,"0");var c=Movement(m,user.UserId,"1000",0) with{Unidade="g",CustoMaterial=new(1,"Conhecido",Evidence,CostEvidence("9999999999999999.99",m.Lot.Codigo))};
            // A complete unit cost exceeding numeric(20,6) is explicitly rejected; no float overflow or partial physical write.
            await Error(await Post(client,"entradas",c,inventory:true),HttpStatusCode.BadRequest);Assert.Equal("0",(await Balance(client,m))!.Quantidade);
            c=c with{Quantidade="100000",CustoMaterial=c.CustoMaterial! with{Evidencia=c.CustoMaterial.Evidencia! with{TotalDocumentado="9999999999999999.99"}}};await Success<JsonElement>(await Post(client,"entradas",c,inventory:true),HttpStatusCode.Created);Assert.Equal("100",(await Balance(client,m))!.Quantidade);Assert.Equal("9999999999999999.99",(await CostLot(client,m.Lot.Id))!.Custo.ValorTotal);
        }
    }
}
