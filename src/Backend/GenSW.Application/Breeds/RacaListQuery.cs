namespace GenSW.Application.Breeds;

public sealed record RacaListQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? EspecieId = null,
    bool? Ativo = null,
    RacaSortField SortBy = RacaSortField.Nome,
    bool SortDescending = false);
