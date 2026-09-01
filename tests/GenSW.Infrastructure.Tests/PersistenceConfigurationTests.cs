using GenSW.Infrastructure;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using GenSW.Application.Species;
using GenSW.Infrastructure.Species;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class PersistenceConfigurationTests
{
    [Fact]
    public void AddInfrastructure_registers_EspecieRepository_as_scoped()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:GenSW"] = "Host=localhost;Database=gensw_test" })
            .Build();
        var services = new ServiceCollection();

        DependencyInjection.AddInfrastructure(services, configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IEspecieRepository) &&
            descriptor.ImplementationType == typeof(EspecieRepository) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

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

    [Fact]
    public void AddInfrastructure_registers_Identity_with_the_approved_password_and_lockout_policy()
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
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(15, options.Password.RequiredLength);
        Assert.False(options.Password.RequireDigit);
        Assert.False(options.Password.RequireLowercase);
        Assert.False(options.Password.RequireUppercase);
        Assert.False(options.Password.RequireNonAlphanumeric);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IUserStore<ApplicationUser>));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRoleStore<IdentityRole<Guid>>));
    }
}
