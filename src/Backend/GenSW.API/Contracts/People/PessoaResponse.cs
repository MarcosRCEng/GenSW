using GenSW.Domain.People;

namespace GenSW.API.Contracts.People;

public sealed record PessoaResponse(Guid Id, TipoPessoa TipoPessoa, string Nome, string? NomeFantasia, bool Ativo, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
