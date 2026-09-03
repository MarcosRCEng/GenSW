namespace GenSW.API.Contracts.Breeds;

public sealed record RacasListResponse(
    IReadOnlyList<RacaResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
