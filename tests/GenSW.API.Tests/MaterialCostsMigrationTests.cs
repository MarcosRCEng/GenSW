using System.Text.Json;
using GenSW.Application.Authentication;
using GenSW.Application.EggBridge;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.MaterialCosts;
using GenSW.Application.Production;
using GenSW.Domain.Catalog;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;
public sealed class MaterialCostsMigrationTests
{
    private const string Prior="20261029000136_AddEggBridge";
    private const string Current="20261103233403_AddMaterialCosts";
    private static readonly string[] Tables=["PrecosReferenciaMateriais","EvidenciasCustoMateriais","CamadasCustoMateriais","EventosCustoMateriais","ParcelasCustoMateriais","PosicoesCustoMateriais","ApuracoesCustoExecucoes","DestinosCustoExecucoes","ComandosCustoMateriais","TravaLivroCustoMaterial"];
    private static GenSWDbContext Db(EphemeralPostgreSql pg)=>new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options);
    private static string Serialize(object value)=>JsonSerializer.Serialize(value);
    private static string Key()=>Guid.NewGuid().ToString("N");
    [Fact]
    public async Task C24_C25_C29_upgrade_preserves_populated_F01_F02_F03_cash_and_replays_dump_restore_and_legacy_recognition()
    {
        await using var pg=await EphemeralPostgreSql.StartAsync();await using var factory=new AuthWebApplicationFactory(pg.ConnectionString);await factory.InitializeAsync();
        await factory.ExecuteDbContextAsync(async db=>{await db.GetService<IMigrator>().MigrateAsync(Prior);await FormulationMigrationTests.SeedPreviousAsync(db);return 0;});
        var actor=(await factory.SeedUserAsync("f04-upgrade",roles:["Admin"])).UserId;
        var legacy=await factory.ExecuteScopeAsync(async services=>
        {
            var clock=services.GetRequiredService<TimeProvider>();var authentication=services.GetRequiredService<IAuthenticationSessionService>();var inventoryRepo=services.GetRequiredService<IInventoryRepository>();
            // Explicitly exercise the previously authorized physical services without the optional F04 hook, on the 20-migration schema.
            var inventory=new InventoryService(inventoryRepo,authentication,clock);var production=new ProductionService(services.GetRequiredService<IProductionRepository>(),inventoryRepo,services.GetRequiredService<IFormulationRepository>(),authentication,clock);
            var bridge=new EggBridgeService(services.GetRequiredService<IEggBridgeRepository>(),inventoryRepo,authentication,services.GetRequiredService<IEggBridgePreviewProtector>(),clock);var catalog=services.GetRequiredService<FormulationService>();
            var item=await catalog.CreateItemAsync(new("F04-UPGRADE-IN","Entrada sintética anterior a F04",null,null,"Alimentar","un",true,true,true,false),actor,default);
            var output=await catalog.CreateItemAsync(new("F04-UPGRADE-OUT","Saída sintética anterior a F04",null,null,"Alimentar","un",true,true,true,false),actor,default);
            var local=InventoryJson.Read<LocalView>((await inventory.CreateLocalAsync(new("F04-UPGRADE-LOCAL","Local sintético de preservação"),Key(),actor,default)).RespostaJson);
            var lot=InventoryJson.Read<LoteView>((await inventory.CreateLoteAsync(new(item.Id,"F04-UPGRADE-LOT","Fixture","Preservação",actor,ItemVersaoEsperada:item.Revisao),Key(),actor,default)).RespostaJson);
            var receipt=new InventoryMovementCommand(lot.Id,"5","un",actor,"Recebimento anterior",new(item.Revisao,lot.Revisao,local.Revisao,0),LocalId:local.Id,DataObservada:InventoryRules.Today(clock),Origem:"Fixture",Fonte:"Preservação");var receiptKey=Key();var receiptResult=await inventory.MoveAsync("entrada",receipt,receiptKey,actor,default);
            var recipe=await catalog.CreateRecipeAsync(new("F04-UPGRADE-RECIPE","Transformação anterior","Preservação"),actor,default);var inId=Guid.NewGuid();var outId=Guid.NewGuid();
            var version=await catalog.CreateVersionAsync(recipe.Id,new("Processamento","Quantidade","2","un",[new(inId,item.Id,"Alimentar","2","un","Variavel")],[new(outId,output.Id,"Saída","2","un","Variavel",true)],[],[],"Fixture",[]),actor,default);version=await catalog.VersionStateAsync(version.Id,version.Revisao,true,actor,default);
            var order=InventoryJson.Read<OrderView>((await production.CreateAsync(new(Guid.NewGuid(),"F04-UPGRADE-ORDER",version.Id,"2","un",actor),Key(),actor,default)).RespostaJson);
            order=InventoryJson.Read<OrderView>((await production.PlanAsync(order.Id,new(order.Revisao,"Plano anterior"),Key(),actor,default)).RespostaJson);
            var allocation=Guid.NewGuid();order=InventoryJson.Read<OrderView>((await production.StartAsync(order.Id,new(order.Revisao,actor,[new(inId,[new(allocation,lot.Id,local.Id,"2","un")])],"Reserva anterior"),Key(),actor,default)).RespostaJson);
            var confirm=new ConfirmCommand(order.Revisao,actor,[new(inId,[new(allocation,lot.Id,local.Id,"2","un")])],[new(Guid.NewGuid(),outId,"2","un","F04-UPGRADE-PRODUCT",local.Id)],[],"Confirmação anterior");var confirmKey=Key();var physical=await production.ConfirmAsync(order.Id,confirm,confirmKey,actor,false,default);
            // Associate one real synthetic egg identity with its already-recorded receipt. No duplicate stock credit.
            var eggs=await services.GetRequiredService<GenSWDbContext>().ProducoesOvos.AsNoTracking().Select(x=>x.Id).ToArrayAsync();await bridge.EnrollAsync(new(item.Id,"Fixture"),Key(),actor,default);
            var approved=await bridge.ApproveScopeAsync(new(item.Id,"Acervo sintético","Fixture integral","Nenhuma",actor,"Preservação",[new(eggs.Single(),"RecebidoComEquivalencia",true,"Contagem sintética")]),Key(),actor,default);
            using var scopeJson=JsonDocument.Parse(approved.RespostaJson);var scope=scopeJson.RootElement.GetProperty("recorte").GetProperty("id").GetGuid();
            using var receiptJson=JsonDocument.Parse(receiptResult.RespostaJson);var movement=receiptJson.RootElement.GetProperty("evento").GetProperty("movimentos")[0].GetProperty("id").GetGuid();
            var proven=await bridge.ApproveProofAsync(new(movement,"5",actor,"Fixture","Prova sintética"),Key(),actor,default);using var proofJson=JsonDocument.Parse(proven.RespostaJson);var proof=proofJson.RootElement.GetProperty("id").GetGuid();
            var preview=await bridge.PreviewAsync(new("Associacao",scope,actor,"Preservação","Correspondência individual",[new(eggs.Single(),"1")],ProvaId:proof),actor,default);Assert.True(preview.PodeConfirmar);services.GetRequiredService<GenSWDbContext>().ChangeTracker.Clear();await bridge.ConfirmAsync(new(preview.Token),Key(),actor,default);
            return new{item,lot,local,receipt,receiptKey,receiptResult,order,confirm,confirmKey,physical};
        });
        SortedDictionary<string,string[]> before=await factory.ExecuteDbContextAsync(db=>FormulationMigrationTests.RowsAsync(db));
        foreach(var name in new[]{"EventosEstoque","MovimentosEstoque","ComandosEstoque","OrdensProducao","ExecucoesProducao","ComandosProducao","GruposPonteOvos","PontesProducaoOvos","ComandosPonteOvos","LancamentosCaixa"})Assert.NotEmpty(before[name]);
        var baselineBackup=Path.Combine(Path.GetTempPath(),"gensw-f04-baseline-"+Key()+".dump");var upgradedBackup=Path.Combine(Path.GetTempPath(),"gensw-f04-upgraded-"+Key()+".dump");
        try
        {
            await pg.BackupAsync(baselineBackup);await using var copied=await EphemeralPostgreSql.StartAsync();await copied.RestoreAsync(baselineBackup);await using var db=Db(copied);Assert.Equal(20,(await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(Serialize(before),Serialize(await FormulationMigrationTests.RowsAsync(db)));
            var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"GenSW.sln")))root=root.Parent;Assert.NotNull(root);
            var sql=await File.ReadAllTextAsync(Path.Combine(root!.FullName,"docs","validation","evidence","517","material-costs-idempotent.sql"));await db.Database.OpenConnectionAsync();await using(var upgrade=db.Database.GetDbConnection().CreateCommand()){upgrade.CommandText=sql;await upgrade.ExecuteNonQueryAsync();await upgrade.ExecuteNonQueryAsync();}
            Assert.Equal(Serialize(before),Serialize(await FormulationMigrationTests.RowsAsync(db,before.Keys)));var after=await FormulationMigrationTests.RowsAsync(db);Assert.Equal(Tables.Order(),after.Keys.Except(before.Keys).Order());foreach(var table in Tables)Assert.Empty(after[table]);Assert.False(db.Database.HasPendingModelChanges());Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            await db.Database.MigrateAsync();Assert.Equal(Serialize(after),Serialize(await FormulationMigrationTests.RowsAsync(db)));await copied.BackupAsync(upgradedBackup);
            await using var recovered=await EphemeralPostgreSql.StartAsync();await recovered.RestoreAsync(upgradedBackup);await using var restored=Db(recovered);Assert.Equal(Serialize(after),Serialize(await FormulationMigrationTests.RowsAsync(restored)));Assert.Equal((await db.Database.GetAppliedMigrationsAsync()).ToArray(),(await restored.Database.GetAppliedMigrationsAsync()).ToArray());
            // Upgrade original only after the preservation comparison; use current DI hooks for new movements.
            await factory.ExecuteDbContextAsync(async context=>{await context.GetService<IMigrator>().MigrateAsync(Current);return 0;});
            var replay=await factory.ExecuteScopeAsync(s=>s.GetRequiredService<InventoryService>().MoveAsync("entrada",legacy.receipt,legacy.receiptKey,actor,default));Assert.True(replay.Replayed);Assert.Equal(legacy.receiptResult.RespostaJson,replay.RespostaJson);
            var productionReplay=await factory.ExecuteScopeAsync(s=>s.GetRequiredService<ProductionService>().ConfirmAsync(legacy.order.Id,legacy.confirm,legacy.confirmKey,actor,false,default));Assert.True(productionReplay.Replayed);Assert.Equal(legacy.physical.RespostaJson,productionReplay.RespostaJson);
            var unknown=await factory.ExecuteScopeAsync(s=>s.GetRequiredService<MaterialCostService>().LotAsync(legacy.lot.Id,new(),actor,default));Assert.Empty(unknown.Camadas);Assert.Null(unknown.Custo.ValorTotal);
            var currentLot=await factory.ExecuteScopeAsync(s=>s.GetRequiredService<InventoryService>().GetLoteAsync(legacy.lot.Id,actor,default));var currentBalance=await factory.ExecuteScopeAsync(s=>s.GetRequiredService<InventoryService>().GetSaldoAsync(legacy.lot.Id,legacy.local.Id,actor,default));
            var destination = InventoryJson.Read<LocalView>((await factory.ExecuteScopeAsync(s =>
                s.GetRequiredService<InventoryService>().CreateLocalAsync(new("F04-UPGRADE-DESTINO", "Destino sintético"), Key(), actor, default))).RespostaJson);
            var transfer = new InventoryMovementCommand(legacy.lot.Id, "1", "un", actor, "Primeira transferência presente",
                new(currentBalance.ItemRevisao, currentLot.Revisao, OrigemLocal: currentBalance.LocalRevisao,
                    DestinoLocal: destination.Revisao, OrigemPosicao: currentBalance.Revisao),
                OrigemLocalId: legacy.local.Id, DestinoLocalId: destination.Id);
            async Task<int> Transfer()
            {
                try
                {
                    return (await factory.ExecuteScopeAsync(s => s.GetRequiredService<InventoryService>()
                        .MoveAsync("transferencia", transfer, Key(), actor, default))).StatusHttp;
                }
                catch (InventoryConflictException) { return 409; }
                catch (InventoryException failure) when (failure.Status == 409) { return 409; }
            }
            var outcomes = await Task.WhenAll(Transfer(), Transfer());
            Assert.Equal(new[] { 201, 409 }, outcomes.Order());
            var current = await factory.ExecuteScopeAsync(s => s.GetRequiredService<MaterialCostService>()
                .LotAsync(legacy.lot.Id, new(), actor, default));
            var layer = Assert.Single(current.Camadas);
            Assert.Equal("Legado", layer.TipoOrigem);
            Assert.Null(current.Custo.ValorTotal);
            Assert.Equal("2", layer.Posicoes.Single(x => x.LocalId == legacy.local.Id).Quantidade);
            Assert.Equal("1", layer.Posicoes.Single(x => x.LocalId == destination.Id).Quantidade);
            var recognition=await factory.ExecuteDbContextAsync(context=>context.Set<EventoCustoMaterial>().SingleAsync(x=>x.Tipo=="ReconhecimentoLegado"));var cut=await Assert.ThrowsAsync<MaterialCostException>(()=>factory.ExecuteScopeAsync(s=>s.GetRequiredService<MaterialCostService>().LotAsync(legacy.lot.Id,new(AteSequencia:recognition.Sequencia.ToString()),actor,default)));Assert.Equal("corte_intermediario",cut.Code);
            var history=(await factory.ExecuteDbContextAsync(context=>context.Database.GetAppliedMigrationsAsync())).ToArray();await Assert.ThrowsAsync<PostgresException>(()=>factory.ExecuteDbContextAsync(async context=>{await context.GetService<IMigrator>().MigrateAsync(Prior);return 0;}));Assert.Equal(history,(await factory.ExecuteDbContextAsync(context=>context.Database.GetAppliedMigrationsAsync())).ToArray());
        }
        finally{File.Delete(baselineBackup);File.Delete(upgradedBackup);}
    }
}
