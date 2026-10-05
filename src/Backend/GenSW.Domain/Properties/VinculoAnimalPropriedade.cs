namespace GenSW.Domain.Properties;

public sealed class VinculoAnimalPropriedade
{
    private VinculoAnimalPropriedade() { }

    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public Guid PropriedadeId { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static VinculoAnimalPropriedade Criar(Guid animalId, Guid propriedadeId, DateOnly dataInicio,
        string? observacao, DateTimeOffset now)
    {
        if (animalId == Guid.Empty || propriedadeId == Guid.Empty)
            throw new ArgumentException("Animal e Propriedade devem ser informados.");
        ValidateDate(dataInicio, now);
        return new()
        {
            Id = Guid.NewGuid(), AnimalId = animalId, PropriedadeId = propriedadeId, DataInicio = dataInicio,
            Observacao = Propriedade.NormalizeOptional(observacao, 2000, nameof(observacao)),
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };
    }

    public void Encerrar(DateOnly dataFim, DateTimeOffset now)
    {
        ValidateDate(dataFim, now);
        if (dataFim < DataInicio) throw new ArgumentException("O fim não pode anteceder o início do vínculo.", nameof(dataFim));
        if (DataFim is not null) throw new ArgumentException("Um vínculo encerrado não pode ser alterado.");
        DataFim = dataFim;
        UpdatedAtUtc = now;
    }

    private static void ValidateDate(DateOnly date, DateTimeOffset now)
    {
        if (date == default || date > DateOnly.FromDateTime(now.UtcDateTime))
            throw new ArgumentException("Informe uma data válida, sem data futura.", nameof(date));
    }
}
