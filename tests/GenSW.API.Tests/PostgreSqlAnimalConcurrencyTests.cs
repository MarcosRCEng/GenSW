using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlAnimalIntegrationCollection : ICollectionFixture<AnimalApiPostgreSqlFixture>
{
    public const string Name = "PostgreSQL animal integration";
}

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class PostgreSqlAnimalConcurrencyTests(AnimalApiPostgreSqlFixture fixture) : IAsyncLifetime
{
    private readonly AuthWebApplicationFactory factory = fixture.Factory;

    public async Task InitializeAsync() => await ResetAnimalDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Concurrent_automatic_posts_return_201_with_distinct_real_PostgreSQL_codes()
    {
        var classifications = await SeedClassificationsAsync();
        using var firstClient = await CreateAuthenticatedClientAsync("automatic_first");
        using var secondClient = await CreateAuthenticatedClientAsync("automatic_second");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = PostWhenReleasedAsync(firstClient, classifications, start.Task);
        var second = PostWhenReleasedAsync(secondClient, classifications, start.Task);
        start.SetResult();
        var responses = await Task.WhenAll(
            first,
            second);

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var codes = await Task.WhenAll(responses.Select(ReadCodigoInternoAsync));
            Assert.Equal(2, codes.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(new[] { "AN-000001", "AN-000002" }, codes.OrderBy(code => code, StringComparer.Ordinal));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Manual_AN_000001_makes_the_automatic_path_skip_to_AN_000002()
    {
        var classifications = await SeedClassificationsAsync();
        using var client = await CreateAuthenticatedClientAsync("manual_then_automatic");
        using var manual = await client.PostAsJsonAsync(
            "/api/v1/animais",
            CreateRequest("AN-000001", classifications, "Manual first"));
        using var automatic = await client.PostAsJsonAsync(
            "/api/v1/animais",
            CreateRequest(null, classifications, "Automatic second"));

        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);
        Assert.Equal(HttpStatusCode.Created, automatic.StatusCode);
        Assert.Equal("AN-000002", await ReadCodigoInternoAsync(automatic));
        Assert.Equal(new SequenceState(2, true), await GetSequenceStateAsync());
    }

    [Fact]
    public async Task Concurrent_equal_manual_codes_return_201_and_409_without_an_allocator_attempt()
    {
        var classifications = await SeedClassificationsAsync();
        using var firstClient = await CreateAuthenticatedClientAsync("manual_first");
        using var secondClient = await CreateAuthenticatedClientAsync("manual_second");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = PostWhenReleasedAsync(firstClient, CreateRequest("AN-MANUAL-CONCURRENT", classifications, "First"), start.Task);
        var second = PostWhenReleasedAsync(secondClient, CreateRequest("AN-MANUAL-CONCURRENT", classifications, "Second"), start.Task);
        start.SetResult();
        var responses = await Task.WhenAll(first, second);

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(new SequenceState(1, false), await GetSequenceStateAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Put_duplicate_code_returns_409_and_keeps_the_persisted_code_unchanged()
    {
        var classifications = await SeedClassificationsAsync();
        using var client = await CreateAuthenticatedClientAsync("put_duplicate");
        var first = await CreateAnimalAsync(client, "AN-PUT-FIRST", classifications, "First");
        _ = await CreateAnimalAsync(client, "AN-PUT-SECOND", classifications, "Second");

        using var update = await client.PutAsJsonAsync(
            $"/api/v1/animais/{first.Id:D}",
            CreateRequest("AN-PUT-SECOND", classifications, "Changed"));
        using var persisted = await client.GetAsync($"/api/v1/animais/{first.Id:D}");

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, persisted.StatusCode);
        Assert.Equal("AN-PUT-FIRST", await ReadCodigoInternoAsync(persisted));
    }

    [Fact]
    public async Task Five_named_collisions_return_409_and_do_not_allocate_a_sixth_candidate()
    {
        var classifications = await SeedClassificationsAsync();
        using var client = await CreateAuthenticatedClientAsync("five_collisions");

        foreach (var number in Enumerable.Range(1, 5))
        {
            using var manual = await client.PostAsJsonAsync(
                "/api/v1/animais",
                CreateRequest($"AN-{number:D6}", classifications, $"Manual {number}"));
            Assert.Equal(HttpStatusCode.Created, manual.StatusCode);
        }

        using var automatic = await client.PostAsJsonAsync(
            "/api/v1/animais",
            CreateRequest(null, classifications, "Automatic collision limit"));

        Assert.Equal(HttpStatusCode.Conflict, automatic.StatusCode);
        Assert.Equal(new SequenceState(5, true), await GetSequenceStateAsync());
        Assert.Equal(5, await CountAnimalsAsync());
    }

    [Fact]
    public async Task Named_collision_is_retry_eligible_but_another_23505_is_not_translated_or_retried()
    {
        var classifications = await SeedClassificationsAsync();
        await SeedAnimalDirectlyAsync("AN-000001", "Named collision", classifications);

        var namedResult = await CreateAutomaticThroughServiceAsync(classifications, "Named collision retry");

        Assert.Equal("AN-000002", namedResult.CodigoInterno);
        Assert.Equal(new SequenceState(2, true), await GetSequenceStateAsync());

        await ResetAnimalDatabaseAsync();
        classifications = await SeedClassificationsAsync();
        await SeedAnimalDirectlyAsync("AN-OTHER-SEED", "Other unique collision", classifications);
        await factory.ExecuteDbContextAsync(context => context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX \"UX_Animais_Task8_Nome\" ON \"Animais\" (\"Nome\");"));

        try
        {
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => CreateAutomaticThroughServiceAsync(classifications, "Other unique collision"));
            var postgres = Assert.IsType<PostgresException>(exception.InnerException);

            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal("UX_Animais_Task8_Nome", postgres.ConstraintName);
            Assert.Equal(new SequenceState(1, true), await GetSequenceStateAsync());
        }
        finally
        {
            await factory.ExecuteDbContextAsync(context => context.Database.ExecuteSqlRawAsync(
                "DROP INDEX IF EXISTS \"UX_Animais_Task8_Nome\";"));
        }
    }

    [Fact]
    public async Task Non_23505_trigger_failure_uses_one_allocation_and_never_retries()
    {
        var classifications = await SeedClassificationsAsync();
        await factory.ExecuteDbContextAsync(context => context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION "Task8RejectAnimalInsert"() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Task 8 forced failure' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER "TR_Task8RejectAnimalInsert"
            BEFORE INSERT ON "Animais"
            FOR EACH ROW EXECUTE FUNCTION "Task8RejectAnimalInsert"();
            """));

        try
        {
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => CreateAutomaticThroughServiceAsync(classifications, "Trigger failure"));
            var postgres = Assert.IsType<PostgresException>(exception.InnerException);

            Assert.Equal("P0001", postgres.SqlState);
            Assert.Equal(new SequenceState(1, true), await GetSequenceStateAsync());
        }
        finally
        {
            await factory.ExecuteDbContextAsync(context => context.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER IF EXISTS "TR_Task8RejectAnimalInsert" ON "Animais";
                DROP FUNCTION IF EXISTS "Task8RejectAnimalInsert"();
                """));
        }
    }

    [Fact]
    public async Task Failed_first_automatic_candidate_consumes_its_sequence_value_and_the_gap_is_accepted()
    {
        var classifications = await SeedClassificationsAsync();
        await SeedAnimalDirectlyAsync("AN-000001", "Existing first candidate", classifications);

        var automatic = await CreateAutomaticThroughServiceAsync(classifications, "Gap accepted");

        Assert.Equal("AN-000002", automatic.CodigoInterno);
        Assert.Equal(new SequenceState(2, true), await GetSequenceStateAsync());
    }

    [Fact]
    public async Task Rollback_detaches_the_aborted_attempt_and_starts_a_different_usable_transaction()
    {
        var classifications = await SeedClassificationsAsync();
        await SeedAnimalDirectlyAsync("AN-000001", "Existing first candidate", classifications);

        await factory.ExecuteScopeAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();
            var allocator = services.GetRequiredService<IAnimalCodeAllocator>();
            var repository = services.GetRequiredService<IAnimalRepository>();
            long firstTransactionId;

            await using (var firstAttempt = await allocator.BeginAttemptAsync())
            {
                var candidate = await firstAttempt.AllocateNextCodigoInternoAsync();
                firstTransactionId = await GetCurrentTransactionIdAsync(context);
                var failedAnimal = CreateDomainAnimal(candidate, classifications, "Will collide");
                await repository.AddAsync(failedAnimal);

                var collision = await Assert.ThrowsAsync<AnimalDuplicateException>(() => repository.SaveChangesAsync());
                Assert.Equal(candidate, collision.CodigoInterno);
                Assert.Equal(AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint, collision.ConflictSource);

                await firstAttempt.RollbackAndDetachAsync(failedAnimal);
            }

            Assert.Null(context.Database.CurrentTransaction);

            await using (var secondAttempt = await allocator.BeginAttemptAsync())
            {
                var secondTransactionId = await GetCurrentTransactionIdAsync(context);
                Assert.NotEqual(firstTransactionId, secondTransactionId);
                var candidate = await secondAttempt.AllocateNextCodigoInternoAsync();
                Assert.Equal("AN-000002", candidate);
                await repository.AddAsync(CreateDomainAnimal(candidate, classifications, "Second transaction"));
                await repository.SaveChangesAsync();
                await secondAttempt.CommitAsync();
            }

            Assert.Null(context.Database.CurrentTransaction);
            return 0;
        });

        Assert.Equal(new SequenceState(2, true), await GetSequenceStateAsync());
    }

    [Fact]
    public async Task Direct_SQL_rejects_breed_and_variety_from_another_species_through_their_composite_foreign_keys()
    {
        var classifications = await SeedClassificationsAsync();
        var otherSpecies = await SeedSpeciesAsync("Other species");

        var breedViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync($"""
            INSERT INTO "Animais" ("Id", "CodigoInterno", "EspecieId", "RacaId", "VariedadeId", "Sexo", "Escopo", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ('{Guid.NewGuid():D}', 'AN-CROSS-BREED', '{otherSpecies.Id:D}', '{classifications.Breed.Id:D}', NULL, 3, 1, TRUE, NOW(), NOW());
            """));
        var varietyViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync($"""
            INSERT INTO "Animais" ("Id", "CodigoInterno", "EspecieId", "RacaId", "VariedadeId", "Sexo", "Escopo", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ('{Guid.NewGuid():D}', 'AN-CROSS-VARIETY', '{otherSpecies.Id:D}', NULL, '{classifications.Variety.Id:D}', 3, 1, TRUE, NOW(), NOW());
            """));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, breedViolation.SqlState);
        Assert.Equal("FK_Animais_Racas_RacaId_EspecieId", breedViolation.ConstraintName);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, varietyViolation.SqlState);
        Assert.Equal("FK_Animais_Variedades_VariedadeId_EspecieId", varietyViolation.ConstraintName);
    }

    [Fact]
    public async Task Referenced_breed_and_variety_cannot_move_species_through_the_API_or_PostgreSQL_restrict_keys()
    {
        var classifications = await SeedClassificationsAsync();
        var otherSpecies = await SeedSpeciesAsync("Other species");
        await SeedAnimalDirectlyAsync("AN-REFERENCED", "Referenced", classifications);
        using var client = await CreateAuthenticatedClientAsync("referenced_classifications");

        using var breedApi = await client.PutAsJsonAsync(
            $"/api/v1/racas/{classifications.Breed.Id:D}",
            new { especieId = otherSpecies.Id, nome = classifications.Breed.Nome });
        using var varietyApi = await client.PutAsJsonAsync(
            $"/api/v1/variedades/{classifications.Variety.Id:D}",
            new { especieId = otherSpecies.Id, nome = classifications.Variety.Nome });

        Assert.Equal(HttpStatusCode.Conflict, breedApi.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, varietyApi.StatusCode);

        var breedViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE \"Racas\" SET \"EspecieId\" = '{otherSpecies.Id:D}' WHERE \"Id\" = '{classifications.Breed.Id:D}';"));
        var varietyViolation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE \"Variedades\" SET \"EspecieId\" = '{otherSpecies.Id:D}' WHERE \"Id\" = '{classifications.Variety.Id:D}';"));

        Assert.Equal("FK_Animais_Racas_RacaId_EspecieId", breedViolation.ConstraintName);
        Assert.Equal("FK_Animais_Variedades_VariedadeId_EspecieId", varietyViolation.ConstraintName);
    }

    private async Task ResetAnimalDatabaseAsync()
    {
        await factory.ExecuteDbContextAsync(async context =>
        {
            await context.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE \"Animais\", \"Racas\", \"Variedades\", \"Especies\" CASCADE;");
            await context.Database.ExecuteSqlRawAsync(
                "ALTER SEQUENCE \"AnimalCodigoInternoSequence\" RESTART WITH 1;");
        });
    }

    private static async Task<HttpResponseMessage> PostWhenReleasedAsync(
        HttpClient client,
        TestClassifications classifications,
        Task release) => await PostWhenReleasedAsync(client, CreateRequest(null, classifications, "Concurrent automatic"), release);

    private static async Task<HttpResponseMessage> PostWhenReleasedAsync(HttpClient client, object request, Task release)
    {
        await release;
        return await client.PostAsJsonAsync("/api/v1/animais", request);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string prefix)
    {
        var user = await factory.SeedUserAsync($"{prefix}_{Guid.NewGuid():N}");
        var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var (token, _) = await login.ReadAccessTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<TestClassifications> SeedClassificationsAsync()
    {
        var species = await SeedSpeciesAsync("Species");
        var now = DateTimeOffset.UtcNow;
        var breed = Raca.Criar(species.Id, "Breed " + Guid.NewGuid().ToString("N"), now);
        var variety = Variedade.Criar(species.Id, "Variety " + Guid.NewGuid().ToString("N"), now);
        await factory.ExecuteDbContextAsync(async context =>
        {
            context.AddRange(breed, variety);
            await context.SaveChangesAsync();
        });
        return new TestClassifications(species, breed, variety);
    }

    private async Task<Especie> SeedSpeciesAsync(string prefix)
    {
        var species = Especie.Criar(prefix + " " + Guid.NewGuid().ToString("N"), null, DateTimeOffset.UtcNow);
        await factory.ExecuteDbContextAsync(async context =>
        {
            context.Especies.Add(species);
            await context.SaveChangesAsync();
        });
        return species;
    }

    private async Task SeedAnimalDirectlyAsync(string code, string name, TestClassifications classifications)
    {
        await factory.ExecuteDbContextAsync(async context =>
        {
            context.Animais.Add(CreateDomainAnimal(code, classifications, name));
            await context.SaveChangesAsync();
        });
    }

    private async Task<AnimalResult> CreateAutomaticThroughServiceAsync(TestClassifications classifications, string name) =>
        await factory.ExecuteScopeAsync(async services =>
        {
            var service = services.GetRequiredService<IAnimalService>();
            return await service.CreateAsync(new CreateAnimalCommand(
                null,
                name,
                classifications.Species.Id,
                classifications.Breed.Id,
                classifications.Variety.Id,
                SexoAnimal.Femea,
                new DateOnly(2020, 1, 2),
                EscopoAnimal.Operacional));
        });

    private static Animal CreateDomainAnimal(string code, TestClassifications classifications, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return Animal.Criar(
            code,
            name,
            classifications.Species.Id,
            classifications.Breed.Id,
            classifications.Variety.Id,
            SexoAnimal.Femea,
            new DateOnly(2020, 1, 2),
            EscopoAnimal.Operacional,
            DateOnly.FromDateTime(now.UtcDateTime),
            now);
    }

    private static object CreateRequest(string? code, TestClassifications classifications, string name) => new
    {
        codigoInterno = code,
        nome = name,
        especieId = classifications.Species.Id,
        racaId = classifications.Breed.Id,
        variedadeId = classifications.Variety.Id,
        sexo = 2,
        dataNascimento = "2020-01-02",
        escopo = 1,
    };

    private static async Task<(Guid Id, string CodigoInterno)> CreateAnimalAsync(
        HttpClient client,
        string code,
        TestClassifications classifications,
        string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/animais", CreateRequest(code, classifications, name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (document.RootElement.GetProperty("id").GetGuid(), document.RootElement.GetProperty("codigoInterno").GetString()!);
    }

    private static async Task<string> ReadCodigoInternoAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("codigoInterno").GetString()!;
    }

    private async Task<SequenceState> GetSequenceStateAsync() => await factory.ExecuteDbContextAsync(async context =>
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT last_value, is_called FROM \"AnimalCodigoInternoSequence\";";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return new SequenceState(reader.GetInt64(0), reader.GetBoolean(1));
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    });

    private async Task<int> CountAnimalsAsync() => await factory.ExecuteDbContextAsync(
        context => context.Animais.CountAsync());

    private async Task ExecuteSqlAsync(string sql) => await factory.ExecuteDbContextAsync(async context =>
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
    });

    private static async Task<long> GetCurrentTransactionIdAsync(GenSWDbContext context)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT txid_current();";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private sealed record TestClassifications(Especie Species, Raca Breed, Variedade Variety);

    private sealed record SequenceState(long Value, bool IsCalled);
}
