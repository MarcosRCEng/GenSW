using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Identificacoes;

public sealed record IdentificacaoAnimalResult(
    Guid Id, Guid AnimalId, TipoIdentificacaoAnimal Tipo, string? DescricaoTipo,
    string Valor, bool Principal, DateOnly? DataAplicacao, string? Observacao,
    bool Ativo, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record IdentificacaoAnimalAnimalResumo(Guid Id, string CodigoInterno, string? Nome);

public sealed record IdentificacaoAnimalGlobalResult(
    IdentificacaoAnimalResult Identificacao, IdentificacaoAnimalAnimalResumo Animal);

public sealed record PagedIdentificacaoAnimalResult(
    IReadOnlyList<IdentificacaoAnimalResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public sealed record PagedIdentificacaoAnimalGlobalResult(
    IReadOnlyList<IdentificacaoAnimalGlobalResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);
