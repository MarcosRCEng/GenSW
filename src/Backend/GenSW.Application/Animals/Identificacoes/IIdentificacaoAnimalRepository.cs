using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Identificacoes;

public interface IIdentificacaoAnimalMutationScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IIdentificacaoAnimalRepository
{
    Task<IIdentificacaoAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default);
    Task<Animal?> LockAnimalAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<bool> AnimalExistsAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task AddAsync(IdentificacaoAnimal identificacao, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimal?> GetByAnimalForUpdateAsync(Guid animalId, Guid identificacaoId, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult?> GetByAnimalReadOnlyAsync(Guid animalId, Guid identificacaoId, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId, IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
    Task<bool> HasDuplicateAsync(TipoIdentificacaoAnimal tipo, string? descricaoTipo, string valor, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimal?> GetCurrentPrincipalForUpdateAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
