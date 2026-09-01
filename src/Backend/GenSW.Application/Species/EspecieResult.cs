namespace GenSW.Application.Species;

public sealed record EspecieResult(
    Guid Id,
    string NomeComum,
    string? NomeCientifico,
    bool Ativo,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
