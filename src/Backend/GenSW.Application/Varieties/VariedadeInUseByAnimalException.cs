namespace GenSW.Application.Varieties;

public sealed class VariedadeInUseByAnimalException(Guid variedadeId)
    : Exception($"Variety '{variedadeId}' is referenced by an animal.")
{
    public Guid VariedadeId { get; } = variedadeId;
}
