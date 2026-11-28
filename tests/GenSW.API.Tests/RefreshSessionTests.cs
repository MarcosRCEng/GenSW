using System.Net;
using GenSW.Infrastructure.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class RefreshSessionTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Login_persists_only_SHA256_of_the_opaque_refresh_token()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("refresh_hash"));
        using var client = factory.CreateHttpsClient();
        using var response = await client.LoginAsync(user.UserName, user.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.GetIssuedCookie();
        var responseBody = await response.Content.ReadAsStringAsync();
        var storedSession = Assert.Single(await GetUserSessionsAsync(user.SubjectId));
        var expectedHash = new RefreshTokenProtector().ComputeHash(cookie.Value);

        Assert.Equal(expectedHash, storedSession.TokenHash);
        Assert.Equal(32, storedSession.TokenHash.Length);
        Assert.DoesNotContain(cookie.Value, responseBody, StringComparison.Ordinal);
        Assert.Null(typeof(CatalogRefreshToken).GetProperty("Token"));
        Assert.Null(typeof(CatalogRefreshToken).GetProperty("RawToken"));
    }

    [Fact]
    public async Task Refresh_rotates_token_preserving_family_and_absolute_expiration()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("refresh_rotate"));
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var originalCookie = login.GetIssuedCookie();
        var original = Assert.Single(await GetUserSessionsAsync(user.SubjectId));

        using var response = await client.PostWithCookieAsync("/api/v1/auth/refresh", originalCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var replacementCookie = response.GetIssuedCookie();
        Assert.Equal(originalCookie.Name, replacementCookie.Name);
        Assert.NotEqual(originalCookie.Value, replacementCookie.Value);
        Assert.Contains("httponly", replacementCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", replacementCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", replacementCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", replacementCookie.Header, StringComparison.OrdinalIgnoreCase);

        var sessions = await GetUserSessionsAsync(user.SubjectId);
        Assert.Equal(2, sessions.Count);
        var consumed = sessions.Single(session => session.Id == original.Id);
        var successor = sessions.Single(session => session.Id != original.Id);
        Assert.NotNull(consumed.ConsumedAtUtc);
        Assert.Equal(original.FamilyId, successor.FamilyId);
        Assert.Equal(original.ExpiresAtUtc, successor.ExpiresAtUtc);
        Assert.Null(successor.RevokedAtUtc);
        Assert.Equal(new RefreshTokenProtector().ComputeHash(replacementCookie.Value), successor.TokenHash);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(originalCookie.Value, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(replacementCookie.Value, responseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_with_expired_token_returns_unauthorized_and_clears_cookie()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("refresh_expired"));
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var issuedCookie = login.GetIssuedCookie();
        var expired = DateTimeOffset.UtcNow.AddMinutes(-1);
        await factory.ExecuteCatalogAsync(context => context.Sessions.Where(session => session.SubjectId == user.SubjectId)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.ExpiresAtUtc, expired)));

        using var response = await client.PostWithCookieAsync("/api/v1/auth/refresh", issuedCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertCookieDeleted(response, issuedCookie.Name);
        var storedExpiredSession = Assert.Single(await GetUserSessionsAsync(user.SubjectId));
        Assert.NotNull(storedExpiredSession.RevokedAtUtc);
        Assert.Null(storedExpiredSession.ConsumedAtUtc);
    }

    [Fact]
    public async Task Inactive_user_cannot_refresh()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("refresh_inactive"));
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var cookie = login.GetIssuedCookie();
        var sessionCountBefore = (await GetUserSessionsAsync(user.SubjectId)).Count;
        await factory.SetUserActiveAsync(user.UserId, isActive: false);

        using var response = await client.PostWithCookieAsync("/api/v1/auth/refresh", cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertCookieDeleted(response, cookie.Name);
        Assert.Equal(sessionCountBefore, (await GetUserSessionsAsync(user.SubjectId)).Count);
    }

    [Fact]
    public async Task Missing_or_malformed_refresh_token_returns_unauthorized_and_clears_cookie()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("refresh_invalid"));
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var cookie = login.GetIssuedCookie();

        using var missing = await client.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        AssertCookieDeleted(missing, cookie.Name);

        using var malformed = await client.PostWithCookieAsync(
            "/api/v1/auth/refresh",
            cookie with { Value = "GENSW_TEST_ONLY_malformed" });
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        AssertCookieDeleted(malformed, cookie.Name);
    }

    [Fact]
    public async Task Replaying_consumed_token_revokes_family_and_successor_cannot_refresh()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("refresh_replay"));
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var originalCookie = login.GetIssuedCookie();
        var original = Assert.Single(await GetUserSessionsAsync(user.SubjectId));
        using var firstRefresh = await client.PostWithCookieAsync("/api/v1/auth/refresh", originalCookie);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var successorCookie = firstRefresh.GetIssuedCookie();

        using var replay = await client.PostWithCookieAsync("/api/v1/auth/refresh", originalCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        AssertCookieDeleted(replay, originalCookie.Name);
        var family = (await GetUserSessionsAsync(user.SubjectId))
            .Where(session => session.FamilyId == original.FamilyId)
            .ToArray();
        Assert.Equal(2, family.Length);
        Assert.All(family, session => Assert.NotNull(session.RevokedAtUtc));

        using var successorAttempt = await client.PostWithCookieAsync(
            "/api/v1/auth/refresh",
            successorCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, successorAttempt.StatusCode);
        AssertCookieDeleted(successorAttempt, successorCookie.Name);
    }

    [Fact]
    public async Task Concurrent_refresh_requests_do_not_create_two_valid_successors()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("refresh_concurrent"));
        using var loginClient = factory.CreateHttpsClient();
        using var login = await loginClient.LoginAsync(user.UserName, user.Password);
        var originalCookie = login.GetIssuedCookie();
        var original = Assert.Single(await GetUserSessionsAsync(user.SubjectId));
        using var firstClient = factory.CreateHttpsClient();
        using var secondClient = factory.CreateHttpsClient();

        var responses = await Task.WhenAll(
            firstClient.PostWithCookieAsync("/api/v1/auth/refresh", originalCookie),
            secondClient.PostWithCookieAsync("/api/v1/auth/refresh", originalCookie));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized));

            var family = (await GetUserSessionsAsync(user.SubjectId))
                .Where(session => session.FamilyId == original.FamilyId)
                .ToArray();
            var successors = family.Where(session => session.Id != original.Id).ToArray();
            Assert.Single(successors);
            Assert.True(successors.Count(session => session.RevokedAtUtc is null) <= 1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private Task<List<CatalogRefreshRow>> GetUserSessionsAsync(Guid subjectId) =>
        ReadRefreshRowsAsync(factory, subjectId);

    internal static Task<List<CatalogRefreshRow>> ReadRefreshRowsAsync(AuthWebApplicationFactory factory, Guid subjectId) =>
        factory.ExecuteCatalogAsync(context => (from token in context.RefreshTokens.AsNoTracking()
                                                join session in context.Sessions.AsNoTracking() on token.SessionId equals session.Id
                                                where session.SubjectId == subjectId
                                                select new CatalogRefreshRow(token.Id, session.Id, token.Digest,
                                                    token.ConsumedAtUtc, session.RevokedAtUtc, session.ExpiresAtUtc)).ToListAsync());

    internal sealed record CatalogRefreshRow(Guid Id, Guid FamilyId, byte[] TokenHash,
        DateTimeOffset? ConsumedAtUtc, DateTimeOffset? RevokedAtUtc, DateTimeOffset ExpiresAtUtc);

    private static void AssertCookieDeleted(HttpResponseMessage response, string cookieName)
    {
        var deletionHeader = response.GetCookieDeletionHeader(cookieName);
        Assert.StartsWith($"{cookieName}=", deletionHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", deletionHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", deletionHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", deletionHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", deletionHeader, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueUserName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
