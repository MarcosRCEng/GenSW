namespace GenSW.Application.Breeds;

public sealed record RacaEspecieResumo(Guid Id, string NomeComum, bool Ativo);

public sealed record RacaResult(
    Guid Id, Guid EspecieId, string Nome, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    RacaEspecieResumo Especie);
