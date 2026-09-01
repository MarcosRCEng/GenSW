namespace GenSW.Application.Species;

public enum EspecieDuplicateField { NomeComum, NomeCientifico }

public sealed class EspecieDuplicateException : Exception
{
    public EspecieDuplicateException(EspecieDuplicateField field) : base(BuildMessage(field))
        => Field = field;

    public EspecieDuplicateException(
        EspecieDuplicateField field,
        Exception innerException) : base(BuildMessage(field), innerException)
        => Field = field;

    public EspecieDuplicateField Field { get; }

    private static string BuildMessage(EspecieDuplicateField field) => field switch
    {
        EspecieDuplicateField.NomeComum => "A species with the same common name already exists.",
        EspecieDuplicateField.NomeCientifico => "A species with the same scientific name already exists.",
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
