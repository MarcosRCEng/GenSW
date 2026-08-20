using Microsoft.AspNetCore.Http;

namespace GenSW.API.Authentication;

internal static class RefreshTokenCookie
{
    public const string Name = "GenSW.RefreshSession";

    private const string CookiePath = "/api/v1/auth";

    public static void Append(HttpResponse response, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        var remainingLifetime = expiresAtUtc - DateTimeOffset.UtcNow;

        response.Cookies.Append(Name, refreshToken, CreateOptions(
            expiresAtUtc,
            remainingLifetime > TimeSpan.Zero ? remainingLifetime : TimeSpan.Zero));
    }

    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(Name, CreateOptions());
    }

    private static CookieOptions CreateOptions(
        DateTimeOffset? expiresAtUtc = null,
        TimeSpan? maxAge = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
            Expires = expiresAtUtc,
            MaxAge = maxAge,
            IsEssential = true
        };
    }
}
