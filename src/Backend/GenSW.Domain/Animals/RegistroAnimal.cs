namespace GenSW.Domain.Animals;

public sealed class RegistroAnimal
{
    private RegistroAnimal() { NumeroRegistro = null!; }

    private RegistroAnimal(Guid animalId, TipoRegistroAnimal tipoRegistro, string numeroRegistro,
        DateOnly dataInicio, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid(); AnimalId = animalId; TipoRegistro = tipoRegistro;
        NumeroRegistro = numeroRegistro; DataInicio = dataInicio; Ativo = true;
        CreatedAtUtc = nowUtc; UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public TipoRegistroAnimal TipoRegistro { get; private set; }
    public string NumeroRegistro { get; private set; }
    public bool Ativo { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static RegistroAnimal Criar(Guid animalId, TipoRegistroAnimal tipoRegistro, string numeroRegistro,
        DateOnly dataInicio, DateTimeOffset nowUtc)
    {
        if (animalId == Guid.Empty) throw new ArgumentException("Animal is required.", nameof(animalId));
        if (!Enum.IsDefined(tipoRegistro)) throw new ArgumentException("Registration type is invalid.", nameof(tipoRegistro));
        return new RegistroAnimal(animalId, tipoRegistro, NormalizeNumero(numeroRegistro), dataInicio, nowUtc);
    }

    public void Inativar(DateOnly dataFim, DateTimeOffset nowUtc)
    {
        if (!Ativo) return;
        if (dataFim < DataInicio) throw new ArgumentException("End date cannot precede start date.", nameof(dataFim));
        Ativo = false; DataFim = dataFim; UpdatedAtUtc = nowUtc;
    }

    private static string NormalizeNumero(string value)
    {
        ArgumentNullException.ThrowIfNull(value); var normalized = value.Trim();
        if (normalized.Length is < 1 or > 128) throw new ArgumentException("Registration number must have between 1 and 128 characters.", nameof(value));
        return normalized;
    }
}
