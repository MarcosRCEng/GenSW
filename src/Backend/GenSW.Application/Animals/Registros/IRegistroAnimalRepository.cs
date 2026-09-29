using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Registros;

public interface IRegistroAnimalMutationScope : IAsyncDisposable { Task CommitAsync(CancellationToken cancellationToken = default); }
public interface IRegistroAnimalRepository
{
    Task<IRegistroAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default);
    Task<Animal?> LockAnimalAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<bool> AnimalExistsAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task AddAsync(RegistroAnimal registro, CancellationToken cancellationToken = default);
    Task<RegistroAnimal?> GetForUpdateAsync(Guid animalId, Guid registroId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistroAnimalResult>> ListByAnimalAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<bool> HasDuplicateAsync(TipoRegistroAnimal tipoRegistro, string numeroRegistro, CancellationToken cancellationToken = default);
    Task<bool> HasActiveTypeAsync(Guid animalId, TipoRegistroAnimal tipoRegistro, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
