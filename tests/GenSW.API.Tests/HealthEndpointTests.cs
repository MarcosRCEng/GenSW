using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class HealthEndpointTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task GetHealth_remains_anonymous_and_returns_ok()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/v1/health");

        response.EnsureSuccessStatusCode();
    }
}
