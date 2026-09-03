using GenSW.Application.Breeds;
using GenSW.Application.Species;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class RacaServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_creates_a_breed_for_an_active_species()
    {
        var species = Especie.Criar("Cão", null, Now);
        var racaRepository = new FakeRacaRepository();
        var service = CreateService(racaRepository, species);

        var result = await service.CreateAsync(new CreateRacaCommand(species.Id, "  Pastor   Alemão  "));

        Assert.Equal(species.Id, result.EspecieId);
        Assert.Equal("Pastor Alemão", result.Nome);
        Assert.True(result.Ativo);
        Assert.Equal(Now, result.CreatedAtUtc);
        Assert.Equal("Cão", result.Especie.NomeComum);
        Assert.Single(racaRepository.Items);
    }

    [Fact]
    public async Task Create_rejects_a_missing_species()
    {
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<EspecieNotFoundException>(() =>
            CreateService(new FakeRacaRepository()).CreateAsync(new CreateRacaCommand(id, "Raça")));

        Assert.Equal(id, exception.EspecieId);
    }

    [Fact]
    public async Task Create_rejects_an_inactive_species()
    {
        var species = InactiveSpecies("Cão");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(new FakeRacaRepository(), species).CreateAsync(new CreateRacaCommand(species.Id, "Raça")));
    }

    [Fact]
    public async Task Create_rejects_a_normalized_name_conflict_in_the_same_species()
    {
        var species = Especie.Criar("Cão", null, Now);
        var repository = new FakeRacaRepository { NameConflict = true };

        await Assert.ThrowsAsync<RacaDuplicateException>(() =>
            CreateService(repository, species).CreateAsync(new CreateRacaCommand(species.Id, "  Pastor   Alemão  ")));

        Assert.Equal(species.Id, repository.LastSpeciesIdChecked);
        Assert.Equal("Pastor Alemão", repository.LastNameChecked);
        Assert.Null(repository.LastExcludedId);
    }

    [Fact]
    public async Task Create_allows_the_same_name_in_a_different_species()
    {
        var dog = Especie.Criar("Cão", null, Now);
        var cat = Especie.Criar("Gato", null, Now);
        var repository = new FakeRacaRepository { ConflictingSpeciesId = dog.Id };

        var result = await CreateService(repository, cat).CreateAsync(new CreateRacaCommand(cat.Id, "Siamês"));

        Assert.Equal(cat.Id, result.EspecieId);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_rejects_invalid_paging(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService(new FakeRacaRepository()).ListAsync(new RacaListQuery(page, pageSize)));
    }

    [Fact]
    public async Task List_normalizes_filters_and_maps_a_page()
    {
        var species = Especie.Criar("Cão", null, Now);
        var raca = Raca.Criar(species.Id, "Pastor", Now);
        var repository = new FakeRacaRepository
        {
            ListPage = new RacaListPage([ReadModel(raca, species)], 26),
        };

        var result = await CreateService(repository).ListAsync(new RacaListQuery(2, 25, "  Pastor  ", species.Id, true));

        Assert.Equal("Pastor", repository.LastListQuery!.Search);
        Assert.Equal(species.Id, repository.LastListQuery.EspecieId);
        Assert.True(repository.LastListQuery.Ativo);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("Cão", Assert.Single(result.Items).Especie.NomeComum);
    }

    [Fact]
    public async Task List_rejects_invalid_sort_without_calling_the_repository()
    {
        var repository = new FakeRacaRepository();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService(repository).ListAsync(new RacaListQuery(SortBy: (RacaSortField)99)));

        Assert.Equal(0, repository.ListCalls);
    }

    [Fact]
    public async Task Get_returns_null_when_the_breed_is_missing()
        => Assert.Null(await CreateService(new FakeRacaRepository()).GetByIdAsync(Guid.NewGuid()));

    [Fact]
    public async Task Update_allows_an_inactive_breed()
    {
        var species = Especie.Criar("Cão", null, Now);
        var breed = Raca.Criar(species.Id, "Pastor", Now.AddDays(-1));
        breed.Inativar(Now.AddHours(-1));
        var repository = new FakeRacaRepository();
        repository.Items.Add(breed);

        var result = await CreateService(repository, species).UpdateAsync(breed.Id, new UpdateRacaCommand(species.Id, "Pastor Alemão"));

        Assert.False(result.Ativo);
        Assert.Equal(breed.Id, repository.LastExcludedId);
    }

    [Fact]
    public async Task Update_rejects_a_new_inactive_species_but_preserves_an_existing_inactive_link()
    {
        var active = Especie.Criar("Cão", null, Now);
        var inactive = InactiveSpecies("Gato");
        var otherInactive = InactiveSpecies("Coelho");
        var breed = Raca.Criar(inactive.Id, "Siamês", Now);
        var repository = new FakeRacaRepository();
        repository.Items.Add(breed);
        repository.ReadModels[breed.Id] = ReadModel(breed, inactive);
        var service = CreateService(repository, active, inactive, otherInactive);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(breed.Id, new UpdateRacaCommand(otherInactive.Id, "Siamês")));
        var updated = await service.UpdateAsync(breed.Id, new UpdateRacaCommand(inactive.Id, "Novo nome"));

        Assert.False(updated.Especie.Ativo);
    }

    [Fact]
    public async Task Update_with_the_same_historical_inactive_species_does_not_read_the_species_repository()
    {
        var inactive = InactiveSpecies("Gato");
        var breed = Raca.Criar(inactive.Id, "Siamês", Now);
        var racaRepository = new FakeRacaRepository();
        racaRepository.Items.Add(breed);
        racaRepository.ReadModels[breed.Id] = ReadModel(breed, inactive);
        var especieRepository = new FakeEspecieRepository(inactive);
        var service = new RacaService(racaRepository, especieRepository, new FixedTimeProvider(Now));

        var result = await service.UpdateAsync(breed.Id, new UpdateRacaCommand(inactive.Id, "Siamês moderno"));

        Assert.Equal("Siamês moderno", result.Nome);
        Assert.Equal(0, especieRepository.ReadOnlyCalls);
    }

    [Fact]
    public async Task Update_moving_to_an_active_species_returns_its_current_summary()
    {
        var active = Especie.Criar("Cão", null, Now);
        var inactive = InactiveSpecies("Gato");
        var breed = Raca.Criar(inactive.Id, "Siamês", Now);
        var repository = new FakeRacaRepository();
        repository.Items.Add(breed);
        repository.ReadModels[breed.Id] = ReadModel(breed, inactive);

        var result = await CreateService(repository, active, inactive)
            .UpdateAsync(breed.Id, new UpdateRacaCommand(active.Id, "Siamês"));

        Assert.Equal(active.Id, result.EspecieId);
        Assert.Equal(active.Id, result.Especie.Id);
        Assert.Equal("Cão", result.Especie.NomeComum);
        Assert.True(result.Especie.Ativo);
    }

    [Fact]
    public async Task Update_allows_lifecycle_with_an_inactive_link_and_moving_it_to_an_active_species()
    {
        var active = Especie.Criar("Cão", null, Now);
        var inactive = InactiveSpecies("Gato");
        var breed = Raca.Criar(inactive.Id, "Siamês", Now);
        var repository = new FakeRacaRepository();
        repository.Items.Add(breed);
        repository.ReadModels[breed.Id] = ReadModel(breed, inactive);
        var service = CreateService(repository, active, inactive);

        Assert.False((await service.SetActiveAsync(breed.Id, false)).Ativo);
        Assert.True((await service.SetActiveAsync(breed.Id, true)).Ativo);
        var moved = await service.UpdateAsync(breed.Id, new UpdateRacaCommand(active.Id, "Siamês"));

        Assert.Equal(active.Id, breed.EspecieId);
        await Assert.ThrowsAsync<RacaNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid(), false));
    }

    private static RacaService CreateService(FakeRacaRepository racaRepository, params Especie[] species)
        => new(racaRepository, new FakeEspecieRepository(species), new FixedTimeProvider(Now));

    private static Especie InactiveSpecies(string name)
    {
        var species = Especie.Criar(name, null, Now.AddDays(-1));
        species.Inativar(Now.AddHours(-1));
        return species;
    }

    private static RacaReadModel ReadModel(Raca raca, Especie species) => new(
        raca.Id, raca.EspecieId, raca.Nome, raca.Ativo, raca.CreatedAtUtc, raca.UpdatedAtUtc,
        new RacaEspecieResumo(species.Id, species.NomeComum, species.Ativo));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider { public override DateTimeOffset GetUtcNow() => utcNow; }

    private sealed class FakeEspecieRepository(params Especie[] species) : IEspecieRepository
    {
        private readonly List<Especie> items = [.. species];
        public int ReadOnlyCalls { get; private set; }
        public Task AddAsync(Especie especie, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) { ReadOnlyCalls++; return Task.FromResult(items.SingleOrDefault(item => item.Id == id)); }
        public Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new EspecieListPage([], 0));
        public Task<bool> HasNomeComumConflictAsync(string nomeComum, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasNomeCientificoConflictAsync(string nomeCientifico, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRacaRepository : IRacaRepository
    {
        public List<Raca> Items { get; } = [];
        public Dictionary<Guid, RacaReadModel> ReadModels { get; } = [];
        public bool NameConflict { get; set; }
        public Guid? ConflictingSpeciesId { get; set; }
        public Guid? LastSpeciesIdChecked { get; private set; }
        public string? LastNameChecked { get; private set; }
        public Guid? LastExcludedId { get; private set; }
        public RacaListQuery? LastListQuery { get; private set; }
        public RacaListPage? ListPage { get; set; }
        public int ListCalls { get; private set; }
        public Task AddAsync(Raca raca, CancellationToken cancellationToken = default) { Items.Add(raca); return Task.CompletedTask; }
        public Task<RacaReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = Items.SingleOrDefault(item => item.Id == id);
            if (ReadModels.TryGetValue(id, out var model) && item is not null)
            {
                return Task.FromResult<RacaReadModel?>(model with
                {
                    EspecieId = item.EspecieId, Nome = item.Nome, Ativo = item.Ativo,
                    CreatedAtUtc = item.CreatedAtUtc, UpdatedAtUtc = item.UpdatedAtUtc,
                });
            }

            return Task.FromResult(item is null ? null : new RacaReadModel(item.Id, item.EspecieId, item.Nome, item.Ativo, item.CreatedAtUtc, item.UpdatedAtUtc, new RacaEspecieResumo(item.EspecieId, string.Empty, true)));
        }
        public Task<Raca?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<RacaListPage> ListAsync(RacaListQuery query, CancellationToken cancellationToken = default) { ListCalls++; LastListQuery = query; return Task.FromResult(ListPage ?? new RacaListPage([], 0)); }
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) { LastSpeciesIdChecked = especieId; LastNameChecked = nome; LastExcludedId = excludingId; return Task.FromResult(NameConflict || ConflictingSpeciesId == especieId); }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
