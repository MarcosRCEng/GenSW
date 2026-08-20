using GenSW.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class AuthenticationConfigurationTests
{
    [Fact]
    public void AddInfrastructure_fails_fast_when_connection_string_is_missing()
    {
        var configuration = CreateConfiguration(
            signingKey: "GENSW_TEST_ONLY_SIGNING_KEY_0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            includeConnectionString: false);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DependencyInjection.AddInfrastructure(new ServiceCollection(), configuration));

        Assert.Contains("ConnectionStrings:GenSW", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Jwt_options_reject_missing_external_signing_key()
    {
        var configuration = CreateConfiguration(signingKey: null, includeConnectionString: true);
        var services = new ServiceCollection();
        DependencyInjection.AddInfrastructure(services, configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<JwtOptions>>().Value);

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("SigningKey is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Jwt_options_reject_signing_key_shorter_than_256_bits()
    {
        var configuration = CreateConfiguration(
            signingKey: "GENSW_TEST_ONLY_too_short",
            includeConnectionString: true);
        var services = new ServiceCollection();
        DependencyInjection.AddInfrastructure(services, configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<JwtOptions>>().Value);

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("at least 32 UTF-8 bytes", StringComparison.Ordinal));
    }

    private static IConfiguration CreateConfiguration(
        string? signingKey,
        bool includeConnectionString)
    {
        var values = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "GenSW.Infrastructure.Tests",
            ["Authentication:Jwt:Audience"] = "GenSW.Infrastructure.Tests.Client",
            ["Authentication:Jwt:AccessTokenMinutes"] = "10",
            ["Authentication:Jwt:SigningKey"] = signingKey,
        };

        if (includeConnectionString)
        {
            values["ConnectionStrings:GenSW"] =
                "Host=127.0.0.1;Database=gensw_test_configuration_only";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
