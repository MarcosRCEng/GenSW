using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.MaterialCosts;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

public sealed class OptimizationMigrationTests
{
    private const string Baseline = "20261103233403_AddMaterialCosts";
    private static readonly string[] AddedTables = ["DerivacoesOtimizacao", "ExecucoesOtimizacao", "ResultadosOtimizacao"];

    [Fact]
    public async Task S05_28_reviewed_upgrade_is_additive_repeatable_and_restorable_on_isolated_postgresql()
    {
        await using var baseline = await EphemeralPostgreSql.StartAsync();
        await using var source = Context(baseline);
        await source.GetService<IMigrator>().MigrateAsync(Baseline);
        await FormulationMigrationTests.SeedPreviousAsync(source);
        using (var factory = new AuthWebApplicationFactory(baseline.ConnectionString))
        {
            var actor = (await factory.SeedUserAsync("f05_migration_fixture", roles: ["Admin"])).UserId;
            await factory.ExecuteScopeAsync(async services =>
            {
                var formulation = services.GetRequiredService<FormulationService>();
                var item = await formulation.CreateItemAsync(new("MIG-F05-A", "Entrada sintética", null, null,
                    "Alimentar", "kg", true, false, false, false), actor, default);
                var output = await formulation.CreateItemAsync(new("MIG-F05-O", "Saída sintética", null, null,
                    "Alimentar", "kg", false, true, false, false), actor, default);
                var recipe = await formulation.CreateRecipeAsync(new("MIG-F05-RECEITA", "Mistura sintética", "Preservação"), actor, default);
                var version = await formulation.CreateVersionAsync(recipe.Id, new("MisturaSimples", "Quantidade", "1", "kg",
                    [new(Guid.NewGuid(), item.Id, "Alimentar", "1", "kg", "Variavel")],
                    [new(Guid.NewGuid(), output.Id, "Saída", "1", "kg", "Variavel", true)], [], [], null, []), actor, default);
                version = await formulation.VersionStateAsync(version.Id, version.Revisao, true, actor, default);
                await formulation.SimulateAsync(new(version.Id, "1", "kg", null, null, []), "f05-migration-baseline", actor, default);

                var today = InventoryRules.Today(services.GetRequiredService<TimeProvider>());
                await services.GetRequiredService<MaterialCostService>().PublishReferenceAsync(
                    new(Guid.NewGuid(), item.Id, "Cotação sintética", "ref-sintetica", today, "BRL", "kg", "1", "2.00",
                        today, null, "Preservação do contrato F04", "Fixture"), "f05-migration-f04", actor, default);
                return true;
            });
        }

        Assert.Equal(21, (await source.Database.GetAppliedMigrationsAsync()).Count());
        var before = await FormulationMigrationTests.RowsAsync(source);
        foreach (var table in new[] { "Animais", "LancamentosCaixa", "SnapshotsFormulacao", "PrecosReferenciaMateriais", "ComandosCustoMateriais" })
            Assert.NotEmpty(before[table]);

        var sourceDump = Path.Combine(Path.GetTempPath(), $"gensw-f05-baseline-{Guid.NewGuid():N}.dump");
        var upgradedDump = Path.Combine(Path.GetTempPath(), $"gensw-f05-upgrade-{Guid.NewGuid():N}.dump");
        try
        {
            await baseline.BackupAsync(sourceDump);
            await using var copy = await EphemeralPostgreSql.StartAsync();
            await copy.RestoreAsync(sourceDump);
            await using var migrated = Context(copy);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(migrated)));

            var script = await File.ReadAllTextAsync(FindScript());
            await migrated.Database.ExecuteSqlRawAsync(script);
            await migrated.Database.ExecuteSqlRawAsync(script);
            var after = await FormulationMigrationTests.RowsAsync(migrated);
            Assert.Equal(AddedTables, after.Keys.Except(before.Keys).Order());
            Assert.All(AddedTables, table => Assert.Empty(after[table]));
            Assert.Equal(JsonSerializer.Serialize(before),
                JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(migrated, before.Keys)));
            Assert.Empty(await migrated.Database.GetPendingMigrationsAsync());
            Assert.False(migrated.Database.HasPendingModelChanges());
            Assert.Equal(22, (await migrated.Database.GetAppliedMigrationsAsync()).Count());

            await copy.BackupAsync(upgradedDump);
            await using var recovered = await EphemeralPostgreSql.StartAsync();
            await recovered.RestoreAsync(upgradedDump);
            await using var restored = Context(recovered);
            Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(restored)));
            Assert.Equal((await migrated.Database.GetAppliedMigrationsAsync()).ToArray(),
                (await restored.Database.GetAppliedMigrationsAsync()).ToArray());
        }
        finally
        {
            File.Delete(sourceDump);
            File.Delete(upgradedDump);
        }
    }

    private static GenSWDbContext Context(EphemeralPostgreSql postgres) =>
        new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(postgres.ConnectionString).Options);

    private static string FindScript()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "GenSW.sln"))) root = root.Parent;
        return Path.Combine(root?.FullName ?? throw new InvalidOperationException("Raiz da solução não encontrada."),
            "docs", "validation", "evidence", "526", "optimization-idempotent.sql");
    }
}
