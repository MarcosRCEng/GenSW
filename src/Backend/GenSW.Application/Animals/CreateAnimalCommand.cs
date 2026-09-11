using GenSW.Domain.Animals;

namespace GenSW.Application.Animals;

public sealed record CreateAnimalCommand(
    string? CodigoInterno, string? Nome, Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo);
