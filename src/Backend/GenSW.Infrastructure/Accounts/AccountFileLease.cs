using GenSW.Application.Accounts;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Accounts;

public sealed class AccountFileLease : IAsyncDisposable
{
    private readonly GenSWDbContext database;
    private readonly FileStream? volume;

    private AccountFileLease(GenSWDbContext database, FileStream? volume)
    {
        this.database = database;
        this.volume = volume;
    }

    public static Task<AccountFileLease> OpenAsync(AccountScope scope, CancellationToken ct) =>
        AccountScope.TranslateAsync(async () =>
        {
            var db = new GenSWDbContext(scope.DatabaseOptions);
            FileStream? file = null;
            try
            {
                if (db.Database.IsNpgsql())
                {
                    file = PrivateVolume.Hold(scope.PrivateRoot, scope.VolumeId, FileShare.Read);
                    await db.Database.BeginTransactionAsync(ct);
                    await db.Database.ExecuteSqlRawAsync("""
                        SELECT pg_advisory_xact_lock_shared(581, 1);
                        SELECT gensw_scope.verify_generation();
                        """, ct);
                    await scope.RevalidateAsync(ct);
                }
                return new AccountFileLease(db, file);
            }
            catch
            {
                if (file is not null)
                    await file.DisposeAsync();
                await db.DisposeAsync();
                throw;
            }
        });

    public async ValueTask DisposeAsync()
    {
        try { await database.DisposeAsync(); }
        finally
        {
            if (volume is not null)
                await volume.DisposeAsync();
        }
    }
}
