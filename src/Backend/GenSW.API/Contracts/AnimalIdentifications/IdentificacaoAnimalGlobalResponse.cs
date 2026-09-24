namespace GenSW.API.Contracts.AnimalIdentifications;

public sealed record IdentificacaoAnimalAnimalResumoResponse(Guid Id, string CodigoInterno, string? Nome);

public sealed record IdentificacaoAnimalGlobalResponse(
    IdentificacaoAnimalResponse Identificacao,
    IdentificacaoAnimalAnimalResumoResponse Animal);

public sealed record IdentificacoesAnimalGlobalListResponse(
    IReadOnlyList<IdentificacaoAnimalGlobalResponse> Items,
    int Page, int PageSize, int TotalItems, int TotalPages);
