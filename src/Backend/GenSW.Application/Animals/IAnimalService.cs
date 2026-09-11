namespace GenSW.Application.Animals;

public interface IAnimalService
{
    Task<AnimalResult> CreateAsync(CreateAnimalCommand command, CancellationToken cancellationToken = default);
    Task<AnimalResult?> GetByIdAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<PagedAnimalResult> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default);
    Task<AnimalResult> UpdateAsync(Guid animalId, UpdateAnimalCommand command, CancellationToken cancellationToken = default);
    Task<AnimalResult> SetActiveAsync(Guid animalId, bool ativo, CancellationToken cancellationToken = default);
}
