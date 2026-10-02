using GenSW.Application.Animals;
using GenSW.Application.Images;
using Microsoft.Extensions.Configuration;
namespace GenSW.Infrastructure.Images;

public sealed class PrivateImageStorage(IConfiguration configuration) : IPrivateImageStorage
{
    private string Root()
    {
        var configured = configuration["Images:PrivateRoot"];
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)) throw Unavailable();
        var root = Path.GetFullPath(configured);
        // Never serve from an application directory or a repository. Operator provisions the volume explicitly.
        for (var dir = new DirectoryInfo(root); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git")) ||
                dir.Name.Equals("wwwroot", StringComparison.OrdinalIgnoreCase) || dir.Name.Equals(".gensw", StringComparison.OrdinalIgnoreCase)) throw Unavailable();
        if (!Directory.Exists(root)) throw Unavailable();
        return root;
    }
    private string Resolve(string key)
    {
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) || Path.GetExtension(key) != ".png" || Path.GetFileName(key) != key) throw Unavailable();
        return Path.Combine(Root(), key);
    }
    public bool Available(string key) { try { return File.Exists(Resolve(key)); } catch (AnimalEvolutionException) { return false; } }
    public async Task<StoredImage> PublishAsync(ImageDerivatives image, CancellationToken ct)
    {
        var files = new StoredImage($"{Guid.NewGuid():N}.png", $"{Guid.NewGuid():N}.png");
        try
        {
            var root = Root();
            var minimumFree = configuration.GetValue<long?>("Images:MinimumFreeBytes") ?? 100L * 1024 * 1024;
            if (new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace < minimumFree + image.View.LongLength + image.Thumbnail.LongLength) throw Unavailable();
            await PublishOne(Resolve(files.ViewKey), image.View, ct);
            await PublishOne(Resolve(files.ThumbnailKey), image.Thumbnail, ct);
            return files;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or AnimalEvolutionException or OperationCanceledException)
        {
            await RemoveAsync(files);
            if (e is OperationCanceledException) throw;
            throw Unavailable();
        }
    }
    private static async Task PublishOne(string path, byte[] bytes, CancellationToken ct)
    {
        var temporary = path + ".tmp";
        try { await File.WriteAllBytesAsync(temporary, bytes, ct); File.Move(temporary, path); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task RemoveAsync(StoredImage image)
    {
        foreach (var key in new[] { image.ViewKey, image.ThumbnailKey })
            try { File.Delete(Resolve(key)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or AnimalEvolutionException) { /* Manual orphan reconciliation documented. */ }
        return Task.CompletedTask;
    }
    public Task<Stream?> OpenAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try { return Task.FromResult<Stream?>(new FileStream(Resolve(key), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true)); }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw Unavailable(); }
    }
    private static AnimalEvolutionException Unavailable() => new(503, "armazenamento_indisponivel", "Armazenamento de imagens indisponível.");
}
