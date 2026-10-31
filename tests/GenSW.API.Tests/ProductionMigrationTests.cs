using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GenSW.API.Tests;

public sealed class ProductionMigrationTests
{
    private const string Baseline = "20261007225228_AddManualInventory";
    private const string ProductionMigration = "20261025184750_AddProductionOrdersAndExecution";
    private static readonly string[] NewTables = ["OrdensProducao", "ReservasProducao", "ExecucoesProducao", "ExecucaoMovimentos", "SaidasProducao", "GenealogiaMateriais", "HistoricoProducao", "OcorrenciasProducao", "ComandosProducao", "ReversoesProducao"];
    private static GenSWDbContext Db(EphemeralPostgreSql pg) => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options);

    [Fact]
    public async Task T19_new_database_has_empty_production_tables_and_all_migrations_applied()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg);
        await db.Database.MigrateAsync();
        var rows = await FormulationMigrationTests.RowsAsync(db);
        foreach (var table in NewTables) Assert.Empty(rows[table]);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True((await db.Database.GetAppliedMigrationsAsync()).Count() > 18);
        Assert.DoesNotContain(rows.Keys, x => x.Contains("OrigemOvo") || x.Contains("CustoProducao"));
    }

    [Fact]
    public async Task T19_destructive_down_is_refused_without_changing_the_installed_schema_or_data()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg);
        await db.GetService<IMigrator>().MigrateAsync(ProductionMigration); await SeedInventory(db);
        var before = JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db));
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();

        await Assert.ThrowsAsync<NotSupportedException>(() => db.GetService<IMigrator>().MigrateAsync(Baseline));

        Assert.Equal(history, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(before, JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db)));
        Assert.DoesNotContain(ProductionMigration, await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task T19_upgrade_of_restored_f01_fixture_preserves_every_legacy_row_and_backup_can_restore_baseline()
    {
        await using var source = await EphemeralPostgreSql.StartAsync(); await using var baseline = Db(source);
        await baseline.GetService<IMigrator>().MigrateAsync(Baseline);
        await FormulationMigrationTests.SeedPreviousAsync(baseline); await SeedInventory(baseline);
        var before = await FormulationMigrationTests.RowsAsync(baseline);
        var backup = Path.Combine(Path.GetTempPath(), "gensw-production-baseline-" + Guid.NewGuid().ToString("N") + ".dump");
        try
        {
            await source.BackupAsync(backup);
            await using var copy = await EphemeralPostgreSql.StartAsync(); await copy.RestoreAsync(backup); await using var db = Db(copy);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db, before.Keys)));
            // This gate verifies the historical F02 transition specifically;
            // later additive schemas have their own migration/restore gates.
            await db.GetService<IMigrator>().MigrateAsync(ProductionMigration);
            var after = await FormulationMigrationTests.RowsAsync(db);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db, before.Keys)));
            Assert.Equal(NewTables.Order(), after.Keys.Except(before.Keys).Order());
            foreach (var table in NewTables) Assert.Empty(after[table]);
            foreach (var table in new[] { "LotesMateriais", "LocaisEstoque", "PosicoesEstoque", "EventosEstoque", "MovimentosEstoque", "HistoricoEstoque", "ComandosEstoque", "Itens", "Animais", "Propriedades", "LancamentosCaixa" }) Assert.NotEmpty(before[table]);
            Assert.Equal(10m, await db.PosicoesEstoque.SumAsync(p => p.Quantidade));
            var migrated = JsonSerializer.Serialize(after);
            await db.GetService<IMigrator>().MigrateAsync(ProductionMigration); Assert.Equal(migrated, JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db)));
            // Recovery restores the pre-upgrade backup into a fresh isolated
            // cluster, avoiding any claim that a destructive Down is safe.
            await using var recovery = await EphemeralPostgreSql.StartAsync();
            await recovery.RestoreAsync(backup); await using var restored = Db(recovery);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(restored)));
            Assert.Equal(Baseline, (await restored.Database.GetAppliedMigrationsAsync()).Last());
        }
        finally { File.Delete(backup); }
    }

    [Fact]
    public async Task T19_reviewed_idempotent_sql_applies_from_exact_inventory_baseline_and_replay_is_readonly()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg);
        await db.GetService<IMigrator>().MigrateAsync(Baseline); await SeedInventory(db);
        var before = await FormulationMigrationTests.RowsAsync(db);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "GenSW.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var sql = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "docs", "operations", "sql", "2026-10-25-f02-from-inventory.sql"));
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db, before.Keys)));
        foreach (var table in NewTables) Assert.Empty((await FormulationMigrationTests.RowsAsync(db, NewTables))[table]);
        Assert.DoesNotContain(ProductionMigration, await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(ProductionMigration, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.EndsWith("_AddProductionOrdersAndExecution", (await db.Database.GetAppliedMigrationsAsync()).Last());
        var migrated = JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db));
        await command.ExecuteNonQueryAsync(); Assert.Equal(migrated, JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db)));
    }

    private static async Task SeedInventory(GenSWDbContext db)
    {
        var now = DateTimeOffset.UtcNow; var today = InventoryRules.Today(TimeProvider.System);
        var person = Pessoa.Criar(TipoPessoa.Fisica, "Responsável sintético anterior a F02", null, now);
        var user = new ApplicationUser { Id = Guid.NewGuid(), PessoaId = person.Id, UserName = "production-preservation", NormalizedUserName = "PRODUCTION-PRESERVATION", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        var item = Item.Create(new("F02-LEGACY", "Estoque sintético anterior a F02", null, null, "Alimentar", "kg", true, true, true, false), now); item.FixUnit();
        var local = LocalEstoque.Create("F02-LEGACY-LOCAL", "Local anterior a F02", null, null, "Ordinario", true, now);
        var lot = LoteMaterial.Create(item.Id, "F02-LEGACY-LOT", "kg", new("", "Fixture", "Preservação sintética", user.Id, null, null, null), null, null, null, null, null, null, false, now, today);
        db.AddRange(person, user, item, local, lot); await db.SaveChangesAsync();
        var position = PosicaoEstoque.Create(lot.Id, local.Id, "kg"); position.SetBalance(10, 0);
        var snapshot = InventoryJson.Write(new { previa = new { quantidade = new { declarada = "10", unidadeDeclarada = "kg", calculada = "10", normalizada = "10", residuo = "0", unidade = "kg", exigeAceite = false } }, pedido = new { aceiteQuantizacao = (object?)null } });
        var eventId = Guid.NewGuid();
        db.AddRange(position,
            new EventoEstoque(eventId, 1, "Entrada", user.Id, user.Id, now, today, null, "Fixture anterior F02", null, null, InventoryRules.Algorithm, snapshot),
            new MovimentoEstoque(Guid.NewGuid(), eventId, 1, lot.Id, local.Id, item.Id, "kg", "Entrada", 10, "10", "kg", "0", 0, 10, null, null, snapshot, "Entrada"),
            new HistoricoEstoque(Guid.NewGuid(), lot.Id, "Lote", "Fixture", 1, user.Id, now, "Preservação", "null", "{}"),
            new ComandoEstoque(Guid.NewGuid(), user.Id, "Entrada", lot.Id, "preservation", new string('a', 64), 201, null, "{}", eventId, lot.Id, now));
        await db.SaveChangesAsync();
    }
}
