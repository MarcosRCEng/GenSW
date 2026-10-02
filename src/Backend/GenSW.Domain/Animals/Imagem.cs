namespace GenSW.Domain.Animals;

// Common behavior only; concrete owners have independent tables and foreign keys.
public abstract class Imagem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string? Legenda { get; private set; }
    public DateOnly? DataCaptura { get; private set; }
    public int Ordem { get; private set; }
    public bool Representativa { get; private set; }
    public bool Ativa { get; private set; } = true;
    public string ArquivoKey { get; private set; } = "";
    public string MiniaturaKey { get; private set; } = "";
    public string Mime { get; private set; } = "image/png";
    public long TamanhoBytes { get; private set; }
    public int Largura { get; private set; }
    public int Altura { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    protected void Inicializar(string arquivo, string miniatura, long bytes, int largura, int altura,
        int ordem, string? legenda, DateOnly? data, DateTimeOffset now)
    {
        if (bytes <= 0 || largura is < 1 or > 2048 || altura is < 1 or > 2048) throw new ArgumentException("Derivados inválidos.");
        ArquivoKey = arquivo; MiniaturaKey = miniatura; TamanhoBytes = bytes; Largura = largura; Altura = altura;
        CreatedAtUtc = now; Ordenar(ordem, now); Editar(legenda, data, now);
    }
    public void Editar(string? legenda, DateOnly? data, DateTimeOffset now)
    {
        legenda = string.IsNullOrWhiteSpace(legenda) ? null : legenda.Trim();
        if (legenda?.Length > 200 || data > DateOnly.FromDateTime(now.UtcDateTime)) throw new ArgumentException("Legenda ou data de captura inválida.");
        Legenda = legenda; DataCaptura = data; UpdatedAtUtc = now;
    }
    public void Ordenar(int ordem, DateTimeOffset now)
    {
        if (ordem < 0) throw new ArgumentException("Ordem deve ser positiva ou zero.");
        Ordem = ordem; UpdatedAtUtc = now;
    }
    public void Preferir(bool value, DateTimeOffset now)
    {
        if (value && !Ativa) throw new InvalidOperationException("Imagem inativa não pode ser representativa.");
        Representativa = value; UpdatedAtUtc = now;
    }
    public void DefinirAtiva(bool value, DateTimeOffset now)
    {
        Ativa = value; if (!value) Representativa = false; UpdatedAtUtc = now;
    }
}

public sealed class ImagemAnimal : Imagem
{
    private ImagemAnimal() { }
    public Guid AnimalId { get; private set; }
    public static ImagemAnimal Criar(Guid owner, string arquivo, string miniatura, long bytes, int largura,
        int altura, int ordem, string? legenda, DateOnly? data, DateTimeOffset now)
    {
        if (owner == Guid.Empty) throw new ArgumentException("Animal obrigatório.");
        var item = new ImagemAnimal { AnimalId = owner };
        item.Inicializar(arquivo, miniatura, bytes, largura, altura, ordem, legenda, data, now); return item;
    }
}
