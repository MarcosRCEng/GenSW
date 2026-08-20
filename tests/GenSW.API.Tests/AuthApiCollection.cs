using Xunit;

namespace GenSW.API.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthApiCollection : ICollectionFixture<AuthWebApplicationFactory>
{
    public const string Name = "Authentication API";
}
