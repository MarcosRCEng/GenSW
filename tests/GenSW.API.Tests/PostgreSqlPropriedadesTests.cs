using System.Net;
using System.Text.Json;
using GenSW.Application.Animals;
using GenSW.Application.Properties;
using GenSW.Domain.Animals;
using GenSW.Domain.Properties;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class PostgreSqlPropriedadesTests(AnimalApiPostgreSqlFixture fixture)
{
    private readonly AuthWebApplicationFactory factory = fixture.Factory;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_first_links_and_transfers_return_one_success_one_stale_conflict_and_one_current_row(bool hasCurrent)
    {
        using var firstClient = await PropertyTestHttp.AuthenticateAsync(factory);
        using var secondClient = await PropertyTestHttp.AuthenticateAsync(factory);
        var animal = await PropertyTestHttp.SeedAnimalAsync(factory);
        var firstTarget = await PropertyTestHttp.CreatePropertyAsync(firstClient);
        var secondTarget = await PropertyTestHttp.CreatePropertyAsync(firstClient);
        Guid? expected = null;
        if (hasCurrent)
        {
            var initial = await PropertyTestHttp.CreatePropertyAsync(firstClient);
            var state = await PropertyTestHttp.TransferAsync(firstClient, animal.Id, initial, "2026-01-01", null);
            expected = state.GetProperty("atual").GetProperty("id").GetGuid();
        }
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = factory.ExecuteDbContextAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Animais\" WHERE \"Id\" = {animal.Id} FOR UPDATE;");
            locked.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await transaction.CommitAsync();
        });
        Task<HttpResponseMessage>? first = null;
        Task<HttpResponseMessage>? second = null;
        try
        {
            await WaitForBarrierAsync(locked.Task, holding);
            first = PropertyTestHttp.PostTransferAsync(firstClient, animal.Id, firstTarget, "2026-02-01", expected);
            second = PropertyTestHttp.PostTransferAsync(secondClient, animal.Id, secondTarget, "2026-02-01", expected);
            Assert.True(await WaitForBlockedAnimalMutationsAsync(2, first, second));
        }
        finally
        {
            release.TrySetResult();
            await holding;
        }
        var responses = await Task.WhenAll(first!, second!).WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("vinculo_desatualizado", (await PropertyTestHttp.ReadAsync(conflict)).GetProperty("code").GetString());
            var state = await PropertyTestHttp.StateAsync(firstClient, animal.Id);
            var history = state.GetProperty("historico").EnumerateArray().ToArray();
            Assert.Equal(hasCurrent ? 2 : 1, history.Length);
            var current = Assert.Single(history, row => row.GetProperty("dataFim").ValueKind == JsonValueKind.Null);
            Assert.Contains(current.GetProperty("propriedadeId").GetGuid(), new[] { firstTarget, secondTarget });
            if (hasCurrent)
                Assert.Equal("2026-02-01", history.Single(row => row.GetProperty("id").GetGuid() == expected).GetProperty("dataFim").GetString());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task Failed_insert_after_closing_the_old_link_rolls_back_and_observers_never_see_a_gap()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var animal = await PropertyTestHttp.SeedAnimalAsync(factory);
        var origin = await PropertyTestHttp.CreatePropertyAsync(client);
        var destination = await PropertyTestHttp.CreatePropertyAsync(client);
        var before = await PropertyTestHttp.TransferAsync(client, animal.Id, origin, "2026-01-01", null);
        var currentId = before.GetProperty("atual").GetProperty("id").GetGuid();
        await factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION "PropriedadesTestRejectInsert"() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Intentional isolated test failure' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER "TR_PropriedadesTestRejectInsert" BEFORE INSERT ON "VinculosAnimalPropriedade"
            FOR EACH ROW EXECUTE FUNCTION "PropriedadesTestRejectInsert"();
            """));
        var closed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<AnimalPropriedadesResult>? transfer = null;
        try
        {
            transfer = factory.ExecuteScopeAsync(async services =>
            {
                var db = services.GetRequiredService<GenSWDbContext>();
                var repository = new PauseAfterSaveRepository(services.GetRequiredService<IPropriedadeRepository>(), async () =>
                {
                    closed.SetResult(await db.Set<VinculoAnimalPropriedade>().CountAsync(row => row.AnimalId == animal.Id && row.DataFim == null));
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                });
                return await new PropriedadeService(repository, TimeProvider.System).TransferAsync(animal.Id,
                    new TransferirAnimalCommand(destination, new DateOnly(2026, 2, 1), currentId));
            });
            await WaitForBarrierAsync(closed.Task, transfer);
            Assert.Equal(0, await closed.Task);
            Assert.Equal(before.ToString(), (await PropertyTestHttp.StateAsync(client, animal.Id)).ToString());
            release.SetResult();
            var exception = await Assert.ThrowsAsync<DbUpdateException>(async () => await transfer);
            Assert.Equal("P0001", Assert.IsType<PostgresException>(exception.InnerException).SqlState);
            Assert.Equal(before.ToString(), (await PropertyTestHttp.StateAsync(client, animal.Id)).ToString());
        }
        finally
        {
            release.TrySetResult();
            if (transfer is not null) try { await transfer; } catch (DbUpdateException) { }
            await factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER IF EXISTS "TR_PropriedadesTestRejectInsert" ON "VinculosAnimalPropriedade";
                DROP FUNCTION IF EXISTS "PropriedadesTestRejectInsert"();
                """));
        }
    }

    [Fact]
    public async Task Partial_unique_index_rejects_a_second_current_row_even_when_bypassing_the_service()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var animal = await PropertyTestHttp.SeedAnimalAsync(factory);
        var origin = await PropertyTestHttp.CreatePropertyAsync(client);
        var destination = await PropertyTestHttp.CreatePropertyAsync(client);
        var before = await PropertyTestHttp.TransferAsync(client, animal.Id, origin, "2026-01-01", null);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => factory.ExecuteDbContextAsync(async db =>
        {
            db.Set<VinculoAnimalPropriedade>().Add(VinculoAnimalPropriedade.Criar(animal.Id, destination,
                new DateOnly(2026, 2, 1), null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        Assert.Equal(before.ToString(), (await PropertyTestHttp.StateAsync(client, animal.Id)).ToString());
    }

    [Fact]
    public async Task Transfer_waits_for_destination_inactivation_and_rejects_the_committed_inactive_destination()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(factory);
        var animal = await PropertyTestHttp.SeedAnimalAsync(factory);
        var destination = await PropertyTestHttp.CreatePropertyAsync(client);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inactivate = factory.ExecuteScopeAsync(async services =>
        {
            var repository = new PauseAfterSaveRepository(services.GetRequiredService<IPropriedadeRepository>(), async () =>
            {
                saved.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            });
            return await new PropriedadeService(repository, TimeProvider.System).SetActiveAsync(destination, false);
        });
        Task<HttpResponseMessage>? transfer = null;
        try
        {
            await WaitForBarrierAsync(saved.Task, inactivate);
            transfer = PropertyTestHttp.PostTransferAsync(client, animal.Id, destination, "2026-01-01", null);
            Assert.True(await WaitForBlockedMutationsAsync("Propriedades", 1, transfer));
        }
        finally
        {
            release.TrySetResult();
            await inactivate;
        }
        using var response = await transfer!;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("propriedade_inativa", (await PropertyTestHttp.ReadAsync(response)).GetProperty("code").GetString());
        Assert.Empty((await PropertyTestHttp.StateAsync(client, animal.Id)).GetProperty("historico").EnumerateArray());
    }

    [Fact]
    public async Task Additive_migration_preserves_existing_operational_reference_animals_and_filiation_without_creating_links()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync();
        var options = new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options;
        await using var db = new GenSWDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261002222623_AddFinancialCash");
        var now = DateTimeOffset.UtcNow;
        var species = GenSW.Domain.Species.Especie.Criar("Migração aditiva", null, now);
        var animal = Animal.Criar("MIG-PROP-OPERACIONAL", "Operacional", species.Id, null, null, SexoAnimal.Macho,
            null, EscopoAnimal.Operacional, DateOnly.FromDateTime(now.UtcDateTime), now);
        var reference = Animal.Criar("MIG-PROP-REFERENCIA", "Referência", species.Id, null, null, SexoAnimal.Macho,
            null, EscopoAnimal.Referencia, DateOnly.FromDateTime(now.UtcDateTime), now);
        var filiation = FiliacaoAnimal.Criar(animal.Id, reference.Id, TipoFiliacaoAnimal.Pai, null, now);
        db.AddRange(species, animal, reference, filiation);
        await db.SaveChangesAsync();
        var existingAnimals = await db.Animais.AsNoTracking().OrderBy(row => row.CodigoInterno).ToArrayAsync();
        var existingFiliation = await db.FiliacoesAnimal.AsNoTracking().SingleAsync();
        var before = JsonSerializer.Serialize(new { Animals = existingAnimals, Filiation = existingFiliation });
        var columnsBefore = await ReadExistingColumnsAsync(db);
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var after = JsonSerializer.Serialize(new
        {
            Animals = await db.Animais.AsNoTracking().OrderBy(row => row.CodigoInterno).ToArrayAsync(),
            Filiation = await db.FiliacoesAnimal.AsNoTracking().SingleAsync(),
        });
        Assert.Equal(before, after);
        Assert.Equal(columnsBefore, await ReadExistingColumnsAsync(db));
        Assert.Empty(await db.Set<Propriedade>().ToArrayAsync());
        Assert.Empty(await db.Set<VinculoAnimalPropriedade>().ToArrayAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private Task<bool> WaitForBlockedAnimalMutationsAsync(int expected, params Task[] operations) =>
        WaitForBlockedMutationsAsync("Animais", expected, operations);

    private async Task<bool> WaitForBlockedMutationsAsync(string table, int expected, params Task[] operations)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeout)
        {
            var count = await factory.ExecuteDbContextAsync(async db =>
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT count(*)::integer FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE @table;";
                command.Parameters.Add(new NpgsqlParameter("table", $"%\"{table}\"%"));
                return (int)(await command.ExecuteScalarAsync())!;
            });
            if (count >= expected) return true;
            if (operations.Any(operation => operation.IsCompleted)) return false;
            await Task.Delay(25);
        }
        return false;
    }

    private static async Task WaitForBarrierAsync(Task barrier, Task operation)
    {
        var completed = await Task.WhenAny(barrier, operation).WaitAsync(TimeSpan.FromSeconds(15));
        if (completed == operation) await operation;
        await barrier.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static async Task<string[]> ReadExistingColumnsAsync(GenSWDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT table_name || '.' || column_name || ':' || data_type || ':' || is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name NOT IN ('Propriedades', 'VinculosAnimalPropriedade')
            ORDER BY table_name, ordinal_position;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values.ToArray();
    }

    private sealed class PauseAfterSaveRepository(IPropriedadeRepository inner, Func<Task> afterFirstSave) : IPropriedadeRepository
    {
        private bool paused;
        public Task AddAsync(Propriedade item, CancellationToken ct) => inner.AddAsync(item, ct);
        public Task<Propriedade?> GetAsync(Guid id, bool tracking, CancellationToken ct) => inner.GetAsync(id, tracking, ct);
        public Task<PropriedadePage> ListAsync(PropriedadeListQuery query, CancellationToken ct) => inner.ListAsync(query, ct);
        public Task<bool> NomeExistsAsync(string nome, Guid? excludingId, CancellationToken ct) => inner.NomeExistsAsync(nome, excludingId, ct);
        public Task<IAnimalMutationScope> BeginMutationAsync(Guid? animalId, CancellationToken ct) => inner.BeginMutationAsync(animalId, ct);
        public Task<Propriedade?> LockPropriedadeAsync(Guid id, CancellationToken ct) => inner.LockPropriedadeAsync(id, ct);
        public Task<bool> AnimalExistsAsync(Guid id, CancellationToken ct) => inner.AnimalExistsAsync(id, ct);
        public Task<VinculoAnimalPropriedade?> GetCurrentAsync(Guid animalId, CancellationToken ct) => inner.GetCurrentAsync(animalId, ct);
        public Task<DateOnly?> GetLastEndAsync(Guid animalId, CancellationToken ct) => inner.GetLastEndAsync(animalId, ct);
        public Task<IReadOnlyList<VinculoAnimalPropriedadeResult>> HistoryAsync(Guid animalId, CancellationToken ct) => inner.HistoryAsync(animalId, ct);
        public Task AddVinculoAsync(VinculoAnimalPropriedade item, CancellationToken ct) => inner.AddVinculoAsync(item, ct);
        public async Task SaveAsync(CancellationToken ct)
        {
            await inner.SaveAsync(ct);
            if (paused) return;
            paused = true;
            await afterFirstSave();
        }
    }
}
