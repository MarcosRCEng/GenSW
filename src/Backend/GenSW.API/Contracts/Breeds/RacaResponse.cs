namespace GenSW.API.Contracts.Breeds;

public sealed record RacaResponse(
    Guid Id,
    Guid EspecieId,
    string Nome,
    bool Ativo,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    EspecieResumoResponse Especie);
