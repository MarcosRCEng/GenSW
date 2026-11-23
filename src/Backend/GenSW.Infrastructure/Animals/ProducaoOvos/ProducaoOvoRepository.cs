using GenSW.Application.Animals.ProducaoOvos;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace GenSW.Infrastructure.Animals.ProducaoOvos;
public sealed class ProducaoOvoRepository(GenSWDbContext context) : IProducaoOvoRepository
{
    public Task AddAsync(ProducaoOvo item, CancellationToken ct = default) => context.ProducoesOvos.AddAsync(item, ct).AsTask();
    public Task<ProducaoOvo?> GetForUpdateAsync(Guid id, CancellationToken ct = default) => context.ProducoesOvos.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task DeleteAsync(ProducaoOvo item, CancellationToken ct = default)
    {
        context.ProducoesOvos.Remove(item);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation })
        { throw new ProducaoOvoLinkedException(); }
        catch (DbUpdateConcurrencyException) { throw new ProducaoOvoNotFoundException(item.Id); }
    }
    public Task<ProducaoOvoResult?> GetAsync(Guid id, CancellationToken ct = default) => Read(context.ProducoesOvos.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ProducaoOvoResult>> ListAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken ct = default) =>
        await Read(Filter(animalId, query)
            .OrderByDescending(x => x.DataPostura)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProducaoOvoResult>> ListAllAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken ct = default) =>
        await Read(Filter(animalId, query).OrderBy(x => x.DataPostura).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)).ToListAsync(ct);

    public Task<ProducaoOvoResumo?> GetResumoAsync(Guid animalId, ProducaoOvoListQuery query, decimal? pesoPadrao, CancellationToken ct = default) =>
        Filter(animalId, query)
            .GroupBy(_ => 1)
            .Select(records => new ProducaoOvoResumo(
                records.Count(),
                records.Sum(x => x.PesoGramas),
                records.Min(x => x.PesoGramas),
                records.Max(x => x.PesoGramas),
                records.Min(x => x.DataPostura),
                records.Min(x => x.PesoGramas >= pesoPadrao ? (DateOnly?)x.DataPostura : null)))
            .SingleOrDefaultAsync(ct);

    public Task<decimal?> GetPesoPadraoAsync(Guid animalId, CancellationToken ct = default) => context.Animais.Where(x => x.Id == animalId).Join(context.Especies, a => a.EspecieId, e => e.Id, (a, e) => e.PesoPadraoOvoGramas).SingleOrDefaultAsync(ct);
    public Task<bool> IsEligibleAsync(Guid animalId, CancellationToken ct = default) => context.Animais.Where(a => a.Id == animalId && a.Sexo == SexoAnimal.Femea).Join(context.Especies.Where(e => e.Ovipara), a => a.EspecieId, e => e.Id, (a, e) => a.Id).AnyAsync(ct);
    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
    private IQueryable<ProducaoOvo> Filter(Guid animalId, ProducaoOvoListQuery filter)
    {
        var query = context.ProducoesOvos.AsNoTracking().Where(x => x.AnimalId == animalId);
        if (filter.DataInicial is { } start) query = query.Where(x => x.DataPostura >= start);
        if (filter.DataFinal is { } end) query = query.Where(x => x.DataPostura <= end);
        return query;
    }
    private static IQueryable<ProducaoOvoResult> Read(IQueryable<ProducaoOvo> query) => query.Select(x => new ProducaoOvoResult(x.Id, x.AnimalId, x.DataPostura, x.PesoGramas, x.Observacao, x.CreatedAtUtc, x.UpdatedAtUtc));
}
