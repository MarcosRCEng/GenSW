using GenSW.Application.Accounts;
using GenSW.Application.Animals;
using GenSW.Application.Images;
using GenSW.Infrastructure.Accounts;
using Microsoft.Extensions.Configuration;

namespace GenSW.Infrastructure.Images;

public sealed record AccountPrivateVolume(string Root, Guid Id);

public sealed class PrivateImageStorage(Func<AccountPrivateVolume> volume, IConfiguration configuration) : IPrivateImageStorage
{
    private string Root()
    {
        try
        {
            var binding = volume();
            PrivateVolume.Verify(binding.Root, binding.Id);
            return binding.Root;
        }
        catch (Exception error) when (error is AccountUnavailableException or IOException or UnauthorizedAccessException)
        {
            throw Unavailable();
        }
    }

    private string Resolve(string key)
    {
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) ||
            Path.GetExtension(key) != ".png" || Path.GetFileName(key) != key)
            throw Unavailable();

        var path = Path.Combine(Root(), key);
        try
        {
            if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw Unavailable();
        }
        catch (FileNotFoundException) { }
        return path;
    }

    public bool Available(string key)
    {
        try { return File.Exists(Resolve(key)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw Unavailable(); }
    }

    private void EnsureFreeSpace(string root, long payloadBytes)
    {
        var minimumFree = configuration.GetValue<long?>("Images:MinimumFreeBytes") ?? 100L * 1024 * 1024;
        var volume = OperatingSystem.IsWindows() ? Path.GetPathRoot(root)! : root;
        if (new DriveInfo(volume).AvailableFreeSpace - payloadBytes < minimumFree)
            throw Unavailable();
    }

    public async Task<StoredImage> PublishAsync(ImageDerivatives image, CancellationToken ct)
    {
        var files = new StoredImage($"{Guid.NewGuid():N}.png", $"{Guid.NewGuid():N}.png");
        var publishedView = false;
        try
        {
            var root = Root();
            EnsureFreeSpace(root, image.View.LongLength + image.Thumbnail.LongLength);
            await PublishOne(Resolve(files.ViewKey), image.View, ct);
            publishedView = true;
            await PublishOne(Resolve(files.ThumbnailKey), image.Thumbnail, ct);
            return files;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or AnimalEvolutionException or OperationCanceledException)
        {
            if (publishedView) RemoveOwnedFile(files.ViewKey);
            if (e is OperationCanceledException) throw;
            throw Unavailable();
        }
    }

    private static async Task PublishOne(string path, byte[] bytes, CancellationToken ct)
    {
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
        {
            try { await stream.WriteAsync(bytes, ct); }
            catch
            {
                await stream.DisposeAsync();
                File.Delete(temporary);
                throw;
            }
        }

        try { File.Move(temporary, path); }
        finally { File.Delete(temporary); }
    }

    public Task RemoveAsync(StoredImage image)
    {
        RemoveOwnedFile(image.ViewKey);
        RemoveOwnedFile(image.ThumbnailKey);
        return Task.CompletedTask;
    }

    private void RemoveOwnedFile(string key)
    {
        try { File.Delete(Resolve(key)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or AnimalEvolutionException)
        {
            // A committed item never loses its files during compensation. Uncertain names are reconciled separately.
        }
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
