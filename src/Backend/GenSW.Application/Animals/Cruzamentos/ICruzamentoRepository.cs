using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.Cruzamentos;
public interface ICruzamentoRepository
{
 Task AddAsync(Cruzamento item, CancellationToken cancellationToken = default);
 Task<Cruzamento?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
 Task<CruzamentoResult?> GetAsync(Guid id, CancellationToken cancellationToken = default);
 Task<(IReadOnlyList<CruzamentoResult> Items, int TotalItems)> ListAsync(CruzamentoListQuery query, CancellationToken cancellationToken = default);
 Task<Animal?> GetAnimalAsync(Guid id, CancellationToken cancellationToken = default);
 Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
