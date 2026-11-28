using System.Security.Cryptography;
using System.Text;
using GenSW.Application.Accounts;
using GenSW.Infrastructure.Catalog;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GenSW.Infrastructure.Accounts;

public sealed record ApprovedErpMigration(string From, string To, string SqlSha256);

public enum ErpMigrationStatus { Completed, Pending, Busy }

// Instantiate in the maintenance host with separately supplied migrator credentials.
public sealed class ErpSchemaMaintenance(
    CatalogDbContext catalog, CatalogBindings bindings, AccountLifecycle lifecycle,
    AccountRevocations revocations, IAccountDestinationVerifier destination)
{
    public static string CreateReviewSql(GenSWDbContext database, string from, string to) =>
        database.GetService<IMigrator>().GenerateScript(from, to, MigrationsSqlGenerationOptions.Idempotent);

    public static string Fingerprint(string sql) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql.ReplaceLineEndings("\n"))));

    public async Task<ErpMigrationStatus> RunAsync(Guid accountId, Guid bindingId,
        ApprovedErpMigration approval, string migrationConnection, CancellationToken ct)
    {
        var (account, binding) = await BeginAsync(accountId, bindingId, ct);
        await revocations.CancelAsync(accountId, account.AccessGeneration - 1);
        try
        {
            var connectionOptions = new NpgsqlConnectionStringBuilder(migrationConnection)
            {
                Pooling = false,
                Enlist = false,
                Timeout = 5,
            };
            if (connectionOptions.Database != binding.DatabaseName ||
                !string.IsNullOrEmpty(connectionOptions.Options) || connectionOptions.Multiplexing)
                throw new AccountUnavailableException();

            await using var connection = new NpgsqlConnection(connectionOptions.ConnectionString);
            await connection.OpenAsync(ct);
            await using var lease = connection.CreateCommand();
            lease.CommandText = "SELECT pg_try_advisory_lock(581, 2)";
            if (await lease.ExecuteScalarAsync(ct) is not true)
                return ErpMigrationStatus.Busy;

            await VerifyMarkerAsync(connection, account, binding, ct);
            await using var database = new GenSWDbContext(new DbContextOptionsBuilder<GenSWDbContext>()
                .UseNpgsql(connection).Options);
            var known = database.Database.GetMigrations().ToArray();
            var applied = (await database.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            await lifecycle.FenceAsync(account, binding, ct);
            var first = Array.IndexOf(known, approval.From);
            var last = Array.IndexOf(known, approval.To);
            if (first < 0 || last < first || last != known.Length - 1 ||
                applied.Length <= first || applied.Length > last + 1 || !known.Take(applied.Length).SequenceEqual(applied) ||
                !Fingerprint(CreateReviewSql(database, approval.From, approval.To))
                    .Equals(approval.SqlSha256, StringComparison.OrdinalIgnoreCase))
            {
                await RecordFailureAsync(accountId, bindingId, "history-or-review", ct);
                return ErpMigrationStatus.Pending;
            }

            // The EF history drives resumption after each committed migration.
            await database.GetService<IMigrator>().MigrateAsync(approval.To, ct);
            await database.Database.ExecuteSqlRawAsync("SELECT gensw_scope.ensure_write_guards()", ct);
            await PostgreSqlAccountDestination.VerifyHistoryAsync(database, approval.To, ct);

            await using var transaction = await catalog.Database.BeginTransactionAsync(ct);
            await bindings.LockAccountAsync(accountId, ct);
            var current = await catalog.Accounts.AsNoTracking().SingleAsync(x => x.Id == accountId, ct);
            binding = await catalog.Bindings.SingleAsync(x => x.Id == bindingId, ct);
            if (current.State != CatalogAccountState.Active || current.AccessGeneration != account.AccessGeneration ||
                !binding.InMaintenance || binding.Revision < 1)
                return ErpMigrationStatus.Pending;

            await using var reopen = connection.CreateCommand();
            reopen.CommandText = """
                UPDATE gensw_scope.destination SET enabled = true
                WHERE database_id = @database AND binding_id = @binding AND generation = @generation
                """;
            reopen.Parameters.AddWithValue("database", binding.DatabaseId);
            reopen.Parameters.AddWithValue("binding", binding.Id);
            reopen.Parameters.AddWithValue("generation", account.AccessGeneration);
            if (await reopen.ExecuteNonQueryAsync(ct) != 1)
                throw new AccountUnavailableException();

            binding.ExpectedMigration = approval.To;
            await destination.VerifyAsync(current, binding, ct);
            binding.VerifiedMigration = approval.To;
            binding.LastMaintenanceFailure = null;
            catalog.Entry(binding).Property(x => x.LastMaintenanceFailure).IsModified = true;
            binding.InMaintenance = false;
            await catalog.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ErpMigrationStatus.Completed;
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is
            AccountUnavailableException or NpgsqlException or InvalidOperationException or ArgumentException or TimeoutException)
        {
            catalog.ChangeTracker.Clear();
            await RecordFailureAsync(accountId, bindingId, "target-unavailable-or-migration-failed", ct);
            return ErpMigrationStatus.Pending;
        }
    }

    private Task<(CatalogAccount, CatalogBinding)> BeginAsync(Guid accountId, Guid bindingId, CancellationToken ct) =>
        AccountScope.TranslateAsync(async () =>
        {
            await using var transaction = await catalog.Database.BeginTransactionAsync(ct);
            await bindings.LockAccountAsync(accountId, ct);
            var account = await catalog.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, ct);
            var binding = await catalog.Bindings.SingleOrDefaultAsync(
                x => x.Id == bindingId && x.AccountId == accountId && x.Published, ct);
            if (account is null || account.State != CatalogAccountState.Active || binding is null)
                throw new AccountUnavailableException();

            if (!binding.InMaintenance)
            {
                binding.InMaintenance = true;
                account.AccessGeneration = checked(account.AccessGeneration + 1);
                await lifecycle.RevokeSessionsAsync(accountId, ct);
                await catalog.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return (account, binding);
        });

    private static async Task VerifyMarkerAsync(NpgsqlConnection connection, CatalogAccount account,
        CatalogBinding binding, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT current_database(), database_id, account_id, binding_id, binding_revision, generation
            FROM gensw_scope.identity()
            """;
        await using var row = await command.ExecuteReaderAsync(ct);
        if (!await row.ReadAsync(ct) || row.GetString(0) != binding.DatabaseName ||
            row.GetGuid(1) != binding.DatabaseId || row.GetGuid(2) != account.Id ||
            row.GetGuid(3) != binding.Id || row.GetInt64(4) != binding.Revision ||
            row.GetInt64(5) < account.AccessGeneration - 1 || row.GetInt64(5) > account.AccessGeneration)
            throw new AccountUnavailableException();
    }

    private Task RecordFailureAsync(Guid accountId, Guid bindingId, string reason, CancellationToken ct) =>
        catalog.Bindings.Where(x => x.AccountId == accountId && x.Id == bindingId && x.InMaintenance)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.LastMaintenanceFailure, reason), ct);
}
