using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class AnimalAutomaticCreatorTests
{
    [Fact]
    public async Task CreateAsync_retries_an_eligible_persisted_collision_only_after_rollback_and_detach_complete()
    {
        var repository = new RecordingRepository(
            new AnimalDuplicateException("AN-000001", AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint));
        var allocator = new RecordingAllocator(repository.Events, ["AN-000001", "AN-000002"]);
        var creator = new AnimalAutomaticCreator(repository, allocator, FixedClock);

        var animal = await creator.CreateAsync(Command());

        Assert.Equal("AN-000002", animal.CodigoInterno);
        Assert.Equal([
            "Begin:1", "Allocate:1", "Add:AN-000001", "Save:AN-000001", "Rollback:1:AN-000001", "Dispose:1",
            "Begin:2", "Allocate:2", "Add:AN-000002", "Save:AN-000002", "Commit:2", "Dispose:2"
        ], repository.Events);
    }

    [Fact]
    public async Task CreateAsync_rolls_back_the_fifth_eligible_collision_and_never_allocates_a_sixth_code()
    {
        var collisions = Enumerable.Range(1, 5)
            .Select(index => (Exception)new AnimalDuplicateException($"AN-{index:D6}", AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint));
        var repository = new RecordingRepository(collisions.ToArray());
        var allocator = new RecordingAllocator(repository.Events, Enumerable.Range(1, 6).Select(index => $"AN-{index:D6}").ToArray());
        var creator = new AnimalAutomaticCreator(repository, allocator, FixedClock);

        await Assert.ThrowsAsync<AnimalAutomaticCodeCollisionLimitExceededException>(() => creator.CreateAsync(Command()));

        Assert.Equal(5, allocator.BeginCount);
        Assert.Equal(5, allocator.AllocateCount);
        Assert.Equal(5, allocator.RollbackCount);
        Assert.DoesNotContain("Begin:6", repository.Events);
    }

    [Theory]
    [MemberData(nameof(NonRetriableSaveFailures))]
    public async Task CreateAsync_propagates_non_retriable_save_failure_without_a_new_attempt(Exception failure)
    {
        var repository = new RecordingRepository(failure);
        var allocator = new RecordingAllocator(repository.Events, ["AN-000001", "AN-000002"]);
        var creator = new AnimalAutomaticCreator(repository, allocator, FixedClock);

        var actual = await Assert.ThrowsAsync(failure.GetType(), () => creator.CreateAsync(Command()));

        Assert.Same(failure, actual);
        Assert.Equal(1, allocator.BeginCount);
        Assert.Equal(1, allocator.AllocateCount);
        Assert.Equal(0, allocator.RollbackCount);
    }

    [Theory]
    [InlineData("rollback")]
    [InlineData("detach")]
    public async Task CreateAsync_propagates_cleanup_failure_without_a_new_attempt(string cleanupFailure)
    {
        var duplicate = new AnimalDuplicateException("AN-000001", AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint);
        var repository = new RecordingRepository(duplicate);
        var allocator = new RecordingAllocator(repository.Events, ["AN-000001", "AN-000002"], cleanupFailure);
        var creator = new AnimalAutomaticCreator(repository, allocator, FixedClock);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => creator.CreateAsync(Command()));

        Assert.Equal($"{cleanupFailure} failure", exception.Message);
        Assert.Equal(1, allocator.BeginCount);
        Assert.Equal(1, allocator.AllocateCount);
        Assert.Equal(1, allocator.RollbackCount);
    }

    public static IEnumerable<object[]> NonRetriableSaveFailures()
    {
        yield return [new AnimalDuplicateException("AN-000001", AnimalDuplicateConflictSource.PreCheck)];
        yield return [new AnimalDuplicateException("AN-OTHER", AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint)];
        yield return [new InvalidOperationException("Raw database failure")];
        yield return [new OperationCanceledException()];
    }

    private static readonly TimeProvider FixedClock = new FixedTimeProvider(
        new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));

    private static CreateAnimalCommand Command() => new(
        null, "Bela", Guid.NewGuid(), null, null, SexoAnimal.Femea, null, EscopoAnimal.Operacional);

    private sealed class RecordingRepository(params Exception[] saveFailures) : IAnimalRepository
    {
        private readonly Queue<Exception> failures = new(saveFailures);
        public List<string> Events { get; } = [];

        public Task AddAsync(Animal animal, CancellationToken cancellationToken = default)
        {
            Events.Add($"Add:{animal.CodigoInterno}");
            return Task.CompletedTask;
        }

        public Task<AnimalReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Animal?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AnimalListPage> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasCodigoInternoConflictAsync(string codigoInterno, Guid? excludingId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Events.Add($"Save:{Events.Last().Split(':')[1]}");
            if (failures.TryDequeue(out var failure))
            {
                return Task.FromException(failure);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAllocator(List<string> events, string[] candidates, string? cleanupFailure = null) : IAnimalCodeAllocator
    {
        private readonly Queue<string> candidates = new(candidates);
        public int BeginCount { get; private set; }
        public int AllocateCount { get; private set; }
        public int RollbackCount { get; private set; }

        public Task<IAnimalAutomaticCodeAttempt> BeginAttemptAsync(CancellationToken cancellationToken = default)
        {
            BeginCount++;
            events.Add($"Begin:{BeginCount}");
            return Task.FromResult<IAnimalAutomaticCodeAttempt>(new Attempt(this, events, candidates.Dequeue(), cleanupFailure));
        }

        private sealed class Attempt(RecordingAllocator allocator, List<string> events, string candidate, string? cleanupFailure) : IAnimalAutomaticCodeAttempt
        {
            public Task<string> AllocateNextCodigoInternoAsync(CancellationToken cancellationToken = default)
            {
                allocator.AllocateCount++;
                events.Add($"Allocate:{allocator.BeginCount}");
                return Task.FromResult(candidate);
            }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                events.Add($"Commit:{allocator.BeginCount}");
                return Task.CompletedTask;
            }

            public Task RollbackAndDetachAsync(Animal failedAnimal, CancellationToken cancellationToken = default)
            {
                allocator.RollbackCount++;
                events.Add($"Rollback:{allocator.BeginCount}:{failedAnimal.CodigoInterno}");
                return cleanupFailure is null
                    ? Task.CompletedTask
                    : Task.FromException(new InvalidOperationException($"{cleanupFailure} failure"));
            }

            public ValueTask DisposeAsync()
            {
                events.Add($"Dispose:{allocator.BeginCount}");
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
