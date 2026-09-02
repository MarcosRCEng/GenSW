namespace GenSW.Application.Breeds;

public sealed class RacaNotFoundException(Guid racaId)
    : Exception($"Raca '{racaId}' was not found.")
{
    public Guid RacaId { get; } = racaId;
}
