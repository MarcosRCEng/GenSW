namespace GenSW.API.Contracts.Species;

public sealed record EspecieResponse(
    Guid Id,
    string NomeComum,
    string? NomeCientifico,
    bool Ovipara,
    decimal? PesoPadraoOvoGramas,
    bool Ativo,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
