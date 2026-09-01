using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GenSW.Application.People;
using GenSW.Application.Species;

namespace GenSW.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_returns_the_same_service_collection()
    {
        var services = new ServiceCollection();

        var result = DependencyInjection.AddApplication(services);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddApplication_registers_person_and_species_services_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service => service.ServiceType == typeof(IPessoaService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service => service.ServiceType == typeof(IEspecieService)).Lifetime);
    }
}
