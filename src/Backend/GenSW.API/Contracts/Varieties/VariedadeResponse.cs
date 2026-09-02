namespace GenSW.API.Contracts.Varieties;

public sealed record VariedadeResponse(
    Guid Id,
    Guid EspecieId,
    string Nome,
    bool Ativo,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    EspecieResumoResponse Especie);
