using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using GenSW.Infrastructure.Catalog;
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
        var familyId = await factory.ExecuteCatalogAsync(context => context.Sessions
            .Where(session => session.SubjectId == user.SubjectId)
            .Select(session => session.Id)
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

        using var revokedAccessToken = await client.GetWithBearerAsync("/api/v1/auth/me", accessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedAccessToken.StatusCode);
    }

    private async Task<IReadOnlyList<CatalogSession>> LoadFamilyAsync(Guid familyId)
    {
        return await factory.ExecuteCatalogAsync(async context =>
            (IReadOnlyList<CatalogSession>)await context.Sessions
                .AsNoTracking()
                .Where(session => session.Id == familyId)
                .ToListAsync());
    }

    [Fact]
    public async Task Stale_tab_cannot_refresh_or_clear_another_sessions_cookie()
    {
        var a = await factory.SeedUserAsync(UniqueUserName("logout_a"));
        var b = await factory.SeedUserAsync(UniqueUserName("logout_b"));
        using var client = factory.CreateHttpsClient();
        using var loginA = await client.LoginAsync(a.UserName, a.Password);
        using var loginB = await client.LoginAsync(b.UserName, b.Password);
        var (accessA, _) = await loginA.ReadAccessTokenAsync();
        var (accessB, _) = await loginB.ReadAccessTokenAsync();
        var sessionA = new JwtSecurityTokenHandler().ReadJwtToken(accessA).Claims.Single(claim => claim.Type == "sid").Value;
        var browserCookie = loginB.GetIssuedCookie();

        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("X-GenSW-Session", sessionA);
        refresh.Headers.Add("Cookie", $"{browserCookie.Name}={browserCookie.Value}");
        using var staleRefresh = await client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, staleRefresh.StatusCode);
        Assert.False(staleRefresh.Headers.Contains("Set-Cookie"));

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Add("X-GenSW-Session", sessionA);
        logout.Headers.Add("Cookie", $"{browserCookie.Name}={browserCookie.Value}");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessA);
        using var staleLogout = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, staleLogout.StatusCode);
        Assert.False(staleLogout.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetWithBearerAsync("/api/v1/auth/me", accessA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetWithBearerAsync("/api/v1/auth/me", accessB)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostWithCookieAsync("/api/v1/auth/refresh", browserCookie)).StatusCode);
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
