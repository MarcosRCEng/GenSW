namespace GenSW.API.Contracts.AnimalIdentifications;

public sealed record IdentificacoesAnimalListResponse(
    IReadOnlyList<IdentificacaoAnimalResponse> Items,
    int Page, int PageSize, int TotalItems, int TotalPages);
