using System.Data;
using System.Net;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.MaterialCosts;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;
public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task C27_transfer_racing_consumption_and_incompatible_receipt_racing_confirmation_conserve_cents()
    {
        var(client,user)=await Session();using(client)
        {
            var m=await CostStock(client,user.UserId,"10","20");var destination=await Success<LocalView>(await Post(client,"locais",new LocalCommand(Unique(),"Destino da disputa F04"),inventory:true),HttpStatusCode.Created);
            var transfer=new InventoryMovementCommand(m.Lot.Id,"7","kg",user.UserId,Evidence,new(m.Item.Revisao,m.Lot.Revisao,OrigemLocal:m.Local.Revisao,DestinoLocal:destination.Revisao,OrigemPosicao:1),OrigemLocalId:m.Local.Id,DestinoLocalId:destination.Id);
            var results=await Race(()=>Post(client,"transferencias",transfer,inventory:true),()=>Post(client,"consumos-internos",Movement(m,user.UserId,"7",1),inventory:true));Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);Assert.Equal(results[0].StatusCode==HttpStatusCode.Created?"20.00":"6.00",(await CostLot(client,m.Lot.Id))!.Custo.ValorTotal);foreach(var r in results)r.Dispose();
            var scenario=await CostScenario(client,user.UserId,"10","20","10");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));var confirmation=Confirm(order,scenario,user.UserId,"10","10","0");confirmation=Apportion(confirmation,[new(confirmation.Saidas[0].Id,"Saida","100")]);
            var receipt=Movement(scenario.Input,user.UserId,"10",1) with{CustoMaterial=new(1,"Conhecido",Evidence,CostEvidence("30","documento-incompativel"))};results=await Race(()=>Post(client,"entradas",receipt,inventory:true),()=>Post(client,$"{order.Id}/confirmacao",confirmation));Assert.Equal(HttpStatusCode.Conflict,results[0].StatusCode);var confirmed=await Success<OrderView>(results[1]);results[0].Dispose();Assert.Equal("20.00",(await CostExecution(client,confirmed.Execucao!.Id))!.Custo.ValorTotal);Assert.Equal("0",(await Balance(client,scenario.Input))!.Quantidade);
        }
    }
    [Fact]
    public async Task C26_failure_before_monetary_snapshot_rolls_back_physical_debits_outputs_and_reservations()
    {
        var(client,user)=await Session();using(client)
        {
            var scenario=await CostScenario(client,user.UserId,"10","20","10");var order=await Order(client,user.UserId,scenario);order=await Success<OrderView>(await Post(client,$"{order.Id}/inicio",Start(order,scenario,user.UserId)));
            var c=Confirm(order,scenario,user.UserId,"10","10","0");c=Apportion(c,[new(c.Saidas[0].Id,"Saida","100")]);var key=Unique();var before=await Snapshot();var money=(await CostLot(client,scenario.Input.Lot.Id))!;
            await Factory.ExecuteDbContextAsync(db=>db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION "F04TestRejectCalculation"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic monetary snapshot fault'; END $$;
                CREATE TRIGGER "F04TestRejectCalculation" BEFORE INSERT ON "ApuracoesCustoExecucoes" FOR EACH ROW EXECUTE FUNCTION "F04TestRejectCalculation"();
                """));
            try{await Error(await Post(client,$"{order.Id}/confirmacao",c,key),HttpStatusCode.InternalServerError);Assert.Equal(before,await Snapshot());Assert.Equal(money.Custo.ValorTotal,(await CostLot(client,scenario.Input.Lot.Id))!.Custo.ValorTotal);Assert.Equal(money.Custo.Revisao,(await CostLot(client,scenario.Input.Lot.Id))!.Custo.Revisao);Assert.Equal("Iniciada",(await Get(client,order.Id))!.Estado);}
            finally{await Factory.ExecuteDbContextAsync(db=>db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"F04TestRejectCalculation\" ON \"ApuracoesCustoExecucoes\"; DROP FUNCTION \"F04TestRejectCalculation\"();"));}
            using var committed=await Post(client,$"{order.Id}/confirmacao",c,key);Assert.Equal(HttpStatusCode.OK,committed.StatusCode);var bytes=await committed.Content.ReadAsStringAsync();using var retry=await Post(client,$"{order.Id}/confirmacao",c,key);Assert.Equal(bytes,await retry.Content.ReadAsStringAsync());Assert.Equal("0",(await Balance(client,scenario.Input))!.Quantidade);
        }
    }
    [Fact]
    public async Task C27_evaluation_racing_consumption_has_only_a_valid_serial_result()
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await CostStock(client,user.UserId,"10","20");var cost=(await CostLot(client,m.Lot.Id))!;var evaluation=new MaterialEvaluationCommand(Guid.NewGuid(),m.Lot.Id,m.Local.Id,"10",cost.Custo.Revisao,Evidence,CostEvidence("30","novo"));
            var results=await Race(()=>CostPost(client,"avaliacoes",evaluation),()=>Post(client,"consumos-internos",Movement(m,user.UserId,"1",1),inventory:true));
            Assert.Equal(HttpStatusCode.Created,results[1].StatusCode);Assert.Contains(results[0].StatusCode,new[]{HttpStatusCode.Created,HttpStatusCode.Conflict});var final=(await CostLot(client,m.Lot.Id))!;Assert.Equal(results[0].StatusCode==HttpStatusCode.Created?"27.00":"18.00",final.Custo.ValorTotal);Assert.Equal("9",(await Balance(client,m))!.Quantidade);foreach(var result in results)result.Dispose();
        }
    }
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)][InlineData(IsolationLevel.RepeatableRead)]
    public async Task C27_C30_stale_noncooperating_projection_writer_cannot_overwrite_new_cost(IsolationLevel isolation)
    {
        var(client,user)=await Session(true);using(client)
        {
            var m=await CostStock(client,user.UserId,"10","20");var before=(await CostLot(client,m.Lot.Id))!;var layer=before.Camadas.Single();var connectionString=await Factory.ExecuteDbContextAsync(db=>Task.FromResult(db.Database.GetConnectionString()!));
            await using var db=new NpgsqlConnection(connectionString);await db.OpenAsync();await using var tx=await db.BeginTransactionAsync(isolation);
            await using(var snapshot=new NpgsqlCommand("SELECT \"ValorConhecido\" FROM \"PosicoesCustoMateriais\" WHERE \"CamadaId\"=@id",db,tx)){snapshot.Parameters.AddWithValue("id",layer.Id);Assert.Equal(20m,await snapshot.ExecuteScalarAsync());}
            await Success<MaterialHistoryView>(await CostPost(client,"avaliacoes",new MaterialEvaluationCommand(Guid.NewGuid(),m.Lot.Id,m.Local.Id,"10",before.Custo.Revisao,Evidence,CostEvidence("30","atual"))),HttpStatusCode.Created);
            var failure=await Assert.ThrowsAsync<PostgresException>(async()=>{await using var stale=new NpgsqlCommand("UPDATE \"PosicoesCustoMateriais\" SET \"ValorConhecido\"=20,\"Revisao\"=2 WHERE \"CamadaId\"=@id",db,tx);stale.Parameters.AddWithValue("id",layer.Id);await stale.ExecuteNonQueryAsync();await tx.CommitAsync();});Assert.Contains(failure.SqlState,new[]{"23514","40001"});Assert.Equal("30.00",(await CostLot(client,m.Lot.Id))!.Custo.ValorTotal);
        }
    }
    [Fact]
    public async Task C34_three_counted_output_units_keep_authoritative_cent_and_round_unit_at_six_decimals()
    {
        var(client,user)=await Session();using(client)
        {
            var input=await CostStock(client,user.UserId,"3","0.01","un");var output=await Formulation(s=>s.CreateItemAsync(new(Unique(),"Produto contado sintético",null,null,"Alimentar","un",true,true,true,false),user.UserId,default));var scenario=new Scenario(input,output,input.Local,await RecipeAsync(user.UserId,input.Item.Id,output.Id,"3","3","un"));var order=await CostConfirmed(client,user.UserId,scenario,"3","3");var cost=(await CostExecution(client,order.Execucao!.Id))!;var destination=Assert.Single(cost.Destinos);Assert.Equal("0.01",destination.Custo.ValorTotal);Assert.Equal("0.003333",destination.Custo.CustoUnitarioCompleto);Assert.True(destination.UnitarioArredondado);Assert.NotNull(destination.ResiduoNumerador);Assert.Equal("0.01",(await CostLot(client,order.Execucao.LotesSaida.Single().LoteId))!.Custo.ValorTotal);
        }
    }
}
