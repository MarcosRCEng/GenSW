using GenSW.Application.Species;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;

namespace GenSW.Application.Breeds;

public sealed class RacaService(IRacaRepository repository, IEspecieRepository especieRepository, TimeProvider timeProvider) : IRacaService
{
    public async Task<RacaResult> CreateAsync(CreateRacaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var especie = await GetActiveEspecieAsync(command.EspecieId, cancellationToken);
        var raca = Raca.Criar(command.EspecieId, command.Nome, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(raca, null, cancellationToken);
        await repository.AddAsync(raca, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(raca, ToResumo(especie));
    }

    public async Task<RacaResult?> GetByIdAsync(Guid racaId, CancellationToken cancellationToken = default)
    {
        var raca = await repository.GetByIdReadOnlyAsync(racaId, cancellationToken);
        return raca is null ? null : ToResult(raca);
    }

    public async Task<PagedRacaResult> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);
        var normalizedQuery = query with { Search = NormalizeSearch(query.Search) };
        var page = await repository.ListAsync(normalizedQuery, cancellationToken);
        var totalPages = page.TotalItems == 0 ? 0 : (int)Math.Ceiling(page.TotalItems / (double)normalizedQuery.PageSize);
        return new PagedRacaResult(page.Items.Select(ToResult).ToArray(), normalizedQuery.Page, normalizedQuery.PageSize, page.TotalItems, totalPages);
    }

    public async Task<RacaResult> UpdateAsync(Guid racaId, UpdateRacaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var raca = await GetTrackedAsync(racaId, cancellationToken);
        RacaEspecieResumo? especieResumo = null;
        if (command.EspecieId != raca.EspecieId)
        {
            especieResumo = ToResumo(await GetActiveEspecieAsync(command.EspecieId, cancellationToken));
        }

        raca.AlterarCadastro(command.EspecieId, command.Nome, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(raca, raca.Id, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return especieResumo is null ? await GetResultAsync(racaId, cancellationToken) : ToResult(raca, especieResumo);
    }

    public async Task<RacaResult> SetActiveAsync(Guid racaId, bool ativo, CancellationToken cancellationToken = default)
    {
        var raca = await GetTrackedAsync(racaId, cancellationToken);
        if (ativo) raca.Reativar(timeProvider.GetUtcNow()); else raca.Inativar(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return await GetResultAsync(racaId, cancellationToken);
    }

    private async Task<Raca> GetTrackedAsync(Guid racaId, CancellationToken cancellationToken) =>
        await repository.GetByIdForUpdateAsync(racaId, cancellationToken) ?? throw new RacaNotFoundException(racaId);

    private async Task<RacaResult> GetResultAsync(Guid racaId, CancellationToken cancellationToken) =>
        (await GetByIdAsync(racaId, cancellationToken)) ?? throw new RacaNotFoundException(racaId);

    private async Task<Especie> GetActiveEspecieAsync(Guid especieId, CancellationToken cancellationToken)
    {
        var especie = await especieRepository.GetByIdReadOnlyAsync(especieId, cancellationToken) ?? throw new EspecieNotFoundException(especieId);
        if (!especie.Ativo) throw new ArgumentException("The breed must be linked to an active species.", nameof(especieId));
        return especie;
    }

    private async Task EnsureUniqueAsync(Raca raca, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await repository.HasNomeConflictAsync(raca.EspecieId, raca.Nome, excludingId, cancellationToken)) throw new RacaDuplicateException();
    }

    private static void ValidateListQuery(RacaListQuery query)
    {
        if (query.Page < 1) throw new ArgumentOutOfRangeException(nameof(query.Page));
        if (query.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        if (!Enum.IsDefined(query.SortBy)) throw new ArgumentOutOfRangeException(nameof(query.SortBy));
    }

    private static string? NormalizeSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    private static RacaEspecieResumo ToResumo(Especie especie) => new(especie.Id, especie.NomeComum, especie.Ativo);
    private static RacaResult ToResult(Raca raca, RacaEspecieResumo especie) => new(raca.Id, raca.EspecieId, raca.Nome, raca.Ativo, raca.CreatedAtUtc, raca.UpdatedAtUtc, especie);
    private static RacaResult ToResult(RacaReadModel raca) => new(raca.Id, raca.EspecieId, raca.Nome, raca.Ativo, raca.CreatedAtUtc, raca.UpdatedAtUtc, raca.Especie);
}
