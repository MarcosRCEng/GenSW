using GenSW.Application.Animals;
using GenSW.Application.Breeds;
using GenSW.Application.Species;
using GenSW.Application.Varieties;
using GenSW.Domain.Animals;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class AnimalClassificationValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 8);

    [Fact]
    public async Task Create_maps_missing_species_breed_and_variety_to_their_existing_not_found_exceptions()
    {
        var speciesId = Guid.NewGuid();
        var breedId = Guid.NewGuid();
        var varietyId = Guid.NewGuid();
        var validator = CreateValidator();

        await Assert.ThrowsAsync<EspecieNotFoundException>(() => validator.ValidateCreateAsync(speciesId, null, null));

        var species = Especie.Criar("Cão", null, Now);
        validator = CreateValidator(species: [species]);
        await Assert.ThrowsAsync<RacaNotFoundException>(() => validator.ValidateCreateAsync(species.Id, breedId, null));
        await Assert.ThrowsAsync<VariedadeNotFoundException>(() => validator.ValidateCreateAsync(species.Id, null, varietyId));
    }

    [Fact]
    public async Task Create_requires_active_species_and_active_supplied_classifications_of_the_same_species()
    {
        var activeSpecies = Especie.Criar("Cão", null, Now);
        var inactiveSpecies = Especie.Criar("Gato", null, Now);
        inactiveSpecies.Inativar(Now);
        var activeBreed = Raca.Criar(activeSpecies.Id, "Pastor", Now);
        var inactiveBreed = Raca.Criar(activeSpecies.Id, "Velho", Now);
        inactiveBreed.Inativar(Now);
        var activeVariety = Variedade.Criar(activeSpecies.Id, "Curto", Now);
        var inactiveVariety = Variedade.Criar(activeSpecies.Id, "Longo", Now);
        inactiveVariety.Inativar(Now);
        var validator = CreateValidator([activeSpecies, inactiveSpecies], [activeBreed, inactiveBreed], [activeVariety, inactiveVariety]);

        await validator.ValidateCreateAsync(activeSpecies.Id, null, null);
        await validator.ValidateCreateAsync(activeSpecies.Id, activeBreed.Id, null);
        await validator.ValidateCreateAsync(activeSpecies.Id, null, activeVariety.Id);
        await validator.ValidateCreateAsync(activeSpecies.Id, activeBreed.Id, activeVariety.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateCreateAsync(inactiveSpecies.Id, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateCreateAsync(activeSpecies.Id, inactiveBreed.Id, null));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateCreateAsync(activeSpecies.Id, null, inactiveVariety.Id));
    }

    [Fact]
    public async Task Create_rejects_independent_classifications_from_another_species()
    {
        var dog = Especie.Criar("Cão", null, Now);
        var cat = Especie.Criar("Gato", null, Now);
        var catBreed = Raca.Criar(cat.Id, "Siamês", Now);
        var catVariety = Variedade.Criar(cat.Id, "Curto", Now);
        var validator = CreateValidator([dog, cat], [catBreed], [catVariety]);

        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateCreateAsync(dog.Id, catBreed.Id, null));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateCreateAsync(dog.Id, null, catVariety.Id));
    }

    [Fact]
    public async Task Update_preserves_historical_inactive_species_breed_and_variety_links()
    {
        var species = Especie.Criar("Cão", null, Now);
        var breed = Raca.Criar(species.Id, "Pastor", Now);
        var variety = Variedade.Criar(species.Id, "Curto", Now);
        var animal = Animal.Criar("AN-000001", null, species.Id, breed.Id, variety.Id, SexoAnimal.Macho, null, EscopoAnimal.Operacional, Today, Now);
        species.Inativar(Now); breed.Inativar(Now); variety.Inativar(Now);
        var validator = CreateValidator([species], [breed], [variety]);

        await validator.ValidateUpdateAsync(animal, species.Id, breed.Id, variety.Id);
    }

    [Fact]
    public async Task Update_rejects_a_new_inactive_classification_and_another_inactive_species()
    {
        var active = Especie.Criar("Cão", null, Now);
        var currentInactive = Especie.Criar("Gato", null, Now);
        currentInactive.Inativar(Now);
        var otherInactive = Especie.Criar("Coelho", null, Now);
        otherInactive.Inativar(Now);
        var otherActive = Especie.Criar("Cavalo", null, Now);
        var inactiveBreed = Raca.Criar(currentInactive.Id, "Velha", Now);
        inactiveBreed.Inativar(Now);
        var inactiveVariety = Variedade.Criar(currentInactive.Id, "Longa", Now);
        inactiveVariety.Inativar(Now);
        var otherSpeciesBreed = Raca.Criar(otherActive.Id, "Árabe", Now);
        var otherSpeciesVariety = Variedade.Criar(otherActive.Id, "Liso", Now);
        var animal = Animal.Criar("AN-000001", null, currentInactive.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, Today, Now);
        var validator = CreateValidator([active, currentInactive, otherInactive, otherActive], [inactiveBreed, otherSpeciesBreed], [inactiveVariety, otherSpeciesVariety]);

        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateUpdateAsync(animal, currentInactive.Id, inactiveBreed.Id, null));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateUpdateAsync(animal, currentInactive.Id, null, inactiveVariety.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateUpdateAsync(animal, currentInactive.Id, otherSpeciesBreed.Id, null));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateUpdateAsync(animal, currentInactive.Id, null, otherSpeciesVariety.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateUpdateAsync(animal, otherInactive.Id, null, null));
    }

    [Fact]
    public async Task Update_with_historical_inactive_species_allows_a_new_active_matching_breed_independently()
    {
        var species = Especie.Criar("Cão", null, Now);
        var replacementBreed = Raca.Criar(species.Id, "Pastor", Now);
        var animal = Animal.Criar("AN-000001", null, species.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, Today, Now);
        species.Inativar(Now);
        var validator = CreateValidator([species], [replacementBreed]);

        await validator.ValidateUpdateAsync(animal, species.Id, replacementBreed.Id, null);
    }

    [Fact]
    public async Task Update_with_historical_inactive_species_allows_a_new_active_matching_variety_independently()
    {
        var species = Especie.Criar("Cão", null, Now);
        var replacementVariety = Variedade.Criar(species.Id, "Curto", Now);
        var animal = Animal.Criar("AN-000001", null, species.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, Today, Now);
        species.Inativar(Now);
        var validator = CreateValidator([species], varieties: [replacementVariety]);

        await validator.ValidateUpdateAsync(animal, species.Id, null, replacementVariety.Id);
    }

    [Fact]
    public async Task Update_rejects_policy_b_snapshot_that_retains_incompatible_classification_after_species_change()
    {
        var dog = Especie.Criar("Cão", null, Now);
        var cat = Especie.Criar("Gato", null, Now);
        var dogBreed = Raca.Criar(dog.Id, "Pastor", Now);
        var dogVariety = Variedade.Criar(dog.Id, "Curto", Now);
        var animal = Animal.Criar("AN-000001", null, dog.Id, dogBreed.Id, dogVariety.Id, SexoAnimal.Macho, null, EscopoAnimal.Operacional, Today, Now);
        var validator = CreateValidator([dog, cat], [dogBreed], [dogVariety]);

        var breedException = await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateUpdateAsync(animal, cat.Id, dogBreed.Id, null));
        var varietyException = await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateUpdateAsync(animal, cat.Id, null, dogVariety.Id));

        Assert.Equal("racaId", breedException.ParamName);
        Assert.Equal("variedadeId", varietyException.ParamName);
    }

    private static AnimalClassificationValidator CreateValidator(
        Especie[]? species = null, Raca[]? breeds = null, Variedade[]? varieties = null)
        => new(new FakeEspecieRepository(species ?? []), new FakeRacaRepository(breeds ?? []), new FakeVariedadeRepository(varieties ?? []));

    private sealed class FakeEspecieRepository(IEnumerable<Especie> items) : IEspecieRepository
    {
        private readonly List<Especie> items = [.. items];
        public Task AddAsync(Especie especie, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new EspecieListPage([], 0));
        public Task<bool> HasNomeComumConflictAsync(string nomeComum, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasNomeCientificoConflictAsync(string nomeCientifico, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRacaRepository(IEnumerable<Raca> items) : IRacaRepository
    {
        private readonly List<Raca> items = [.. items];
        public Task AddAsync(Raca raca, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RacaReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var raca = items.SingleOrDefault(item => item.Id == id);
            return Task.FromResult(raca is null
                ? null
                : new RacaReadModel(raca.Id, raca.EspecieId, raca.Nome, raca.Ativo, raca.CreatedAtUtc, raca.UpdatedAtUtc,
                    new RacaEspecieResumo(raca.EspecieId, string.Empty, true)));
        }
        public Task<Raca?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<RacaListPage> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new RacaListPage([], 0));
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsReferencedByAnimalAsync(Guid racaId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeVariedadeRepository(IEnumerable<Variedade> items) : IVariedadeRepository
    {
        private readonly List<Variedade> items = [.. items];
        public Task AddAsync(Variedade variedade, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<VariedadeReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var variedade = items.SingleOrDefault(item => item.Id == id);
            return Task.FromResult(variedade is null
                ? null
                : new VariedadeReadModel(variedade.Id, variedade.EspecieId, variedade.Nome, variedade.Ativo, variedade.CreatedAtUtc, variedade.UpdatedAtUtc,
                    new VariedadeEspecieResumo(variedade.EspecieId, string.Empty, true)));
        }
        public Task<Variedade?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<VariedadeListPage> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new VariedadeListPage([], 0));
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsReferencedByAnimalAsync(Guid variedadeId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
