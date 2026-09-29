using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Registros;

public sealed record RegistroAnimalResult(Guid Id, Guid AnimalId, TipoRegistroAnimal TipoRegistro, string NumeroRegistro,
    bool Ativo, DateOnly DataInicio, DateOnly? DataFim, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
