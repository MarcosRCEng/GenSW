using GenSW.Application.Species;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;

namespace GenSW.Application.Varieties;

public sealed class VariedadeService(IVariedadeRepository repository, IEspecieRepository especieRepository, TimeProvider timeProvider) : IVariedadeService
{
    public async Task<VariedadeResult> CreateAsync(CreateVariedadeCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var especie = await GetActiveEspecieAsync(command.EspecieId, cancellationToken);
        var variedade = Variedade.Criar(command.EspecieId, command.Nome, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(variedade, null, cancellationToken);
        await repository.AddAsync(variedade, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(variedade, ToResumo(especie));
    }

    public async Task<VariedadeResult?> GetByIdAsync(Guid variedadeId, CancellationToken cancellationToken = default)
    {
        var variedade = await repository.GetByIdReadOnlyAsync(variedadeId, cancellationToken);
        return variedade is null ? null : ToResult(variedade);
    }

    public async Task<PagedVariedadeResult> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);
        var normalizedQuery = query with { Search = NormalizeSearch(query.Search) };
        var page = await repository.ListAsync(normalizedQuery, cancellationToken);
        var totalPages = page.TotalItems == 0 ? 0 : (int)Math.Ceiling(page.TotalItems / (double)normalizedQuery.PageSize);
        return new PagedVariedadeResult(page.Items.Select(ToResult).ToArray(), normalizedQuery.Page, normalizedQuery.PageSize, page.TotalItems, totalPages);
    }

    public async Task<VariedadeResult> UpdateAsync(Guid variedadeId, UpdateVariedadeCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var variedade = await GetTrackedAsync(variedadeId, cancellationToken);
        VariedadeEspecieResumo? especieResumo = null;
        if (command.EspecieId != variedade.EspecieId)
        {
            especieResumo = ToResumo(await GetActiveEspecieAsync(command.EspecieId, cancellationToken));
        }

        variedade.AlterarCadastro(command.EspecieId, command.Nome, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(variedade, variedade.Id, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return especieResumo is null ? await GetResultAsync(variedadeId, cancellationToken) : ToResult(variedade, especieResumo);
    }

    public async Task<VariedadeResult> SetActiveAsync(Guid variedadeId, bool ativo, CancellationToken cancellationToken = default)
    {
        var variedade = await GetTrackedAsync(variedadeId, cancellationToken);
        if (ativo) variedade.Reativar(timeProvider.GetUtcNow()); else variedade.Inativar(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return await GetResultAsync(variedadeId, cancellationToken);
    }

    private async Task<Variedade> GetTrackedAsync(Guid variedadeId, CancellationToken cancellationToken) =>
        await repository.GetByIdForUpdateAsync(variedadeId, cancellationToken) ?? throw new VariedadeNotFoundException(variedadeId);

    private async Task<VariedadeResult> GetResultAsync(Guid variedadeId, CancellationToken cancellationToken) =>
        (await GetByIdAsync(variedadeId, cancellationToken)) ?? throw new VariedadeNotFoundException(variedadeId);

    private async Task<Especie> GetActiveEspecieAsync(Guid especieId, CancellationToken cancellationToken)
    {
        var especie = await especieRepository.GetByIdReadOnlyAsync(especieId, cancellationToken) ?? throw new EspecieNotFoundException(especieId);
        if (!especie.Ativo) throw new ArgumentException("The variety must be linked to an active species.", nameof(especieId));
        return especie;
    }

    private async Task EnsureUniqueAsync(Variedade variedade, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await repository.HasNomeConflictAsync(variedade.EspecieId, variedade.Nome, excludingId, cancellationToken)) throw new VariedadeDuplicateException();
    }

    private static void ValidateListQuery(VariedadeListQuery query)
    {
        if (query.Page < 1) throw new ArgumentOutOfRangeException(nameof(query.Page));
        if (query.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        if (!Enum.IsDefined(query.SortBy)) throw new ArgumentOutOfRangeException(nameof(query.SortBy));
    }

    private static string? NormalizeSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    private static VariedadeEspecieResumo ToResumo(Especie especie) => new(especie.Id, especie.NomeComum, especie.Ativo);
    private static VariedadeResult ToResult(Variedade variedade, VariedadeEspecieResumo especie) => new(variedade.Id, variedade.EspecieId, variedade.Nome, variedade.Ativo, variedade.CreatedAtUtc, variedade.UpdatedAtUtc, especie);
    private static VariedadeResult ToResult(VariedadeReadModel variedade) => new(variedade.Id, variedade.EspecieId, variedade.Nome, variedade.Ativo, variedade.CreatedAtUtc, variedade.UpdatedAtUtc, variedade.Especie);
}
