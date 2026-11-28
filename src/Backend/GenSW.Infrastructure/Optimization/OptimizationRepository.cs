using GenSW.Application.Accounts;
using GenSW.Application.Formulation;
using GenSW.Application.Optimization;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Optimization;

internal sealed class OptimizationRepository(GenSWDbContext db, IErpAccountScope accountScope) : IOptimizationRepository
{
    private IQueryable<OptimizationExecution> Executions
    {
        get
        {
            var account = accountScope.Current.AccountId;
            return db.Set<OptimizationExecution>().AsNoTracking()
                .Where(x => x.AccountId == account || x.AccountId == null);
        }
    }

    private IQueryable<OptimizationResultRow> Results => db.Set<OptimizationResultRow>().AsNoTracking();
    private IQueryable<OptimizationDerivation> Derivations
    {
        get
        {
            var account = accountScope.Current.AccountId;
            return db.Set<OptimizationDerivation>().AsNoTracking()
                .Where(x => x.AccountId == account || x.AccountId == null);
        }
    }

    public Task<OptimizationExecution?> FindAsync(Guid id, CancellationToken ct) =>
        Executions.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<OptimizationExecution?> FindByKeyAsync(ErpAccountContext account, string key, CancellationToken ct) =>
        ScopedExecutions(account).SingleOrDefaultAsync(x => x.AutorId == account.ActorId && x.Chave == key, ct);

    public Task<bool> HasActiveLeaseAsync(ErpAccountContext account, DateTimeOffset now, CancellationToken ct) =>
        ScopedExecutions(account).AnyAsync(x => x.Estado == "EmCalculo" && x.DeadlineUtc > now, ct);

    public Task<OptimizationResultRow?> FindResultAsync(Guid executionId, CancellationToken ct) =>
        Results.SingleOrDefaultAsync(x => x.ExecutionId == executionId &&
            Executions.Any(execution => execution.Id == x.ExecutionId), ct);

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
                       execution.Id, execution.AutorId, execution.CreatedAtUtc,
                       Estado = execution.Estado == "Quarentenada" ? "Finalizada" : execution.Estado,
                       Desfecho = execution.Estado == "Quarentenada" ? "ErroAlgoritmo" : result == null ? null : result.Desfecho,
                       execution.EspecieId, execution.Fase
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

    public async Task<bool> TryCompleteAsync(ErpAccountContext account, Guid executionId, Guid ownerToken, OptimizationResultRow result,
        bool expired, CancellationToken ct)
    {
        if (result.ExecutionId != executionId) throw new ArgumentException("Resultado de outra execução.", nameof(result));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ExecucoesOtimizacao"
            SET "Estado" = 'Finalizada', "Revisao" = "Revisao" + 1
            WHERE "Id" = {executionId} AND "OwnerToken" = {ownerToken} AND "Estado" = 'EmCalculo'
                AND "AccountId" = {account.AccountId} AND "BindingId" = {account.BindingId}
                AND "BindingRevision" = {account.BindingRevision} AND "AccessGeneration" = {account.AccessGeneration}
                AND "AutorId" = {account.ActorId} AND ("DeadlineUtc" <= clock_timestamp()) = {expired}
            """, ct);
        if (updated == 0) return false;

        db.Add(result);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OptimizationExecution>> FindExpiredAsync(ErpAccountContext account, DateTimeOffset now,
        int limit, CancellationToken ct) =>
        await ScopedExecutions(account).Where(x => x.AutorId == account.ActorId && x.Estado == "EmCalculo" && x.DeadlineUtc <= now)
            .OrderBy(x => x.DeadlineUtc).ThenBy(x => x.Id).Take(limit).ToListAsync(ct);

    public Task<OptimizationDerivation?> FindDerivationAsync(ErpAccountContext account, string key, CancellationToken ct) =>
        Derivations.SingleOrDefaultAsync(x => x.AccountId == account.AccountId && x.BindingId == account.BindingId &&
            x.BindingRevision == account.BindingRevision && x.AccessGeneration == account.AccessGeneration &&
            x.AutorId == account.ActorId && x.Chave == key, ct);

    public Task<OptimizationDerivation?> FindDerivationByVersionAsync(Guid versionId, CancellationToken ct) =>
        Derivations.SingleOrDefaultAsync(x => x.VersaoId == versionId, ct);

    public void Add(OptimizationDerivation derivation) => db.Add(derivation);

    private IQueryable<OptimizationExecution> ScopedExecutions(ErpAccountContext account) =>
        Executions.Where(x => x.AccountId == account.AccountId && x.BindingId == account.BindingId &&
            x.BindingRevision == account.BindingRevision && x.AccessGeneration == account.AccessGeneration);
}
