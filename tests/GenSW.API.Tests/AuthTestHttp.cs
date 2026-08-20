using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GenSW.API.Tests;

internal static class AuthTestHttp
{
    public static Task<HttpResponseMessage> LoginAsync(
        this HttpClient client,
        string userName,
        string password)
    {
        return client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
    }

    public static async Task<HttpResponseMessage> PostWithCookieAsync(
        this HttpClient client,
        string path,
        ResponseCookie cookie,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Cookie", $"{cookie.Name}={cookie.Value}");
        return await client.SendAsync(request, cancellationToken);
    }

    public static async Task<HttpResponseMessage> GetWithBearerAsync(
        this HttpClient client,
        string path,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    public static ResponseCookie GetIssuedCookie(this HttpResponseMessage response)
    {
        var headers = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : throw new InvalidOperationException("Response did not issue a cookie.");

        foreach (var header in headers)
        {
            var pair = header.Split(';', 2, StringSplitOptions.TrimEntries)[0];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0 && separator < pair.Length - 1)
            {
                return new ResponseCookie(
                    pair[..separator],
                    pair[(separator + 1)..],
                    header);
            }
        }

        throw new InvalidOperationException("Response did not issue a non-empty cookie.");
    }

    public static string GetCookieDeletionHeader(
        this HttpResponseMessage response,
        string cookieName)
    {
        var headers = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : throw new InvalidOperationException("Response did not delete a cookie.");

        return headers.Single(header => header.StartsWith(
            $"{cookieName}=",
            StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<(string AccessToken, DateTimeOffset ExpiresAtUtc)> ReadAccessTokenAsync(
        this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        return (
            GetProperty(root, "accessToken").GetString()
                ?? throw new InvalidOperationException("accessToken was null."),
            GetProperty(root, "expiresAtUtc").GetDateTimeOffset());
    }

    public static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new KeyNotFoundException($"JSON property '{propertyName}' was not found.");
    }
}

internal sealed record ResponseCookie(string Name, string Value, string Header);
