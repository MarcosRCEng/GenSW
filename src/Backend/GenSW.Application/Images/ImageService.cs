using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Domain.Varieties;
namespace GenSW.Application.Images;

public sealed class ImageService(IImageRepository repository, IPrivateImageStorage storage, IImageProcessor processor, TimeProvider clock)
{
    public async Task<ImageResult> UploadAsync(ImageOwner kind, Guid owner, Stream input, string? caption, DateOnly? date, CancellationToken ct)
    {
        await EnsureOwner(kind, owner, ct);
        if (caption?.Trim().Length > 200 || date > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) throw new ArgumentException("Legenda ou data inválida.");
        var image = await processor.ProcessAsync(input, ct);
        await using var tx = await repository.BeginAsync(kind, owner, ct);
        await EnsureOwner(kind, owner, ct);
        var active = await repository.ActiveAsync(kind, owner, ct);
        CheckQuota(active, image.View.LongLength + image.Thumbnail.LongLength);
        var files = await storage.PublishAsync(image, ct);
        try
        {
            var order = active.Count == 0 ? 0 : checked(active.Max(x => x.Ordem) + 1);
            Imagem item = kind == ImageOwner.Animal
                ? ImagemAnimal.Criar(owner, files.ViewKey, files.ThumbnailKey, image.View.LongLength + image.Thumbnail.LongLength, image.Width, image.Height, order, caption, date, clock.GetUtcNow())
                : ImagemVariedade.Criar(owner, files.ViewKey, files.ThumbnailKey, image.View.LongLength + image.Thumbnail.LongLength, image.Width, image.Height, order, caption, date, clock.GetUtcNow());
            await repository.AddAsync(item, ct); await repository.SaveAsync(ct); await tx.CommitAsync(ct);
            return ToResult(kind, owner, item);
        }
        catch { await storage.RemoveAsync(files); throw; }
    }
    public async Task<AnimalEvolutionPage<ImageResult>> ListAsync(ImageOwner kind, Guid owner, bool? active, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size); await EnsureOwner(kind, owner, ct);
        var result = await repository.ListAsync(kind, owner, active, page, size, ct);
        return new(result.Items.Select(x => ToResult(kind, owner, x)).ToArray(), page, size, result.TotalItems, result.TotalPages);
    }
    public async Task<ImageResult> EditAsync(ImageOwner kind, Guid owner, Guid id, string? caption, DateOnly? date, CancellationToken ct) =>
        await Mutate(kind, owner, id, (item, _) => { item.Editar(caption, date, clock.GetUtcNow()); return Task.CompletedTask; }, ct);
    public async Task<ImageResult> SetActiveAsync(ImageOwner kind, Guid owner, Guid id, bool active, CancellationToken ct) =>
        await Mutate(kind, owner, id, async (item, items) =>
        {
            if (active && !item.Ativa) { CheckQuota(items, item.TamanhoBytes); RequireAvailable(item); }
            item.DefinirAtiva(active, clock.GetUtcNow()); await Task.CompletedTask;
        }, ct);
    public async Task<ImageResult> PreferAsync(ImageOwner kind, Guid owner, Guid id, bool preferred, CancellationToken ct) =>
        await Mutate(kind, owner, id, async (item, items) =>
        {
            if (preferred)
            {
                if (!item.Ativa) throw Conflict("imagem_inativa", "Reative a imagem antes de selecioná-la.");
                RequireAvailable(item);
                foreach (var previous in items.Where(x => x.Representativa && x.Id != id)) previous.Preferir(false, clock.GetUtcNow());
                await repository.SaveAsync(ct); // release unique slot before assigning the next one
            }
            item.Preferir(preferred, clock.GetUtcNow());
        }, ct);
    public async Task ReorderAsync(ImageOwner kind, Guid owner, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await using var tx = await repository.BeginAsync(kind, owner, ct); await EnsureOwner(kind, owner, ct);
        var items = await repository.ActiveAsync(kind, owner, ct);
        if (ids.Count != items.Count || ids.Distinct().Count() != ids.Count || !ids.ToHashSet().SetEquals(items.Select(x => x.Id)))
            throw Conflict("ordem_obsoleta", "A galeria mudou. Atualize antes de reordenar.");
        var indexed = items.ToDictionary(x => x.Id);
        for (var i = 0; i < ids.Count; i++) indexed[ids[i]].Ordenar(i, clock.GetUtcNow());
        await repository.SaveAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<PreferredImage> PreferredAsync(ImageOwner kind, Guid owner, CancellationToken ct)
    {
        await EnsureOwner(kind, owner, ct);
        return SelectPreferred(kind, owner, await repository.ActiveAsync(kind, owner, ct), storage);
    }
    public static PreferredImage SelectPreferred(ImageOwner kind, Guid owner, IEnumerable<Imagem> images, IPrivateImageStorage storage)
    {
        var selected = images.Where(x => x.Ativa).OrderByDescending(x => x.Representativa).ThenBy(x => x.Ordem)
            .ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).FirstOrDefault(x => storage.Available(x.ArquivoKey) && storage.Available(x.MiniaturaKey));
        return selected is null ? new("nenhuma", null) : new(selected.Representativa ? "representativa" : "ordenacao",
            new(selected.Id, selected.Legenda, Path(kind, owner, selected.Id) + "?versao=miniatura"));
    }
    public async Task<Stream> ContentAsync(ImageOwner kind, Guid owner, Guid id, string version, CancellationToken ct)
    {
        if (version is not ("miniatura" or "visualizacao")) throw new ArgumentException("Versão inválida.");
        await EnsureOwner(kind, owner, ct);
        var item = await repository.GetAsync(kind, owner, id, ct);
        if (item is null || !item.Ativa) throw Missing();
        return await storage.OpenAsync(version == "miniatura" ? item.MiniaturaKey : item.ArquivoKey, ct) ?? throw Missing();
    }
    private async Task<ImageResult> Mutate(ImageOwner kind, Guid owner, Guid id, Func<Imagem, IReadOnlyList<Imagem>, Task> action, CancellationToken ct)
    {
        await using var tx = await repository.BeginAsync(kind, owner, ct); await EnsureOwner(kind, owner, ct);
        var item = await repository.GetAsync(kind, owner, id, ct) ?? throw Missing();
        await action(item, await repository.ActiveAsync(kind, owner, ct));
        await repository.SaveAsync(ct); await tx.CommitAsync(ct); return ToResult(kind, owner, item);
    }
    private async Task EnsureOwner(ImageOwner kind, Guid owner, CancellationToken ct)
    {
        if (!await repository.OwnerExistsAsync(kind, owner, ct)) throw new AnimalEvolutionException(404, "proprietario_nao_encontrado", "Proprietário não encontrado.");
    }
    private void RequireAvailable(Imagem item) { if (!storage.Available(item.ArquivoKey) || !storage.Available(item.MiniaturaKey)) throw Conflict("imagem_indisponivel", "Arquivos da imagem indisponíveis."); }
    private static void CheckQuota(IReadOnlyList<Imagem> images, long bytes)
    {
        if (images.Count >= 50 || images.Sum(x => x.TamanhoBytes) + bytes > 100L * 1024 * 1024) throw Conflict("cota_imagens", "Limite de 50 imagens ativas ou 100 MiB por proprietário atingido.");
    }
    private static void ValidatePage(int page, int size) { if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue) throw new ArgumentException("Página inválida."); }
    private static AnimalEvolutionException Conflict(string code, string text) => new(409, code, text);
    private static AnimalEvolutionException Missing() => new(404, "imagem_nao_encontrada", "Imagem não encontrada.");
    private static string Path(ImageOwner kind, Guid owner, Guid id) => $"/api/v1/{(kind == ImageOwner.Animal ? "animais" : "variedades")}/{owner}/imagens/{id}/conteudo";
    private static ImageResult ToResult(ImageOwner kind, Guid owner, Imagem x) => new(x.Id, kind == ImageOwner.Animal ? owner : null,
        kind == ImageOwner.Variedade ? owner : null, x.Legenda, x.DataCaptura, x.Ordem, x.Ativa, x.Representativa, x.Mime,
        x.Largura, x.Altura, x.TamanhoBytes, x.CreatedAtUtc, x.UpdatedAtUtc, Path(kind, owner, x.Id) + "?versao=miniatura", Path(kind, owner, x.Id) + "?versao=visualizacao");
}
