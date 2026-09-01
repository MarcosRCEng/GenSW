namespace GenSW.Application.Species;

public interface IEspecieService
{
    Task<EspecieResult> CreateAsync(CreateEspecieCommand command, CancellationToken cancellationToken = default);

    Task<EspecieResult?> GetByIdAsync(Guid especieId, CancellationToken cancellationToken = default);

    Task<PagedEspecieResult> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default);

    Task<EspecieResult> UpdateAsync(Guid especieId, UpdateEspecieCommand command, CancellationToken cancellationToken = default);

    Task<EspecieResult> SetActiveAsync(Guid especieId, bool ativo, CancellationToken cancellationToken = default);
}
