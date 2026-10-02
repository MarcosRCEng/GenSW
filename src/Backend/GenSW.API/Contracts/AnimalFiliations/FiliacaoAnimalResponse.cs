using GenSW.Domain.Animals;
namespace GenSW.API.Contracts.AnimalFiliations;
public sealed record FiliacaoAnimalResponse(Guid Id, Guid AnimalId, Guid ProgenitorId, TipoFiliacaoAnimal TipoFiliacao, bool Ativa, DateOnly? DataRegistro, DateOnly? DataFim, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, GenSW.Application.Animals.Filiacoes.ProgenitorSummary? Progenitor = null);
public sealed record PedigreeAnimalResponse(Guid AnimalId, string CodigoInterno, string? Nome, IReadOnlyList<PedigreeAnimalResponse> Progenitores);
