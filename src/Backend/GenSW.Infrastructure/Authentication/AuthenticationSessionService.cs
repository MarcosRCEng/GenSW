using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using GenSW.Application.Authentication;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace GenSW.Infrastructure.Authentication;

internal sealed class AuthenticationSessionService(
    UserManager<ApplicationUser> userManager,
    GenSWDbContext dbContext,
    IAccessTokenService accessTokenService,
    RefreshTokenProtector refreshTokenProtector,
    TimeProvider timeProvider,
    ILogger<AuthenticationSessionService> logger) : IAuthenticationSessionService
{
    private const int RefreshSessionDays = 7;
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private static readonly ApplicationUser DummyPasswordUser = new();
    private static readonly string DummyPasswordHash = new PasswordHasher<ApplicationUser>()
        .HashPassword(DummyPasswordUser, Guid.NewGuid().ToString("N"));

    public async Task<AuthenticationSessionResult?> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("Authentication login rejected. Reason: {Reason}.", "InvalidInput");
            return null;
        }

        var user = await userManager.FindByNameAsync(userName);

        if (user is null)
        {
            PerformDummyPasswordVerification(password);
            logger.LogWarning("Authentication login rejected. Reason: {Reason}.", "UnknownUser");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (await userManager.IsLockedOutAsync(user))
        {
            PerformDummyPasswordVerification(password);
            logger.LogWarning(
                "Authentication login rejected for user {UserId}. Reason: {Reason}.",
                user.Id,
                "LockedOut");
            return null;
        }

        if (!user.IsActive)
        {
            PerformDummyPasswordVerification(password);
            logger.LogWarning(
                "Authentication login rejected for user {UserId}. Reason: {Reason}.",
                user.Id,
                "InactiveUser");
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await RegisterFailedLoginAsync(user);
            return null;
        }

        if (!await ResetFailedLoginCountAsync(user))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var accessToken = accessTokenService.Create(user.Id, roles);
        var refreshToken = refreshTokenProtector.Generate();
        var refreshTokenHash = refreshTokenProtector.ComputeHash(refreshToken);
        var now = GetUtcNow();
        var familyId = Guid.NewGuid();
        var expiresAtUtc = now.AddDays(RefreshSessionDays);

        dbContext.RefreshSessions.Add(new RefreshSession
        {
            Id = familyId,
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = refreshTokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Authentication login succeeded for user {UserId}.", user.Id);

        return new AuthenticationSessionResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = expiresAtUtc,
        };
    }

    public async Task<AuthenticationSessionResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (!refreshTokenProtector.TryComputeHash(refreshToken, out var refreshTokenHash))
        {
            logger.LogWarning("Authentication refresh rejected. Reason: {Reason}.", "InvalidToken");
            return null;
        }

        try
        {
            var lookup = await FindSessionLookupAsync(refreshTokenHash, cancellationToken);

            if (lookup is null)
            {
                logger.LogWarning("Authentication refresh rejected. Reason: {Reason}.", "UnknownToken");
                return null;
            }

            await using var transaction = await BeginFamilyTransactionAsync(cancellationToken);

            if (!await TryLockFamilyRootAsync(lookup.FamilyId, cancellationToken))
            {
                logger.LogWarning("Authentication refresh rejected. Reason: {Reason}.", "MissingFamilyRoot");
                return null;
            }

            var current = await FindCurrentSessionAsync(lookup.Id, lookup.FamilyId, refreshTokenHash, cancellationToken);

            if (current is null)
            {
                logger.LogWarning("Authentication refresh rejected. Reason: {Reason}.", "SessionChanged");
                return null;
            }

            var now = GetUtcNow();

            if (current.RevokedAtUtc is not null || current.ReplacedBySessionId is not null)
            {
                await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogWarning(
                    "Refresh token replay detected for user {UserId}; the session family was revoked.",
                    current.UserId);
                return null;
            }

            if (current.ExpiresAtUtc <= now)
            {
                await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogWarning(
                    "Authentication refresh rejected for user {UserId}. Reason: {Reason}.",
                    current.UserId,
                    "ExpiredSession");
                return null;
            }

            if (!current.User.IsActive)
            {
                await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogWarning(
                    "Authentication refresh rejected for user {UserId}. Reason: {Reason}.",
                    current.UserId,
                    "InactiveUser");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var roles = (await userManager.GetRolesAsync(current.User)).ToArray();
            var consumedRows = await ConsumeCurrentSessionAsync(current, now, cancellationToken);

            if (consumedRows != 1)
            {
                await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogWarning(
                    "Authentication refresh lost a concurrency check for user {UserId}; the session family was revoked.",
                    current.UserId);
                return null;
            }

            var successorToken = refreshTokenProtector.Generate();
            var successor = new RefreshSession
            {
                Id = Guid.NewGuid(),
                UserId = current.UserId,
                FamilyId = current.FamilyId,
                TokenHash = refreshTokenProtector.ComputeHash(successorToken),
                CreatedAtUtc = now,
                ExpiresAtUtc = current.ExpiresAtUtc,
            };

            dbContext.RefreshSessions.Add(successor);
            await dbContext.SaveChangesAsync(cancellationToken);

            var linkedRows = await dbContext.RefreshSessions
                .Where(session =>
                    session.Id == current.Id &&
                    session.UserId == current.UserId &&
                    session.FamilyId == current.FamilyId &&
                    session.RevokedAtUtc != null &&
                    session.ReplacedBySessionId == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        session => session.ReplacedBySessionId,
                        (Guid?)successor.Id),
                    cancellationToken);

            if (linkedRows != 1)
            {
                throw new InvalidOperationException("The consumed refresh session could not be linked to its successor.");
            }

            var accessToken = accessTokenService.Create(current.UserId, roles);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Authentication refresh succeeded for user {UserId}.", current.UserId);

            return new AuthenticationSessionResult
            {
                AccessToken = accessToken,
                RefreshToken = successorToken,
                RefreshTokenExpiresAtUtc = successor.ExpiresAtUtc,
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(refreshTokenHash);
        }
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (!refreshTokenProtector.TryComputeHash(refreshToken, out var refreshTokenHash))
        {
            return;
        }

        try
        {
            var lookup = await FindSessionLookupAsync(refreshTokenHash, cancellationToken);

            if (lookup is null)
            {
                return;
            }

            await using var transaction = await BeginFamilyTransactionAsync(cancellationToken);

            if (!await TryLockFamilyRootAsync(lookup.FamilyId, cancellationToken))
            {
                return;
            }

            var current = await FindCurrentSessionAsync(lookup.Id, lookup.FamilyId, refreshTokenHash, cancellationToken);

            if (current is null)
            {
                return;
            }

            await RevokeFamilyAsync(current.FamilyId, GetUtcNow(), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Authentication logout revoked a session family for user {UserId}.", current.UserId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(refreshTokenHash);
        }
    }

    public async Task<CurrentUserResult?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.PessoaId,
                candidate.IsActive,
                candidate.UserName,
                candidate.Pessoa.Nome,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == user.Id)
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.Name)
            .Where(roleName => roleName != null)
            .OrderBy(roleName => roleName)
            .Select(roleName => roleName!)
            .ToArrayAsync(cancellationToken);

        return new CurrentUserResult
        {
            UserId = user.Id,
            PessoaId = user.PessoaId,
            Nome = user.Nome,
            UserName = user.UserName ?? string.Empty,
            Roles = roles,
        };
    }

    private async Task RegisterFailedLoginAsync(ApplicationUser user)
    {
        var lockoutEnabled = userManager.SupportsUserLockout &&
            await userManager.GetLockoutEnabledAsync(user);

        if (lockoutEnabled)
        {
            var failureResult = await userManager.AccessFailedAsync(user);

            if (!failureResult.Succeeded)
            {
                logger.LogError(
                    "Authentication failed to update the access-failure count for user {UserId}.",
                    user.Id);
                return;
            }
        }

        if (lockoutEnabled && await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Authentication lockout activated for user {UserId}.", user.Id);
            return;
        }

        logger.LogWarning(
            "Authentication login rejected for user {UserId}. Reason: {Reason}.",
            user.Id,
            "InvalidPassword");
    }

    private void PerformDummyPasswordVerification(string password)
    {
        _ = userManager.PasswordHasher.VerifyHashedPassword(
            DummyPasswordUser,
            DummyPasswordHash,
            password);
    }

    private async Task<bool> ResetFailedLoginCountAsync(ApplicationUser user)
    {
        if (!userManager.SupportsUserLockout)
        {
            return true;
        }

        var resetResult = await userManager.ResetAccessFailedCountAsync(user);

        if (resetResult.Succeeded)
        {
            return true;
        }

        logger.LogError(
            "Authentication failed to reset the access-failure count for user {UserId}.",
            user.Id);
        return false;
    }

    private async Task<SessionLookup?> FindSessionLookupAsync(
        byte[] refreshTokenHash,
        CancellationToken cancellationToken)
    {
        return await dbContext.RefreshSessions
            .AsNoTracking()
            .Where(session => session.TokenHash == refreshTokenHash)
            .Select(session => new SessionLookup(session.Id, session.FamilyId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<RefreshSession?> FindCurrentSessionAsync(
        Guid sessionId,
        Guid familyId,
        byte[] refreshTokenHash,
        CancellationToken cancellationToken)
    {
        return await dbContext.RefreshSessions
            .AsNoTracking()
            .Include(session => session.User)
            .Where(session =>
                session.Id == sessionId &&
                session.FamilyId == familyId &&
                session.TokenHash == refreshTokenHash)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<IDbContextTransaction> BeginFamilyTransactionAsync(
        CancellationToken cancellationToken)
    {
        var isolationLevel = string.Equals(
            dbContext.Database.ProviderName,
            SqliteProviderName,
            StringComparison.Ordinal)
            ? IsolationLevel.Serializable
            : IsolationLevel.ReadCommitted;

        return await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    private async Task<bool> TryLockFamilyRootAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(dbContext.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            return await TryLockNpgsqlFamilyRootAsync(familyId, cancellationToken);
        }

        if (string.Equals(dbContext.Database.ProviderName, SqliteProviderName, StringComparison.Ordinal))
        {
            var lockedRows = await dbContext.RefreshSessions
                .Where(session => session.Id == familyId && session.FamilyId == familyId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(session => session.FamilyId, session => session.FamilyId),
                    cancellationToken);

            return lockedRows == 1;
        }

        throw new NotSupportedException(
            $"Refresh-session family locking is not supported by provider '{dbContext.Database.ProviderName}'.");
    }

    private async Task<bool> TryLockNpgsqlFamilyRootAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var currentTransaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A transaction is required before locking a refresh-session family.");
        var connection = dbContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.Transaction = currentTransaction.GetDbTransaction();
        command.CommandText = """
            SELECT 1
            FROM "RefreshSessions"
            WHERE "Id" = @familyId AND "FamilyId" = @familyId
            FOR UPDATE
            """;

        var familyParameter = command.CreateParameter();
        familyParameter.ParameterName = "familyId";
        familyParameter.DbType = DbType.Guid;
        familyParameter.Value = familyId;
        command.Parameters.Add(familyParameter);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task<int> ConsumeCurrentSessionAsync(
        RefreshSession current,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSession = dbContext.RefreshSessions.Where(session =>
            session.Id == current.Id &&
            session.UserId == current.UserId &&
            session.FamilyId == current.FamilyId &&
            session.RevokedAtUtc == null &&
            session.ReplacedBySessionId == null);

        if (string.Equals(dbContext.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            activeSession = activeSession.Where(session => session.ExpiresAtUtc > now);
        }

        return await activeSession.ExecuteUpdateAsync(
            setters => setters.SetProperty(session => session.RevokedAtUtc, (DateTimeOffset?)now),
            cancellationToken);
    }

    private Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        return dbContext.RefreshSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.RevokedAtUtc,
                    (DateTimeOffset?)revokedAtUtc),
                cancellationToken);
    }

    private DateTimeOffset GetUtcNow()
    {
        return timeProvider.GetUtcNow().ToUniversalTime();
    }

    private sealed record SessionLookup(Guid Id, Guid FamilyId);
}
