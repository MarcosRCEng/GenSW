using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.Filiacoes;
public sealed record CreateFiliacaoAnimalCommand(Guid ProgenitorId, TipoFiliacaoAnimal TipoFiliacao, DateOnly? DataRegistro);
public sealed record FiliacaoAnimalResult(Guid Id, Guid AnimalId, Guid ProgenitorId, TipoFiliacaoAnimal TipoFiliacao, bool Ativa, DateOnly? DataRegistro, DateOnly? DataFim, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, ProgenitorSummary? Progenitor = null);
public sealed record PedigreeAnimalResult(Guid AnimalId, string CodigoInterno, string? Nome, IReadOnlyList<PedigreeAnimalResult> Progenitores);
public sealed class FiliacaoAnimalConflictException(string message) : InvalidOperationException(message);
public sealed class FiliacaoAnimalNotFoundException(Guid animalId, Guid id) : KeyNotFoundException($"Filiation {id} was not found for animal {animalId}.");
