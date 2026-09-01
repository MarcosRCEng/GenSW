using GenSW.Domain.Species;

namespace GenSW.Application.Species;

public interface IEspecieRepository
{
    Task AddAsync(Especie especie, CancellationToken cancellationToken = default);

    Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default);

    Task<bool> HasNomeComumConflictAsync(string nomeComum, Guid? excludingId = null, CancellationToken cancellationToken = default);

    Task<bool> HasNomeCientificoConflictAsync(string nomeCientifico, Guid? excludingId = null, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record EspecieListPage(IReadOnlyList<Especie> Items, int TotalItems);
