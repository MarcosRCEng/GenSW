using GenSW.Application.Animals;
using GenSW.Domain.Properties;

namespace GenSW.Application.Properties;

public sealed class PropriedadeService(IPropriedadeRepository repository, TimeProvider timeProvider)
{
    public async Task<PropriedadeResult> CreateAsync(PropriedadeCommand command, CancellationToken ct = default)
    {
        var item = Propriedade.Criar(command.Nome, command.Localizacao, command.Observacao, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(item, null, ct);
        await repository.AddAsync(item, ct);
        await repository.SaveAsync(ct);
        return Map(item);
    }

    public async Task<PropriedadeResult> GetAsync(Guid id, CancellationToken ct = default) =>
        Map(await repository.GetAsync(id, false, ct) ?? throw NotFound("propriedade"));

    public async Task<AnimalEvolutionPage<PropriedadeResult>> ListAsync(PropriedadeListQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw new ArgumentException("Paginação inválida.");
        if (query.SortBy is not ("nome" or "ativo" or "createdAtUtc") || query.SortDirection is not ("asc" or "desc"))
            throw new ArgumentException("Ordenação inválida.");
        var page = await repository.ListAsync(query with { Search = query.Search?.Trim() }, ct);
        return new(page.Items.Select(Map).ToArray(), query.Page, query.PageSize, page.TotalItems,
            (int)Math.Ceiling(page.TotalItems / (double)query.PageSize));
    }

    public async Task<PropriedadeResult> UpdateAsync(Guid id, PropriedadeCommand command, CancellationToken ct = default)
    {
        await using var scope = await repository.BeginMutationAsync(null, ct);
        var item = await repository.LockPropriedadeAsync(id, ct) ?? throw NotFound("propriedade");
        item.AlterarCadastro(command.Nome, command.Localizacao, command.Observacao, timeProvider.GetUtcNow());
        await EnsureUniqueAsync(item, id, ct);
        await repository.SaveAsync(ct);
        await scope.CommitAsync(ct);
        return Map(item);
    }

    public async Task<PropriedadeResult> SetActiveAsync(Guid id, bool ativo, CancellationToken ct = default)
    {
        await using var scope = await repository.BeginMutationAsync(null, ct);
        var item = await repository.LockPropriedadeAsync(id, ct) ?? throw NotFound("propriedade");
        item.AlterarStatus(ativo, timeProvider.GetUtcNow());
        await repository.SaveAsync(ct);
        await scope.CommitAsync(ct);
        return Map(item);
    }

    public async Task<AnimalPropriedadesResult> HistoryAsync(Guid animalId, CancellationToken ct = default)
    {
        await EnsureAnimalAsync(animalId, ct);
        return await ReadHistoryAsync(animalId, ct);
    }

    public async Task<AnimalPropriedadesResult> TransferAsync(Guid animalId, TransferirAnimalCommand command, CancellationToken ct = default)
    {
        await using var scope = await repository.BeginMutationAsync(animalId, ct);
        await EnsureAnimalAsync(animalId, ct);
        var current = await repository.GetCurrentAsync(animalId, ct);
        EnsureExpected(current, command.VinculoAtualIdEsperado);
        var destination = await repository.LockPropriedadeAsync(command.PropriedadeId, ct) ?? throw NotFound("propriedade");
        if (!destination.Ativo) throw Conflict("propriedade_inativa", "A Propriedade de destino está inativa.");
        if (current?.PropriedadeId == destination.Id)
            throw Conflict("propriedade_atual", "O Animal já está vinculado a esta Propriedade.");
        var now = timeProvider.GetUtcNow();
        var next = VinculoAnimalPropriedade.Criar(animalId, destination.Id, command.DataInicio, command.Observacao, now);
        if (current is null && await repository.GetLastEndAsync(animalId, ct) is { } lastEnd && command.DataInicio < lastEnd)
            throw new ArgumentException("O início não pode anteceder o último encerramento do histórico.");
        if (current is not null)
        {
            current.Encerrar(command.DataInicio, now);
            // Persist closure before insertion to satisfy the partial unique index within the same transaction.
            await repository.SaveAsync(ct);
        }
        await repository.AddVinculoAsync(next, ct);
        await repository.SaveAsync(ct);
        var result = await ReadHistoryAsync(animalId, ct);
        await scope.CommitAsync(ct);
        return result;
    }

    public async Task<AnimalPropriedadesResult> DetachAsync(Guid animalId, DesvincularAnimalCommand command, CancellationToken ct = default)
    {
        await using var scope = await repository.BeginMutationAsync(animalId, ct);
        await EnsureAnimalAsync(animalId, ct);
        var current = await repository.GetCurrentAsync(animalId, ct);
        EnsureExpected(current, command.VinculoAtualIdEsperado);
        if (current is null) throw Conflict("vinculo_ausente", "O Animal não possui Propriedade atual.");
        current.Encerrar(command.DataFim, timeProvider.GetUtcNow());
        await repository.SaveAsync(ct);
        var result = await ReadHistoryAsync(animalId, ct);
        await scope.CommitAsync(ct);
        return result;
    }

    private async Task EnsureUniqueAsync(Propriedade item, Guid? excludingId, CancellationToken ct)
    {
        if (await repository.NomeExistsAsync(item.Nome, excludingId, ct))
            throw Conflict("propriedade_duplicada", "Já existe uma Propriedade com este nome.");
    }

    private async Task EnsureAnimalAsync(Guid animalId, CancellationToken ct)
    {
        if (!await repository.AnimalExistsAsync(animalId, ct)) throw NotFound("animal");
    }

    private async Task<AnimalPropriedadesResult> ReadHistoryAsync(Guid animalId, CancellationToken ct)
    {
        var history = await repository.HistoryAsync(animalId, ct);
        return new(history.SingleOrDefault(x => x.DataFim is null), history);
    }

    private static void EnsureExpected(VinculoAnimalPropriedade? current, Guid? expected)
    {
        if (current?.Id != expected)
            throw Conflict("vinculo_desatualizado", "A Propriedade atual foi alterada. Atualize o histórico antes de tentar novamente.");
    }

    private static AnimalEvolutionException NotFound(string entity) => new(404, $"{entity}_nao_encontrado", "Registro não encontrado.");
    private static AnimalEvolutionException Conflict(string code, string message) => new(409, code, message);
    private static PropriedadeResult Map(Propriedade item) =>
        new(item.Id, item.Nome, item.Localizacao, item.Observacao, item.Ativo, item.CreatedAtUtc, item.UpdatedAtUtc);
}
