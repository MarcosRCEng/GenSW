using GenSW.Domain.Animals;

namespace GenSW.Application.Animals;

public interface IAnimalCodeAllocator
{
    Task<IAnimalAutomaticCodeAttempt> BeginAttemptAsync(CancellationToken cancellationToken = default);
}

public interface IAnimalAutomaticCodeAttempt : IAsyncDisposable
{
    Task<string> AllocateNextCodigoInternoAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAndDetachAsync(Animal failedAnimal, CancellationToken cancellationToken = default);
}
