using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.ProducaoOvos;
public interface IProducaoOvoRepository
{
    Task AddAsync(ProducaoOvo item, CancellationToken cancellationToken = default);
    Task<ProducaoOvo?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProducaoOvoResult?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ProducaoOvoResult> Items, int TotalItems)> ListAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProducaoOvoResult>> ListAllAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken cancellationToken = default);
    Task<decimal?> GetPesoPadraoAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
