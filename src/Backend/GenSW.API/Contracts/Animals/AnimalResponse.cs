using GenSW.Domain.Animals;

namespace GenSW.API.Contracts.Animals;

public sealed record AnimalEspecieResumoResponse(Guid Id, string NomeComum, bool Ativo);
public sealed record AnimalRacaResumoResponse(Guid Id, string Nome, bool Ativo);
public sealed record AnimalVariedadeResumoResponse(Guid Id, string Nome, bool Ativo);

public sealed record AnimalResponse(
    Guid Id, string CodigoInterno, string? Nome,
    Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    AnimalEspecieResumoResponse Especie,
    AnimalRacaResumoResponse? Raca,
    AnimalVariedadeResumoResponse? Variedade);
