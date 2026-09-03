namespace GenSW.Application.Varieties;

public sealed class VariedadeNotFoundException(Guid variedadeId) : Exception($"Variety '{variedadeId}' was not found.")
{
    public Guid VariedadeId { get; } = variedadeId;
}
