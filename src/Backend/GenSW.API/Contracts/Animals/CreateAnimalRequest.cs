using GenSW.Domain.Animals;

namespace GenSW.API.Contracts.Animals;

public sealed record CreateAnimalRequest(
    string? CodigoInterno, string? Nome, Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo);
