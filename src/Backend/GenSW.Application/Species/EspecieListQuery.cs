namespace GenSW.Application.Species;

public sealed record EspecieListQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    bool? Ativo = null,
    EspecieSortField SortBy = EspecieSortField.NomeComum,
    bool SortDescending = false);
