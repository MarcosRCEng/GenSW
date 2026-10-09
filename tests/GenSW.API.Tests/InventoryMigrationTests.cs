using System.Text.Json;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed class InventoryMigrationTests
{
    private const string Previous = "20261006220939_AddCatalogAndFormulation";
    private static readonly string[] Tables = ["LocaisEstoque", "LotesMateriais", "PosicoesEstoque", "EventosEstoque", "MovimentosEstoque", "HistoricoEstoque", "ComandosEstoque"];
    private static GenSWDbContext Db(EphemeralPostgreSql pg) => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options);

    [Fact]
    public async Task New_database_has_seven_empty_inventory_tables_and_no_deferred_phases()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg);
        await db.Database.MigrateAsync();
        var rows = await FormulationMigrationTests.RowsAsync(db);
        foreach (var table in Tables) Assert.Empty(rows[table]);
        Assert.DoesNotContain(rows.Keys, x => x.Contains("Reserva") || x.Contains("OrdemProducao") || x.Contains("OrigemOvo") || x.Contains("Custo") || x.Contains("Compra") || x.Contains("Venda"));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Additive_up_preserves_every_previous_row_in_a_restored_copy_before_inventory_fixtures()
    {
        await using var source = await EphemeralPostgreSql.StartAsync();
        await using var baseline = Db(source);
        await baseline.GetService<IMigrator>().MigrateAsync(Previous);
        await FormulationMigrationTests.SeedPreviousAsync(baseline);
        await SeedCatalogAsync(baseline);
        var before = await FormulationMigrationTests.RowsAsync(baseline);
        var backup = Path.Combine(Path.GetTempPath(), "gensw-inventory-baseline-" + Guid.NewGuid().ToString("N") + ".dump");
        try
        {
            await source.BackupAsync(backup);
            await using var copy = await EphemeralPostgreSql.StartAsync(); await copy.RestoreAsync(backup); await using var db = Db(copy);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db, before.Keys)));
            await db.Database.MigrateAsync();
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db, before.Keys)));
            var after = await FormulationMigrationTests.RowsAsync(db);
            Assert.Equal(Tables.Order(), after.Keys.Except(before.Keys).Order());
            foreach (var table in Tables) Assert.Empty(after[table]);
            foreach (var table in new[] { "Animais", "FiliacoesAnimal", "Cruzamentos", "CiclosReprodutivos", "Proles", "ProducoesOvos", "Propriedades", "LancamentosCaixa", "Itens", "ConversoesItens", "PerfisNutricionais", "Receitas", "ReceitasVersoes", "ReceitasReferencias", "SnapshotsFormulacao", "HistoricoFormulacao" }) Assert.NotEmpty(before[table]);
        }
        finally { File.Delete(backup); }
    }

    private static async Task SeedCatalogAsync(GenSWDbContext db)
    {
        var now = DateTimeOffset.UtcNow; var author = Guid.NewGuid();
        var category = CategoriaItem.Create("Categoria sintética de preservação");
        var item = Item.Create(new("INV-LEGACY", "Material sintético anterior a F01", null, category.Id, "Alimentar", "kg", true, true, true, true), now);
        var conversion = ConversaoItem.Create(item.Id, 1, new("un", "kg", "3", "Fixture", "Pesagem sintética", new(2026, 1, 1), "Fixture isolada", "Amostra fictícia", "Medido"), author, now);
        var profile = NutritionProfile.Create(item.Id, 1, new("Perfil sintético", "Fixture de preservação", "Método sintético", "Amostra integral", "Fixture", "Genérico documental", null, null, null, null, [new("PB", "Conhecido", "100", "Declarado", "BN", "g/kg", "Método sintético", "Fixture")]), author, now);
        profile.Publish(1, now); item.FixUnit();
        var recipe = Recipe.Create(new("INV-RECIPE", "Mistura sintética", "Preservação documental"), now);
        var version = RecipeVersion.Create(recipe.Id, 1, new("MisturaSimples", "Quantidade", "1", "kg", [new(Guid.NewGuid(), item.Id, "Alimentar", "1", "kg", "Variavel", profile.Id)], [new(Guid.NewGuid(), item.Id, "Saída sintética", "1", "kg", "Variavel", true, profile.Id)], [], [], "Fixture sem material real", []), author, now);
        version.Publish(1, now);
        var snapshot = FormulationSnapshot.Create("Simulacao", author, "inventory-preservation", new string('a',64), "{\"fixture\":\"preservacao\"}", now);
        db.AddRange(category, item, conversion, profile, recipe, version, new RecipeReference(Guid.NewGuid(),version.Id,item.Id,profile.Id,conversion.Id,null), snapshot, new CatalogAudit(Guid.NewGuid(),item.Id,"Item","Criacao",author,now,"null",FormulationJson.Write(item)));
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("20261002222623_AddFinancialCash", "2026-10-08-estoque-from-financial.sql")]
    [InlineData(Previous, "2026-10-08-estoque-from-catalog.sql")]
    public async Task Reviewed_sql_applies_all_pending_migrations_and_replay_preserves_rows(string baseline, string file)
    {
        await using var pg=await EphemeralPostgreSql.StartAsync(); await using var db=Db(pg);
        await db.GetService<IMigrator>().MigrateAsync(baseline);
        var before=await FormulationMigrationTests.RowsAsync(db);
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,"GenSW.sln"))) root=root.Parent;
        Assert.NotNull(root);
        var sql=await File.ReadAllTextAsync(Path.Combine(root!.FullName,"docs","operations","sql",file));
        await ExecuteMigrationScriptAsync(db,sql);
        Assert.Equal(JsonSerializer.Serialize(before),JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db,before.Keys)));
        foreach (var table in Tables) Assert.Empty((await FormulationMigrationTests.RowsAsync(db,Tables))[table]);
        var rows=await FormulationMigrationTests.RowsAsync(db); var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await ExecuteMigrationScriptAsync(db,sql);
        Assert.Equal(JsonSerializer.Serialize(rows),JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db)));
        Assert.Equal(migrations,(await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private static async Task ExecuteMigrationScriptAsync(GenSWDbContext db,string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command=db.Database.GetDbConnection().CreateCommand();
        command.CommandText=sql;
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Database_guards_reject_immutable_writes_negative_fractional_mismatch_and_unbalanced_transfers()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg); await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow; var today = DateOnly.FromDateTime(now.UtcDateTime);
        var person = Pessoa.Criar(TipoPessoa.Fisica, "Responsável sintético", null, now);
        var user = new ApplicationUser { Id=Guid.NewGuid(),PessoaId=person.Id,IsActive=true,CreatedAtUtc=now,UpdatedAtUtc=now,UserName="migrationfixture",NormalizedUserName="MIGRATIONFIXTURE" };
        var item = Item.Create(new("INV-GUARD", "Fixture sem estoque real", null, null, "Alimentar", "kg", true, true, true, false),now); item.FixUnit();
        var a = LocalEstoque.Create("INV-A","Origem sintética",null,null,"Ordinario",true,now);
        var b = LocalEstoque.Create("INV-B","Destino sintético",null,null,"Ordinario",true,now);
        var lot = LoteMaterial.Create(item.Id,"INV-LOT","kg",new("","Fixture","Fixture",user.Id,null,null,null),null,null,null,null,null,null,false,now,today);
        db.AddRange(person,user,item,a,b,lot); await db.SaveChangesAsync();
        var eventId=Guid.NewGuid(); var position=PosicaoEstoque.Create(lot.Id,a.Id,"kg"); position.SetBalance(10,0);
        var snapshot=QuantitySnapshot("10","10","0");
        db.AddRange(position,new EventoEstoque(eventId,1,"Entrada",user.Id,user.Id,now,today,null,"Fixture",null,null,InventoryRules.Algorithm,snapshot),new MovimentoEstoque(Guid.NewGuid(),eventId,1,lot.Id,a.Id,item.Id,"kg","Entrada",10,"10","kg","0",0,10,null,null,snapshot,"Entrada"),new HistoricoEstoque(Guid.NewGuid(),lot.Id,"Lote","Fixture",2,user.Id,now,"Fixture","null","{}"),new ComandoEstoque(Guid.NewGuid(),user.Id,"Entrada",lot.Id,"fixture",new string('a',64),201,null,"{}",eventId,lot.Id,now));
        await db.SaveChangesAsync();
        await Reject(db, $"UPDATE \"EventosEstoque\" SET \"Motivo\"='mutado' WHERE \"Id\"={eventId}");
        await Reject(db, $"DELETE FROM \"MovimentosEstoque\" WHERE \"EventoId\"={eventId}");
        await Reject(db, $"UPDATE \"HistoricoEstoque\" SET \"Motivo\"='mutado' WHERE \"RegistroId\"={lot.Id}");
        await Reject(db, $"DELETE FROM \"ComandosEstoque\" WHERE \"RecursoId\"={lot.Id}");
        await Reject(db, $"UPDATE \"PosicoesEstoque\" SET \"Quantidade\"=-1 WHERE \"LoteId\"={lot.Id}");
        await Reject(db, $"UPDATE \"Itens\" SET \"Unidade\"='L' WHERE \"Id\"={item.Id}");
        await Reject(db, $"UPDATE \"LotesMateriais\" SET \"Unidade\"='L' WHERE \"Id\"={lot.Id}");
        await Reject(db, $"UPDATE \"LotesMateriais\" SET \"Situacao\"='Encerrado' WHERE \"Id\"={lot.Id}");
        await Reject(db, $"UPDATE \"PosicoesEstoque\" SET \"Quantidade\"=11 WHERE \"LoteId\"={lot.Id}");
        var countedItem=Item.Create(new("INV-COUNT", "Contagem sintética", null, null, "Embalagem", "un", true, false, true, false),now); countedItem.FixUnit();
        var countedLot=LoteMaterial.Create(countedItem.Id,"INV-COUNT-LOT","un",new("","Fixture","Fixture",user.Id,null,null,null),null,null,null,null,null,null,false,now,today);
        var countedPosition=PosicaoEstoque.Create(countedLot.Id,a.Id,"un"); countedPosition.SetBalance(0,0);
        db.AddRange(countedItem,countedLot,countedPosition); await db.SaveChangesAsync();
        await Reject(db, $"UPDATE \"PosicoesEstoque\" SET \"Quantidade\"=0.5 WHERE \"LoteId\"={countedLot.Id}");
        var excessiveSnapshot=QuantitySnapshot("1.0000001","1","0");
        db.Add(new MovimentoEstoque(Guid.NewGuid(),eventId,2,lot.Id,a.Id,item.Id,"kg","Entrada",1,"1.0000001","kg","0",10,11,null,null,excessiveSnapshot,"Entrada"));
        var excessive=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        Assert.Equal("CK_MovimentosEstoque_Declarado",((PostgresException)excessive.InnerException!).ConstraintName); db.ChangeTracker.Clear();
        var mismatch=PosicaoEstoque.Create(lot.Id,b.Id,"un"); mismatch.SetBalance(0,0); db.Add(mismatch);
        var wrong=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); Assert.Equal("23514",((PostgresException)wrong.InnerException!).SqlState); db.ChangeTracker.Clear();
        var missingUnitSnapshot=InventoryJson.Write(new { previa=new { quantidade=new { declarada="1",unidadeDeclarada="kg",calculada="1",normalizada="1",residuo="0",exigeAceite=false } },pedido=new { aceiteQuantizacao=(object?)null } });
        db.Add(new MovimentoEstoque(Guid.NewGuid(),eventId,2,lot.Id,a.Id,item.Id,"kg","Entrada",1,"1","kg","0",10,11,null,null,missingUnitSnapshot,"Entrada"));
        var missingUnit=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); Assert.Equal("23514",((PostgresException)missingUnit.InnerException!).SqlState); db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync();
        var transferId=Guid.NewGuid(); var transferSnapshot=QuantitySnapshot("1","1","0");
        var actual=await db.PosicoesEstoque.SingleAsync(p=>p.LoteId==lot.Id&&p.LocalId==a.Id); actual.SetBalance(9,1);
        db.AddRange(new EventoEstoque(transferId,3,"Transferencia",user.Id,user.Id,now,today,null,"Fixture",null,null,InventoryRules.Algorithm,transferSnapshot),new MovimentoEstoque(Guid.NewGuid(),transferId,1,lot.Id,a.Id,item.Id,"kg","Saida",1,"1","kg","0",10,9,null,null,transferSnapshot,"Transferencia"));
        await db.SaveChangesAsync();
        var failure=await Assert.ThrowsAsync<PostgresException>(()=>tx.CommitAsync()); Assert.Equal("23514",failure.SqlState);
        await tx.DisposeAsync(); db.ChangeTracker.Clear(); Assert.Equal(10,await db.PosicoesEstoque.Where(p=>p.LoteId==lot.Id&&p.LocalId==a.Id).Select(p=>p.Quantidade).SingleAsync()); Assert.False(await db.EventosEstoque.AnyAsync(e=>e.Id==transferId));
    }
    private static string QuantitySnapshot(string declared,string normalized,string residue) => InventoryJson.Write(new { previa=new { quantidade=new { declarada=declared,unidadeDeclarada="kg",calculada=normalized,normalizada=normalized,residuo=residue,unidade="kg",exigeAceite=false } },pedido=new { aceiteQuantizacao=(object?)null } });
    private static async Task Reject(GenSWDbContext db,FormattableString sql)
    { var failure=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync(sql)); Assert.Equal("23514",failure.SqlState); }
}
