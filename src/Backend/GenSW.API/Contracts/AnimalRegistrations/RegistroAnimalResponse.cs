using GenSW.Domain.Animals;
namespace GenSW.API.Contracts.AnimalRegistrations;
public sealed record RegistroAnimalResponse(Guid Id, Guid AnimalId, TipoRegistroAnimal TipoRegistro, string NumeroRegistro, bool Ativo, DateOnly DataInicio, DateOnly? DataFim, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
