using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Catalog;

public sealed class CatalogCredentialRevocation(CatalogDbContext catalog, CatalogBindings bindings, TimeProvider clock)
{
    /// <summary>
    /// In the caller's catalog transaction, lock the principal before consuming the
    /// reset grant or changing its verifier. Commit those changes with the revocations.
    /// </summary>
    public async Task<bool> LockAndRevokeSessionsAsync(Guid subjectId, CancellationToken ct)
    {
        if (!await bindings.LockSubjectAsync(subjectId, ct))
            return false;

        var now = clock.GetUtcNow();
        await catalog.Sessions.Where(session => session.SubjectId == subjectId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAtUtc, (DateTimeOffset?)now), ct);
        return true;
    }
}
