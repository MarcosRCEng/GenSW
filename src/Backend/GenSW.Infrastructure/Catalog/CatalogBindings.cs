using System.Data;
using GenSW.Application.Accounts;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Catalog;

public sealed record CatalogErpBinding(CatalogAccount Account, CatalogBinding Binding);

public sealed class CatalogBindings(CatalogDbContext catalog)
{
    public async Task<CatalogErpBinding?> ResolveAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var accounts = await (from candidate in catalog.Accounts.AsNoTracking()
                              join user in catalog.Users.AsNoTracking() on candidate.SubjectId equals user.Id
                              where user.Id == subjectId && user.IsActive
                              select candidate).Take(2).ToListAsync(cancellationToken);
        if (accounts.Count > 1)
            throw new AccountUnavailableException();

        var account = accounts.SingleOrDefault();
        if (account is null || account.State != CatalogAccountState.Active || !account.Confirmed)
            return null;

        var bindings = await catalog.Bindings.AsNoTracking()
            .Where(row => row.AccountId == account.Id && row.Published)
            .Take(2).ToListAsync(cancellationToken);
        if (bindings.Count != 1 || !bindings[0].Ready || bindings[0].InMaintenance ||
            account.Id == Guid.Empty || account.SubjectId == Guid.Empty || account.AccessGeneration <= 0)
            throw new AccountUnavailableException();

        var binding = bindings[0];
        if (binding.Id == Guid.Empty || binding.DatabaseId == Guid.Empty || binding.VolumeId == Guid.Empty ||
            binding.ErpActorId == Guid.Empty || binding.Revision <= 0 ||
            string.IsNullOrWhiteSpace(binding.DatabaseName) || string.IsNullOrWhiteSpace(binding.DatabaseSecretRef) ||
            string.IsNullOrWhiteSpace(binding.VolumeRef) || string.IsNullOrWhiteSpace(binding.ExpectedMigration))
            throw new AccountUnavailableException();

        return new CatalogErpBinding(account, binding);
    }

    public async Task<CatalogErpBinding?> ResolveSessionAsync(
        CatalogSessionIdentity identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = await (from row in catalog.Sessions.AsNoTracking()
                             join principal in catalog.Users.AsNoTracking() on row.SubjectId equals principal.Id
                             where row.Id == identity.SessionId && principal.IsActive &&
                                   row.Audience == CatalogSessionAudience.Erp && row.RevokedAtUtc == null &&
                                   row.SubjectId == identity.SubjectId && row.AccountId == identity.AccountId &&
                                   row.BindingId == identity.BindingId && row.BindingRevision == identity.BindingRevision &&
                                   row.AccessGeneration == identity.AccessGeneration
                             select row).SingleOrDefaultAsync(cancellationToken);
        if (session is null || session.ExpiresAtUtc <= now)
            return null;

        var resolved = await ResolveAsync(identity.SubjectId, cancellationToken);
        return resolved is not null && identity.Matches(resolved) ? resolved : null;
    }

    public Task<bool> LockSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
        LockAsync(
            $"SELECT 1 FROM \"CatalogUsers\" WHERE \"Id\" = {subjectId} FOR UPDATE",
            $"UPDATE \"CatalogUsers\" SET \"Id\" = \"Id\" WHERE \"Id\" = {subjectId}", cancellationToken);

    public Task<bool> LockAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        LockAsync(
            $"SELECT 1 FROM \"CatalogAccounts\" WHERE \"Id\" = {accountId} FOR UPDATE",
            $"UPDATE \"CatalogAccounts\" SET \"Id\" = \"Id\" WHERE \"Id\" = {accountId}", cancellationToken);

    public Task<bool> LockSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        LockAsync(
            $"SELECT 1 FROM \"CatalogSessions\" WHERE \"Id\" = {sessionId} FOR UPDATE",
            $"UPDATE \"CatalogSessions\" SET \"Id\" = \"Id\" WHERE \"Id\" = {sessionId}", cancellationToken);

    public IsolationLevel TransactionIsolation =>
        catalog.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted;

    private async Task<bool> LockAsync(
        FormattableString postgreSql, FormattableString sqlite, CancellationToken cancellationToken)
    {
        if (catalog.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A catalog transaction is required.");

        return catalog.Database.ProviderName switch
        {
            "Npgsql.EntityFrameworkCore.PostgreSQL" =>
                (await catalog.Database.SqlQuery<int>(postgreSql).ToListAsync(cancellationToken)).Count == 1,
            "Microsoft.EntityFrameworkCore.Sqlite" =>
                await catalog.Database.ExecuteSqlInterpolatedAsync(sqlite, cancellationToken) == 1,
            _ => throw new NotSupportedException("Unsupported catalog provider."),
        };
    }
}
