using GenSW.Domain.Animals;

namespace GenSW.Application.Animals;

public sealed record AnimalEspecieResumo(Guid Id, string NomeComum, bool Ativo);
public sealed record AnimalRacaResumo(Guid Id, string Nome, bool Ativo);
public sealed record AnimalVariedadeResumo(Guid Id, string Nome, bool Ativo);

public sealed record AnimalResult(
    Guid Id, string CodigoInterno, string? Nome, Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    AnimalEspecieResumo Especie, AnimalRacaResumo? Raca, AnimalVariedadeResumo? Variedade);

public sealed record AnimalReadModel(
    Guid Id, string CodigoInterno, string? Nome, Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    AnimalEspecieResumo Especie, AnimalRacaResumo? Raca, AnimalVariedadeResumo? Variedade);
