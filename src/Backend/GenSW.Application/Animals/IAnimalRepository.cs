using GenSW.Domain.Animals;

namespace GenSW.Application.Animals;

public interface IAnimalRepository
{
    Task AddAsync(Animal animal, CancellationToken cancellationToken = default);
    Task<AnimalReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Animal?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AnimalListPage> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default);
    Task<bool> HasCodigoInternoConflictAsync(string codigoInterno, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
