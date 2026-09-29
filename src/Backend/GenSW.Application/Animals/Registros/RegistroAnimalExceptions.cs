using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Registros;

public sealed class RegistroAnimalNotFoundException(Guid animalId, Guid registroId)
    : Exception($"Registration {registroId} was not found for animal {animalId}.");

public sealed class RegistroAnimalDuplicateException(TipoRegistroAnimal tipoRegistro, string numeroRegistro)
    : Exception($"Registration {tipoRegistro}:{numeroRegistro} already exists.");

public sealed class RegistroAnimalActiveTypeConflictException(Guid animalId, TipoRegistroAnimal tipoRegistro)
    : Exception($"Animal {animalId} already has an active {tipoRegistro} registration.");
