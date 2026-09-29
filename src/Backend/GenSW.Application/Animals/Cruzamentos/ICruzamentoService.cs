namespace GenSW.Application.Animals.Cruzamentos;
public interface ICruzamentoService
{
 Task<CruzamentoResult> CreateAsync(CreateCruzamentoCommand command, CancellationToken cancellationToken = default);
 Task<CruzamentoResult> GetAsync(Guid id, CancellationToken cancellationToken = default);
 Task<PagedCruzamentoResult> ListAsync(CruzamentoListQuery query, CancellationToken cancellationToken = default);
 Task<CruzamentoResult> UpdateAsync(Guid id, UpdateCruzamentoCommand command, CancellationToken cancellationToken = default);
 Task<CruzamentoResult> SetStatusAsync(Guid id, CruzamentoStatusCommand command, CancellationToken cancellationToken = default);
}
