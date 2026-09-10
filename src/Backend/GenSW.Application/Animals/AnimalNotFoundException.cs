namespace GenSW.Application.Animals;

public sealed class AnimalNotFoundException(Guid animalId) : Exception($"Animal '{animalId}' was not found.")
{
    public Guid AnimalId { get; } = animalId;
}
