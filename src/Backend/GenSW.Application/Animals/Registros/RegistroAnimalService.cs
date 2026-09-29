using GenSW.Application.Animals;
using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Registros;

public sealed class RegistroAnimalService(IRegistroAnimalRepository repository, TimeProvider timeProvider) : IRegistroAnimalService
{
    public async Task<RegistroAnimalResult> CreateAsync(Guid animalId, CreateRegistroAnimalCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var scope = await repository.BeginMutationAsync(cancellationToken);
        _ = await repository.LockAnimalAsync(animalId, cancellationToken) ?? throw new AnimalNotFoundException(animalId);
        var registro = RegistroAnimal.Criar(animalId, command.TipoRegistro, command.NumeroRegistro, command.DataInicio, timeProvider.GetUtcNow());
        if (await repository.HasDuplicateAsync(registro.TipoRegistro, registro.NumeroRegistro, cancellationToken))
            throw new RegistroAnimalDuplicateException(registro.TipoRegistro, registro.NumeroRegistro);
        if (await repository.HasActiveTypeAsync(animalId, registro.TipoRegistro, cancellationToken))
            throw new RegistroAnimalActiveTypeConflictException(animalId, registro.TipoRegistro);
        await repository.AddAsync(registro, cancellationToken); await repository.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken); return ToResult(registro);
    }

    public async Task<IReadOnlyList<RegistroAnimalResult>> ListByAnimalAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        if (!await repository.AnimalExistsAsync(animalId, cancellationToken)) throw new AnimalNotFoundException(animalId);
        return await repository.ListByAnimalAsync(animalId, cancellationToken);
    }

    public async Task<RegistroAnimalResult> InactivateAsync(Guid animalId, Guid registroId, InactivateRegistroAnimalCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var scope = await repository.BeginMutationAsync(cancellationToken);
        _ = await repository.LockAnimalAsync(animalId, cancellationToken) ?? throw new AnimalNotFoundException(animalId);
        var registro = await repository.GetForUpdateAsync(animalId, registroId, cancellationToken) ?? throw new RegistroAnimalNotFoundException(animalId, registroId);
        registro.Inativar(command.DataFim, timeProvider.GetUtcNow()); await repository.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken); return ToResult(registro);
    }

    private static RegistroAnimalResult ToResult(RegistroAnimal item) => new(item.Id, item.AnimalId, item.TipoRegistro,
        item.NumeroRegistro, item.Ativo, item.DataInicio, item.DataFim, item.CreatedAtUtc, item.UpdatedAtUtc);
}
