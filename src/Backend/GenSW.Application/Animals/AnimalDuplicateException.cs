namespace GenSW.Application.Animals;

public enum AnimalDuplicateConflictSource
{
    PreCheck,
    PersistedNamedCodigoInternoUniqueConstraint
}

public sealed class AnimalDuplicateException : Exception
{
    public AnimalDuplicateException(
        string codigoInterno,
        AnimalDuplicateConflictSource source,
        Exception? innerException = null)
        : base("CodigoInterno conflict.", innerException)
    {
        CodigoInterno = codigoInterno;
        ConflictSource = source;
    }

    public string CodigoInterno { get; }
    public AnimalDuplicateConflictSource ConflictSource { get; }
}
