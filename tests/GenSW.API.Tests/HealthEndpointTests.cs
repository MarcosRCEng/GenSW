using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GenSW.API.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        response.EnsureSuccessStatusCode();
    }
}
