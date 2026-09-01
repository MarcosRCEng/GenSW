using GenSW.Domain.Species;

namespace GenSW.Application.Species;

public sealed class EspecieService(IEspecieRepository repository, TimeProvider timeProvider) : IEspecieService
{
    public async Task<EspecieResult> CreateAsync(CreateEspecieCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var especie = Especie.Criar(command.NomeComum, command.NomeCientifico, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(especie, null, cancellationToken);
        await repository.AddAsync(especie, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(especie);
    }

    public async Task<EspecieResult?> GetByIdAsync(Guid especieId, CancellationToken cancellationToken = default)
    {
        var especie = await repository.GetByIdReadOnlyAsync(especieId, cancellationToken);
        return especie is null ? null : ToResult(especie);
    }

    public async Task<PagedEspecieResult> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);
        var normalizedQuery = query with { Search = NormalizeSearch(query.Search) };
        var page = await repository.ListAsync(normalizedQuery, cancellationToken);
        var totalPages = page.TotalItems == 0 ? 0 : (int)Math.Ceiling(page.TotalItems / (double)normalizedQuery.PageSize);

        return new PagedEspecieResult(
            page.Items.Select(ToResult).ToArray(),
            normalizedQuery.Page,
            normalizedQuery.PageSize,
            page.TotalItems,
            totalPages);
    }

    public async Task<EspecieResult> UpdateAsync(Guid especieId, UpdateEspecieCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var especie = await GetTrackedAsync(especieId, cancellationToken);
        especie.AlterarCadastro(command.NomeComum, command.NomeCientifico, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(especie, especie.Id, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(especie);
    }

    public async Task<EspecieResult> SetActiveAsync(Guid especieId, bool ativo, CancellationToken cancellationToken = default)
    {
        var especie = await GetTrackedAsync(especieId, cancellationToken);
        if (ativo)
        {
            especie.Reativar(timeProvider.GetUtcNow());
        }
        else
        {
            especie.Inativar(timeProvider.GetUtcNow());
        }

        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(especie);
    }

    private async Task<Especie> GetTrackedAsync(Guid especieId, CancellationToken cancellationToken)
        => await repository.GetByIdForUpdateAsync(especieId, cancellationToken)
            ?? throw new EspecieNotFoundException(especieId);

    private async Task EnsureUniqueAsync(Especie especie, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await repository.HasNomeComumConflictAsync(especie.NomeComum, excludingId, cancellationToken))
        {
            throw new EspecieDuplicateException(EspecieDuplicateField.NomeComum);
        }

        if (especie.NomeCientifico is { } nomeCientifico &&
            await repository.HasNomeCientificoConflictAsync(nomeCientifico, excludingId, cancellationToken))
        {
            throw new EspecieDuplicateException(EspecieDuplicateField.NomeCientifico);
        }
    }

    private static void ValidateListQuery(EspecieListQuery query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page));
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        }

        if (!Enum.IsDefined(query.SortBy))
        {
            throw new ArgumentOutOfRangeException(nameof(query.SortBy));
        }
    }

    private static string? NormalizeSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static EspecieResult ToResult(Especie especie) => new(
        especie.Id,
        especie.NomeComum,
        especie.NomeCientifico,
        especie.Ativo,
        especie.CreatedAtUtc,
        especie.UpdatedAtUtc);
}
