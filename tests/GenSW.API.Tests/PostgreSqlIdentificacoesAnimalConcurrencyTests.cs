using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class PostgreSqlIdentificacoesAnimalConcurrencyTests(AnimalApiPostgreSqlFixture fixture)
    : IAsyncLifetime
{
    private readonly AuthWebApplicationFactory factory = fixture.Factory;

    public async Task InitializeAsync() => await factory.ExecuteDbContextAsync(context =>
        context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"IdentificacoesAnimal\", \"Animais\", \"Racas\", \"Variedades\", \"Especies\" CASCADE;"));

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_B_valid_switch_flushes_the_old_principal_without_exposing_the_intermediate_state()
    {
        var seeded = await SeedAsync(("A-SWITCH", true, true), ("B-SWITCH", true, false));
        var firstFlush = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var switchOperation = factory.ExecuteScopeAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();
            var inner = services.GetRequiredService<IIdentificacaoAnimalRepository>();
            var repository = new FirstFlushBarrierRepository(inner, async () =>
            {
                var count = await context.IdentificacoesAnimal.AsNoTracking()
                    .CountAsync(item => item.AnimalId == seeded.AnimalId && item.Ativo && item.Principal);
                firstFlush.SetResult(count);
                await release.Task;
            });
            return await new IdentificacaoAnimalService(repository, TimeProvider.System).SetPrincipalAsync(
                seeded.AnimalId, seeded.Ids["B-SWITCH"], new SetIdentificacaoAnimalPrincipalCommand(true));
        });

        int sameTransactionPrincipalCount;
        string[] observerBeforeCommit;
        try
        {
            sameTransactionPrincipalCount = await WaitForBarrierAsync(firstFlush.Task, switchOperation);
            observerBeforeCommit = await ReadActivePrincipalsAsync(seeded.AnimalId);
        }
        finally
        {
            release.TrySetResult();
            await DrainIgnoringFailureAsync(switchOperation);
        }
        var result = await switchOperation;
        var committed = await ReadActivePrincipalsAsync(seeded.AnimalId);

        Assert.Equal(0, sameTransactionPrincipalCount);
        Assert.Equal(["A-SWITCH"], observerBeforeCommit);
        Assert.True(result.Principal);
        Assert.Equal("B-SWITCH", result.Valor);
        Assert.Equal(["B-SWITCH"], committed);
    }

    [Fact]
    public async Task C_failed_promotion_after_the_first_flush_rolls_back_and_restores_A()
    {
        var seeded = await SeedAsync(("A-ROLLBACK", true, true), ("B-ROLLBACK", true, false));
        var firstFlush = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await InstallFailureTriggerAsync("UPDATE", "B-ROLLBACK", "Task8FailPromotion");
        Task<IdentificacaoAnimalResult>? operation = null;

        try
        {
            operation = factory.ExecuteScopeAsync(async services =>
            {
                var context = services.GetRequiredService<GenSWDbContext>();
                var inner = services.GetRequiredService<IIdentificacaoAnimalRepository>();
                var repository = new FirstFlushBarrierRepository(inner, async () =>
                {
                    firstFlush.SetResult(await context.IdentificacoesAnimal.AsNoTracking()
                        .CountAsync(item => item.AnimalId == seeded.AnimalId && item.Ativo && item.Principal));
                    await release.Task;
                });
                return await new IdentificacaoAnimalService(repository, TimeProvider.System).SetPrincipalAsync(
                    seeded.AnimalId, seeded.Ids["B-ROLLBACK"], new SetIdentificacaoAnimalPrincipalCommand(true));
            });

            try
            {
                Assert.Equal(0, await WaitForBarrierAsync(firstFlush.Task, operation));
                Assert.Equal(["A-ROLLBACK"], await ReadActivePrincipalsAsync(seeded.AnimalId));
            }
            finally
            {
                release.TrySetResult();
            }

            var exception = await Assert.ThrowsAsync<DbUpdateException>(async () => await operation);

            Assert.Equal("P0001", Assert.IsType<PostgresException>(exception.InnerException).SqlState);
            Assert.Equal(["A-ROLLBACK"], await ReadActivePrincipalsAsync(seeded.AnimalId));
            Assert.False(await ReadPrincipalFlagAsync(seeded.Ids["B-ROLLBACK"]));
        }
        finally
        {
            release.TrySetResult();
            if (operation is not null) await DrainIgnoringFailureAsync(operation);
            await DropFailureTriggerAsync("Task8FailPromotion");
        }
    }

    [Fact]
    public async Task D_create_principal_flushes_the_old_row_then_inserts_and_promotes_the_new_row()
    {
        var seeded = await SeedAsync(("A-CREATE", true, true));
        var firstFlush = new TaskCompletionSource<CreateFirstFlushState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var createOperation = factory.ExecuteScopeAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();
            var inner = services.GetRequiredService<IIdentificacaoAnimalRepository>();
            var repository = new FirstFlushBarrierRepository(inner, async () =>
            {
                var activePrincipalCount = await context.IdentificacoesAnimal.AsNoTracking()
                    .CountAsync(item => item.AnimalId == seeded.AnimalId && item.Ativo && item.Principal);
                var newRowExists = await context.IdentificacoesAnimal.AsNoTracking()
                    .AnyAsync(item => item.Valor == "B-CREATE");
                firstFlush.SetResult(new CreateFirstFlushState(activePrincipalCount, newRowExists));
                await release.Task;
            });
            return await new IdentificacaoAnimalService(repository, TimeProvider.System).CreateAsync(
                seeded.AnimalId, CreateCommand("B-CREATE", principal: true));
        });

        CreateFirstFlushState sameTransactionState;
        string[] observerBeforeCommit;
        try
        {
            sameTransactionState = await WaitForBarrierAsync(firstFlush.Task, createOperation);
            observerBeforeCommit = await ReadActivePrincipalsAsync(seeded.AnimalId);
        }
        finally
        {
            release.TrySetResult();
            await DrainIgnoringFailureAsync(createOperation);
        }
        var created = await createOperation;

        Assert.Equal(new CreateFirstFlushState(0, false), sameTransactionState);
        Assert.Equal(["A-CREATE"], observerBeforeCommit);
        Assert.True(created.Principal);
        Assert.Equal(["B-CREATE"], await ReadActivePrincipalsAsync(seeded.AnimalId));
        Assert.False(await ReadPrincipalFlagAsync(seeded.Ids["A-CREATE"]));
    }

    [Fact]
    public async Task E_failed_new_principal_insert_rolls_back_and_restores_the_previous_principal()
    {
        var seeded = await SeedAsync(("A-CREATE-ROLLBACK", true, true));
        var firstFlush = new TaskCompletionSource<CreateFirstFlushState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await InstallFailureTriggerAsync("INSERT", "B-CREATE-ROLLBACK", "Task8FailPrincipalInsert");
        Task<IdentificacaoAnimalResult>? operation = null;

        try
        {
            operation = factory.ExecuteScopeAsync(async services =>
            {
                var context = services.GetRequiredService<GenSWDbContext>();
                var inner = services.GetRequiredService<IIdentificacaoAnimalRepository>();
                var repository = new FirstFlushBarrierRepository(inner, async () =>
                {
                    var activePrincipalCount = await context.IdentificacoesAnimal.AsNoTracking()
                        .CountAsync(item => item.AnimalId == seeded.AnimalId && item.Ativo && item.Principal);
                    var newRowExists = await context.IdentificacoesAnimal.AsNoTracking()
                        .AnyAsync(item => item.Valor == "B-CREATE-ROLLBACK");
                    firstFlush.SetResult(new CreateFirstFlushState(activePrincipalCount, newRowExists));
                    await release.Task;
                });
                return await new IdentificacaoAnimalService(repository, TimeProvider.System).CreateAsync(
                    seeded.AnimalId, CreateCommand("B-CREATE-ROLLBACK", principal: true));
            });

            try
            {
                Assert.Equal(new CreateFirstFlushState(0, false),
                    await WaitForBarrierAsync(firstFlush.Task, operation));
                Assert.Equal(["A-CREATE-ROLLBACK"], await ReadActivePrincipalsAsync(seeded.AnimalId));
            }
            finally
            {
                release.TrySetResult();
            }

            var exception = await Assert.ThrowsAsync<DbUpdateException>(async () => await operation);

            Assert.Equal("P0001", Assert.IsType<PostgresException>(exception.InnerException).SqlState);
            Assert.Equal(["A-CREATE-ROLLBACK"], await ReadActivePrincipalsAsync(seeded.AnimalId));
            Assert.False(await IdentificationExistsAsync("B-CREATE-ROLLBACK"));
        }
        finally
        {
            release.TrySetResult();
            if (operation is not null) await DrainIgnoringFailureAsync(operation);
            await DropFailureTriggerAsync("Task8FailPrincipalInsert");
        }
    }

    [Fact]
    public async Task F_same_animal_competing_switches_wait_on_the_parent_row_and_leave_exactly_one_principal()
    {
        var seeded = await SeedAsync(
            ("A-CONCURRENT", true, true),
            ("B-CONCURRENT", true, false),
            ("C-CONCURRENT", true, false));
        var firstHasLock = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTriedLock = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = factory.ExecuteScopeAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();
            var inner = services.GetRequiredService<IIdentificacaoAnimalRepository>();
            var repository = new LockBarrierRepository(inner, beforeLock: null, afterLock: async () =>
            {
                firstHasLock.SetResult(await GetBackendProcessIdAsync(context));
                await releaseFirst.Task;
            });
            return await new IdentificacaoAnimalService(repository, TimeProvider.System).SetPrincipalAsync(
                seeded.AnimalId, seeded.Ids["B-CONCURRENT"], new SetIdentificacaoAnimalPrincipalCommand(true));
        });
        Task<IdentificacaoAnimalResult>? second = null;
        var databaseBlockingObserved = false;

        try
        {
            var firstBackendPid = await WaitForBarrierAsync(firstHasLock.Task, first);
            second = factory.ExecuteScopeAsync(async services =>
            {
                var context = services.GetRequiredService<GenSWDbContext>();
                var inner = services.GetRequiredService<IIdentificacaoAnimalRepository>();
                var repository = new LockBarrierRepository(inner,
                    beforeLock: async () => secondTriedLock.SetResult(await GetBackendProcessIdAsync(context)),
                    afterLock: () => { secondHasLock.SetResult(); return Task.CompletedTask; });
                return await new IdentificacaoAnimalService(repository, TimeProvider.System).SetPrincipalAsync(
                    seeded.AnimalId, seeded.Ids["C-CONCURRENT"], new SetIdentificacaoAnimalPrincipalCommand(true));
            });

            var secondBackendPid = await WaitForBarrierAsync(secondTriedLock.Task, second);
            databaseBlockingObserved = await WaitUntilBlockedByAsync(secondBackendPid, firstBackendPid, second);
            Assert.False(secondHasLock.Task.IsCompleted);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await DrainIgnoringFailureAsync(first, second);
        }

        await Task.WhenAll(first, second!);

        Assert.True(databaseBlockingObserved);
        Assert.Equal(["C-CONCURRENT"], await ReadActivePrincipalsAsync(seeded.AnimalId));
    }

    [Fact]
    public async Task G_paused_service_mutation_for_animal_A_does_not_block_a_principal_mutation_for_animal_B()
    {
        var animalA = await SeedAsync(("A1-PRINCIPAL", true, true), ("A2-TARGET", true, false));
        var animalB = await SeedAsync(("B1-PRINCIPAL", true, true), ("B2-TARGET", true, false));
        var animalALockHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAnimalA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var mutationA = ExecuteWithRepositoryAsync(
            inner => new LockBarrierRepository(inner, beforeLock: null, afterLock: async () =>
            {
                animalALockHeld.SetResult();
                await releaseAnimalA.Task;
            }),
            service => service.SetPrincipalAsync(animalA.AnimalId, animalA.Ids["A2-TARGET"],
                new SetIdentificacaoAnimalPrincipalCommand(true)));
        Task<IdentificacaoAnimalResult>? mutationB = null;
        var mutationBCompletedBeforeARelease = false;

        try
        {
            await WaitForBarrierAsync(animalALockHeld.Task, mutationA);
            mutationB = ExecuteServiceAsync(service => service.SetPrincipalAsync(
                animalB.AnimalId, animalB.Ids["B2-TARGET"], new SetIdentificacaoAnimalPrincipalCommand(true)));
            var switchedB = await mutationB
                .WaitAsync(TimeSpan.FromSeconds(5));

            mutationBCompletedBeforeARelease = switchedB.Principal && !releaseAnimalA.Task.IsCompleted;
        }
        finally
        {
            releaseAnimalA.TrySetResult();
            await DrainIgnoringFailureAsync(mutationA, mutationB);
        }

        await Task.WhenAll(mutationA, mutationB!);
        Assert.True(mutationBCompletedBeforeARelease);
        Assert.Equal(["A2-TARGET"], await ReadActivePrincipalsAsync(animalA.AnimalId));
        Assert.Equal(["B2-TARGET"], await ReadActivePrincipalsAsync(animalB.AnimalId));
    }

    [Fact]
    public async Task Normalized_case_insensitive_marker_duplicate_is_rejected()
    {
        var firstAnimal = await SeedAsync();
        var secondAnimal = await SeedAsync();
        _ = await ExecuteServiceAsync(service => service.CreateAsync(
            firstAnimal.AnimalId, CreateCommand("  DUPLICATE-MARKER  ", principal: false)));

        var exception = await Assert.ThrowsAsync<IdentificacaoAnimalDuplicateException>(() =>
            ExecuteServiceAsync(service => service.CreateAsync(
                secondAnimal.AnimalId, CreateCommand("duplicate-marker", principal: false))));

        Assert.Equal(IdentificacaoAnimalDuplicateConflictSource.PreCheck, exception.ConflictSource);
        Assert.Equal(1, await CountIdentificationsAsync("duplicate-marker"));
    }

    [Fact]
    public async Task Inactive_identification_cannot_be_promoted_and_keeps_the_existing_principal()
    {
        var seeded = await SeedAsync(("ACTIVE-PRINCIPAL", true, true), ("INACTIVE-TARGET", false, false));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteServiceAsync(service =>
            service.SetPrincipalAsync(seeded.AnimalId, seeded.Ids["INACTIVE-TARGET"],
                new SetIdentificacaoAnimalPrincipalCommand(true))));

        Assert.Equal(["ACTIVE-PRINCIPAL"], await ReadActivePrincipalsAsync(seeded.AnimalId));
        Assert.False(await ReadPrincipalFlagAsync(seeded.Ids["INACTIVE-TARGET"]));
    }

    private async Task<bool> WaitUntilBlockedByAsync(int waitingBackendPid, int blockingBackendPid, Task operation)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await IsBlockedByAsync(waitingBackendPid, blockingBackendPid)) return true;
            if (operation.IsCompleted)
            {
                await operation;
                return false;
            }
            await Task.Delay(25);
        }
        return false;
    }

    private Task<bool> IsBlockedByAsync(int waitingBackendPid, int blockingBackendPid) =>
        factory.ExecuteDbContextAsync(async context =>
        {
            await context.Database.OpenConnectionAsync();
            try
            {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT @blocking_pid = ANY(pg_blocking_pids(@waiting_pid));";
                command.Parameters.Add(new NpgsqlParameter("blocking_pid", blockingBackendPid));
                command.Parameters.Add(new NpgsqlParameter("waiting_pid", waitingBackendPid));
                return (bool)(await command.ExecuteScalarAsync())!;
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        });

    private static async Task<int> GetBackendProcessIdAsync(GenSWDbContext context)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<T> WaitForBarrierAsync<T>(Task<T> barrier, Task operation)
    {
        var completed = await Task.WhenAny(barrier, operation).WaitAsync(TimeSpan.FromSeconds(10));
        if (ReferenceEquals(completed, operation))
        {
            await operation;
            throw new InvalidOperationException("Operation completed without reaching the expected test barrier.");
        }
        return await barrier;
    }

    private static async Task WaitForBarrierAsync(Task barrier, Task operation)
    {
        var completed = await Task.WhenAny(barrier, operation).WaitAsync(TimeSpan.FromSeconds(10));
        if (ReferenceEquals(completed, operation))
        {
            await operation;
            throw new InvalidOperationException("Operation completed without reaching the expected test barrier.");
        }
        await barrier;
    }

    private static async Task DrainIgnoringFailureAsync(params Task?[] operations)
    {
        foreach (var operation in operations)
        {
            if (operation is null) continue;
            try
            {
                await operation;
            }
            catch
            {
                // Cleanup must observe all retained operations without masking the test's primary failure.
            }
        }
    }

    private async Task<SeededAnimal> SeedAsync(params (string Value, bool Active, bool Principal)[] identifications)
    {
        var now = DateTimeOffset.UtcNow;
        var species = Especie.Criar("Species " + Guid.NewGuid().ToString("N"), null, now);
        var animal = Animal.Criar(
            "AN-" + Guid.NewGuid().ToString("N"), null, species.Id, null, null,
            SexoAnimal.Indeterminado, null, EscopoAnimal.Operacional,
            DateOnly.FromDateTime(now.UtcDateTime), now);
        var rows = identifications.Select(item =>
        {
            var row = IdentificacaoAnimal.Criar(animal.Id, TipoIdentificacaoAnimal.Microchip, null,
                item.Value, item.Principal, null, null, now);
            if (!item.Active) row.Inativar(now.AddSeconds(1));
            return row;
        }).ToArray();

        await factory.ExecuteDbContextAsync(async context =>
        {
            context.AddRange(species, animal);
            context.IdentificacoesAnimal.AddRange(rows);
            await context.SaveChangesAsync();
        });

        return new SeededAnimal(animal.Id, rows.ToDictionary(row => row.Valor, row => row.Id));
    }

    private Task<IdentificacaoAnimalResult> ExecuteServiceAsync(
        Func<IIdentificacaoAnimalService, Task<IdentificacaoAnimalResult>> action) =>
        factory.ExecuteScopeAsync(services => action(services.GetRequiredService<IIdentificacaoAnimalService>()));

    private Task<IdentificacaoAnimalResult> ExecuteWithRepositoryAsync(
        Func<IIdentificacaoAnimalRepository, IIdentificacaoAnimalRepository> decorate,
        Func<IIdentificacaoAnimalService, Task<IdentificacaoAnimalResult>> action) =>
        factory.ExecuteScopeAsync(services => action(new IdentificacaoAnimalService(
            decorate(services.GetRequiredService<IIdentificacaoAnimalRepository>()), TimeProvider.System)));

    private Task<string[]> ReadActivePrincipalsAsync(Guid animalId) => factory.ExecuteDbContextAsync(context =>
        context.IdentificacoesAnimal.AsNoTracking()
            .Where(item => item.AnimalId == animalId && item.Ativo && item.Principal)
            .OrderBy(item => item.Valor)
            .Select(item => item.Valor)
            .ToArrayAsync());

    private Task<bool> ReadPrincipalFlagAsync(Guid identificationId) => factory.ExecuteDbContextAsync(context =>
        context.IdentificacoesAnimal.AsNoTracking()
            .Where(item => item.Id == identificationId)
            .Select(item => item.Principal)
            .SingleAsync());

    private Task<bool> IdentificationExistsAsync(string value) => factory.ExecuteDbContextAsync(context =>
        context.IdentificacoesAnimal.AsNoTracking().AnyAsync(item => item.Valor == value));

    private Task<int> CountIdentificationsAsync(string value) => factory.ExecuteDbContextAsync(context =>
        context.IdentificacoesAnimal.AsNoTracking().CountAsync(item => EF.Functions.ILike(item.Valor, value)));

    private static CreateIdentificacaoAnimalCommand CreateCommand(string value, bool principal) =>
        new(TipoIdentificacaoAnimal.Microchip, null, value, principal, null, null);

    private async Task InstallFailureTriggerAsync(string operation, string value, string name)
    {
        var sql = $$"""
            CREATE FUNCTION "{{name}}"() RETURNS trigger LANGUAGE plpgsql AS $function$
            BEGIN
                IF NEW."Valor" = '{{value}}' AND NEW."Principal" THEN
                    RAISE EXCEPTION 'Task 8 forced failure' USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END;
            $function$;
            CREATE TRIGGER "TR_{{name}}"
            BEFORE {{operation}} ON "IdentificacoesAnimal"
            FOR EACH ROW EXECUTE FUNCTION "{{name}}"();
            """;
        await factory.ExecuteDbContextAsync(context => context.Database.ExecuteSqlRawAsync(sql));
    }

    private async Task DropFailureTriggerAsync(string name)
    {
        var sql = $$"""
            DROP TRIGGER IF EXISTS "TR_{{name}}" ON "IdentificacoesAnimal";
            DROP FUNCTION IF EXISTS "{{name}}"();
            """;
        await factory.ExecuteDbContextAsync(context => context.Database.ExecuteSqlRawAsync(sql));
    }

    private sealed record SeededAnimal(Guid AnimalId, IReadOnlyDictionary<string, Guid> Ids);

    private sealed record CreateFirstFlushState(int ActivePrincipalCount, bool NewRowExists);

    private class DelegatingRepository(IIdentificacaoAnimalRepository inner) : IIdentificacaoAnimalRepository
    {
        protected IIdentificacaoAnimalRepository Inner { get; } = inner;

        public Task<IIdentificacaoAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default) =>
            Inner.BeginMutationAsync(cancellationToken);
        public virtual Task<Animal?> LockAnimalAsync(Guid animalId, CancellationToken cancellationToken = default) =>
            Inner.LockAnimalAsync(animalId, cancellationToken);
        public Task<bool> AnimalExistsAsync(Guid animalId, CancellationToken cancellationToken = default) =>
            Inner.AnimalExistsAsync(animalId, cancellationToken);
        public Task AddAsync(IdentificacaoAnimal identificacao, CancellationToken cancellationToken = default) =>
            Inner.AddAsync(identificacao, cancellationToken);
        public Task<IdentificacaoAnimal?> GetByAnimalForUpdateAsync(Guid animalId, Guid identificacaoId,
            CancellationToken cancellationToken = default) =>
            Inner.GetByAnimalForUpdateAsync(animalId, identificacaoId, cancellationToken);
        public Task<IdentificacaoAnimalResult?> GetByAnimalReadOnlyAsync(Guid animalId, Guid identificacaoId,
            CancellationToken cancellationToken = default) =>
            Inner.GetByAnimalReadOnlyAsync(animalId, identificacaoId, cancellationToken);
        public Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId, IdentificacaoAnimalListQuery query,
            CancellationToken cancellationToken = default) => Inner.ListByAnimalAsync(animalId, query, cancellationToken);
        public Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query,
            CancellationToken cancellationToken = default) => Inner.ListGlobalAsync(query, cancellationToken);
        public Task<bool> HasDuplicateAsync(TipoIdentificacaoAnimal tipo, string? descricaoTipo, string valor,
            CancellationToken cancellationToken = default) => Inner.HasDuplicateAsync(tipo, descricaoTipo, valor, cancellationToken);
        public Task<IdentificacaoAnimal?> GetCurrentPrincipalForUpdateAsync(Guid animalId,
            CancellationToken cancellationToken = default) => Inner.GetCurrentPrincipalForUpdateAsync(animalId, cancellationToken);
        public virtual Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class FirstFlushBarrierRepository(
        IIdentificacaoAnimalRepository inner,
        Func<Task> afterFirstSave) : DelegatingRepository(inner)
    {
        private int saveCount;

        public override async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await Inner.SaveChangesAsync(cancellationToken);
            if (Interlocked.Increment(ref saveCount) == 1) await afterFirstSave();
        }
    }

    private sealed class LockBarrierRepository(
        IIdentificacaoAnimalRepository inner,
        Func<Task>? beforeLock,
        Func<Task> afterLock) : DelegatingRepository(inner)
    {
        public override async Task<Animal?> LockAnimalAsync(Guid animalId,
            CancellationToken cancellationToken = default)
        {
            if (beforeLock is not null) await beforeLock();
            var animal = await Inner.LockAnimalAsync(animalId, cancellationToken);
            await afterLock();
            return animal;
        }
    }
}
