using GenSW.Domain.People;

namespace GenSW.Application.People;

public sealed record CreatePessoaCommand(TipoPessoa TipoPessoa, string Nome, string? NomeFantasia);
