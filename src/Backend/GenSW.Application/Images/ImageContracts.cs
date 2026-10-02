using GenSW.Application.Animals;
using GenSW.Domain.Animals;
namespace GenSW.Application.Images;

public enum ImageOwner { Animal, Variedade }
public sealed record ImageDerivatives(byte[] View, byte[] Thumbnail, int Width, int Height);
public sealed record StoredImage(string ViewKey, string ThumbnailKey);
public sealed record ImageSummary(Guid Id, string? Legenda, string ThumbnailPath);
public sealed record PreferredImage(string Origem, ImageSummary? Imagem);
public sealed record ImageResult(Guid Id, Guid? AnimalId, Guid? VariedadeId, string? Legenda, DateOnly? DataCaptura,
    int Ordem, bool Ativa, bool Representativa, string Mime, int Largura, int Altura, long TamanhoBytes,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string ThumbnailPath, string ConteudoPath);
public interface IImageProcessor { Task<ImageDerivatives> ProcessAsync(Stream input, CancellationToken ct); }
public interface IPrivateImageStorage
{
    Task<StoredImage> PublishAsync(ImageDerivatives image, CancellationToken ct);
    Task RemoveAsync(StoredImage image);
    bool Available(string key);
    Task<Stream?> OpenAsync(string key, CancellationToken ct);
}
public interface IImageRepository
{
    Task<IAnimalMutationScope> BeginAsync(ImageOwner kind, Guid owner, CancellationToken ct);
    Task<bool> OwnerExistsAsync(ImageOwner kind, Guid owner, CancellationToken ct);
    Task<Imagem?> GetAsync(ImageOwner kind, Guid owner, Guid id, CancellationToken ct);
    Task<IReadOnlyList<Imagem>> ActiveAsync(ImageOwner kind, Guid owner, CancellationToken ct);
    Task<AnimalEvolutionPage<Imagem>> ListAsync(ImageOwner kind, Guid owner, bool? active, int page, int size, CancellationToken ct);
    Task AddAsync(Imagem image, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
