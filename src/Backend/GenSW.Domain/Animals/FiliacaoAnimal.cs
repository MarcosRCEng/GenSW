namespace GenSW.Domain.Animals;

public sealed class FiliacaoAnimal
{
    private FiliacaoAnimal() { }
    private FiliacaoAnimal(Guid animalId, Guid progenitorId, TipoFiliacaoAnimal tipo, DateOnly? dataRegistro, DateTimeOffset now)
    { Id = Guid.NewGuid(); AnimalId = animalId; ProgenitorId = progenitorId; TipoFiliacao = tipo; DataRegistro = dataRegistro; Ativa = true; CreatedAtUtc = now; UpdatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public Guid ProgenitorId { get; private set; }
    public TipoFiliacaoAnimal TipoFiliacao { get; private set; }
    public bool Ativa { get; private set; }
    public DateOnly? DataRegistro { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static FiliacaoAnimal Criar(Guid animalId, Guid progenitorId, TipoFiliacaoAnimal tipo, DateOnly? dataRegistro, DateTimeOffset now)
    { if (animalId == Guid.Empty || progenitorId == Guid.Empty) throw new ArgumentException("Animal and progenitor are required."); if (animalId == progenitorId) throw new ArgumentException("Animal cannot be its own progenitor."); if (!Enum.IsDefined(tipo)) throw new ArgumentException("Filiation type is invalid."); return new(animalId, progenitorId, tipo, dataRegistro, now); }
    public void Inativar(DateOnly? dataFim, DateTimeOffset now) { if (!Ativa) return; if (dataFim < DataRegistro) throw new ArgumentException("End date cannot precede registration date."); Ativa = false; DataFim = dataFim; UpdatedAtUtc = now; }
}
