namespace GenSW.Application.Varieties;

public interface IVariedadeService
{
    Task<VariedadeResult> CreateAsync(CreateVariedadeCommand command, CancellationToken cancellationToken = default);
    Task<VariedadeResult?> GetByIdAsync(Guid variedadeId, CancellationToken cancellationToken = default);
    Task<PagedVariedadeResult> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default);
    Task<VariedadeResult> UpdateAsync(Guid variedadeId, UpdateVariedadeCommand command, CancellationToken cancellationToken = default);
    Task<VariedadeResult> SetActiveAsync(Guid variedadeId, bool ativo, CancellationToken cancellationToken = default);
}
