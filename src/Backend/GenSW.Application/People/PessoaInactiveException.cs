namespace GenSW.Application.People;

public sealed class PessoaInactiveException(Guid pessoaId)
    : Exception($"Pessoa '{pessoaId}' is inactive.")
{
    public Guid PessoaId { get; } = pessoaId;
}
