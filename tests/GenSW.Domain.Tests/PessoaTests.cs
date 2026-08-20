using GenSW.Domain.People;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class PessoaTests
{
    [Fact]
    public void Pessoa_has_only_the_minimum_A2_domain_fields()
    {
        var propertyNames = typeof(Pessoa)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            ["CreatedAtUtc", "Id", "Nome", "UpdatedAtUtc"],
            propertyNames);
        Assert.Equal(typeof(Guid), typeof(Pessoa).GetProperty(nameof(Pessoa.Id))!.PropertyType);
        Assert.Equal(typeof(string), typeof(Pessoa).GetProperty(nameof(Pessoa.Nome))!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), typeof(Pessoa).GetProperty(nameof(Pessoa.CreatedAtUtc))!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), typeof(Pessoa).GetProperty(nameof(Pessoa.UpdatedAtUtc))!.PropertyType);
    }

    [Fact]
    public void Pessoa_belongs_to_Domain_without_Identity_or_Infrastructure_dependencies()
    {
        var domainAssembly = typeof(Pessoa).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        Assert.Equal(typeof(AssemblyReference).Assembly, domainAssembly);
        Assert.DoesNotContain(
            referencedAssemblies,
            assembly => assembly.Name is not null &&
                (assembly.Name.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) ||
                 assembly.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                 assembly.Name.StartsWith("GenSW.Infrastructure", StringComparison.Ordinal)));
    }
}
