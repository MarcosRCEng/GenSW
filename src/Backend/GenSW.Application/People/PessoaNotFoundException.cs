namespace GenSW.Application.People;

public sealed class PessoaNotFoundException(Guid pessoaId)
    : Exception($"Pessoa '{pessoaId}' was not found.")
{
    public Guid PessoaId { get; } = pessoaId;
}
