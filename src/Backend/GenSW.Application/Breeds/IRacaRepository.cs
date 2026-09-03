using GenSW.Domain.Breeds;

namespace GenSW.Application.Breeds;

public interface IRacaRepository
{
    Task AddAsync(Raca raca, CancellationToken cancellationToken = default);
    Task<RacaReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Raca?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RacaListPage> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default);
    Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record RacaReadModel(
    Guid Id, Guid EspecieId, string Nome, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    RacaEspecieResumo Especie);

public sealed record RacaListPage(IReadOnlyList<RacaReadModel> Items, int TotalItems);
