using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class IdentificacaoAnimalContractsTests
{
    [Fact]
    public void Contracts_preserve_all_constructor_values()
    {
        var id = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var create = new CreateIdentificacaoAnimalCommand(TipoIdentificacaoAnimal.Anilha, null, "A  01-0", true,
            new DateOnly(2026, 9, 1), "observação");
        var metadata = new UpdateIdentificacaoAnimalMetadataCommand(true, new DateOnly(2026, 9, 2), true, "nova");
        var ativo = new SetIdentificacaoAnimalAtivoCommand(false);
        var principal = new SetIdentificacaoAnimalPrincipalCommand(true);
        var result = new IdentificacaoAnimalResult(id, animalId, TipoIdentificacaoAnimal.Anilha, null, "A  01-0", true,
            new DateOnly(2026, 9, 1), "observação", true, createdAt, createdAt);
        var animal = new IdentificacaoAnimalAnimalResumo(animalId, "AN-000001", "Bela");
        var global = new IdentificacaoAnimalGlobalResult(result, animal);
        var page = new PagedIdentificacaoAnimalResult([result], 1, 25, 1, 1);
        var globalPage = new PagedIdentificacaoAnimalGlobalResult([global], 1, 25, 1, 1);

        Assert.Equal(TipoIdentificacaoAnimal.Anilha, create.Tipo);
        Assert.Equal(new DateOnly(2026, 9, 2), metadata.DataAplicacao);
        Assert.False(ativo.Ativo);
        Assert.True(principal.Principal);
        Assert.Equal(id, result.Id);
        Assert.Equal("AN-000001", global.Animal.CodigoInterno);
        Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.Equal(animalId, Assert.Single(globalPage.Items).Animal.Id);
    }

    [Fact]
    public void List_query_has_approved_defaults()
    {
        var query = new IdentificacaoAnimalListQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Null(query.Tipo);
        Assert.Null(query.Valor);
        Assert.Null(query.Ativo);
        Assert.Null(query.Principal);
    }

    [Theory]
    [InlineData(false, null, false, null)]
    [InlineData(true, "2026-09-03", false, null)]
    [InlineData(true, null, false, null)]
    [InlineData(false, null, true, "nota")]
    [InlineData(false, null, true, null)]
    public void Metadata_command_preserves_absent_value_and_null_states(bool hasDataAplicacao, string? dataAplicacao,
        bool hasObservacao, string? observacao)
    {
        var command = new UpdateIdentificacaoAnimalMetadataCommand(hasDataAplicacao,
            dataAplicacao is null ? null : DateOnly.Parse(dataAplicacao), hasObservacao, observacao);

        Assert.Equal(hasDataAplicacao, command.HasDataAplicacao);
        Assert.Equal(dataAplicacao is null ? null : DateOnly.Parse(dataAplicacao), command.DataAplicacao);
        Assert.Equal(hasObservacao, command.HasObservacao);
        Assert.Equal(observacao, command.Observacao);
    }

    [Fact]
    public void Conflict_source_enums_have_distinct_values()
    {
        var duplicateValues = Enum.GetValues<IdentificacaoAnimalDuplicateConflictSource>();
        var principalValues = Enum.GetValues<IdentificacaoAnimalPrincipalConflictSource>();

        Assert.Equal([
            IdentificacaoAnimalDuplicateConflictSource.PreCheck,
            IdentificacaoAnimalDuplicateConflictSource.PersistedNamedTipoValorUniqueConstraint,
            IdentificacaoAnimalDuplicateConflictSource.PersistedNamedOutroDescricaoTipoValorUniqueConstraint
        ], duplicateValues);
        Assert.Equal(IdentificacaoAnimalPrincipalConflictSource.PersistedNamedPrincipalAtivaUniqueConstraint,
            Assert.Single(principalValues));
    }

    [Fact]
    public void Duplicate_exception_preserves_its_structured_conflict_details()
    {
        var inner = new InvalidOperationException();
        var exception = new IdentificacaoAnimalDuplicateException(TipoIdentificacaoAnimal.Outro, "Colar", "X-1",
            IdentificacaoAnimalDuplicateConflictSource.PersistedNamedOutroDescricaoTipoValorUniqueConstraint, inner);

        Assert.Equal(TipoIdentificacaoAnimal.Outro, exception.Tipo);
        Assert.Equal("Colar", exception.DescricaoTipo);
        Assert.Equal("X-1", exception.Valor);
        Assert.Equal(IdentificacaoAnimalDuplicateConflictSource.PersistedNamedOutroDescricaoTipoValorUniqueConstraint,
            exception.ConflictSource);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Principal_conflict_exception_carries_only_persisted_constraint_provenance()
    {
        var exception = new IdentificacaoAnimalPrincipalConflictException(
            IdentificacaoAnimalPrincipalConflictSource.PersistedNamedPrincipalAtivaUniqueConstraint);

        Assert.Equal(IdentificacaoAnimalPrincipalConflictSource.PersistedNamedPrincipalAtivaUniqueConstraint,
            exception.ConflictSource);
        var property = Assert.Single(exception.GetType().GetProperties(), property => property.DeclaringType == exception.GetType());
        Assert.Equal(nameof(IdentificacaoAnimalPrincipalConflictException.ConflictSource), property.Name);
    }

    [Fact]
    public void Add_application_registers_only_the_identification_service_and_time_provider()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var service = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IIdentificacaoAnimalService));
        Assert.Equal(ServiceLifetime.Scoped, service.Lifetime);
        Assert.Equal(typeof(IdentificacaoAnimalService), service.ImplementationType);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IIdentificacaoAnimalRepository));
    }

    [Fact]
    public void Application_project_does_not_reference_infrastructure()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Backend/GenSW.Application/GenSW.Application.csproj"));

        Assert.DoesNotContain("GenSW.Infrastructure", File.ReadAllText(projectPath), StringComparison.Ordinal);
    }
}
