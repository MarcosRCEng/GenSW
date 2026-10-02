using GenSW.Domain.Animals;
namespace GenSW.Domain.Varieties;

public sealed class ImagemVariedade : Imagem
{
    private ImagemVariedade() { }
    public Guid VariedadeId { get; private set; }
    public static ImagemVariedade Criar(Guid owner, string arquivo, string miniatura, long bytes, int largura,
        int altura, int ordem, string? legenda, DateOnly? data, DateTimeOffset now)
    {
        if (owner == Guid.Empty) throw new ArgumentException("Variedade obrigatória.");
        var item = new ImagemVariedade { VariedadeId = owner };
        item.Inicializar(arquivo, miniatura, bytes, largura, altura, ordem, legenda, data, now); return item;
    }
}
