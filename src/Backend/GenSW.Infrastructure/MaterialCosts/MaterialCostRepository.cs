using System.Linq.Expressions;
using GenSW.Application.Inventory;
using GenSW.Application.MaterialCosts;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.MaterialCosts;

public sealed class MaterialCostRepository(GenSWDbContext db, IInventoryRepository inventory) : IMaterialCostRepository
{
    public string TransactionIdentity => db.Database.CurrentTransaction?.TransactionId.ToString("D") ?? throw new InvalidOperationException("Cost facts require the shared physical transaction.");
    public async Task<IInventoryScope> BeginMutationAsync(CancellationToken ct)
    {
        var scope = await inventory.BeginMutationAsync(ct);
        try { if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(430,2)", ct); return scope; }
        catch { await scope.DisposeAsync(); throw; }
    }
    public Task<IInventoryScope> BeginReadAsync(CancellationToken ct) => inventory.BeginReadAsync(ct);
    public Task SaveAsync(CancellationToken ct) => inventory.SaveAsync(ct);
    public void ClearTracking() => db.ChangeTracker.Clear();
    public void Add<T>(T value) where T : class => db.Add(value);
    public Task<T?> GetAsync<T>(Guid id, bool tracking, CancellationToken ct) where T : class =>
        (tracking ? db.Set<T>() : db.Set<T>().AsNoTracking()).SingleOrDefaultAsync(x => EF.Property<Guid>(x,"Id") == id, ct);
    public async Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T,bool>> predicate, bool tracking, CancellationToken ct) where T : class =>
        await (tracking ? db.Set<T>() : db.Set<T>().AsNoTracking()).Where(predicate).ToArrayAsync(ct);
    public async Task<long> LastSequenceAsync(CancellationToken ct) => await db.Set<EventoCustoMaterial>().Select(x => (long?)x.Sequencia).MaxAsync(ct) ?? 0;
    public async Task<long> NextSequenceAsync(CancellationToken ct) => db.Database.IsNpgsql()
        ? await db.Database.SqlQueryRaw<long>("SELECT nextval('\"SequenciaCustoMaterial\"') AS \"Value\"").SingleAsync(ct)
        : await LastSequenceAsync(ct) + db.ChangeTracker.Entries<EventoCustoMaterial>().Count(x => x.State == EntityState.Added) + 1;
    public async Task<IReadOnlyList<PosicaoCustoMaterial>> LayerPositionsAsync(Guid lot, bool tracking, CancellationToken ct)
    {
        var query = from position in tracking ? db.Set<PosicaoCustoMaterial>() : db.Set<PosicaoCustoMaterial>().AsNoTracking()
                    join layer in db.Set<CamadaCustoMaterial>() on position.CamadaId equals layer.Id
                    where layer.LoteId == lot orderby layer.SequenciaOrigem, layer.Id, position.LocalId select position;
        return await query.ToArrayAsync(ct);
    }
    public Task<bool> LayerWasUsedAsync(Guid layer, CancellationToken ct) => db.Set<ParcelaCustoMaterial>().AnyAsync(x => x.CamadaId == layer && x.Sentido == "Saida", ct);
    public async Task<bool> LotWasUsedSinceAsync(Guid lot, DateTimeOffset since, CancellationToken ct) =>
        await (from movement in db.MovimentosEstoque join stockEvent in db.EventosEstoque on movement.EventoId equals stockEvent.Id
               where movement.LoteId == lot && stockEvent.CreatedAtUtc > since select movement.Id).AnyAsync(ct) ||
        await db.Set<ReservaProducao>().AnyAsync(x => x.LoteId == lot && x.CreatedAtUtc > since, ct);
}
