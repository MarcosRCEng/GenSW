using GenSW.Domain.Animals;

namespace GenSW.Application.Animals;

public sealed class AnimalService : IAnimalService
{
    private readonly IAnimalRepository repository;
    private readonly AnimalClassificationValidator classificationValidator;
    private readonly AnimalAutomaticCreator automaticCreator;
    private readonly TimeProvider timeProvider;

    internal AnimalService(
        IAnimalRepository repository,
        AnimalClassificationValidator classificationValidator,
        AnimalAutomaticCreator automaticCreator,
        TimeProvider timeProvider)
    {
        this.repository = repository;
        this.classificationValidator = classificationValidator;
        this.automaticCreator = automaticCreator;
        this.timeProvider = timeProvider;
    }

    public async Task<AnimalResult> CreateAsync(CreateAnimalCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateClassificationIds(command.EspecieId, command.RacaId, command.VariedadeId);
        await classificationValidator.ValidateCreateAsync(command.EspecieId, command.RacaId, command.VariedadeId, cancellationToken);

        if (command.CodigoInterno is null)
        {
            var automaticAnimal = await automaticCreator.CreateAsync(command, cancellationToken);
            return await GetResultAsync(automaticAnimal.Id, cancellationToken);
        }

        var nowUtc = timeProvider.GetUtcNow();
        var animal = Animal.Criar(
            command.CodigoInterno,
            command.Nome,
            command.EspecieId,
            command.RacaId,
            command.VariedadeId,
            command.Sexo,
            command.DataNascimento,
            command.Escopo,
            DateOnly.FromDateTime(nowUtc.UtcDateTime),
            nowUtc);

        await EnsureCodigoInternoIsAvailableAsync(animal.CodigoInterno, null, cancellationToken);
        await repository.AddAsync(animal, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetResultAsync(animal.Id, cancellationToken);
    }

    public async Task<AnimalResult?> GetByIdAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        var animal = await repository.GetByIdReadOnlyAsync(animalId, cancellationToken);
        return animal is null ? null : ToResult(animal);
    }

    public async Task<PagedAnimalResult> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);

        var normalizedQuery = query with { Search = NormalizeSearch(query.Search) };
        var page = await repository.ListAsync(normalizedQuery, cancellationToken);
        var totalPages = page.TotalItems == 0 ? 0 : (int)Math.Ceiling(page.TotalItems / (double)normalizedQuery.PageSize);
        return new PagedAnimalResult(page.Items.Select(ToResult).ToArray(), normalizedQuery.Page, normalizedQuery.PageSize, page.TotalItems, totalPages);
    }

    public async Task<AnimalResult> UpdateAsync(Guid animalId, UpdateAnimalCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateClassificationIds(command.EspecieId, command.RacaId, command.VariedadeId);
        var animal = await GetTrackedAsync(animalId, cancellationToken);
        await classificationValidator.ValidateUpdateAsync(animal, command.EspecieId, command.RacaId, command.VariedadeId, cancellationToken);

        var nowUtc = timeProvider.GetUtcNow();
        animal.AlterarCadastro(
            command.CodigoInterno,
            command.Nome,
            command.EspecieId,
            command.RacaId,
            command.VariedadeId,
            command.Sexo,
            command.DataNascimento,
            command.Escopo,
            DateOnly.FromDateTime(nowUtc.UtcDateTime),
            nowUtc);

        await EnsureCodigoInternoIsAvailableAsync(animal.CodigoInterno, animal.Id, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetResultAsync(animal.Id, cancellationToken);
    }

    public async Task<AnimalResult> SetActiveAsync(Guid animalId, bool ativo, CancellationToken cancellationToken = default)
    {
        var animal = await GetTrackedAsync(animalId, cancellationToken);
        if (ativo)
        {
            animal.Reativar(timeProvider.GetUtcNow());
        }
        else
        {
            animal.Inativar(timeProvider.GetUtcNow());
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await GetResultAsync(animal.Id, cancellationToken);
    }

    private async Task<Animal> GetTrackedAsync(Guid animalId, CancellationToken cancellationToken) =>
        await repository.GetByIdForUpdateAsync(animalId, cancellationToken) ?? throw new AnimalNotFoundException(animalId);

    private async Task<AnimalResult> GetResultAsync(Guid animalId, CancellationToken cancellationToken) =>
        (await GetByIdAsync(animalId, cancellationToken)) ?? throw new AnimalNotFoundException(animalId);

    private async Task EnsureCodigoInternoIsAvailableAsync(string codigoInterno, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await repository.HasCodigoInternoConflictAsync(codigoInterno, excludingId, cancellationToken))
        {
            throw new AnimalDuplicateException(codigoInterno, AnimalDuplicateConflictSource.PreCheck);
        }
    }

    private static void ValidateListQuery(AnimalListQuery query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page));
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        }

        if (query.Sexo is not null && !Enum.IsDefined(query.Sexo.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(query.Sexo));
        }

        if (query.Escopo is not null && !Enum.IsDefined(query.Escopo.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(query.Escopo));
        }

        if (!Enum.IsDefined(query.SortBy))
        {
            throw new ArgumentOutOfRangeException(nameof(query.SortBy));
        }
    }

    private static void ValidateClassificationIds(Guid especieId, Guid? racaId, Guid? variedadeId)
    {
        if (especieId == Guid.Empty)
        {
            throw new ArgumentException("Species is required.", nameof(especieId));
        }

        if (racaId == Guid.Empty)
        {
            throw new ArgumentException("Breed cannot be empty when supplied.", nameof(racaId));
        }

        if (variedadeId == Guid.Empty)
        {
            throw new ArgumentException("Variety cannot be empty when supplied.", nameof(variedadeId));
        }
    }

    private static string? NormalizeSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static AnimalResult ToResult(AnimalReadModel animal) => new(
        animal.Id,
        animal.CodigoInterno,
        animal.Nome,
        animal.EspecieId,
        animal.RacaId,
        animal.VariedadeId,
        animal.Sexo,
        animal.DataNascimento,
        animal.Escopo,
        animal.Ativo,
        animal.CreatedAtUtc,
        animal.UpdatedAtUtc,
        animal.Especie,
        animal.Raca,
        animal.Variedade);
}
