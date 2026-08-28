namespace GenSW.API.Contracts.People;

public sealed record PessoasListResponse(IReadOnlyList<PessoaResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
