using GenSW.Application.Breeds;
using GenSW.Domain.Breeds;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.Infrastructure.Breeds;

public sealed class RacaRepository(GenSWDbContext context) : IRacaRepository
{
    private const string NameIndex = "UX_Racas_EspecieId_Nome_CaseInsensitive";

    public Task AddAsync(Raca raca, CancellationToken cancellationToken = default) => context.Racas.AddAsync(raca, cancellationToken).AsTask();

    public Task<Raca?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => context.Racas.SingleOrDefaultAsync(raca => raca.Id == id, cancellationToken);

    public Task<RacaReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => ReadModels(context.Racas.AsNoTracking().Where(raca => raca.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<RacaListPage> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(context.Racas.AsNoTracking(), query);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await ReadModels(ApplyOrdering(filtered, query.SortBy, query.SortDescending))
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new RacaListPage(items, totalItems);
    }

    public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) =>
        context.Racas.AsNoTracking().AnyAsync(raca => raca.EspecieId == especieId && (excludingId == null || raca.Id != excludingId) && raca.Nome.ToLower() == nome.ToLower(), cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NameIndex })
        { throw new RacaDuplicateException(exception); }
    }

    private IQueryable<RacaReadModel> ReadModels(IQueryable<Raca>? racas = null) =>
        from raca in racas ?? context.Racas.AsNoTracking()
        join especie in context.Especies.AsNoTracking() on raca.EspecieId equals especie.Id
        select new RacaReadModel(raca.Id, raca.EspecieId, raca.Nome, raca.Ativo, raca.CreatedAtUtc, raca.UpdatedAtUtc, new RacaEspecieResumo(especie.Id, especie.NomeComum, especie.Ativo));

    private static IQueryable<Raca> ApplyFilters(IQueryable<Raca> query, RacaListQuery filters)
    {
        if (filters.EspecieId is { } especieId) query = query.Where(raca => raca.EspecieId == especieId);
        if (filters.Ativo is { } ativo) query = query.Where(raca => raca.Ativo == ativo);
        var search = string.IsNullOrWhiteSpace(filters.Search) ? null : filters.Search.Trim();
        return search is null ? query : query.Where(raca => raca.Nome.ToLower().Contains(search.ToLower()));
    }

    private static IOrderedQueryable<Raca> ApplyOrdering(IQueryable<Raca> query, RacaSortField sortBy, bool descending) => (sortBy, descending) switch
    {
        (RacaSortField.Nome, false) => query.OrderBy(item => item.Nome).ThenBy(item => item.Id),
        (RacaSortField.Nome, true) => query.OrderByDescending(item => item.Nome).ThenByDescending(item => item.Id),
        (RacaSortField.Ativo, false) => query.OrderBy(item => item.Ativo).ThenBy(item => item.Id),
        (RacaSortField.Ativo, true) => query.OrderByDescending(item => item.Ativo).ThenByDescending(item => item.Id),
        (RacaSortField.CreatedAtUtc, false) => query.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id),
        (RacaSortField.CreatedAtUtc, true) => query.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy)),
    };
}
