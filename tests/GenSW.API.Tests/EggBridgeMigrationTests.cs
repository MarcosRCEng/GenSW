using System.Text.Json;
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

// Every destination is a newly initialized local cluster; no operational
// configuration, source database or pilot backup is consulted.
public sealed class EggBridgeMigrationTests
{
    private const string Baseline = "20261025184750_AddProductionOrdersAndExecution";
    private static readonly string[] Tables = ["AncorasConciliacaoOvos", "RecortesConciliacaoOvos", "DecisoesConciliacaoOvos",
        "ProvasEntradaOvos", "GruposPonteOvos", "PontesProducaoOvos", "AlocacoesProvaOvos", "OrigensLoteOvos",
        "TratamentosPonteOvos", "ComandosPonteOvos", "OrigensRecebimentoOvos"];
    private static GenSWDbContext Db(EphemeralPostgreSql pg) => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options);

    [Fact]
    public async Task F03_21_new_database_installs_empty_bridge_without_backfill_and_matches_model()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg);
        await db.Database.MigrateAsync();
        var rows = await FormulationMigrationTests.RowsAsync(db);
        foreach (var table in Tables) Assert.Empty(rows[table]);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.EndsWith("_AddEggBridge", (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task F03_21_isolated_restored_copy_preserves_all_previous_rows_and_upgraded_backup_restores()
    {
        await using var source = await EphemeralPostgreSql.StartAsync(); await using var prior = Db(source);
        await prior.GetService<IMigrator>().MigrateAsync(Baseline);
        await FormulationMigrationTests.SeedPreviousAsync(prior); await SeedStock(prior);
        var before = await FormulationMigrationTests.RowsAsync(prior);
        Assert.All(new[] { "Animais", "ProducoesOvos", "CiclosReprodutivos", "Itens", "LotesMateriais", "EventosEstoque", "MovimentosEstoque", "PosicoesEstoque", "LancamentosCaixa" },
            table => Assert.NotEmpty(before[table]));
        var baselineBackup = Path.Combine(Path.GetTempPath(), "gensw-f03-baseline-" + Guid.NewGuid().ToString("N") + ".dump");
        var upgradedBackup = Path.Combine(Path.GetTempPath(), "gensw-f03-upgraded-" + Guid.NewGuid().ToString("N") + ".dump");
        try
        {
            await source.BackupAsync(baselineBackup);
            await using var copy = await EphemeralPostgreSql.StartAsync(); await copy.RestoreAsync(baselineBackup); await using var db = Db(copy);
            Assert.Equal(Serialize(before), Serialize(await FormulationMigrationTests.RowsAsync(db)));
            await db.Database.MigrateAsync();
            var after = await FormulationMigrationTests.RowsAsync(db);
            Assert.Equal(Tables.Order(), after.Keys.Except(before.Keys).Order());
            Assert.Equal(Serialize(before), Serialize(await FormulationMigrationTests.RowsAsync(db, before.Keys)));
            foreach (var table in Tables) Assert.Empty(after[table]);
            Assert.Equal(5m, await db.PosicoesEstoque.SumAsync(x => x.Quantidade));
            await db.Database.MigrateAsync(); Assert.Equal(Serialize(after), Serialize(await FormulationMigrationTests.RowsAsync(db)));
            await copy.BackupAsync(upgradedBackup);
            await using var recovered = await EphemeralPostgreSql.StartAsync(); await recovered.RestoreAsync(upgradedBackup); await using var restored = Db(recovered);
            Assert.Equal(Serialize(after), Serialize(await FormulationMigrationTests.RowsAsync(restored)));
            Assert.Equal((await db.Database.GetAppliedMigrationsAsync()).ToArray(), (await restored.Database.GetAppliedMigrationsAsync()).ToArray());
        }
        finally { File.Delete(baselineBackup); File.Delete(upgradedBackup); }
    }

    [Theory]
    [InlineData("egg-bridge-incremental.sql")]
    [InlineData("egg-bridge-idempotent.sql")]
    public async Task F03_21_delivered_sql_upgrades_exact_f02_baseline_without_altering_legacy_rows(string filename)
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg);
        await db.GetService<IMigrator>().MigrateAsync(Baseline); await SeedStock(db);
        var before = await FormulationMigrationTests.RowsAsync(db);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "GenSW.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var sql = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "docs", "validation", "evidence", "501", filename));
        // Compare actual preexisting rows below. A trigger's tuple-locking
        // no-op UPDATE is not a data backfill and must not be rejected by text.
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
        Assert.Equal(Serialize(before), Serialize(await FormulationMigrationTests.RowsAsync(db, before.Keys)));
        foreach (var table in Tables) Assert.Empty((await FormulationMigrationTests.RowsAsync(db, Tables))[table]);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        if (filename.Contains("idempotent", StringComparison.Ordinal))
        {
            var first = Serialize(await FormulationMigrationTests.RowsAsync(db));
            await command.ExecuteNonQueryAsync();
            Assert.Equal(first, Serialize(await FormulationMigrationTests.RowsAsync(db)));
        }
    }

    [Fact]
    public async Task F03_21_destructive_down_refuses_without_schema_or_data_loss()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync(); await using var db = Db(pg);
        await db.Database.MigrateAsync(); await SeedStock(db);
        var before = Serialize(await FormulationMigrationTests.RowsAsync(db));
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => db.GetService<IMigrator>().MigrateAsync(Baseline));
        Assert.True(failure is NotSupportedException || failure is Npgsql.PostgresException { SqlState: "23514" });
        Assert.Equal(history, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(before, Serialize(await FormulationMigrationTests.RowsAsync(db)));
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private static async Task SeedStock(GenSWDbContext db)
    {
        var now = DateTimeOffset.UtcNow; var today = InventoryRules.Today(TimeProvider.System);
        var person = Pessoa.Criar(TipoPessoa.Fisica, "Responsável sintético anterior a F03", null, now);
        var user = new ApplicationUser { Id = Guid.NewGuid(), PessoaId = person.Id, UserName = "f03-preservation", NormalizedUserName = "F03-PRESERVATION", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        var item = Item.Create(new("F03-LEGACY", "Estoque sintético anterior a F03", null, null, "Alimentar", "un", true, true, true, false), now); item.FixUnit();
        var local = LocalEstoque.Create("F03-LEGACY-LOCAL", "Local anterior a F03", null, null, "Ordinario", true, now);
        var lot = LoteMaterial.Create(item.Id, "F03-LEGACY-LOT", "un", new("", "Fixture", "Preservação sintética", user.Id, null, null, null), null, null, null, null, null, null, false, now, today);
        db.AddRange(person, user, item, local, lot); await db.SaveChangesAsync();
        var position = PosicaoEstoque.Create(lot.Id, local.Id, "un"); position.SetBalance(5, 0);
        var snapshot = InventoryJson.Write(new { previa = new { quantidade = new { declarada = "5", unidadeDeclarada = "un", calculada = "5", normalizada = "5", residuo = "0", unidade = "un", exigeAceite = false } }, pedido = new { aceiteQuantizacao = (object?)null } });
        var eventId = Guid.NewGuid();
        db.AddRange(position,
            new EventoEstoque(eventId, 1, "Entrada", user.Id, user.Id, now, today, null, "Fixture anterior F03", null, null, InventoryRules.Algorithm, snapshot),
            new MovimentoEstoque(Guid.NewGuid(), eventId, 1, lot.Id, local.Id, item.Id, "un", "Entrada", 5, "5", "un", "0", 0, 5, null, null, snapshot, "Entrada"),
            new HistoricoEstoque(Guid.NewGuid(), lot.Id, "Lote", "Fixture", 1, user.Id, now, "Preservação", "null", "{}"),
            new ComandoEstoque(Guid.NewGuid(), user.Id, "Entrada", lot.Id, "preservation-f03", new string('a', 64), 201, null, "{}", eventId, lot.Id, now));
        await db.SaveChangesAsync();
    }
}
