using GenSW.Domain.People;

namespace GenSW.API.Contracts.People;

public sealed record CreatePessoaRequest(TipoPessoa TipoPessoa, string Nome, string? NomeFantasia);
