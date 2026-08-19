using Xunit;

namespace GenSW.Domain.Tests;

public sealed class AssemblyReferenceTests
{
    [Fact]
    public void DomainAssembly_is_available()
    {
        Assert.NotNull(typeof(AssemblyReference).Assembly);
    }
}
