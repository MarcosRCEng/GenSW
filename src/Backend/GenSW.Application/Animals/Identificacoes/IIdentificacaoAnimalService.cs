namespace GenSW.Application.Animals.Identificacoes;

public interface IIdentificacaoAnimalService
{
    Task<IdentificacaoAnimalResult> CreateAsync(Guid animalId, CreateIdentificacaoAnimalCommand command, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId, IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> GetByAnimalAsync(Guid animalId, Guid identificacaoId, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> UpdateMetadataAsync(Guid animalId, Guid identificacaoId, UpdateIdentificacaoAnimalMetadataCommand command, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> SetAtivoAsync(Guid animalId, Guid identificacaoId, SetIdentificacaoAnimalAtivoCommand command, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> SetPrincipalAsync(Guid animalId, Guid identificacaoId, SetIdentificacaoAnimalPrincipalCommand command, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
}
