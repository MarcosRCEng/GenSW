using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.ProducaoOvos;
public interface IProducaoOvoRepository
{
    Task AddAsync(ProducaoOvo item, CancellationToken cancellationToken = default);
    Task<ProducaoOvo?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(ProducaoOvo item, CancellationToken cancellationToken = default);
    Task<ProducaoOvoResult?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProducaoOvoResult>> ListAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProducaoOvoResult>> ListAllAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken cancellationToken = default);
    Task<ProducaoOvoResumo?> GetResumoAsync(Guid animalId, ProducaoOvoListQuery query, decimal? pesoPadrao, CancellationToken cancellationToken = default);
    Task<decimal?> GetPesoPadraoAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
