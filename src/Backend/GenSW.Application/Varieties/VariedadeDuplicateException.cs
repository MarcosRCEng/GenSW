namespace GenSW.Application.Varieties;

public sealed class VariedadeDuplicateException : Exception
{
    public VariedadeDuplicateException() : base("A variety with this name already exists for the species.") { }
    public VariedadeDuplicateException(Exception innerException) : base("A variety with this name already exists for the species.", innerException) { }
}
