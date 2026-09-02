using GenSW.Domain.Varieties;

namespace GenSW.Application.Varieties;

public interface IVariedadeRepository
{
    Task AddAsync(Variedade variedade, CancellationToken cancellationToken = default);
    Task<VariedadeReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Variedade?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VariedadeListPage> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default);
    Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record VariedadeReadModel(
    Guid Id, Guid EspecieId, string Nome, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    VariedadeEspecieResumo Especie);

public sealed record VariedadeListPage(IReadOnlyList<VariedadeReadModel> Items, int TotalItems);
