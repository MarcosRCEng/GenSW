using GenSW.Application.Animals.ProducaoOvos;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace GenSW.Infrastructure.Animals.ProducaoOvos;
public sealed class ProducaoOvoRepository(GenSWDbContext context) : IProducaoOvoRepository
{
    public Task AddAsync(ProducaoOvo item, CancellationToken ct = default) => context.ProducoesOvos.AddAsync(item, ct).AsTask();
    public Task<ProducaoOvo?> GetForUpdateAsync(Guid id, CancellationToken ct = default) => context.ProducoesOvos.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ProducaoOvoResult?> GetAsync(Guid id, CancellationToken ct = default) => Read(context.ProducoesOvos.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
    public async Task<(IReadOnlyList<ProducaoOvoResult> Items, int TotalItems)> ListAsync(Guid animalId, ProducaoOvoListQuery q, CancellationToken ct = default)
    { var query = Filter(context.ProducoesOvos.AsNoTracking().Where(x => x.AnimalId == animalId), q); var total = await query.CountAsync(ct); return (await Read(query.OrderByDescending(x => x.DataPostura).ThenByDescending(x => x.CreatedAtUtc)).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct), total); }
    public async Task<IReadOnlyList<ProducaoOvoResult>> ListAllAsync(Guid animalId, ProducaoOvoListQuery q, CancellationToken ct = default) => await Read(Filter(context.ProducoesOvos.AsNoTracking().Where(x => x.AnimalId == animalId), q)).ToListAsync(ct);
    public Task<decimal?> GetPesoPadraoAsync(Guid animalId, CancellationToken ct = default) => context.Animais.Where(x => x.Id == animalId).Join(context.Especies, a => a.EspecieId, e => e.Id, (a, e) => e.PesoPadraoOvoGramas).SingleOrDefaultAsync(ct);
    public Task<bool> IsEligibleAsync(Guid animalId, CancellationToken ct = default) => context.Animais.Where(a => a.Id == animalId && a.Sexo == SexoAnimal.Femea).Join(context.Especies.Where(e => e.Ovipara), a => a.EspecieId, e => e.Id, (a, e) => a.Id).AnyAsync(ct);
    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
    private static IQueryable<ProducaoOvo> Filter(IQueryable<ProducaoOvo> query, ProducaoOvoListQuery q) { if (q.DataInicial is { } start) query = query.Where(x => x.DataPostura >= start); if (q.DataFinal is { } end) query = query.Where(x => x.DataPostura <= end); return query; }
    private static IQueryable<ProducaoOvoResult> Read(IQueryable<ProducaoOvo> query) => query.Select(x => new ProducaoOvoResult(x.Id, x.AnimalId, x.DataPostura, x.PesoGramas, x.Observacao, x.CreatedAtUtc, x.UpdatedAtUtc));
}
