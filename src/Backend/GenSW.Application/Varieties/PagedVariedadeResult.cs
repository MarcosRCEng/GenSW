namespace GenSW.Application.Varieties;

public sealed record PagedVariedadeResult(
    IReadOnlyList<VariedadeResult> Items,
    int Page, int PageSize, int TotalItems, int TotalPages);
