namespace GenSW.API.Contracts.Varieties;

public sealed record VariedadesListResponse(
    IReadOnlyList<VariedadeResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
