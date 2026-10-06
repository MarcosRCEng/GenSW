using System.Data.Common;
using System.Text.Json;
using GenSW.Domain.Animals;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Financial;
using GenSW.Domain.Properties;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed class FormulationMigrationTests
{
    [Fact]
    public async Task Additive_migration_preserves_every_previous_table_and_does_not_seed_operational_nutrition()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync();
        await using var db = new GenSWDbContext(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261005132012_AddOperationalProperties");
        var now = DateTimeOffset.UtcNow; var date = new DateOnly(2026, 1, 1); var author = Guid.NewGuid();
        var species = GenSW.Domain.Species.Especie.Criar("Fixture de preservação", null, true, 12m, now);
        var father = Animal.Criar("MIG-FORM-PAI", "Pai", species.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, date, now);
        var mother = Animal.Criar("MIG-FORM-MAE", "Mãe", species.Id, null, null, SexoAnimal.Femea, null, EscopoAnimal.Operacional, date, now);
        var child = Animal.Criar("MIG-FORM-FILHO", "Filho", species.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, date, now);
        var filiation = FiliacaoAnimal.Criar(child.Id, father.Id, TipoFiliacaoAnimal.Pai, null, now);
        var breeding = Cruzamento.Criar(father.Id, mother.Id, StatusCruzamento.Concluido, date, date, "Fixture", null, now);
        var cycle = CicloReprodutivo.Criar(breeding.Id, TipoCicloReprodutivo.Oviparo, StatusCicloReprodutivo.Concluido, date, date, date, 1, 1, 1, 1, 0, 12m, null, null, null, null, null, null, null, "Fixture", now);
        var prole = Prole.Criar(cycle.Id, TipoRegistroProle.Individual, 1, TipoOrigemProle.Eclosao, date, 8m, SexoAnimal.Indeterminado, "Fixture", null, null, now);
        var egg = ProducaoOvo.Criar(mother.Id, date, 12.25m, "Fixture", now);
        var property = Propriedade.Criar("Fixture de preservação", "Local sintético", null, now);
        var link = VinculoAnimalPropriedade.Criar(child.Id, property.Id, date, null, now);
        var category = await db.Set<CategoriaFinanceira>().FirstAsync(x => x.Natureza == NaturezaFinanceira.Receita);
        var cash = new LancamentoCaixa(Guid.NewGuid(), NaturezaFinanceira.Receita, date, 123.45m, "Fixture de preservação", category.Id, FormaPagamento.Pix, null, child.Id, null, false, 1, author, now, now, OrigemLancamento.Ordinario, null, null);
        db.AddRange(species, father, mother, child, filiation, breeding, cycle, prole, egg, property, link, cash);
        await db.SaveChangesAsync();
        var before = await RowsAsync(db);
        await db.Database.MigrateAsync();
        var after = await RowsAsync(db, before.Keys);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.NotEmpty(before["Animais"]); Assert.NotEmpty(before["FiliacoesAnimal"]); Assert.NotEmpty(before["Cruzamentos"]); Assert.NotEmpty(before["CiclosReprodutivos"]); Assert.NotEmpty(before["Proles"]); Assert.NotEmpty(before["ProducoesOvos"]); Assert.NotEmpty(before["Propriedades"]); Assert.NotEmpty(before["LancamentosCaixa"]);
        Assert.Empty(await db.Set<Item>().ToArrayAsync()); Assert.Empty(await db.Set<NutritionProfile>().ToArrayAsync()); Assert.Empty(await db.Set<Recipe>().ToArrayAsync()); Assert.Empty(await db.Set<FormulationSnapshot>().ToArrayAsync());
        var names = (await RowsAsync(db)).Keys.ToArray(); Assert.DoesNotContain(names, x => x.Contains("LoteMaterial") || x.Contains("Estoque") || x.Contains("OrdemProducao")); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        // PostgreSQL guards remain effective when application rules are bypassed.
        var item = Item.Create(new("CHECK", "Fixture", null, null, "Alimentar", "kg", true, true, true, false), now); db.Add(item); await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Itens\" SET \"Unidade\"='ton' WHERE \"Id\"={item.Id}")); Assert.Equal("23514", error.SqlState);
    }
    private static async Task<SortedDictionary<string, string[]>> RowsAsync(GenSWDbContext db, IEnumerable<string>? selected = null)
    {
        await db.Database.OpenConnectionAsync(); var conn = db.Database.GetDbConnection(); var tables = new List<string>();
        if (selected is null)
        {
            using var command = conn.CreateCommand(); command.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory' ORDER BY tablename";
            using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }
        var result = new SortedDictionary<string, string[]>();
        foreach (var table in selected ?? tables)
        {
            using var command = conn.CreateCommand(); command.CommandText = "SELECT to_jsonb(t)::text FROM \"" + table.Replace("\"", "\"\"") + "\" t ORDER BY to_jsonb(t)::text";
            using var reader = await command.ExecuteReaderAsync(); var rows = new List<string>(); while (await reader.ReadAsync()) rows.Add(reader.GetString(0)); result[table] = rows.ToArray();
        }
        return result;
    }
}
