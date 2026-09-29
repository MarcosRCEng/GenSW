namespace GenSW.Application.Animals.ProducaoOvos;
public interface IProducaoOvoService
{
    Task<ProducaoOvoResult> CreateAsync(Guid animalId, ProducaoOvoCommand command, CancellationToken cancellationToken = default);
    Task<ProducaoOvoResult> UpdateAsync(Guid animalId, Guid id, ProducaoOvoCommand command, CancellationToken cancellationToken = default);
    Task<PagedProducaoOvoResult> ListAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken cancellationToken = default);
}
