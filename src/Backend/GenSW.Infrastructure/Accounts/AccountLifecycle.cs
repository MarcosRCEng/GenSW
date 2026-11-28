using GenSW.Application.Accounts;
using GenSW.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GenSW.Infrastructure.Accounts;

public enum AccountDeactivation { Pending, Inactive }

public sealed class AccountLifecycle(
    CatalogDbContext catalog, CatalogBindings bindings, AccountResources resources,
    IAccountDestinationVerifier verifier, AccountRevocations revocations, TimeProvider clock,
    ILogger<AccountLifecycle> logger)
{
    // Provisioning proves the DB and volume markers and sets the target generation before publication.
    public Task ActivateAsync(Guid accountId, Guid bindingId, long revision, long generation, CancellationToken ct) =>
        AccountScope.TranslateAsync(async () =>
        {
            await using var transaction = await catalog.Database.BeginTransactionAsync(ct);
            await bindings.LockAccountAsync(accountId, ct);
            var account = await catalog.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, ct);
            var binding = await catalog.Bindings.SingleOrDefaultAsync(
                x => x.AccountId == accountId && x.Id == bindingId && x.Revision == revision, ct);
            if (account is null || binding is null || !account.Confirmed ||
                generation != (account.State == CatalogAccountState.Inactive ? checked(account.AccessGeneration + 1) : account.AccessGeneration) ||
                account.State == CatalogAccountState.Deactivating || !binding.Ready || binding.InMaintenance ||
                await catalog.Bindings.AnyAsync(x => x.AccountId == accountId && x.Id != bindingId && x.Published, ct))
                throw new AccountUnavailableException();

            account.AccessGeneration = generation;
            await verifier.VerifyAsync(account, binding, ct);
            binding.Published = true;
            account.State = CatalogAccountState.Active;
            await catalog.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });

    public async Task<AccountDeactivation> DeactivateAsync(Guid accountId, CancellationToken ct)
    {
        var operation = await AccountScope.TranslateAsync(async () =>
        {
            await using var transaction = await catalog.Database.BeginTransactionAsync(ct);
            await bindings.LockAccountAsync(accountId, ct);
            var account = await catalog.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, ct)
                ?? throw new AccountUnavailableException();
            if (account.State == CatalogAccountState.Inactive)
                return (Account: account, Binding: (CatalogBinding?)null);
            if (account.State == CatalogAccountState.Pending)
                throw new AccountUnavailableException();

            if (account.State != CatalogAccountState.Deactivating)
            {
                account.State = CatalogAccountState.Deactivating;
                account.AccessGeneration = checked(account.AccessGeneration + 1);
                await RevokeSessionsAsync(accountId, ct);
                await catalog.SaveChangesAsync(ct);
            }

            var published = await catalog.Bindings.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Published).Take(2).ToListAsync(ct);
            await transaction.CommitAsync(ct);
            return (Account: account, Binding: published.Count == 1 ? published[0] : null);
        });
        if (operation.Account.State == CatalogAccountState.Inactive)
            return AccountDeactivation.Inactive;

        await revocations.CancelAsync(accountId, operation.Account.AccessGeneration - 1);
        if (operation.Binding is null)
            return AccountDeactivation.Pending;

        try
        {
            await FenceAsync(operation.Account, operation.Binding, ct);
        }
        catch (AccountUnavailableException)
        {
            logger.LogInformation("ERP account {AccountId} remains pending deactivation at generation {Generation}",
                accountId, operation.Account.AccessGeneration);
            return AccountDeactivation.Pending;
        }

        return await AccountScope.TranslateAsync(async () =>
        {
            await using var transaction = await catalog.Database.BeginTransactionAsync(ct);
            await bindings.LockAccountAsync(accountId, ct);
            var account = await catalog.Accounts.AsNoTracking().SingleAsync(x => x.Id == accountId, ct);
            if (account.State == CatalogAccountState.Inactive)
                return AccountDeactivation.Inactive;
            if (account.State != CatalogAccountState.Deactivating ||
                account.AccessGeneration != operation.Account.AccessGeneration)
                return AccountDeactivation.Pending;
            await catalog.Accounts.Where(x => x.Id == accountId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, CatalogAccountState.Inactive), ct);
            await transaction.CommitAsync(ct);
            return AccountDeactivation.Inactive;
        });
    }

    internal Task RevokeSessionsAsync(Guid accountId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return catalog.Sessions.Where(x => x.AccountId == accountId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
    }

    internal Task FenceAsync(CatalogAccount account, CatalogBinding binding, CancellationToken ct) =>
        AccountScope.TranslateAsync(async () =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var limit = deadline.Token;

            try
            {
                // The marker also guards file streams if their database connection is lost.
                await using var volume = await DrainVolumeAsync(binding, limit);
                await using var connection = new NpgsqlConnection(resources.MaintenanceConnection(binding));
                await connection.OpenAsync(limit);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT current_database() = @name AND
                        gensw_scope.advance_fence(@database, @account, @binding, @revision, @generation, true)
                    """;
                command.Parameters.AddWithValue("name", binding.DatabaseName);
                command.Parameters.AddWithValue("database", binding.DatabaseId);
                command.Parameters.AddWithValue("account", account.Id);
                command.Parameters.AddWithValue("binding", binding.Id);
                command.Parameters.AddWithValue("revision", binding.Revision);
                command.Parameters.AddWithValue("generation", account.AccessGeneration);
                if (await command.ExecuteScalarAsync(limit) is not true)
                    throw new AccountUnavailableException();
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AccountUnavailableException();
            }
        });

    private async Task<FileStream> DrainVolumeAsync(CatalogBinding binding, CancellationToken ct)
    {
        var root = resources.Volume(binding);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return PrivateVolume.Hold(root, binding.VolumeId, FileShare.None); }
            catch (IOException) when (Directory.Exists(root) && File.Exists(Path.Combine(root, PrivateVolume.MarkerName)))
            {
                await Task.Delay(50, ct);
            }
        }
    }
}
