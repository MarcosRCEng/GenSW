namespace GenSW.Application.Animals.Identificacoes;

public sealed class IdentificacaoAnimalNotFoundException(Guid animalId, Guid identificacaoId)
    : Exception($"Identification '{identificacaoId}' was not found for animal '{animalId}'.")
{
    public Guid AnimalId { get; } = animalId;
    public Guid IdentificacaoId { get; } = identificacaoId;
}
