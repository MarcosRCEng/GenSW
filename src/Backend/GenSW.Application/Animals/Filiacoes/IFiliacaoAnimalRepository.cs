using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.Filiacoes;
public interface IFiliacaoAnimalMutationScope : IAsyncDisposable { Task CommitAsync(CancellationToken cancellationToken = default); }
public interface IFiliacaoAnimalRepository
{
 Task<IFiliacaoAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default);
 Task<Animal?> LockAnimalAsync(Guid id, CancellationToken cancellationToken = default);
 Task<Animal?> GetAnimalAsync(Guid id, CancellationToken cancellationToken = default);
 Task<FiliacaoAnimal?> GetActiveAsync(Guid animalId, TipoFiliacaoAnimal tipo, CancellationToken cancellationToken = default);
 Task<FiliacaoAnimal?> GetForUpdateAsync(Guid animalId, Guid id, CancellationToken cancellationToken = default);
 Task<IReadOnlyList<FiliacaoAnimalResult>> ListAsync(Guid animalId, CancellationToken cancellationToken = default);
 Task<bool> WouldCreateCycleAsync(Guid animalId, Guid progenitorId, CancellationToken cancellationToken = default);
 Task AddAsync(FiliacaoAnimal item, CancellationToken cancellationToken = default);
 Task<PedigreeAnimalResult?> GetPedigreeAsync(Guid animalId, int generations, CancellationToken cancellationToken = default);
 Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
