namespace GenSW.Application.Varieties;

public sealed record VariedadeEspecieResumo(Guid Id, string NomeComum, bool Ativo);

public sealed record VariedadeResult(
    Guid Id, Guid EspecieId, string Nome, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    VariedadeEspecieResumo Especie);
