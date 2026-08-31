using GenSW.Application.People;
using GenSW.Domain.People;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.People;

public sealed class PessoaRepository(GenSWDbContext context) : IPessoaRepository
{
    public Task AddAsync(Pessoa pessoa, CancellationToken cancellationToken = default)
        => context.Pessoas.AddAsync(pessoa, cancellationToken).AsTask();

    public Task<Pessoa?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Pessoas.AsNoTracking().SingleOrDefaultAsync(pessoa => pessoa.Id == id, cancellationToken);

    public Task<Pessoa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Pessoas.SingleOrDefaultAsync(pessoa => pessoa.Id == id, cancellationToken);

    public async Task<PessoaListPage> ListAsync(PessoaListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<Pessoa> filtered = context.Pessoas.AsNoTracking();
        if (query.TipoPessoa is { } tipoPessoa)
        {
            filtered = filtered.Where(pessoa => pessoa.TipoPessoa == tipoPessoa);
        }

        if (query.Ativo is { } ativo)
        {
            filtered = filtered.Where(pessoa => pessoa.Ativo == ativo);
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search is not null)
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            filtered = filtered.Where(pessoa =>
                EF.Functions.ILike(pessoa.Nome, pattern, "\\") ||
                (pessoa.NomeFantasia != null && EF.Functions.ILike(pessoa.NomeFantasia, pattern, "\\")));
        }

        var totalItems = await filtered.CountAsync(cancellationToken);
        var ordered = ApplyOrdering(filtered, query.SortBy, query.SortDescending);
        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PessoaListPage(items, totalItems);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    private static IOrderedQueryable<Pessoa> ApplyOrdering(
        IQueryable<Pessoa> query,
        PessoaSortField sortBy,
        bool descending) => (sortBy, descending) switch
    {
        (PessoaSortField.Nome, false) => query.OrderBy(pessoa => pessoa.Nome).ThenBy(pessoa => pessoa.Id),
        (PessoaSortField.Nome, true) => query.OrderByDescending(pessoa => pessoa.Nome).ThenByDescending(pessoa => pessoa.Id),
        (PessoaSortField.TipoPessoa, false) => query.OrderBy(pessoa => pessoa.TipoPessoa).ThenBy(pessoa => pessoa.Id),
        (PessoaSortField.TipoPessoa, true) => query.OrderByDescending(pessoa => pessoa.TipoPessoa).ThenByDescending(pessoa => pessoa.Id),
        (PessoaSortField.Ativo, false) => query.OrderBy(pessoa => pessoa.Ativo).ThenBy(pessoa => pessoa.Id),
        (PessoaSortField.Ativo, true) => query.OrderByDescending(pessoa => pessoa.Ativo).ThenByDescending(pessoa => pessoa.Id),
        (PessoaSortField.CreatedAtUtc, false) => query.OrderBy(pessoa => pessoa.CreatedAtUtc).ThenBy(pessoa => pessoa.Id),
        (PessoaSortField.CreatedAtUtc, true) => query.OrderByDescending(pessoa => pessoa.CreatedAtUtc).ThenByDescending(pessoa => pessoa.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy)),
    };

    private static string EscapeLikePattern(string search) => search
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
