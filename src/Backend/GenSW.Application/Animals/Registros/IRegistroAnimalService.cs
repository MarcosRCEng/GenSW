namespace GenSW.Application.Animals.Registros;

public interface IRegistroAnimalService
{
    Task<RegistroAnimalResult> CreateAsync(Guid animalId, CreateRegistroAnimalCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistroAnimalResult>> ListByAnimalAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<RegistroAnimalResult> InactivateAsync(Guid animalId, Guid registroId, InactivateRegistroAnimalCommand command, CancellationToken cancellationToken = default);
}
