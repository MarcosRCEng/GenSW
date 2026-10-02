using GenSW.Application.Animals;
using GenSW.Application.Images;
using ImageMagick;
namespace GenSW.Infrastructure.Images;

public sealed class MagickImageProcessor : IImageProcessor
{
    private static readonly SemaphoreSlim Decoder = new(1, 1);
    static MagickImageProcessor()
    {
        ResourceLimits.Width = 8192; ResourceLimits.Height = 8192; ResourceLimits.ListLength = 2;
        ResourceLimits.Memory = 256 * 1024 * 1024; ResourceLimits.Disk = 0;
        ResourceLimits.Thread = 1; ResourceLimits.Time = 5;
    }
    public async Task<ImageDerivatives> ProcessAsync(Stream input, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var block = new byte[65536];
        int read;
        while ((read = await input.ReadAsync(block, ct)) != 0)
        {
            if (buffer.Length + read > 5 * 1024 * 1024) throw new AnimalEvolutionException(413, "arquivo_grande", "Arquivo excede 5 MiB.");
            buffer.Write(block, 0, read);
        }
        var bytes = buffer.ToArray();
        var format = Detect(bytes);
        RejectAnimatedContainer(bytes, format);
        if (!await Decoder.WaitAsync(TimeSpan.FromSeconds(5), ct)) throw new AnimalEvolutionException(503, "decodificador_ocupado", "Processador de imagens ocupado.");
        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var settings = new MagickReadSettings { Format = format };
                using var info = new MagickImageCollection();
                info.Warning += (_, warning) => throw warning.Exception;
                info.Ping(bytes, settings);
                if (info.Count != 1 || info[0].Width > 8192 || info[0].Height > 8192 || (long)info[0].Width * info[0].Height > 20_000_000)
                    throw new ArgumentException("Imagem animada ou dimensões acima do limite.");
                using var images = new MagickImageCollection();
                images.Warning += (_, warning) => throw warning.Exception;
                images.Read(bytes, settings);
                if (images.Count != 1) throw new ArgumentException("Imagens animadas não são aceitas.");
                var image = images[0];
                image.AutoOrient(); image.Strip();
                image.Resize(new MagickGeometry(2048, 2048) { Greater = true });
                var width = (int)image.Width; var height = (int)image.Height;
                var view = image.ToByteArray(MagickFormat.Png);
                image.Resize(new MagickGeometry(256, 256) { Greater = true });
                var thumbnail = image.ToByteArray(MagickFormat.Png);
                ct.ThrowIfCancellationRequested();
                return new ImageDerivatives(view, thumbnail, width, height);
            }, ct);
        }
        catch (MagickException) { throw new ArgumentException("Arquivo de imagem inválido, truncado ou acima dos limites de processamento."); }
        finally { Decoder.Release(); }
    }
    private static MagickFormat Detect(byte[] bytes)
    {
        if (bytes.Length >= 12 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return MagickFormat.Jpeg;
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return MagickFormat.Png;
        if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return MagickFormat.WebP;
        throw new AnimalEvolutionException(415, "formato_imagem", "Use JPEG, PNG ou WebP estático.");
    }

    private static void RejectAnimatedContainer(byte[] bytes, MagickFormat format)
    {
        if (format is not (MagickFormat.Png or MagickFormat.WebP)) return;
        var offset = format == MagickFormat.Png ? 8 : 12;
        while (offset + 8 <= bytes.Length)
        {
            var length = format == MagickFormat.Png
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4))
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            var tag = System.Text.Encoding.ASCII.GetString(bytes, offset + (format == MagickFormat.Png ? 4 : 0), 4);
            if (tag is "acTL" or "ANIM" or "ANMF") throw new ArgumentException("Imagens animadas não são aceitas.");
            var next = (long)offset + length + (format == MagickFormat.Png ? 12 : 8 + length % 2);
            if (next > bytes.Length) throw new ArgumentException("Arquivo de imagem truncado.");
            offset = (int)next;
        }
    }
}
