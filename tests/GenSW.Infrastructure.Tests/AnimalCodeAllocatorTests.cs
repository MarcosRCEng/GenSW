using System.Diagnostics;
using System.Reflection;
using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using GenSW.Infrastructure;
using GenSW.Infrastructure.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class AnimalCodeAllocatorTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql postgreSql = null!;

    public async Task InitializeAsync()
    {
        postgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    [Fact]
    public async Task AllocateNextCodigoInternoAsync_returns_the_first_sequence_value_in_AN_D6_format()
    {
        await ResetSequenceAsync();
        await using var context = CreateContext();
        var allocator = new PostgreSqlAnimalCodeAllocator(context);

        await using var attempt = await allocator.BeginAttemptAsync();

        Assert.Equal("AN-000001", await attempt.AllocateNextCodigoInternoAsync());
    }

    [Fact]
    public async Task AllocateNextCodigoInternoAsync_preserves_all_digits_after_999999()
    {
        await ResetSequenceAsync(1_000_000);
        await using var context = CreateContext();
        var allocator = new PostgreSqlAnimalCodeAllocator(context);

        await using var attempt = await allocator.BeginAttemptAsync();

        Assert.Equal("AN-1000000", await attempt.AllocateNextCodigoInternoAsync());
    }

    [Fact]
    public async Task AllocateNextCodigoInternoAsync_does_not_reposition_the_sequence_for_a_manual_looking_code()
    {
        await ResetSequenceAsync();
        await using var context = CreateContext();
        var especie = Especie.Criar("Manual-code test species", null, DateTimeOffset.UtcNow);
        context.Especies.Add(especie);
        context.Animais.Add(CreateAnimal("AN-999999", especie.Id));
        await context.SaveChangesAsync();
        var allocator = new PostgreSqlAnimalCodeAllocator(context);

        await using var attempt = await allocator.BeginAttemptAsync();

        Assert.Equal("AN-000001", await attempt.AllocateNextCodigoInternoAsync());
    }

    [Fact]
    public async Task RollbackAndDetachAsync_keeps_the_allocated_sequence_value_consumed()
    {
        await ResetSequenceAsync();
        await using var context = CreateContext();
        var allocator = new PostgreSqlAnimalCodeAllocator(context);
        var failedAnimal = CreateAnimal("AN-000001");

        await using (var attempt = await allocator.BeginAttemptAsync())
        {
            Assert.Equal("AN-000001", await attempt.AllocateNextCodigoInternoAsync());
            context.Animais.Add(failedAnimal);

            await attempt.RollbackAndDetachAsync(failedAnimal);
        }

        Assert.DoesNotContain(context.ChangeTracker.Entries<Animal>(), entry => entry.Entity == failedAnimal);

        await using var nextAttempt = await allocator.BeginAttemptAsync();
        Assert.Equal("AN-000002", await nextAttempt.AllocateNextCodigoInternoAsync());
    }

    [Fact]
    public async Task Named_codigo_collision_replaces_the_aborted_attempt_transaction_before_the_next_insert()
    {
        await ResetSequenceAsync();
        await using var context = CreateContext();
        var species = Especie.Criar("Transaction test species", null, DateTimeOffset.UtcNow);
        context.Especies.Add(species);
        context.Animais.Add(CreateAnimal("AN-000001", species.Id));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new AnimalRepository(context);
        var allocator = new PostgreSqlAnimalCodeAllocator(context);

        long firstTransactionId;
        await using (var firstAttempt = await allocator.BeginAttemptAsync())
        {
            var candidate = await firstAttempt.AllocateNextCodigoInternoAsync();
            firstTransactionId = await GetCurrentTransactionIdAsync(context);
            var failedAnimal = CreateAnimal(candidate, species.Id);
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
            await repository.AddAsync(CreateAnimal(candidate, species.Id));
            await repository.SaveChangesAsync();
            await secondAttempt.CommitAsync();
        }

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(2, await GetSequenceLastValueAsync());
    }

    [Fact]
    public async Task AllocateNextCodigoInternoAsync_translates_only_sequence_exhaustion_to_the_domain_exception()
    {
        await ResetSequenceAsync();
        await ExecuteSqlAsync("ALTER SEQUENCE \"AnimalCodigoInternoSequence\" MINVALUE 0 MAXVALUE 1 RESTART WITH 1;");
        await using var context = CreateContext();
        var allocator = new PostgreSqlAnimalCodeAllocator(context);

        await using (var firstAttempt = await allocator.BeginAttemptAsync())
        {
            Assert.Equal("AN-000001", await firstAttempt.AllocateNextCodigoInternoAsync());
            await firstAttempt.CommitAsync();
        }

        await using var exhaustedAttempt = await allocator.BeginAttemptAsync();
        await Assert.ThrowsAsync<AnimalCodeSequenceExhaustedException>(
            () => exhaustedAttempt.AllocateNextCodigoInternoAsync());
    }

    [Fact]
    public async Task CommitAsync_then_dispose_is_terminal_without_a_rollback_or_second_transaction_disposal()
    {
        await ResetSequenceAsync();
        using var diagnostics = new TransactionDiagnostics();
        await using var context = CreateContext(diagnostics);
        var allocator = new PostgreSqlAnimalCodeAllocator(context);
        var attempt = await allocator.BeginAttemptAsync();

        await attempt.AllocateNextCodigoInternoAsync();
        await attempt.CommitAsync();
        var lastValueAfterCommit = await GetSequenceLastValueAsync();
        await attempt.DisposeAsync();
        await attempt.DisposeAsync();

        Assert.Equal(lastValueAfterCommit, await GetSequenceLastValueAsync());
        Assert.Equal(1, diagnostics.CommitCount);
        Assert.Equal(0, diagnostics.RollbackCount);
        Assert.Equal(1, diagnostics.DisposeCount);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task RollbackAndDetachAsync_then_dispose_is_terminal_without_a_second_rollback_or_detach()
    {
        await ResetSequenceAsync();
        using var diagnostics = new TransactionDiagnostics();
        await using var context = CreateContext(diagnostics);
        var allocator = new PostgreSqlAnimalCodeAllocator(context);
        var failedAnimal = CreateAnimal("AN-000001");
        var attempt = await allocator.BeginAttemptAsync();
        context.Animais.Add(failedAnimal);

        await attempt.RollbackAndDetachAsync(failedAnimal);
        var lastValueAfterRollbackAndDetach = await GetSequenceLastValueAsync();
        context.Animais.Add(failedAnimal);
        await attempt.DisposeAsync();
        await attempt.DisposeAsync();

        Assert.Equal(lastValueAfterRollbackAndDetach, await GetSequenceLastValueAsync());
        Assert.Equal(0, diagnostics.CommitCount);
        Assert.Equal(1, diagnostics.RollbackCount);
        Assert.Equal(1, diagnostics.DisposeCount);
        Assert.Equal(EntityState.Added, context.Entry(failedAnimal).State);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task RollbackAndDetachAsync_when_detach_fails_does_not_repeat_a_successful_rollback_or_transaction_disposal()
    {
        await ResetSequenceAsync();
        using var diagnostics = new TransactionDiagnostics();
        await using var context = CreateContext(diagnostics);
        var allocator = new PostgreSqlAnimalCodeAllocator(context);
        var attempt = await allocator.BeginAttemptAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => attempt.RollbackAndDetachAsync(null!));
        await attempt.DisposeAsync();
        await attempt.DisposeAsync();

        Assert.Equal(1, diagnostics.RollbackCount);
        Assert.Equal(1, diagnostics.DisposeCount);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task DisposeAsync_on_an_open_attempt_rolls_back_and_disposes_exactly_once_without_allocating_again()
    {
        await ResetSequenceAsync();
        using var diagnostics = new TransactionDiagnostics();
        await using var context = CreateContext(diagnostics);
        var allocator = new PostgreSqlAnimalCodeAllocator(context);
        var attempt = await allocator.BeginAttemptAsync();

        Assert.Equal("AN-000001", await attempt.AllocateNextCodigoInternoAsync());
        var lastValueBeforeDispose = await GetSequenceLastValueAsync();
        await attempt.DisposeAsync();
        await attempt.DisposeAsync();

        Assert.Equal(lastValueBeforeDispose, await GetSequenceLastValueAsync());
        Assert.Equal(0, diagnostics.CommitCount);
        Assert.Equal(1, diagnostics.RollbackCount);
        Assert.Equal(1, diagnostics.DisposeCount);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task DisposeAsync_when_transaction_disposal_fails_propagates_the_original_failure_without_retrying_cleanup()
    {
        await using var context = CreateContext();
        var transaction = new DisposalFailingTransaction();
        var attempt = CreateAttempt(context, transaction);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => attempt.DisposeAsync().AsTask());
        await attempt.DisposeAsync();

        Assert.Same(transaction.DisposalFailure, exception);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeAsyncCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void AddInfrastructure_registers_the_animal_code_allocator_as_scoped()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:GenSW"] = "Host=localhost;Database=gensw_test" })
            .Build();
        var services = new ServiceCollection();

        DependencyInjection.AddInfrastructure(services, configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAnimalCodeAllocator) &&
            descriptor.ImplementationType == typeof(PostgreSqlAnimalCodeAllocator) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    public Task DisposeAsync() => postgreSql.DisposeAsync().AsTask();

    private GenSWDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new GenSWDbContext(options);
    }

    private async Task ResetSequenceAsync(long restartWith = 1)
    {
        await ExecuteSqlAsync("ALTER SEQUENCE \"AnimalCodigoInternoSequence\" NO MAXVALUE;");
        await ExecuteSqlAsync($"ALTER SEQUENCE \"AnimalCodigoInternoSequence\" RESTART WITH {restartWith};");
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<long> GetSequenceLastValueAsync()
    {
        await using var context = CreateContext();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT last_value FROM \"AnimalCodigoInternoSequence\";";
            return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<long> GetCurrentTransactionIdAsync(GenSWDbContext context)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT txid_current();";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Animal CreateAnimal(string codigoInterno, Guid? especieId = null) => Animal.Criar(
        codigoInterno,
        null,
        especieId ?? Guid.NewGuid(),
        null,
        null,
        SexoAnimal.Indeterminado,
        null,
        EscopoAnimal.Operacional,
        new DateOnly(2026, 9, 8),
        DateTimeOffset.UtcNow);

    private static IAnimalAutomaticCodeAttempt CreateAttempt(
        GenSWDbContext context,
        IDbContextTransaction transaction)
    {
        var attemptType = typeof(PostgreSqlAnimalCodeAllocator).GetNestedType(
            "AnimalAutomaticCodeAttempt",
            BindingFlags.NonPublic)!;
        return (IAnimalAutomaticCodeAttempt)Activator.CreateInstance(
            attemptType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context, transaction],
            culture: null)!;
    }

    private sealed class DisposalFailingTransaction : IDbContextTransaction
    {
        public InvalidOperationException DisposalFailure { get; } = new("Expected disposal failure.");
        public int RollbackCount { get; private set; }
        public int DisposeAsyncCount { get; private set; }
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool SupportsSavepoints => false;

        public void Commit() { }

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Rollback() => RollbackCount++;

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }

        public void CreateSavepoint(string name) => throw new NotSupportedException();

        public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public void RollbackToSavepoint(string name) => throw new NotSupportedException();

        public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public void ReleaseSavepoint(string name) => throw new NotSupportedException();

        public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public void Dispose() { }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.FromException(DisposalFailure);
        }
    }

    private sealed class TransactionDiagnostics : DbTransactionInterceptor, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly IDisposable allListenersSubscription;
        private IDisposable? entityFrameworkSubscription;
        private readonly HashSet<Guid> transactionIds = [];

        public TransactionDiagnostics()
        {
            allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int DisposeCount { get; private set; }

        public override ValueTask<System.Data.Common.DbTransaction> TransactionStartedAsync(
            System.Data.Common.DbConnection connection,
            TransactionEndEventData eventData,
            System.Data.Common.DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            transactionIds.Add(eventData.TransactionId);
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(
            System.Data.Common.DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public override Task TransactionRolledBackAsync(
            System.Data.Common.DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Microsoft.EntityFrameworkCore")
            {
                entityFrameworkSubscription = value.Subscribe(this);
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == "Microsoft.EntityFrameworkCore.Database.Transaction.TransactionDisposed" &&
                value.Value is TransactionEventData eventData &&
                transactionIds.Contains(eventData.TransactionId))
            {
                DisposeCount++;
            }
        }

        public void Dispose()
        {
            entityFrameworkSubscription?.Dispose();
            allListenersSubscription.Dispose();
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }
}
