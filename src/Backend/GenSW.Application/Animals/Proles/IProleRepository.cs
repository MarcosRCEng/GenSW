using GenSW.Domain.Animals;
using GenSW.Application.Animals.Cruzamentos;
namespace GenSW.Application.Animals.Proles;
public interface IProleRepository
{
 Task<IAnimalMutationScope> BeginMutationAsync(Guid? cicloId, Guid? proleId, CancellationToken ct = default);
 Task AddAsync(Prole item, CancellationToken ct = default); Task<Prole?> GetForUpdateAsync(Guid id, CancellationToken ct = default); Task<ProleResult?> GetAsync(Guid id, CancellationToken ct = default); Task<(IReadOnlyList<ProleResult> Items,int TotalItems)> ListAsync(ProleListQuery query, CancellationToken ct = default); Task<CicloReprodutivo?> GetCicloForUpdateAsync(Guid id, CancellationToken ct = default); Task<int> GetQuantidadeRegistradaAsync(Guid cicloId, CancellationToken ct = default); Task<CruzamentoAnimalResumo?> GetPaiAsync(Guid cicloId, CancellationToken ct = default); Task<CruzamentoAnimalResumo?> GetMaeAsync(Guid cicloId, CancellationToken ct = default); Task SaveChangesAsync(CancellationToken ct = default);
}
