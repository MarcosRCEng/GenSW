using System.Net;
using GenSW.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class LogoutTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Logout_revokes_family_clears_cookie_and_is_idempotent()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("logout"));
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var cookie = login.GetIssuedCookie();
        var (accessToken, _) = await login.ReadAccessTokenAsync();
        var familyId = await factory.ExecuteDbContextAsync(context => context.RefreshSessions
            .Where(session => session.UserId == user.UserId)
            .Select(session => session.FamilyId)
            .SingleAsync());

        using var response = await client.PostWithCookieAsync("/api/v1/auth/logout", cookie);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertDeletionCookie(response, cookie.Name);
        var family = await LoadFamilyAsync(familyId);
        Assert.NotEmpty(family);
        Assert.All(family, session => Assert.NotNull(session.RevokedAtUtc));

        using var repeated = await client.PostWithCookieAsync("/api/v1/auth/logout", cookie);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        AssertDeletionCookie(repeated, cookie.Name);

        using var absentCookie = await client.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, absentCookie.StatusCode);

        using var stillValidAccessToken = await client.GetWithBearerAsync("/api/v1/auth/me", accessToken);
        Assert.Equal(HttpStatusCode.OK, stillValidAccessToken.StatusCode);
    }

    private async Task<IReadOnlyList<RefreshSession>> LoadFamilyAsync(Guid familyId)
    {
        return await factory.ExecuteDbContextAsync(async context =>
            (IReadOnlyList<RefreshSession>)await context.RefreshSessions
                .AsNoTracking()
                .Where(session => session.FamilyId == familyId)
                .ToListAsync());
    }

    private static void AssertDeletionCookie(HttpResponseMessage response, string cookieName)
    {
        var header = response.GetCookieDeletionHeader(cookieName);
        Assert.Contains("path=/api/v1/auth", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueUserName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
