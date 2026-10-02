using GenSW.Application.Animals;
using GenSW.Application.Images;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace GenSW.Infrastructure.Images;

public sealed class ImageRepository(GenSWDbContext context) : IImageRepository
{
    public async Task<IAnimalMutationScope> BeginAsync(ImageOwner kind, Guid owner, CancellationToken ct)
    {
        var tx = await AnimalMutationScope.BeginAsync(context, kind == ImageOwner.Animal ? owner : null, false, ct);
        try
        {
            if (kind == ImageOwner.Variedade) await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Variedades\" WHERE \"Id\" = {owner} FOR UPDATE", ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    public Task<bool> OwnerExistsAsync(ImageOwner kind, Guid owner, CancellationToken ct) => kind == ImageOwner.Animal
        ? context.Animais.AnyAsync(x => x.Id == owner, ct) : context.Variedades.AnyAsync(x => x.Id == owner, ct);
    public async Task<Imagem?> GetAsync(ImageOwner kind, Guid owner, Guid id, CancellationToken ct) => kind == ImageOwner.Animal
        ? await context.ImagensAnimal.SingleOrDefaultAsync(x => x.AnimalId == owner && x.Id == id, ct)
        : await context.ImagensVariedade.SingleOrDefaultAsync(x => x.VariedadeId == owner && x.Id == id, ct);
    public async Task<IReadOnlyList<Imagem>> ActiveAsync(ImageOwner kind, Guid owner, CancellationToken ct) => kind == ImageOwner.Animal
        ? await context.ImagensAnimal.Where(x => x.AnimalId == owner && x.Ativa).OrderBy(x => x.Ordem).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToListAsync(ct)
        : await context.ImagensVariedade.Where(x => x.VariedadeId == owner && x.Ativa).OrderBy(x => x.Ordem).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
    public async Task<AnimalEvolutionPage<Imagem>> ListAsync(ImageOwner kind, Guid owner, bool? active, int page, int size, CancellationToken ct) => kind == ImageOwner.Animal
        ? await Page(context.ImagensAnimal.Where(x => x.AnimalId == owner), active, page, size, ct)
        : await Page(context.ImagensVariedade.Where(x => x.VariedadeId == owner), active, page, size, ct);
    private static async Task<AnimalEvolutionPage<Imagem>> Page<T>(IQueryable<T> query, bool? active, int page, int size, CancellationToken ct) where T : Imagem
    {
        query = query.AsNoTracking();
        if (active.HasValue) query = query.Where(x => x.Ativa == active);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Ordem).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items, page, size, count, (int)Math.Ceiling(count / (double)size));
    }
    public async Task AddAsync(Imagem image, CancellationToken ct) => await context.AddAsync(image, ct);
    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
