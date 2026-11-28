using Microsoft.AspNetCore.Http;

namespace GenSW.API.Authentication;

internal static class RefreshTokenCookie
{
    public const string Name = "GenSW.RefreshSession.v2";

    private const string CookiePath = "/api/v1/auth";
    private const string PlatformName = "GenSW.PlatformRefreshSession.v2";
    private const string PlatformPath = "/api/v1/platform/auth";

    public static void Append(HttpResponse response, string refreshToken, DateTimeOffset expiresAtUtc, bool platform = false)
    {
        var remainingLifetime = expiresAtUtc - DateTimeOffset.UtcNow;

        response.Cookies.Append(CookieName(platform), refreshToken, CreateOptions(platform,
            expiresAtUtc,
            remainingLifetime > TimeSpan.Zero ? remainingLifetime : TimeSpan.Zero));
    }

    public static void Delete(HttpResponse response, bool platform = false)
    {
        response.Cookies.Delete(CookieName(platform), CreateOptions(platform));
    }

    public static string CookieName(bool platform) => platform ? PlatformName : Name;

    private static CookieOptions CreateOptions(
        bool platform,
        DateTimeOffset? expiresAtUtc = null,
        TimeSpan? maxAge = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = platform ? PlatformPath : CookiePath,
            Expires = expiresAtUtc,
            MaxAge = maxAge,
            IsEssential = true
        };
    }
}
