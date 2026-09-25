namespace GenSW.Application.Animals.Identificacoes;

public sealed class IdentificacaoAnimalService(IIdentificacaoAnimalRepository repository, TimeProvider timeProvider)
    : IIdentificacaoAnimalService
{
    private readonly IIdentificacaoAnimalRepository _repository = repository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IdentificacaoAnimalResult> CreateAsync(Guid animalId, CreateIdentificacaoAnimalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!command.Principal)
        {
            await EnsureAnimalExistsAsync(animalId, cancellationToken);
            var identification = CreateIdentification(animalId, command);
            await EnsureNotDuplicateAsync(identification, cancellationToken);
            await _repository.AddAsync(identification, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return ToResult(identification);
        }

        await using var mutation = await _repository.BeginMutationAsync(cancellationToken);
        _ = await _repository.LockAnimalAsync(animalId, cancellationToken)
            ?? throw new AnimalNotFoundException(animalId);

        var principal = CreateIdentification(animalId, command with { Principal = false });
        await EnsureNotDuplicateAsync(principal, cancellationToken);

        var currentPrincipal = await _repository.GetCurrentPrincipalForUpdateAsync(animalId, cancellationToken);
        if (currentPrincipal is not null)
        {
            currentPrincipal.RemoverPrincipal(_timeProvider.GetUtcNow());
            await _repository.SaveChangesAsync(cancellationToken);
        }

        principal.DefinirPrincipal(_timeProvider.GetUtcNow());
        await _repository.AddAsync(principal, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        await mutation.CommitAsync(cancellationToken);
        return ToResult(principal);
    }

    public async Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId,
        IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);
        await EnsureAnimalExistsAsync(animalId, cancellationToken);
        return await _repository.ListByAnimalAsync(animalId, query, cancellationToken);
    }

    public async Task<IdentificacaoAnimalResult> GetByAnimalAsync(Guid animalId, Guid identificacaoId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAnimalExistsAsync(animalId, cancellationToken);
        return await _repository.GetByAnimalReadOnlyAsync(animalId, identificacaoId, cancellationToken)
            ?? throw new IdentificacaoAnimalNotFoundException(animalId, identificacaoId);
    }

    public async Task<IdentificacaoAnimalResult> UpdateMetadataAsync(Guid animalId, Guid identificacaoId,
        UpdateIdentificacaoAnimalMetadataCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.HasDataAplicacao && !command.HasObservacao)
        {
            throw new ArgumentException("At least one metadata field must be supplied.", nameof(command));
        }

        await EnsureAnimalExistsAsync(animalId, cancellationToken);
        var identification = await GetForUpdateAsync(animalId, identificacaoId, cancellationToken);
        identification.AlterarMetadados(command.HasDataAplicacao, command.DataAplicacao,
            command.HasObservacao, command.Observacao, _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return ToResult(identification);
    }

    public async Task<IdentificacaoAnimalResult> SetAtivoAsync(Guid animalId, Guid identificacaoId,
        SetIdentificacaoAnimalAtivoCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Ativo)
        {
            await EnsureAnimalExistsAsync(animalId, cancellationToken);
            var activeIdentification = await GetForUpdateAsync(animalId, identificacaoId, cancellationToken);
            activeIdentification.Reativar(_timeProvider.GetUtcNow());
            await _repository.SaveChangesAsync(cancellationToken);
            return ToResult(activeIdentification);
        }

        await using var mutation = await _repository.BeginMutationAsync(cancellationToken);
        _ = await _repository.LockAnimalAsync(animalId, cancellationToken)
            ?? throw new AnimalNotFoundException(animalId);
        var identification = await GetForUpdateAsync(animalId, identificacaoId, cancellationToken);
        identification.Inativar(_timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        await mutation.CommitAsync(cancellationToken);
        return ToResult(identification);
    }

    public async Task<IdentificacaoAnimalResult> SetPrincipalAsync(Guid animalId, Guid identificacaoId,
        SetIdentificacaoAnimalPrincipalCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var mutation = await _repository.BeginMutationAsync(cancellationToken);
        _ = await _repository.LockAnimalAsync(animalId, cancellationToken)
            ?? throw new AnimalNotFoundException(animalId);
        var target = await GetForUpdateAsync(animalId, identificacaoId, cancellationToken);

        if (!command.Principal)
        {
            target.RemoverPrincipal(_timeProvider.GetUtcNow());
            await _repository.SaveChangesAsync(cancellationToken);
            await mutation.CommitAsync(cancellationToken);
            return ToResult(target);
        }

        if (!target.Ativo)
        {
            throw new InvalidOperationException("Inactive identification cannot be principal.");
        }

        var currentPrincipal = await _repository.GetCurrentPrincipalForUpdateAsync(animalId, cancellationToken);
        if (currentPrincipal is not null && currentPrincipal.Id != target.Id)
        {
            currentPrincipal.RemoverPrincipal(_timeProvider.GetUtcNow());
            await _repository.SaveChangesAsync(cancellationToken);
        }

        target.DefinirPrincipal(_timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        await mutation.CommitAsync(cancellationToken);
        return ToResult(target);
    }

    public async Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);
        return await _repository.ListGlobalAsync(query, cancellationToken);
    }

    private GenSW.Domain.Animals.IdentificacaoAnimal CreateIdentification(Guid animalId,
        CreateIdentificacaoAnimalCommand command) => GenSW.Domain.Animals.IdentificacaoAnimal.Criar(
        animalId, command.Tipo, command.DescricaoTipo, command.Valor, command.Principal,
        command.DataAplicacao, command.Observacao, _timeProvider.GetUtcNow());

    private async Task EnsureNotDuplicateAsync(GenSW.Domain.Animals.IdentificacaoAnimal identification,
        CancellationToken cancellationToken)
    {
        if (await _repository.HasDuplicateAsync(identification.Tipo, identification.DescricaoTipo,
                identification.Valor, cancellationToken))
        {
            throw new IdentificacaoAnimalDuplicateException(identification.Tipo, identification.DescricaoTipo,
                identification.Valor, IdentificacaoAnimalDuplicateConflictSource.PreCheck);
        }
    }

    private async Task EnsureAnimalExistsAsync(Guid animalId, CancellationToken cancellationToken)
    {
        if (!await _repository.AnimalExistsAsync(animalId, cancellationToken))
        {
            throw new AnimalNotFoundException(animalId);
        }
    }

    private async Task<GenSW.Domain.Animals.IdentificacaoAnimal> GetForUpdateAsync(Guid animalId,
        Guid identificacaoId, CancellationToken cancellationToken) =>
        await _repository.GetByAnimalForUpdateAsync(animalId, identificacaoId, cancellationToken)
        ?? throw new IdentificacaoAnimalNotFoundException(animalId, identificacaoId);

    private static void ValidateListQuery(IdentificacaoAnimalListQuery query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page));
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        }

        if (query.Tipo is not null && !Enum.IsDefined(query.Tipo.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(query.Tipo));
        }
    }

    private static IdentificacaoAnimalResult ToResult(GenSW.Domain.Animals.IdentificacaoAnimal identification) =>
        new(identification.Id, identification.AnimalId, identification.Tipo, identification.DescricaoTipo,
            identification.Valor, identification.Principal, identification.DataAplicacao,
            identification.Observacao, identification.Ativo, identification.CreatedAtUtc,
            identification.UpdatedAtUtc);
}
