namespace GenSW.API.Contracts.Species;

public sealed record EspeciesListResponse(
    IReadOnlyList<EspecieResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
