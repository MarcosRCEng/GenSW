using GenSW.Infrastructure.Persistence;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class IdentificacaoAnimalMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260908223306_AddAnimalBase";
    private const string OutroDescricaoTipoValorIndex = "UX_IdentificacoesAnimal_Outro_DescricaoTipo_Valor_CaseInsensiti";
    private GenSW.API.Tests.EphemeralPostgreSql postgreSql = null!;

    public async Task InitializeAsync() => postgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();

    [Fact]
    public async Task Migration_creates_and_reverses_all_identification_artifacts()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        Assert.True(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'r' AND relname = 'IdentificacoesAnimal')"));
        Assert.Equal("uuid", await ColumnTypeAsync(context, "Id"));
        Assert.Equal("uuid", await ColumnTypeAsync(context, "AnimalId"));
        Assert.Equal("date", await ColumnTypeAsync(context, "DataAplicacao"));
        Assert.Contains("FOREIGN KEY (\"AnimalId\") REFERENCES \"Animais\"(\"Id\") ON DELETE RESTRICT", await ConstraintDefinitionAsync(context, "FK_IdentificacoesAnimal_Animais_AnimalId"));
        Assert.Contains("\"Tipo\" = ANY", await ConstraintDefinitionAsync(context, "CK_IdentificacoesAnimal_Tipo"));
        var valorCheck = await ConstraintDefinitionAsync(context, "CK_IdentificacoesAnimal_Valor_Canonical");
        Assert.Contains("\"Valor\"", valorCheck);
        Assert.Contains("<> ''", valorCheck);
        Assert.Contains("btrim", valorCheck, StringComparison.OrdinalIgnoreCase);
        var descricaoCheck = await ConstraintDefinitionAsync(context, "CK_IdentificacoesAnimal_DescricaoTipo_Semantics");
        Assert.Contains("\"Tipo\" = 6", descricaoCheck);
        Assert.Contains("btrim", descricaoCheck, StringComparison.OrdinalIgnoreCase);
        var tipoValorIndex = await IndexDefinitionAsync(context, "UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive");
        Assert.Contains("CREATE UNIQUE INDEX", tipoValorIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lower", tipoValorIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Valor\"", tipoValorIndex);
        Assert.Contains("WHERE (\"Tipo\" <> 6)", tipoValorIndex);
        var outroIndex = await IndexDefinitionAsync(context, OutroDescricaoTipoValorIndex);
        Assert.Contains("lower", outroIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"DescricaoTipo\"", outroIndex);
        Assert.Contains("\"Valor\"", outroIndex);
        Assert.Contains("WHERE (\"Tipo\" = 6)", outroIndex);
        var principalIndex = await IndexDefinitionAsync(context, "UX_IdentificacoesAnimal_Animal_PrincipalAtiva");
        Assert.Contains("WHERE (\"Ativo\" AND \"Principal\")", principalIndex);

        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.False(await ExistsAsync(context, "SELECT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'r' AND relname = 'IdentificacoesAnimal')"));
        foreach (var index in new[] { "UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive", OutroDescricaoTipoValorIndex, "UX_IdentificacoesAnimal_Animal_PrincipalAtiva" })
            Assert.False(await ExistsAsync(context, $"SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = '{index}')"));
    }

    [Fact]
    public async Task Migration_enforces_foreign_key_restrict_and_each_named_check()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        await AssertConstraintViolationAsync(context, "FK_IdentificacoesAnimal_Animais_AnimalId",
            Guid.NewGuid(), 1, null, "FK-MISSING", false, true);

        await AssertConstraintViolationIsolatedAsync(context, "CK_IdentificacoesAnimal_Tipo",
            Guid.NewGuid(), 0, null, "TYPE-INVALID", false, true,
            "CK_IdentificacoesAnimal_DescricaoTipo_Semantics");
        await AssertConstraintViolationAsync(context, "CK_IdentificacoesAnimal_Valor_Canonical",
            Guid.NewGuid(), 1, null, "", false, true);
        await AssertConstraintViolationAsync(context, "CK_IdentificacoesAnimal_DescricaoTipo_Semantics",
            Guid.NewGuid(), 6, null, "OUTRO-SEM-DESCRICAO", false, true);

        var animal = await AddAnimalAsync(context, "AN-RESTRICT");
        await InsertAsync(context, animal.Id, 1, null, "RESTRICT-1", false, true);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(context,
            "DELETE FROM \"Animais\" WHERE \"Id\" = @animalId", new NpgsqlParameter("animalId", animal.Id)));

        Assert.Equal("FK_IdentificacoesAnimal_Animais_AnimalId", exception.ConstraintName);
    }

    [Fact]
    public async Task Migration_enforces_case_insensitive_historical_uniqueness_and_active_principal_rules()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var firstAnimal = await AddAnimalAsync(context, "AN-UNIQUE-1");
        var secondAnimal = await AddAnimalAsync(context, "AN-UNIQUE-2");

        await InsertAsync(context, firstAnimal.Id, 1, null, "BRINCO-CASE", false, true);
        await AssertConstraintViolationAsync(context, "UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive",
            secondAnimal.Id, 1, null, "brinco-case", false, true);

        await InsertAsync(context, firstAnimal.Id, 6, "Colar", "OUTRO-CASE", false, true);
        await AssertConstraintViolationAsync(context, OutroDescricaoTipoValorIndex,
            secondAnimal.Id, 6, "colar", "outro-case", false, true);

        await InsertAsync(context, firstAnimal.Id, 2, null, "HISTORICO-INATIVO", false, false);
        await AssertConstraintViolationAsync(context, "UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive",
            secondAnimal.Id, 2, null, "historico-inativo", false, true);

        await InsertAsync(context, firstAnimal.Id, 6, "Marca A", "OUTRO-COMPARTILHADO", false, true);
        await InsertAsync(context, secondAnimal.Id, 6, "Marca B", "outro-compartilhado", false, true);

        await InsertAsync(context, firstAnimal.Id, 3, null, "PRINCIPAL-1", true, true);
        await AssertConstraintViolationAsync(context, "UX_IdentificacoesAnimal_Animal_PrincipalAtiva",
            firstAnimal.Id, 4, null, "PRINCIPAL-2", true, true);
    }

    public Task DisposeAsync() => postgreSql.DisposeAsync().AsTask();

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(postgreSql.ConnectionString).Options);
    private static async Task<bool> ExistsAsync(GenSWDbContext context, string sql) => bool.Parse(await ScalarAsync(context, sql));
    private static Task<string> ConstraintDefinitionAsync(GenSWDbContext context, string name) => ScalarAsync(context, $"SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = '{name}'");
    private static Task<string> IndexDefinitionAsync(GenSWDbContext context, string name) => ScalarAsync(context, $"SELECT indexdef FROM pg_indexes WHERE indexname = '{name}'");
    private static Task<string> ColumnTypeAsync(GenSWDbContext context, string column) => ScalarAsync(context, $"SELECT data_type FROM information_schema.columns WHERE table_name = 'IdentificacoesAnimal' AND column_name = '{column}'");
    private static async Task<string> ScalarAsync(GenSWDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)!;
        }
        finally { await context.Database.CloseConnectionAsync(); }
    }

    private static async Task AssertConstraintViolationAsync(
        GenSWDbContext context, string constraintName, Guid animalId, int tipo, string? descricaoTipo,
        string valor, bool principal, bool ativo)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAsync(context, animalId, tipo, descricaoTipo, valor, principal, ativo));
        Assert.Equal(constraintName, exception.ConstraintName);
    }

    private static async Task AssertConstraintViolationIsolatedAsync(
        GenSWDbContext context, string constraintName, Guid animalId, int tipo, string? descricaoTipo,
        string valor, bool principal, bool ativo, string temporarilyDroppedConstraint)
    {
        await context.Database.OpenConnectionAsync();
        await using var transaction = await context.Database.GetDbConnection().BeginTransactionAsync();
        try
        {
            await using (var drop = context.Database.GetDbConnection().CreateCommand())
            {
                drop.Transaction = transaction;
                drop.CommandText = $"ALTER TABLE \"IdentificacoesAnimal\" DROP CONSTRAINT \"{temporarilyDroppedConstraint}\"";
                await drop.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertWithinTransactionAsync(
                context, transaction, animalId, tipo, descricaoTipo, valor, principal, ativo));
            Assert.Equal(constraintName, exception.ConstraintName);
        }
        finally
        {
            await transaction.RollbackAsync();
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task InsertWithinTransactionAsync(GenSWDbContext context, System.Data.Common.DbTransaction transaction,
        Guid animalId, int tipo, string? descricaoTipo, string valor, bool principal, bool ativo)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
        INSERT INTO "IdentificacoesAnimal"
            ("Id", "AnimalId", "Tipo", "DescricaoTipo", "Valor", "Principal", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
        VALUES
            (@id, @animalId, @tipo, @descricaoTipo, @valor, @principal, @ativo, @now, @now)
        """;
        command.Parameters.Add(new NpgsqlParameter("id", Guid.NewGuid()));
        command.Parameters.Add(new NpgsqlParameter("animalId", animalId));
        command.Parameters.Add(new NpgsqlParameter("tipo", tipo));
        command.Parameters.Add(new NpgsqlParameter("descricaoTipo", (object?)descricaoTipo ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("valor", valor));
        command.Parameters.Add(new NpgsqlParameter("principal", principal));
        command.Parameters.Add(new NpgsqlParameter("ativo", ativo));
        command.Parameters.Add(new NpgsqlParameter("now", DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync();
    }

    private static Task InsertAsync(GenSWDbContext context, Guid animalId, int tipo, string? descricaoTipo,
        string valor, bool principal, bool ativo) => ExecuteAsync(context, """
        INSERT INTO "IdentificacoesAnimal"
            ("Id", "AnimalId", "Tipo", "DescricaoTipo", "Valor", "Principal", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
        VALUES
            (@id, @animalId, @tipo, @descricaoTipo, @valor, @principal, @ativo, @now, @now)
        """,
        new NpgsqlParameter("id", Guid.NewGuid()),
        new NpgsqlParameter("animalId", animalId),
        new NpgsqlParameter("tipo", tipo),
        new NpgsqlParameter("descricaoTipo", (object?)descricaoTipo ?? DBNull.Value),
        new NpgsqlParameter("valor", valor),
        new NpgsqlParameter("principal", principal),
        new NpgsqlParameter("ativo", ativo),
        new NpgsqlParameter("now", DateTimeOffset.UtcNow));

    private static async Task ExecuteAsync(GenSWDbContext context, string sql, params NpgsqlParameter[] parameters)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters) command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
        }
        finally { await context.Database.CloseConnectionAsync(); }
    }

    private static async Task<Animal> AddAnimalAsync(GenSWDbContext context, string code)
    {
        var now = DateTimeOffset.UtcNow;
        var species = Especie.Criar($"Espécie {code}", null, now);
        context.Especies.Add(species);
        await context.SaveChangesAsync();
        var animal = Animal.Criar(code, null, species.Id, null, null, SexoAnimal.Indeterminado,
            null, EscopoAnimal.Operacional, DateOnly.FromDateTime(now.UtcDateTime), now);
        context.Animais.Add(animal);
        await context.SaveChangesAsync();
        return animal;
    }
}
