using System.Net;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class CorsAndRateLimitingTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Cors_allows_configured_origin_with_credentials_and_never_uses_wildcard()
    {
        using var client = factory.CreateHttpsClient();
        using var request = CreatePreflight(AuthWebApplicationFactory.AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            AuthWebApplicationFactory.AllowedOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.DoesNotContain(
            "*",
            response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_does_not_allow_unconfigured_origin()
    {
        using var client = factory.CreateHttpsClient();
        using var request = CreatePreflight("https://untrusted.example.test");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Login_rate_limiter_returns_429_after_partition_limit()
    {
        var rateLimitedFactory = new AuthWebApplicationFactory(2, 60);
        await rateLimitedFactory.InitializeAsync();

        try
        {
            using var client = rateLimitedFactory.CreateHttpsClient();
            using var first = await client.LoginAsync(
                UniqueUserName("rate_first"),
                AuthWebApplicationFactory.ValidPassword);
            using var second = await client.LoginAsync(
                UniqueUserName("rate_second"),
                AuthWebApplicationFactory.ValidPassword);
            using var limited = await client.LoginAsync(
                UniqueUserName("rate_limited"),
                AuthWebApplicationFactory.ValidPassword);

            Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }
        finally
        {
            await ((IAsyncLifetime)rateLimitedFactory).DisposeAsync();
        }
    }

    private static HttpRequestMessage CreatePreflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "content-type");
        return request;
    }

    private static string UniqueUserName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
