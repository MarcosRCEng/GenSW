using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class AnimalMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260901230349_AddRacasVariedades";
    private GenSW.API.Tests.EphemeralPostgreSql postgreSql = null!;

    public async Task InitializeAsync() => postgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();

    [Fact]
    public async Task Migration_creates_and_reverses_the_animal_schema_without_leaving_alternate_keys()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        Assert.True(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'r' AND relname = 'Animais')"));
        Assert.True(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'S' AND relname = 'AnimalCodigoInternoSequence')"));
        Assert.Equal("1", await ScalarAsync(context, "SELECT seqstart::text FROM pg_sequence JOIN pg_class ON pg_sequence.seqrelid = pg_class.oid WHERE relname = 'AnimalCodigoInternoSequence'"));
        Assert.Equal("1", await ScalarAsync(context, "SELECT seqincrement::text FROM pg_sequence JOIN pg_class ON pg_sequence.seqrelid = pg_class.oid WHERE relname = 'AnimalCodigoInternoSequence'"));
        Assert.Equal("1", await ScalarAsync(context, "SELECT seqmin::text FROM pg_sequence JOIN pg_class ON pg_sequence.seqrelid = pg_class.oid WHERE relname = 'AnimalCodigoInternoSequence'"));
        Assert.Equal("int8", await ScalarAsync(context, "SELECT typname FROM pg_sequence JOIN pg_class ON pg_sequence.seqrelid = pg_class.oid JOIN pg_type ON pg_sequence.seqtypid = pg_type.oid WHERE pg_class.relname = 'AnimalCodigoInternoSequence'"));
        Assert.Equal("false", await ScalarAsync(context, "SELECT seqcycle::text FROM pg_sequence JOIN pg_class ON pg_sequence.seqrelid = pg_class.oid WHERE relname = 'AnimalCodigoInternoSequence'"));
        Assert.Equal("1", await ScalarAsync(context, "SELECT seqcache::text FROM pg_sequence JOIN pg_class ON pg_sequence.seqrelid = pg_class.oid WHERE relname = 'AnimalCodigoInternoSequence'"));
        var codigoCheck = await ConstraintDefinitionAsync(context, "CK_Animais_CodigoInterno_Canonical");
        Assert.Contains("CodigoInterno", codigoCheck);
        Assert.Contains("<> ''", codigoCheck);
        Assert.Contains("!~", codigoCheck);
        Assert.Contains("(^ | $|  )", codigoCheck);
        var nomeCheck = await ConstraintDefinitionAsync(context, "CK_Animais_Nome_Canonical");
        Assert.Contains("Nome", nomeCheck);
        Assert.Contains("IS NULL", nomeCheck);
        Assert.Contains("!~", nomeCheck);
        Assert.Contains("\"Sexo\" = ANY", await ConstraintDefinitionAsync(context, "CK_Animais_Sexo"));
        Assert.Contains("\"Escopo\" = ANY", await ConstraintDefinitionAsync(context, "CK_Animais_Escopo"));
        var codeIndex = await ScalarAsync(context, "SELECT indexdef FROM pg_indexes WHERE indexname = 'UX_Animais_CodigoInterno_CaseInsensitive'");
        Assert.Contains("CREATE UNIQUE INDEX", codeIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lower", codeIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"CodigoInterno\"", codeIndex);
        Assert.Contains("UNIQUE (\"Id\", \"EspecieId\")", await ConstraintDefinitionAsync(context, "AK_Racas_Id_EspecieId"));
        Assert.Contains("UNIQUE (\"Id\", \"EspecieId\")", await ConstraintDefinitionAsync(context, "AK_Variedades_Id_EspecieId"));
        Assert.Contains("FOREIGN KEY (\"RacaId\", \"EspecieId\") REFERENCES \"Racas\"(\"Id\", \"EspecieId\") ON DELETE RESTRICT", await ConstraintDefinitionAsync(context, "FK_Animais_Racas_RacaId_EspecieId"));
        Assert.Contains("FOREIGN KEY (\"VariedadeId\", \"EspecieId\") REFERENCES \"Variedades\"(\"Id\", \"EspecieId\") ON DELETE RESTRICT", await ConstraintDefinitionAsync(context, "FK_Animais_Variedades_VariedadeId_EspecieId"));

        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        Assert.False(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'r' AND relname = 'Animais')"));
        Assert.False(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'S' AND relname = 'AnimalCodigoInternoSequence')"));
        Assert.False(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'AK_Racas_Id_EspecieId')"));
        Assert.False(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'AK_Variedades_Id_EspecieId')"));
    }

    [Fact]
    public async Task Composite_foreign_keys_reject_cross_species_classifications_and_restrict_referenced_reassignment()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var dog = GenSW.Domain.Species.Especie.Criar("Cão " + Guid.NewGuid().ToString("N"), null, now);
        var cat = GenSW.Domain.Species.Especie.Criar("Gato " + Guid.NewGuid().ToString("N"), null, now);
        var breed = GenSW.Domain.Breeds.Raca.Criar(dog.Id, "Pastor " + Guid.NewGuid().ToString("N"), now);
        var variety = GenSW.Domain.Varieties.Variedade.Criar(dog.Id, "Curto " + Guid.NewGuid().ToString("N"), now);
        context.AddRange(dog, cat, breed, variety);
        await context.SaveChangesAsync();

        var breedViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(context, $"""
            INSERT INTO "Animais" ("Id", "CodigoInterno", "EspecieId", "RacaId", "VariedadeId", "Sexo", "Escopo", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ('{Guid.NewGuid():D}', 'AN-CROSS-BREED', '{cat.Id:D}', '{breed.Id:D}', NULL, 3, 1, TRUE, NOW(), NOW());
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, breedViolation.SqlState);
        Assert.Equal("FK_Animais_Racas_RacaId_EspecieId", breedViolation.ConstraintName);

        var varietyViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(context, $"""
            INSERT INTO "Animais" ("Id", "CodigoInterno", "EspecieId", "RacaId", "VariedadeId", "Sexo", "Escopo", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ('{Guid.NewGuid():D}', 'AN-CROSS-VARIETY', '{cat.Id:D}', NULL, '{variety.Id:D}', 3, 1, TRUE, NOW(), NOW());
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, varietyViolation.SqlState);
        Assert.Equal("FK_Animais_Variedades_VariedadeId_EspecieId", varietyViolation.ConstraintName);

        context.Animais.Add(GenSW.Domain.Animals.Animal.Criar(
            "AN-REFERENCED", null, dog.Id, breed.Id, variety.Id,
            GenSW.Domain.Animals.SexoAnimal.Indeterminado, null,
            GenSW.Domain.Animals.EscopoAnimal.Operacional,
            DateOnly.FromDateTime(now.UtcDateTime), now));
        await context.SaveChangesAsync();

        var breedMoveViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            context, $"UPDATE \"Racas\" SET \"EspecieId\" = '{cat.Id:D}' WHERE \"Id\" = '{breed.Id:D}';"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, breedMoveViolation.SqlState);
        Assert.Equal("FK_Animais_Racas_RacaId_EspecieId", breedMoveViolation.ConstraintName);

        var varietyMoveViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            context, $"UPDATE \"Variedades\" SET \"EspecieId\" = '{cat.Id:D}' WHERE \"Id\" = '{variety.Id:D}';"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, varietyMoveViolation.SqlState);
        Assert.Equal("FK_Animais_Variedades_VariedadeId_EspecieId", varietyMoveViolation.ConstraintName);
    }

    public Task DisposeAsync() => postgreSql.DisposeAsync().AsTask();

    private GenSWDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(postgreSql.ConnectionString).Options);

    private static async Task<bool> ExistsAsync(GenSWDbContext context, string sql)
        => bool.Parse(await ScalarAsync(context, sql));

    private static async Task<string> ScalarAsync(GenSWDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)!;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static Task<string> ConstraintDefinitionAsync(GenSWDbContext context, string name)
        => ScalarAsync(context, $"SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = '{name}'");

    private static async Task ExecuteSqlAsync(GenSWDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
