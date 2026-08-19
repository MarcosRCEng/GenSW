using GenSW.Infrastructure;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class PersistenceConfigurationTests
{
    [Fact]
    public void AddInfrastructure_registers_postgresql_context_when_connection_string_is_configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GenSW"] = "Host=localhost;Database=gensw_test"
            })
            .Build();
        var services = new ServiceCollection();

        DependencyInjection.AddInfrastructure(services, configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GenSWDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }
}
