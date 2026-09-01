using GenSW.Application.Species;
using GenSW.Domain.Species;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.Infrastructure.Species;

public sealed class EspecieRepository(GenSWDbContext context) : IEspecieRepository
{
    private const string CommonNameIndex = "UX_Especies_NomeComum_CaseInsensitive";
    private const string ScientificNameIndex = "UX_Especies_NomeCientifico_CaseInsensitive";

    public Task AddAsync(Especie especie, CancellationToken cancellationToken = default)
        => context.Especies.AddAsync(especie, cancellationToken).AsTask();

    public Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Especies.AsNoTracking().SingleOrDefaultAsync(especie => especie.Id == id, cancellationToken);

    public Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Especies.SingleOrDefaultAsync(especie => especie.Id == id, cancellationToken);

    public async Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<Especie> filtered = context.Especies.AsNoTracking();
        if (query.Ativo is { } ativo)
        {
            filtered = filtered.Where(especie => especie.Ativo == ativo);
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search is not null)
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            filtered = filtered.Where(especie =>
                EF.Functions.ILike(especie.NomeComum, pattern, "\\") ||
                (especie.NomeCientifico != null && EF.Functions.ILike(especie.NomeCientifico, pattern, "\\")));
        }

        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await ApplyOrdering(filtered, query.SortBy, query.SortDescending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new EspecieListPage(items, totalItems);
    }

    public Task<bool> HasNomeComumConflictAsync(
        string nomeComum,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default) =>
        context.Especies.AsNoTracking().AnyAsync(
            especie =>
                (excludingId == null || especie.Id != excludingId) &&
                especie.NomeComum.ToLower() == nomeComum.ToLower(),
            cancellationToken);

    public Task<bool> HasNomeCientificoConflictAsync(
        string nomeCientifico,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default) =>
        context.Especies.AsNoTracking().AnyAsync(
            especie =>
                (excludingId == null || especie.Id != excludingId) &&
                especie.NomeCientifico != null &&
                especie.NomeCientifico.ToLower() == nomeCientifico.ToLower(),
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: CommonNameIndex
            })
        {
            throw new EspecieDuplicateException(EspecieDuplicateField.NomeComum, exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ScientificNameIndex
            })
        {
            throw new EspecieDuplicateException(EspecieDuplicateField.NomeCientifico, exception);
        }
    }

    private static IOrderedQueryable<Especie> ApplyOrdering(
        IQueryable<Especie> query,
        EspecieSortField sortBy,
        bool descending) => (sortBy, descending) switch
    {
        (EspecieSortField.NomeComum, false) => query.OrderBy(especie => especie.NomeComum).ThenBy(especie => especie.Id),
        (EspecieSortField.NomeComum, true) => query.OrderByDescending(especie => especie.NomeComum).ThenByDescending(especie => especie.Id),
        (EspecieSortField.NomeCientifico, false) => query.OrderBy(especie => especie.NomeCientifico).ThenBy(especie => especie.Id),
        (EspecieSortField.NomeCientifico, true) => query.OrderByDescending(especie => especie.NomeCientifico).ThenByDescending(especie => especie.Id),
        (EspecieSortField.Ativo, false) => query.OrderBy(especie => especie.Ativo).ThenBy(especie => especie.Id),
        (EspecieSortField.Ativo, true) => query.OrderByDescending(especie => especie.Ativo).ThenByDescending(especie => especie.Id),
        (EspecieSortField.CreatedAtUtc, false) => query.OrderBy(especie => especie.CreatedAtUtc).ThenBy(especie => especie.Id),
        (EspecieSortField.CreatedAtUtc, true) => query.OrderByDescending(especie => especie.CreatedAtUtc).ThenByDescending(especie => especie.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy)),
    };

    private static string EscapeLikePattern(string search) => search
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
