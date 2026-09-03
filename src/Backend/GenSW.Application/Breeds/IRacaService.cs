namespace GenSW.Application.Breeds;

public interface IRacaService
{
    Task<RacaResult> CreateAsync(CreateRacaCommand command, CancellationToken cancellationToken = default);
    Task<RacaResult?> GetByIdAsync(Guid racaId, CancellationToken cancellationToken = default);
    Task<PagedRacaResult> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default);
    Task<RacaResult> UpdateAsync(Guid racaId, UpdateRacaCommand command, CancellationToken cancellationToken = default);
    Task<RacaResult> SetActiveAsync(Guid racaId, bool ativo, CancellationToken cancellationToken = default);
}
