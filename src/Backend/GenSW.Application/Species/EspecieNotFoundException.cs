namespace GenSW.Application.Species;

public sealed class EspecieNotFoundException(Guid especieId)
    : Exception($"Especie '{especieId}' was not found.")
{
    public Guid EspecieId { get; } = especieId;
}
