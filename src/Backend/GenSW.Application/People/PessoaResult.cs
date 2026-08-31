using GenSW.Domain.People;

namespace GenSW.Application.People;

public sealed record PessoaResult(
    Guid Id,
    TipoPessoa TipoPessoa,
    string Nome,
    string? NomeFantasia,
    bool Ativo,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
