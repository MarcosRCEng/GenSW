using GenSW.Domain.Animals;
namespace GenSW.API.Contracts.AnimalFiliations;
public sealed record CreateFiliacaoAnimalRequest(Guid ProgenitorId, TipoFiliacaoAnimal TipoFiliacao, DateOnly? DataRegistro);
