using Microsoft.Extensions.DependencyInjection;
using Xunit;

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
}
