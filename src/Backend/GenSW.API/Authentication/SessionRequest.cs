namespace GenSW.API.Authentication;

internal static class SessionRequest
{
    public const string ExpectedSessionHeader = "X-GenSW-Session";

    public static bool IsTrustedOrigin(HttpRequest request, IConfiguration configuration)
    {
        var origins = request.Headers.Origin;
        if (origins.Count == 0)
            return !string.Equals(request.Headers["Sec-Fetch-Site"], "cross-site", StringComparison.OrdinalIgnoreCase);

        if (origins.Count != 1 || !Uri.TryCreate(origins[0], UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(origin.UserInfo) ||
            origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            return false;

        var authority = origin.GetLeftPart(UriPartial.Authority);
        return string.Equals(authority, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase) ||
            (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .Contains(authority, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGetExpectedSession(HttpRequest request, out Guid? sessionId)
    {
        sessionId = null;
        var values = request.Headers[ExpectedSessionHeader];
        if (values.Count == 0)
            return true;
        if (values.Count != 1 || !Guid.TryParseExact(values[0], "D", out var value) || value == Guid.Empty)
            return false;

        sessionId = value;
        return true;
    }
}
