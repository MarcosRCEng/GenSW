using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Identificacoes;

public enum IdentificacaoAnimalDuplicateConflictSource
{
    PreCheck,
    PersistedNamedTipoValorUniqueConstraint,
    PersistedNamedOutroDescricaoTipoValorUniqueConstraint
}

public sealed class IdentificacaoAnimalDuplicateException : Exception
{
    public IdentificacaoAnimalDuplicateException(TipoIdentificacaoAnimal tipo, string? descricaoTipo, string valor,
        IdentificacaoAnimalDuplicateConflictSource conflictSource, Exception? innerException = null)
        : base("Animal identification conflict.", innerException)
    {
        Tipo = tipo;
        DescricaoTipo = descricaoTipo;
        Valor = valor;
        ConflictSource = conflictSource;
    }

    public TipoIdentificacaoAnimal Tipo { get; }
    public string? DescricaoTipo { get; }
    public string Valor { get; }
    public IdentificacaoAnimalDuplicateConflictSource ConflictSource { get; }
}
