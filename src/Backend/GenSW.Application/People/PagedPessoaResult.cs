namespace GenSW.Application.People;

public sealed record PagedPessoaResult(
    IReadOnlyList<PessoaResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
