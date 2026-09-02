namespace GenSW.Application.Varieties;

public sealed record VariedadeListQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? EspecieId = null,
    bool? Ativo = null,
    VariedadeSortField SortBy = VariedadeSortField.Nome,
    bool SortDescending = false);
