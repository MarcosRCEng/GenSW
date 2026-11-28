using System.Security.Cryptography;
using GenSW.Application.Accounts;
using GenSW.Application.Authentication;
using GenSW.Infrastructure.Accounts;
using GenSW.Infrastructure.Catalog;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GenSW.Infrastructure.Authentication;

internal sealed class AuthenticationSessionService(
    UserManager<CatalogUser> userManager,
    CatalogDbContext catalog,
    CatalogBindings bindings,
    AccountScope accountScope,
    IServiceProvider services,
    IAccessTokenService accessTokenService,
    RefreshTokenProtector refreshTokenProtector,
    TimeProvider timeProvider,
    ILogger<AuthenticationSessionService> logger) : IAuthenticationSessionService, IPlatformAuthenticationSessionService
{
    private const int SessionDays = 7;
    private static readonly CatalogUser DummyPasswordUser = new();
    private static readonly string DummyPasswordHash = new PasswordHasher<CatalogUser>()
        .HashPassword(DummyPasswordUser, Guid.NewGuid().ToString("N"));

    public Task<AuthenticationSessionResult?> LoginAsync(string userName, string password, CancellationToken ct) =>
        LoginAsync(userName, password, CatalogSessionAudience.Erp, ct);

    Task<AuthenticationSessionResult?> IPlatformAuthenticationSessionService.LoginAsync(
        string userName, string password, CancellationToken ct) =>
        LoginAsync(userName, password, CatalogSessionAudience.Platform, ct);

    private async Task<AuthenticationSessionResult?> LoginAsync(
        string userName, string password, CatalogSessionAudience audience, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            return null;

        var normalizedName = userManager.NormalizeName(userName);
        var subjectId = await catalog.Users.AsNoTracking().Where(row => row.NormalizedUserName == normalizedName)
            .Select(row => (Guid?)row.Id).SingleOrDefaultAsync(ct);
        if (subjectId is null)
        {
            VerifyDummyPassword(password);
            return null;
        }

        await using var transaction = await catalog.Database.BeginTransactionAsync(bindings.TransactionIsolation, ct);
        if (!await bindings.LockSubjectAsync(subjectId.Value, ct))
        {
            VerifyDummyPassword(password);
            return null;
        }

        var user = await userManager.FindByIdAsync(subjectId.Value.ToString("D"));
        if (user is null || user.NormalizedUserName != normalizedName || !user.IsActive ||
            await userManager.IsLockedOutAsync(user))
        {
            VerifyDummyPassword(password);
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            if (await userManager.GetLockoutEnabledAsync(user))
                await userManager.AccessFailedAsync(user);
            await transaction.CommitAsync(ct);
            return null;
        }

        if (!(await userManager.ResetAccessFailedCountAsync(user)).Succeeded)
            return null;

        CatalogErpBinding? resolved = null;
        if (audience == CatalogSessionAudience.Erp)
        {
            var accountIds = await catalog.Accounts.AsNoTracking().Where(row => row.SubjectId == user.Id)
                .Select(row => row.Id).Take(2).ToListAsync(ct);
            if (accountIds.Count > 1)
                throw new AccountUnavailableException();
            if (accountIds.Count == 0 || !await bindings.LockAccountAsync(accountIds[0], ct))
                return null;

            resolved = await bindings.ResolveAsync(user.Id, ct);
            if (resolved is null)
                return null;

            await accountScope.ResolveAsync(resolved.Account, resolved.Binding, ct);
        }
        else if (!await HasPlatformAccessAsync(user))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var session = new CatalogSession
        {
            Id = Guid.NewGuid(),
            SubjectId = user.Id,
            Audience = audience,
            AccountId = resolved?.Account.Id,
            BindingId = resolved?.Binding.Id,
            BindingRevision = resolved?.Binding.Revision,
            AccessGeneration = resolved?.Account.AccessGeneration,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(SessionDays),
        };
        catalog.Sessions.Add(session);
        var result = await IssueAsync(session, now, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Session {SessionId} issued for principal {SubjectId}, audience {Audience}.",
            session.Id, session.SubjectId, audience);
        return result;
    }

    public Task<AuthenticationSessionResult?> RefreshAsync(string refreshToken, CancellationToken ct) =>
        RefreshAsync(refreshToken, expectedSessionId: null, ct);

    public Task<AuthenticationSessionResult?> RefreshAsync(string refreshToken, Guid? expectedSessionId, CancellationToken ct) =>
        RefreshAsync(refreshToken, expectedSessionId, CatalogSessionAudience.Erp, ct);

    Task<AuthenticationSessionResult?> IPlatformAuthenticationSessionService.RefreshAsync(
        string refreshToken, Guid? expectedSessionId, CancellationToken ct) =>
        RefreshAsync(refreshToken, expectedSessionId, CatalogSessionAudience.Platform, ct);

    private async Task<AuthenticationSessionResult?> RefreshAsync(
        string refreshToken, Guid? expectedSessionId, CatalogSessionAudience audience, CancellationToken ct)
    {
        var lookup = await FindRefreshAsync(refreshToken, audience, ct);
        if (lookup is null)
            return null;

        if (expectedSessionId is Guid expected && expected != lookup.SessionId)
            throw new AuthenticationSessionMismatchException();

        await using var transaction = await catalog.Database.BeginTransactionAsync(bindings.TransactionIsolation, ct);
        if (!await bindings.LockSubjectAsync(lookup.SubjectId, ct) ||
            lookup.AccountId is Guid accountId && !await bindings.LockAccountAsync(accountId, ct) ||
            !await bindings.LockSessionAsync(lookup.SessionId, ct))
            return null;

        var session = await catalog.Sessions.SingleAsync(row => row.Id == lookup.SessionId, ct);
        if (session.RevokedAtUtc is not null)
            return null;

        var token = await catalog.RefreshTokens.SingleAsync(row => row.Id == lookup.TokenId, ct);
        var now = timeProvider.GetUtcNow();
        var user = await userManager.FindByIdAsync(session.SubjectId.ToString("D"));
        if (token.ConsumedAtUtc is not null || session.ExpiresAtUtc <= now || user is null || !user.IsActive)
        {
            // A spent digest can identify its own family. Revoking the session also revokes
            // every bearer issued by it, including a rotation that won the lock.
            await RevokeAsync(session, now, ct);
            await transaction.CommitAsync(ct);
            if (token.ConsumedAtUtc is not null)
                logger.LogWarning("Refresh replay revoked session {SessionId} for principal {SubjectId}.",
                    session.Id, session.SubjectId);
            return null;
        }

        if (audience == CatalogSessionAudience.Erp)
        {
            var resolved = await bindings.ResolveAsync(session.SubjectId, ct);
            if (resolved is null || session.AccountId != resolved.Account.Id ||
                session.AccessGeneration != resolved.Account.AccessGeneration ||
                session.BindingId != resolved.Binding.Id || session.BindingRevision != resolved.Binding.Revision)
            {
                await RevokeAsync(session, now, ct);
                await transaction.CommitAsync(ct);
                return null;
            }

            await accountScope.ResolveAsync(resolved.Account, resolved.Binding, ct);
        }
        else if (!await HasPlatformAccessAsync(user))
        {
            await RevokeAsync(session, now, ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        token.ConsumedAtUtc = now;
        await catalog.SaveChangesAsync(ct);
        var result = await IssueAsync(session, now, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct) =>
        _ = await LogoutAsync(refreshToken, null, null, null, ct);

    public Task<bool> LogoutAsync(
        string? refreshToken, Guid? expectedSessionId, Guid? bearerSubjectId, Guid? bearerSessionId, CancellationToken ct) =>
        LogoutAsync(refreshToken, expectedSessionId, bearerSubjectId, bearerSessionId, CatalogSessionAudience.Erp, ct);

    Task<bool> IPlatformAuthenticationSessionService.LogoutAsync(
        string? refreshToken, Guid? expectedSessionId, Guid? bearerSubjectId, Guid? bearerSessionId, CancellationToken ct) =>
        LogoutAsync(refreshToken, expectedSessionId, bearerSubjectId, bearerSessionId, CatalogSessionAudience.Platform, ct);

    private async Task<bool> LogoutAsync(
        string? refreshToken, Guid? expectedSessionId, Guid? bearerSubjectId, Guid? bearerSessionId,
        CatalogSessionAudience audience, CancellationToken ct)
    {
        var lookup = await FindRefreshAsync(refreshToken, audience, ct);
        var targetSessionId = bearerSessionId ?? lookup?.SessionId;
        if (targetSessionId is null)
            return true;

        if (expectedSessionId is Guid expected && expected != targetSessionId)
            throw new AuthenticationSessionMismatchException();

        var targetSubjectId = bearerSubjectId ?? lookup?.SubjectId;
        await using var transaction = await catalog.Database.BeginTransactionAsync(bindings.TransactionIsolation, ct);
        if (await bindings.LockSessionAsync(targetSessionId.Value, ct))
        {
            var session = await catalog.Sessions.SingleOrDefaultAsync(row =>
                row.Id == targetSessionId && row.SubjectId == targetSubjectId && row.Audience == audience, ct);
            if (session is { RevokedAtUtc: null })
                await RevokeAsync(session, timeProvider.GetUtcNow(), ct);

            await transaction.CommitAsync(ct);
        }

        return lookup is null || lookup.SessionId == targetSessionId;
    }

    public async Task<CurrentUserResult?> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var scope = accountScope.Current;
        if (scope.ActorId != userId)
            return null;

        var db = services.GetRequiredService<GenSWDbContext>();
        var actor = await db.Users.AsNoTracking().Where(row => row.Id == userId && row.IsActive && row.Pessoa.Ativo)
            .Select(row => new { row.PessoaId, row.Pessoa.Nome }).SingleOrDefaultAsync(ct);
        if (actor is null)
            return null;

        var roles = await (from membership in db.UserRoles.AsNoTracking()
                           join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                           where membership.UserId == userId && role.Name != null
                           orderby role.Name
                           select role.Name!).ToArrayAsync(ct);
        return new CurrentUserResult { UserId = userId, ErpActorId = userId, PessoaId = actor.PessoaId, Nome = actor.Nome, Roles = roles };
    }

    public async Task<CurrentUserResult?> GetSessionUserAsync(Guid subjectId, CancellationToken ct)
    {
        var scope = accountScope.Current;
        var user = await (from account in catalog.Accounts.AsNoTracking()
                          join subject in catalog.Users.AsNoTracking() on account.SubjectId equals subject.Id
                          where account.Id == scope.AccountId && subject.Id == subjectId && subject.IsActive
                          select subject.UserName).SingleOrDefaultAsync(ct);
        var actor = await GetCurrentUserAsync(scope.ActorId, ct);
        return user is null || actor is null ? null : new CurrentUserResult
        {
            UserId = subjectId,
            SubjectId = subjectId,
            ErpActorId = actor.ErpActorId,
            PessoaId = actor.PessoaId,
            Nome = actor.Nome,
            UserName = user,
            Roles = actor.Roles,
        };
    }

    Task<PlatformUserResult?> IPlatformAuthenticationSessionService.GetSessionUserAsync(
        Guid subjectId, Guid sessionId, CancellationToken ct) => GetPlatformUserAsync(subjectId, sessionId, ct);

    private async Task<PlatformUserResult?> GetPlatformUserAsync(Guid subjectId, Guid sessionId, CancellationToken ct)
    {
        var result = await (from session in catalog.Sessions.AsNoTracking()
                            join user in catalog.Users.AsNoTracking() on session.SubjectId equals user.Id
                            where session.Id == sessionId && session.SubjectId == subjectId && user.IsActive &&
                                session.Audience == CatalogSessionAudience.Platform && session.RevokedAtUtc == null
                            select new { User = user, session.ExpiresAtUtc }).SingleOrDefaultAsync(ct);
        if (result is null || result.ExpiresAtUtc <= timeProvider.GetUtcNow())
            return null;

        // Role changes are observed when authorizing a bearer, independently of JWT expiry.
        var roles = await userManager.GetRolesAsync(result.User);
        return new PlatformUserResult(subjectId, result.User.UserName ?? string.Empty, result.User.DisplayName, roles.ToArray());
    }

    private async Task<AuthenticationSessionResult> IssueAsync(CatalogSession session, DateTimeOffset now, CancellationToken ct)
    {
        var token = refreshTokenProtector.Generate();
        catalog.RefreshTokens.Add(new CatalogRefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Digest = refreshTokenProtector.ComputeHash(token),
            CreatedAtUtc = now,
        });
        await catalog.SaveChangesAsync(ct);
        return new AuthenticationSessionResult
        {
            AccessToken = accessTokenService.Create(session.SubjectId, session.Id, session.ExpiresAtUtc,
                session.Audience == CatalogSessionAudience.Erp
                    ? new ErpAccessClaims(session.AccountId!.Value, session.BindingId!.Value,
                        session.BindingRevision!.Value, session.AccessGeneration!.Value)
                    : null),
            RefreshToken = token,
            RefreshTokenExpiresAtUtc = session.ExpiresAtUtc,
        };
    }

    private async Task<RefreshLookup?> FindRefreshAsync(
        string? token, CatalogSessionAudience audience, CancellationToken ct)
    {
        if (!refreshTokenProtector.TryComputeHash(token, out var digest))
            return null;

        try
        {
            return await (from refresh in catalog.RefreshTokens.AsNoTracking()
                          join session in catalog.Sessions.AsNoTracking() on refresh.SessionId equals session.Id
                          where refresh.Digest == digest && session.Audience == audience
                          select new RefreshLookup(refresh.Id, session.Id, session.SubjectId, session.AccountId)).SingleOrDefaultAsync(ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private async Task RevokeAsync(CatalogSession session, DateTimeOffset now, CancellationToken ct)
    {
        session.RevokedAtUtc = now;
        await catalog.SaveChangesAsync(ct);
    }

    private void VerifyDummyPassword(string password) =>
        _ = userManager.PasswordHasher.VerifyHashedPassword(DummyPasswordUser, DummyPasswordHash, password);

    private async Task<bool> HasPlatformAccessAsync(CatalogUser user) =>
        await userManager.IsInRoleAsync(user, PlatformAuthentication.ManageAccountsRole) ||
        await userManager.IsInRoleAsync(user, PlatformAuthentication.AccessRole);

    private sealed record RefreshLookup(Guid TokenId, Guid SessionId, Guid SubjectId, Guid? AccountId);
}
