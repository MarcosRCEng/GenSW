using GenSW.Domain.Animals;

namespace GenSW.API.Contracts.AnimalIdentifications;

public sealed record CreateIdentificacaoAnimalRequest(
    TipoIdentificacaoAnimal Tipo, string? DescricaoTipo, string Valor,
    bool Principal, DateOnly? DataAplicacao, string? Observacao);
