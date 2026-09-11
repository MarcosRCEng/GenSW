using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GenSW.Application.People;
using GenSW.Application.Species;
using GenSW.Application.Breeds;
using GenSW.Application.Varieties;
using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;

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
    public void AddApplication_registers_person_species_breed_variety_and_animal_services_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service => service.ServiceType == typeof(IPessoaService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service => service.ServiceType == typeof(IEspecieService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service => service.ServiceType == typeof(IRacaService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service => service.ServiceType == typeof(IVariedadeService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service => service.ServiceType == typeof(IAnimalService)).Lifetime);
    }

    [Fact]
    public void AddApplication_resolves_the_animal_service_from_a_scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAnimalRepository, UnusedAnimalRepository>();
        services.AddScoped<IAnimalCodeAllocator, UnusedAnimalCodeAllocator>();
        services.AddScoped<IEspecieRepository, UnusedEspecieRepository>();
        services.AddScoped<IRacaRepository, UnusedRacaRepository>();
        services.AddScoped<IVariedadeRepository, UnusedVariedadeRepository>();
        services.AddApplication();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<AnimalService>(scope.ServiceProvider.GetRequiredService<IAnimalService>());
    }

    private sealed class UnusedAnimalRepository : IAnimalRepository
    {
        public Task AddAsync(Animal animal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AnimalReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Animal?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AnimalListPage> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasCodigoInternoConflictAsync(string codigoInterno, Guid? excludingId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedAnimalCodeAllocator : IAnimalCodeAllocator
    {
        public Task<IAnimalAutomaticCodeAttempt> BeginAttemptAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedEspecieRepository : IEspecieRepository
    {
        public Task AddAsync(Especie especie, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasNomeComumConflictAsync(string nomeComum, Guid? excludingId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasNomeCientificoConflictAsync(string nomeCientifico, Guid? excludingId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedRacaRepository : IRacaRepository
    {
        public Task AddAsync(Raca raca, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RacaReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Raca?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RacaListPage> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsReferencedByAnimalAsync(Guid racaId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedVariedadeRepository : IVariedadeRepository
    {
        public Task AddAsync(Variedade variedade, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VariedadeReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Variedade?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VariedadeListPage> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsReferencedByAnimalAsync(Guid variedadeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
