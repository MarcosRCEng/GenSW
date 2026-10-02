namespace GenSW.Domain.Animals;

public enum TipoMarcoPesagem { Livre = 1, Nascimento, IdadeEmDias, PrimeiraPostura, Abate, Outro }

public sealed class PesagemAnimal
{
    private PesagemAnimal() { }
    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public DateOnly DataMedicao { get; private set; }
    public decimal PesoGramas { get; private set; }
    public TipoMarcoPesagem TipoMarco { get; private set; }
    public string? DescricaoMarco { get; private set; }
    public int? IdadeReferenciaDias { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PesagemAnimal Criar(Guid animalId, DateOnly data, decimal peso, TipoMarcoPesagem marco,
        string? descricao, int? idadeAlvo, string? observacao, DateOnly? nascimento, DateTimeOffset now)
    {
        if (animalId == Guid.Empty) throw new ArgumentException("Animal obrigatório.");
        var item = new PesagemAnimal { Id = Guid.NewGuid(), AnimalId = animalId, CreatedAtUtc = now };
        item.Atualizar(data, peso, marco, descricao, idadeAlvo, observacao, nascimento, now);
        return item;
    }

    public void Atualizar(DateOnly data, decimal peso, TipoMarcoPesagem marco, string? descricao,
        int? idadeAlvo, string? observacao, DateOnly? nascimento, DateTimeOffset now)
    {
        descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        if (peso is < 0.01m or > 99999999.99m || decimal.Round(peso, 2) != peso)
            throw new ArgumentException("Peso deve estar entre 0,01 e 99.999.999,99 g, com até duas casas decimais.");
        if (!Enum.IsDefined(marco) || descricao?.Length > 100 || observacao?.Length > 2000 ||
            (marco == TipoMarcoPesagem.Outro && descricao is null) ||
            (marco == TipoMarcoPesagem.IdadeEmDias ? idadeAlvo is null or < 0 : idadeAlvo is not null))
            throw new ArgumentException("Marco, idade alvo ou descrição inválidos.");
        if (data > DateOnly.FromDateTime(now.UtcDateTime) ||
            (nascimento is { } birth && (data < birth || (marco == TipoMarcoPesagem.Nascimento && data != birth))))
            throw new ArgumentException("Data da medição incompatível com hoje ou com o nascimento.");
        DataMedicao = data; PesoGramas = peso; TipoMarco = marco; DescricaoMarco = descricao;
        IdadeReferenciaDias = idadeAlvo; Observacao = observacao; UpdatedAtUtc = now;
    }
}
