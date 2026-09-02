namespace GenSW.Application.Breeds;

public sealed record PagedRacaResult(
    IReadOnlyList<RacaResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
