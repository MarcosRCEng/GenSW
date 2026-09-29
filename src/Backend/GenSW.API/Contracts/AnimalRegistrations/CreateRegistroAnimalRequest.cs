using GenSW.Domain.Animals;
namespace GenSW.API.Contracts.AnimalRegistrations;
public sealed record CreateRegistroAnimalRequest(TipoRegistroAnimal TipoRegistro, string NumeroRegistro, DateOnly DataInicio);
