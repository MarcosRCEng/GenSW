namespace GenSW.Domain.Animals;

public sealed class ProducaoOvo
{
    private ProducaoOvo() { }
    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public DateOnly DataPostura { get; private set; }
    public decimal PesoGramas { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ProducaoOvo Criar(Guid animalId, DateOnly dataPostura, decimal pesoGramas, string? observacao, DateTimeOffset now)
    {
        if (animalId == Guid.Empty) throw new ArgumentException("Animal is required.");
        var item = new ProducaoOvo { Id = Guid.NewGuid(), AnimalId = animalId, CreatedAtUtc = now };
        item.Atualizar(dataPostura, pesoGramas, observacao, now);
        return item;
    }

    public void Atualizar(DateOnly dataPostura, decimal pesoGramas, string? observacao, DateTimeOffset now)
    {
        if (pesoGramas is <= 0 or > 100000) throw new ArgumentException("Egg weight must be greater than zero and realistic.");
        var clean = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        if (clean?.Length > 2000) throw new ArgumentException("Observation is too long.");
        DataPostura = dataPostura; PesoGramas = pesoGramas; Observacao = clean; UpdatedAtUtc = now;
    }
}
