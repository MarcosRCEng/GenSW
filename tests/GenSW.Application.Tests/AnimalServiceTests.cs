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

public sealed class AnimalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 8);

    [Fact]
    public async Task Create_with_a_manual_code_validates_then_persists_once_without_using_automatic_allocation()
    {
        var species = ActiveSpecies();
        var repository = new FakeAnimalRepository();
        var allocator = new FakeAllocator(["AN-000001"]);
        var service = CreateService(repository, allocator, [species]);

        var result = await service.CreateAsync(Command(codigoInterno: "  Manual   01  ", especieId: species.Id));

        Assert.Equal("Manual 01", result.CodigoInterno);
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal("Manual 01", repository.LastCodigoInternoChecked);
        Assert.Null(repository.LastExcludedId);
        Assert.Equal(0, allocator.BeginCount);
    }

    [Fact]
    public async Task Create_with_null_code_uses_automatic_flow_and_never_prechecks_a_manual_code()
    {
        var species = ActiveSpecies();
        var repository = new FakeAnimalRepository();
        var allocator = new FakeAllocator(["AN-000001"]);
        var service = CreateService(repository, allocator, [species]);

        var result = await service.CreateAsync(Command(codigoInterno: null, especieId: species.Id));

        Assert.Equal("AN-000001", result.CodigoInterno);
        Assert.Equal(1, allocator.BeginCount);
        Assert.Equal(1, allocator.CommitCount);
        Assert.Null(repository.LastCodigoInternoChecked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_an_explicit_empty_or_whitespace_code_without_automatic_retry(string codigoInterno)
    {
        var species = ActiveSpecies();
        var allocator = new FakeAllocator(["AN-000001"]);
        var service = CreateService(new FakeAnimalRepository(), allocator, [species]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Command(codigoInterno, species.Id)));

        Assert.Equal(0, allocator.BeginCount);
    }

    [Theory]
    [InlineData("species")]
    [InlineData("breed")]
    [InlineData("variety")]
    public async Task Create_rejects_empty_classification_ids_as_invalid_input_before_repository_lookup(string emptyField)
    {
        var species = ActiveSpecies();
        var command = emptyField switch
        {
            "species" => Command("AN-000001", Guid.Empty),
            "breed" => Command("AN-000001", species.Id, Guid.Empty),
            _ => Command("AN-000001", species.Id, null, Guid.Empty),
        };
        FakeEspecieRepository.Reset();
        FakeRacaRepository.Reset();
        FakeVariedadeRepository.Reset();
        var service = CreateService(new FakeAnimalRepository(), new FakeAllocator(["AN-000001"]), [species]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(command));

        Assert.Equal(0, FakeEspecieRepository.ReadOnlyCalls);
        Assert.Equal(0, FakeRacaRepository.ReadOnlyCalls);
        Assert.Equal(0, FakeVariedadeRepository.ReadOnlyCalls);
    }

    [Fact]
    public async Task Create_rejects_a_manual_precheck_conflict_with_explicit_provenance_and_no_retry()
    {
        var species = ActiveSpecies();
        var repository = new FakeAnimalRepository { CodigoConflict = true };
        var allocator = new FakeAllocator(["AN-000001"]);
        var service = CreateService(repository, allocator, [species]);

        var exception = await Assert.ThrowsAsync<AnimalDuplicateException>(() =>
            service.CreateAsync(Command("MANUAL", species.Id)));

        Assert.Equal("MANUAL", exception.CodigoInterno);
        Assert.Equal(AnimalDuplicateConflictSource.PreCheck, exception.ConflictSource);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, allocator.BeginCount);
    }

    [Fact]
    public async Task Create_propagates_a_persisted_manual_race_without_automatic_retry()
    {
        var species = ActiveSpecies();
        var persistedRace = new AnimalDuplicateException("MANUAL", AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint);
        var repository = new FakeAnimalRepository { SaveFailure = persistedRace };
        var allocator = new FakeAllocator(["AN-000001"]);
        var service = CreateService(repository, allocator, [species]);

        var exception = await Assert.ThrowsAsync<AnimalDuplicateException>(() =>
            service.CreateAsync(Command("MANUAL", species.Id)));

        Assert.Same(persistedRace, exception);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(0, allocator.BeginCount);
    }

    [Fact]
    public async Task Create_validates_independent_active_classifications_and_rejects_inactive_or_incompatible_destinations()
    {
        var dog = ActiveSpecies();
        var cat = ActiveSpecies();
        var breed = Raca.Criar(dog.Id, "Pastor", Now);
        var variety = Variedade.Criar(dog.Id, "Curto", Now);
        var inactiveBreed = Raca.Criar(dog.Id, "Antiga", Now);
        inactiveBreed.Inativar(Now);
        var catVariety = Variedade.Criar(cat.Id, "Longo", Now);
        var service = CreateService(new FakeAnimalRepository(), new FakeAllocator(["AN-000001"]), [dog, cat], [breed, inactiveBreed], [variety, catVariety]);

        var valid = await service.CreateAsync(Command("AN-01", dog.Id, breed.Id, variety.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Command("AN-02", dog.Id, inactiveBreed.Id, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Command("AN-03", dog.Id, null, catVariety.Id)));

        Assert.Equal(breed.Id, valid.RacaId);
        Assert.Equal(variety.Id, valid.VariedadeId);
    }

    [Fact]
    public async Task Get_returns_the_read_model_including_an_inactive_animal_or_null_when_missing()
    {
        var species = ActiveSpecies();
        var animal = Animal.Criar("AN-000001", "Bela", species.Id, null, null, SexoAnimal.Femea, null, EscopoAnimal.Operacional, Today, Now);
        animal.Inativar(Now.AddMinutes(1));
        var repository = new FakeAnimalRepository();
        repository.Items.Add(animal);
        var service = CreateService(repository, new FakeAllocator(["AN-000002"]), [species]);

        var found = await service.GetByIdAsync(animal.Id);

        Assert.False(found!.Ativo);
        Assert.Equal(animal.Id, found.Id);
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_validates_every_query_boundary_normalizes_search_and_maps_the_repository_page()
    {
        var species = ActiveSpecies();
        var animal = Animal.Criar("AN-000001", "Bela", species.Id, null, null, SexoAnimal.Femea, null, EscopoAnimal.Operacional, Today, Now);
        var repository = new FakeAnimalRepository { ListPage = new AnimalListPage([ReadModel(animal, species)], 26) };
        var service = CreateService(repository, new FakeAllocator(["AN-000002"]), [species]);

        var result = await service.ListAsync(new AnimalListQuery(2, 25, "  Bela  ", species.Id, null, null, SexoAnimal.Femea, EscopoAnimal.Operacional, true, AnimalSortField.Nome, true));

        Assert.Equal("Bela", repository.LastListQuery!.Search);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(animal.Id, Assert.Single(result.Items).Id);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(new AnimalListQuery(0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(new AnimalListQuery(PageSize: 101)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(new AnimalListQuery(Sexo: (SexoAnimal)99)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(new AnimalListQuery(Escopo: (EscopoAnimal)99)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(new AnimalListQuery(SortBy: (AnimalSortField)99)));
    }

    [Fact]
    public async Task Update_preserves_historical_inactive_links_and_allows_a_new_active_matching_classification()
    {
        var species = ActiveSpecies();
        var replacementBreed = Raca.Criar(species.Id, "Pastor", Now);
        var replacementVariety = Variedade.Criar(species.Id, "Curto", Now);
        var animal = Animal.Criar("AN-000001", "Bela", species.Id, null, null, SexoAnimal.Femea, null, EscopoAnimal.Operacional, Today, Now);
        species.Inativar(Now);
        animal.Inativar(Now);
        var repository = new FakeAnimalRepository();
        repository.Items.Add(animal);
        var service = CreateService(repository, new FakeAllocator(["AN-000002"]), [species], [replacementBreed], [replacementVariety]);

        var result = await service.UpdateAsync(animal.Id, new UpdateAnimalCommand("AN-000010", "Nova Bela", species.Id, replacementBreed.Id, replacementVariety.Id, SexoAnimal.Macho, new DateOnly(2020, 1, 1), EscopoAnimal.Referencia));

        Assert.False(result.Ativo);
        Assert.Equal("AN-000010", result.CodigoInterno);
        Assert.Equal(SexoAnimal.Macho, result.Sexo);
        Assert.Equal(EscopoAnimal.Referencia, result.Escopo);
        Assert.Equal(replacementBreed.Id, result.RacaId);
        Assert.Equal(replacementVariety.Id, result.VariedadeId);
        Assert.Equal(animal.Id, repository.LastExcludedId);
    }

    [Fact]
    public async Task Update_rejects_policy_b_incompatible_snapshot_and_duplicate_code()
    {
        var dog = ActiveSpecies();
        var cat = ActiveSpecies();
        var dogBreed = Raca.Criar(dog.Id, "Pastor", Now);
        var animal = Animal.Criar("AN-000001", null, dog.Id, dogBreed.Id, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, Today, Now);
        var repository = new FakeAnimalRepository();
        repository.Items.Add(animal);
        var service = CreateService(repository, new FakeAllocator(["AN-000002"]), [dog, cat], [dogBreed]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(animal.Id, new UpdateAnimalCommand("AN-000002", null, cat.Id, dogBreed.Id, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional)));
        repository.CodigoConflict = true;
        var exception = await Assert.ThrowsAsync<AnimalDuplicateException>(() => service.UpdateAsync(animal.Id, new UpdateAnimalCommand("AN-000002", null, dog.Id, dogBreed.Id, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional)));

        Assert.Equal(AnimalDuplicateConflictSource.PreCheck, exception.ConflictSource);
        Assert.Equal(animal.Id, repository.LastExcludedId);
    }

    [Fact]
    public async Task Update_and_lifecycle_use_tracked_animals_and_keep_lifecycle_idempotent()
    {
        var species = ActiveSpecies();
        var animal = Animal.Criar("AN-000001", null, species.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, Today, Now);
        var repository = new FakeAnimalRepository();
        repository.Items.Add(animal);
        var service = CreateService(repository, new FakeAllocator(["AN-000002"]), [species]);

        var inactive = await service.SetActiveAsync(animal.Id, false);
        var afterFirstChange = animal.UpdatedAtUtc;
        var stillInactive = await service.SetActiveAsync(animal.Id, false);
        var active = await service.SetActiveAsync(animal.Id, true);

        Assert.False(inactive.Ativo);
        Assert.False(stillInactive.Ativo);
        Assert.Equal(afterFirstChange, stillInactive.UpdatedAtUtc);
        Assert.True(active.Ativo);
        await Assert.ThrowsAsync<AnimalNotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), new UpdateAnimalCommand("AN-2", null, species.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional)));
        await Assert.ThrowsAsync<AnimalNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid(), true));
    }

    private static AnimalService CreateService(FakeAnimalRepository animals, FakeAllocator allocator, IEnumerable<Especie> species, IEnumerable<Raca>? breeds = null, IEnumerable<Variedade>? varieties = null)
    {
        var validator = new AnimalClassificationValidator(
            new FakeEspecieRepository(species),
            new FakeRacaRepository(breeds ?? []),
            new FakeVariedadeRepository(varieties ?? []));
        return new AnimalService(animals, validator, new AnimalAutomaticCreator(animals, allocator, new FixedTimeProvider(Now)), new FixedTimeProvider(Now));
    }

    private static CreateAnimalCommand Command(string? codigoInterno, Guid especieId, Guid? racaId = null, Guid? variedadeId = null)
        => new(codigoInterno, "Bela", especieId, racaId, variedadeId, SexoAnimal.Femea, null, EscopoAnimal.Operacional);

    private static Especie ActiveSpecies() => Especie.Criar("Cão", null, Now);
    private static AnimalReadModel ReadModel(Animal animal, Especie species) => new(animal.Id, animal.CodigoInterno, animal.Nome, animal.EspecieId, animal.RacaId, animal.VariedadeId, animal.Sexo, animal.DataNascimento, animal.Escopo, animal.Ativo, animal.CreatedAtUtc, animal.UpdatedAtUtc, new AnimalEspecieResumo(species.Id, species.NomeComum, species.Ativo), null, null);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class FakeAnimalRepository : IAnimalRepository
    {
        public List<Animal> Items { get; } = [];
        public bool CodigoConflict { get; set; }
        public Exception? SaveFailure { get; set; }
        public int AddCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public string? LastCodigoInternoChecked { get; private set; }
        public Guid? LastExcludedId { get; private set; }
        public AnimalListQuery? LastListQuery { get; private set; }
        public AnimalListPage? ListPage { get; set; }
        public Task AddAsync(Animal animal, CancellationToken cancellationToken = default) { AddCalls++; Items.Add(animal); return Task.CompletedTask; }
        public Task<AnimalReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var animal = Items.SingleOrDefault(item => item.Id == id);
            return Task.FromResult(animal is null ? null : ReadModel(animal, new AnimalEspecieResumo(animal.EspecieId, "Espécie", true)));
        }
        public Task<Animal?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<AnimalListPage> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default) { LastListQuery = query; return Task.FromResult(ListPage ?? new AnimalListPage([], 0)); }
        public Task<bool> HasCodigoInternoConflictAsync(string codigoInterno, Guid? excludingId = null, CancellationToken cancellationToken = default) { LastCodigoInternoChecked = codigoInterno; LastExcludedId = excludingId; return Task.FromResult(CodigoConflict); }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCalls++; return SaveFailure is null ? Task.CompletedTask : Task.FromException(SaveFailure); }
        private static AnimalReadModel ReadModel(Animal animal, AnimalEspecieResumo species) => new(animal.Id, animal.CodigoInterno, animal.Nome, animal.EspecieId, animal.RacaId, animal.VariedadeId, animal.Sexo, animal.DataNascimento, animal.Escopo, animal.Ativo, animal.CreatedAtUtc, animal.UpdatedAtUtc, species, animal.RacaId is null ? null : new AnimalRacaResumo(animal.RacaId.Value, "Raça", true), animal.VariedadeId is null ? null : new AnimalVariedadeResumo(animal.VariedadeId.Value, "Variedade", true));
    }

    private sealed class FakeAllocator(IEnumerable<string> candidates) : IAnimalCodeAllocator
    {
        private readonly Queue<string> candidates = new(candidates);
        public int BeginCount { get; private set; }
        public int CommitCount { get; private set; }
        public Task<IAnimalAutomaticCodeAttempt> BeginAttemptAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAnimalAutomaticCodeAttempt>(new Attempt(this, candidates.Dequeue()));
        private sealed class Attempt(FakeAllocator owner, string candidate) : IAnimalAutomaticCodeAttempt
        {
            public Task<string> AllocateNextCodigoInternoAsync(CancellationToken cancellationToken = default) { owner.BeginCount++; return Task.FromResult(candidate); }
            public Task CommitAsync(CancellationToken cancellationToken = default) { owner.CommitCount++; return Task.CompletedTask; }
            public Task RollbackAndDetachAsync(Animal failedAnimal, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeEspecieRepository(IEnumerable<Especie> items) : IEspecieRepository
    {
        private readonly List<Especie> items = [.. items];
        public static int ReadOnlyCalls { get; private set; }
        public static void Reset() => ReadOnlyCalls = 0;
        public Task AddAsync(Especie especie, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) { ReadOnlyCalls++; return Task.FromResult(items.SingleOrDefault(item => item.Id == id)); }
        public Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new EspecieListPage([], 0));
        public Task<bool> HasNomeComumConflictAsync(string nomeComum, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasNomeCientificoConflictAsync(string nomeCientifico, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRacaRepository(IEnumerable<Raca> items) : IRacaRepository
    {
        private readonly List<Raca> items = [.. items];
        public static int ReadOnlyCalls { get; private set; }
        public static void Reset() => ReadOnlyCalls = 0;
        public Task AddAsync(Raca raca, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RacaReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) { ReadOnlyCalls++; var item = items.SingleOrDefault(item => item.Id == id); return Task.FromResult(item is null ? null : new RacaReadModel(item.Id, item.EspecieId, item.Nome, item.Ativo, item.CreatedAtUtc, item.UpdatedAtUtc, new RacaEspecieResumo(item.EspecieId, "Espécie", true))); }
        public Task<Raca?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<RacaListPage> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new RacaListPage([], 0));
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsReferencedByAnimalAsync(Guid racaId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeVariedadeRepository(IEnumerable<Variedade> items) : IVariedadeRepository
    {
        private readonly List<Variedade> items = [.. items];
        public static int ReadOnlyCalls { get; private set; }
        public static void Reset() => ReadOnlyCalls = 0;
        public Task AddAsync(Variedade variedade, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<VariedadeReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) { ReadOnlyCalls++; var item = items.SingleOrDefault(item => item.Id == id); return Task.FromResult(item is null ? null : new VariedadeReadModel(item.Id, item.EspecieId, item.Nome, item.Ativo, item.CreatedAtUtc, item.UpdatedAtUtc, new VariedadeEspecieResumo(item.EspecieId, "Espécie", true))); }
        public Task<Variedade?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<VariedadeListPage> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new VariedadeListPage([], 0));
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsReferencedByAnimalAsync(Guid variedadeId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
