using GenSW.Domain.Animals;

namespace GenSW.API.Contracts.AnimalIdentifications;

public sealed record IdentificacaoAnimalResponse(
    Guid Id, Guid AnimalId, TipoIdentificacaoAnimal Tipo, string? DescricaoTipo,
    string Valor, bool Principal, DateOnly? DataAplicacao, string? Observacao,
    bool Ativo, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
