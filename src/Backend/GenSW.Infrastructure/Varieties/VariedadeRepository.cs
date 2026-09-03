using GenSW.Application.Varieties;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.Infrastructure.Varieties;

public sealed class VariedadeRepository(GenSWDbContext context) : IVariedadeRepository
{
    private const string NameIndex = "UX_Variedades_EspecieId_Nome_CaseInsensitive";
    public Task AddAsync(Variedade variedade, CancellationToken cancellationToken = default) => context.Variedades.AddAsync(variedade, cancellationToken).AsTask();
    public Task<Variedade?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => context.Variedades.SingleOrDefaultAsync(variedade => variedade.Id == id, cancellationToken);
    public Task<VariedadeReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => ReadModels(context.Variedades.AsNoTracking().Where(variedade => variedade.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<VariedadeListPage> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(context.Variedades.AsNoTracking(), query);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await ReadModels(ApplyOrdering(filtered, query.SortBy, query.SortDescending)).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new VariedadeListPage(items, totalItems);
    }

    public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) => context.Variedades.AsNoTracking().AnyAsync(variedade => variedade.EspecieId == especieId && (excludingId == null || variedade.Id != excludingId) && variedade.Nome.ToLower() == nome.ToLower(), cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NameIndex })
        { throw new VariedadeDuplicateException(exception); }
    }

    private IQueryable<VariedadeReadModel> ReadModels(IQueryable<Variedade>? variedades = null) =>
        from variedade in variedades ?? context.Variedades.AsNoTracking()
        join especie in context.Especies.AsNoTracking() on variedade.EspecieId equals especie.Id
        select new VariedadeReadModel(variedade.Id, variedade.EspecieId, variedade.Nome, variedade.Ativo, variedade.CreatedAtUtc, variedade.UpdatedAtUtc, new VariedadeEspecieResumo(especie.Id, especie.NomeComum, especie.Ativo));

    private static IQueryable<Variedade> ApplyFilters(IQueryable<Variedade> query, VariedadeListQuery filters)
    {
        if (filters.EspecieId is { } especieId) query = query.Where(variedade => variedade.EspecieId == especieId);
        if (filters.Ativo is { } ativo) query = query.Where(variedade => variedade.Ativo == ativo);
        var search = string.IsNullOrWhiteSpace(filters.Search) ? null : filters.Search.Trim();
        return search is null ? query : query.Where(variedade => variedade.Nome.ToLower().Contains(search.ToLower()));
    }

    private static IOrderedQueryable<Variedade> ApplyOrdering(IQueryable<Variedade> query, VariedadeSortField sortBy, bool descending) => (sortBy, descending) switch
    {
        (VariedadeSortField.Nome, false) => query.OrderBy(item => item.Nome).ThenBy(item => item.Id),
        (VariedadeSortField.Nome, true) => query.OrderByDescending(item => item.Nome).ThenByDescending(item => item.Id),
        (VariedadeSortField.Ativo, false) => query.OrderBy(item => item.Ativo).ThenBy(item => item.Id),
        (VariedadeSortField.Ativo, true) => query.OrderByDescending(item => item.Ativo).ThenByDescending(item => item.Id),
        (VariedadeSortField.CreatedAtUtc, false) => query.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id),
        (VariedadeSortField.CreatedAtUtc, true) => query.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy)),
    };
}
