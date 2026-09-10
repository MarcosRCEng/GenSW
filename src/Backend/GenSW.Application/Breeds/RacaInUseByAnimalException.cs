namespace GenSW.Application.Breeds;

public sealed class RacaInUseByAnimalException(Guid racaId)
    : Exception($"Breed '{racaId}' is referenced by an animal.")
{
    public Guid RacaId { get; } = racaId;
}
