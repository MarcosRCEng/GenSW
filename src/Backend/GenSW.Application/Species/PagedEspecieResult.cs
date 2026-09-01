namespace GenSW.Application.Species;

public sealed record PagedEspecieResult(
    IReadOnlyList<EspecieResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
