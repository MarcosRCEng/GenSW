using GenSW.Application.Formulation;
using GenSW.Application.Optimization;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Optimization;

internal sealed class OptimizationRepository(GenSWDbContext db) : IOptimizationRepository
{
    private IQueryable<OptimizationExecution> Executions => db.Set<OptimizationExecution>().AsNoTracking();
    private IQueryable<OptimizationResultRow> Results => db.Set<OptimizationResultRow>().AsNoTracking();
    private IQueryable<OptimizationDerivation> Derivations => db.Set<OptimizationDerivation>().AsNoTracking();

    public Task<OptimizationExecution?> FindAsync(Guid id, CancellationToken ct) =>
        Executions.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<OptimizationExecution?> FindByKeyAsync(Guid actor, string key, CancellationToken ct) =>
        Executions.SingleOrDefaultAsync(x => x.AutorId == actor && x.Chave == key, ct);

    public Task<OptimizationResultRow?> FindResultAsync(Guid executionId, CancellationToken ct) =>
        Results.SingleOrDefaultAsync(x => x.ExecutionId == executionId, ct);

    public async Task<CatalogPage<OptimizationSummary>> ListAsync(OptimizationQuery query, CancellationToken ct)
    {
        query.Validate();
        var executions = Executions;
        if (query.EspecieId is { } species) executions = executions.Where(x => x.EspecieId == species);
        if (query.Fase is { } phase) executions = executions.Where(x => x.Fase == phase);
        if (query.De is { } start) executions = executions.Where(x => x.CreatedAtUtc >= start.ToUniversalTime());
        if (query.Ate is { } end) executions = executions.Where(x => x.CreatedAtUtc <= end.ToUniversalTime());

        var rows = from execution in executions
                   join result in Results on execution.Id equals result.ExecutionId into joined
                   from result in joined.DefaultIfEmpty()
                   select new
                   {
                       execution.Id, execution.AutorId, execution.CreatedAtUtc, execution.Estado,
                       Desfecho = result == null ? null : result.Desfecho, execution.EspecieId, execution.Fase
                   };
        if (query.Desfecho is { } outcome) rows = rows.Where(x => x.Desfecho == outcome);
        var ordered = query.SortDirection == "asc"
            ? rows.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            : rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id);
        var total = await rows.CountAsync(ct);
        var page = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new OptimizationSummary(x.Id, x.AutorId, x.CreatedAtUtc, x.Estado, x.Desfecho, x.EspecieId, x.Fase))
            .ToListAsync(ct);
        return new(page, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public void Add(OptimizationExecution execution) => db.Add(execution);

    public async Task<bool> TryCompleteAsync(Guid executionId, Guid ownerToken, OptimizationResultRow result,
        bool expired, CancellationToken ct)
    {
        if (result.ExecutionId != executionId) throw new ArgumentException("Resultado de outra execução.", nameof(result));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pending = Executions.Where(x => x.Id == executionId && x.OwnerToken == ownerToken && x.Estado == "EmCalculo");
        pending = expired
            ? pending.Where(x => x.DeadlineUtc <= result.CreatedAtUtc)
            : pending.Where(x => x.DeadlineUtc > result.CreatedAtUtc);
        var updated = await pending.ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Estado, "Finalizada")
            .SetProperty(x => x.Revisao, x => x.Revisao + 1), ct);
        if (updated == 0) return false;

        db.Add(result);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OptimizationExecution>> FindExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await Executions.Where(x => x.Estado == "EmCalculo" && x.DeadlineUtc <= now)
            .OrderBy(x => x.DeadlineUtc).ThenBy(x => x.Id).Take(limit).ToListAsync(ct);

    public Task<OptimizationDerivation?> FindDerivationAsync(Guid actor, string key, CancellationToken ct) =>
        Derivations.SingleOrDefaultAsync(x => x.AutorId == actor && x.Chave == key, ct);

    public Task<OptimizationDerivation?> FindDerivationByVersionAsync(Guid versionId, CancellationToken ct) =>
        Derivations.SingleOrDefaultAsync(x => x.VersaoId == versionId, ct);

    public void Add(OptimizationDerivation derivation) => db.Add(derivation);
}
