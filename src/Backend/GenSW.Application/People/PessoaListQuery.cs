using GenSW.Domain.People;

namespace GenSW.Application.People;

public sealed record PessoaListQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    TipoPessoa? TipoPessoa = null,
    bool? Ativo = null,
    PessoaSortField SortBy = PessoaSortField.Nome,
    bool SortDescending = false);
