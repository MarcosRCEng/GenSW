namespace GenSW.Application.Animals.Identificacoes;

public enum IdentificacaoAnimalPrincipalConflictSource
{
    PersistedNamedPrincipalAtivaUniqueConstraint
}

public sealed class IdentificacaoAnimalPrincipalConflictException : Exception
{
    public IdentificacaoAnimalPrincipalConflictException(IdentificacaoAnimalPrincipalConflictSource conflictSource,
        Exception? innerException = null)
        : base("Animal identification principal conflict.", innerException)
    {
        ConflictSource = conflictSource;
    }

    public IdentificacaoAnimalPrincipalConflictSource ConflictSource { get; }
}
