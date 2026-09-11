using GenSW.Domain.Animals;

namespace GenSW.Application.Animals;

internal sealed class AnimalAutomaticCreator(
    IAnimalRepository repository,
    IAnimalCodeAllocator allocator,
    TimeProvider timeProvider)
{
    private const int MaxAutomaticCodeAttempts = 5;

    public async Task<Animal> CreateAsync(
        CreateAnimalCommand command,
        CancellationToken cancellationToken = default)
    {
        for (var attemptNumber = 1; attemptNumber <= MaxAutomaticCodeAttempts; attemptNumber++)
        {
            await using var attempt = await allocator.BeginAttemptAsync(cancellationToken);
            var codigoInterno = await attempt.AllocateNextCodigoInternoAsync(cancellationToken);
            var nowUtc = timeProvider.GetUtcNow();
            var animal = Animal.Criar(
                codigoInterno,
                command.Nome,
                command.EspecieId,
                command.RacaId,
                command.VariedadeId,
                command.Sexo,
                command.DataNascimento,
                command.Escopo,
                DateOnly.FromDateTime(nowUtc.UtcDateTime),
                nowUtc);

            await repository.AddAsync(animal, cancellationToken);

            try
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (AnimalDuplicateException exception) when (
                exception.ConflictSource == AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint &&
                string.Equals(exception.CodigoInterno, codigoInterno, StringComparison.Ordinal))
            {
                await attempt.RollbackAndDetachAsync(animal, cancellationToken);
                if (attemptNumber == MaxAutomaticCodeAttempts)
                {
                    throw new AnimalAutomaticCodeCollisionLimitExceededException();
                }

                continue;
            }

            await attempt.CommitAsync(cancellationToken);
            return animal;
        }

        throw new InvalidOperationException("Automatic code allocation did not complete.");
    }
}
