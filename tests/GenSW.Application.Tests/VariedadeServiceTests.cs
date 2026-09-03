using GenSW.Application.Species;
using GenSW.Application.Varieties;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class VariedadeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_creates_a_variety_for_an_active_species()
    {
        var especie = Especie.Criar("Cão", null, Now);
        var repository = new FakeVariedadeRepository();

        var result = await CreateService(repository, especie).CreateAsync(new CreateVariedadeCommand(especie.Id, "  Pelo   curto  "));

        Assert.Equal(especie.Id, result.EspecieId);
        Assert.Equal("Pelo curto", result.Nome);
        Assert.True(result.Ativo);
        Assert.Equal("Cão", result.Especie.NomeComum);
        Assert.Single(repository.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_an_empty_name(string nome)
    {
        var especie = Especie.Criar("Cão", null, Now);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(new FakeVariedadeRepository(), especie).CreateAsync(new CreateVariedadeCommand(especie.Id, nome)));
    }

    [Fact]
    public async Task Create_rejects_a_name_longer_than_200_characters()
    {
        var especie = Especie.Criar("Cão", null, Now);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(new FakeVariedadeRepository(), especie).CreateAsync(new CreateVariedadeCommand(especie.Id, new string('a', 201))));
    }

    [Fact]
    public async Task Create_rejects_a_missing_species()
    {
        var id = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<EspecieNotFoundException>(() => CreateService(new FakeVariedadeRepository()).CreateAsync(new CreateVariedadeCommand(id, "Variedade")));
        Assert.Equal(id, exception.EspecieId);
    }

    [Fact]
    public async Task Create_rejects_an_inactive_species()
    {
        var especie = InactiveSpecies("Cão");
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(new FakeVariedadeRepository(), especie).CreateAsync(new CreateVariedadeCommand(especie.Id, "Variedade")));
    }

    [Fact]
    public async Task Create_rejects_a_normalized_name_conflict_in_the_same_species()
    {
        var especie = Especie.Criar("Cão", null, Now);
        var repository = new FakeVariedadeRepository { NameConflict = true };
        await Assert.ThrowsAsync<VariedadeDuplicateException>(() => CreateService(repository, especie).CreateAsync(new CreateVariedadeCommand(especie.Id, "  Pelo   curto  ")));
        Assert.Equal(especie.Id, repository.LastSpeciesIdChecked);
        Assert.Equal("Pelo curto", repository.LastNameChecked);
    }

    [Fact]
    public async Task Create_allows_the_same_name_in_a_different_species()
    {
        var dog = Especie.Criar("Cão", null, Now);
        var cat = Especie.Criar("Gato", null, Now);
        var result = await CreateService(new FakeVariedadeRepository { ConflictingSpeciesId = dog.Id }, cat).CreateAsync(new CreateVariedadeCommand(cat.Id, "Curto"));
        Assert.Equal(cat.Id, result.EspecieId);
    }

    [Fact]
    public async Task Get_returns_null_when_the_variety_is_missing() => Assert.Null(await CreateService(new FakeVariedadeRepository()).GetByIdAsync(Guid.NewGuid()));

    [Fact]
    public async Task List_normalizes_filters_and_maps_a_page()
    {
        var especie = Especie.Criar("Cão", null, Now);
        var variedade = Variedade.Criar(especie.Id, "Curto", Now);
        var repository = new FakeVariedadeRepository { ListPage = new VariedadeListPage([ReadModel(variedade, especie)], 26) };
        var result = await CreateService(repository).ListAsync(new VariedadeListQuery(2, 25, "  Curto  ", especie.Id, true));
        Assert.Equal("Curto", repository.LastListQuery!.Search);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("Cão", Assert.Single(result.Items).Especie.NomeComum);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_rejects_invalid_paging(int page, int pageSize) =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateService(new FakeVariedadeRepository()).ListAsync(new VariedadeListQuery(page, pageSize)));

    [Fact]
    public async Task List_rejects_invalid_sort_without_calling_the_repository()
    {
        var repository = new FakeVariedadeRepository();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateService(repository).ListAsync(new VariedadeListQuery(SortBy: (VariedadeSortField)99)));
        Assert.Equal(0, repository.ListCalls);
    }

    [Fact]
    public async Task Update_allows_an_inactive_variety()
    {
        var especie = Especie.Criar("Cão", null, Now);
        var variedade = Variedade.Criar(especie.Id, "Curto", Now); variedade.Inativar(Now);
        var repository = new FakeVariedadeRepository(); repository.Items.Add(variedade); repository.ReadModels[variedade.Id] = ReadModel(variedade, especie);
        var result = await CreateService(repository, especie).UpdateAsync(variedade.Id, new UpdateVariedadeCommand(especie.Id, "Longo"));
        Assert.False(result.Ativo);
    }

    [Fact]
    public async Task Update_keeps_a_historical_inactive_species_without_reading_it_and_allows_name_and_lifecycle_changes()
    {
        var especie = InactiveSpecies("Gato");
        var variedade = Variedade.Criar(especie.Id, "Curto", Now);
        var repository = new FakeVariedadeRepository(); repository.Items.Add(variedade); repository.ReadModels[variedade.Id] = ReadModel(variedade, especie);
        var especieRepository = new FakeEspecieRepository(especie);
        var service = new VariedadeService(repository, especieRepository, new FixedTimeProvider(Now));
        var updated = await service.UpdateAsync(variedade.Id, new UpdateVariedadeCommand(especie.Id, "Longo"));
        Assert.Equal("Longo", updated.Nome); Assert.Equal(0, especieRepository.ReadOnlyCalls);
        Assert.False((await service.SetActiveAsync(variedade.Id, false)).Ativo);
        Assert.True((await service.SetActiveAsync(variedade.Id, true)).Ativo);
    }

    [Fact]
    public async Task Update_rejects_a_new_inactive_species_and_can_move_to_an_active_species_with_its_summary()
    {
        var inactive = InactiveSpecies("Gato"); var otherInactive = InactiveSpecies("Coelho"); var active = Especie.Criar("Cão", null, Now);
        var variedade = Variedade.Criar(inactive.Id, "Curto", Now);
        var repository = new FakeVariedadeRepository(); repository.Items.Add(variedade); repository.ReadModels[variedade.Id] = ReadModel(variedade, inactive);
        var service = CreateService(repository, inactive, otherInactive, active);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(variedade.Id, new UpdateVariedadeCommand(otherInactive.Id, "Curto")));
        var moved = await service.UpdateAsync(variedade.Id, new UpdateVariedadeCommand(active.Id, "Curto"));
        Assert.Equal(active.Id, moved.EspecieId); Assert.Equal("Cão", moved.Especie.NomeComum); Assert.True(moved.Especie.Ativo);
        await Assert.ThrowsAsync<VariedadeNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid(), false));
    }

    private static VariedadeService CreateService(FakeVariedadeRepository repository, params Especie[] species) => new(repository, new FakeEspecieRepository(species), new FixedTimeProvider(Now));
    private static Especie InactiveSpecies(string name) { var especie = Especie.Criar(name, null, Now); especie.Inativar(Now); return especie; }
    private static VariedadeReadModel ReadModel(Variedade variedade, Especie especie) => new(variedade.Id, variedade.EspecieId, variedade.Nome, variedade.Ativo, variedade.CreatedAtUtc, variedade.UpdatedAtUtc, new VariedadeEspecieResumo(especie.Id, especie.NomeComum, especie.Ativo));
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class FakeEspecieRepository(params Especie[] species) : IEspecieRepository
    {
        private readonly List<Especie> items = [.. species]; public int ReadOnlyCalls { get; private set; }
        public Task AddAsync(Especie especie, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) { ReadOnlyCalls++; return Task.FromResult(items.SingleOrDefault(x => x.Id == id)); }
        public Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(x => x.Id == id));
        public Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new EspecieListPage([], 0));
        public Task<bool> HasNomeComumConflictAsync(string nomeComum, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasNomeCientificoConflictAsync(string nomeCientifico, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class FakeVariedadeRepository : IVariedadeRepository
    {
        public List<Variedade> Items { get; } = []; public Dictionary<Guid, VariedadeReadModel> ReadModels { get; } = []; public bool NameConflict { get; set; } public Guid? ConflictingSpeciesId { get; set; }
        public Guid? LastSpeciesIdChecked { get; private set; } public string? LastNameChecked { get; private set; } public VariedadeListQuery? LastListQuery { get; private set; } public VariedadeListPage? ListPage { get; set; } public int ListCalls { get; private set; }
        public Task AddAsync(Variedade variedade, CancellationToken cancellationToken = default) { Items.Add(variedade); return Task.CompletedTask; }
        public Task<VariedadeReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) { var item = Items.SingleOrDefault(x => x.Id == id); if (item is null) return Task.FromResult<VariedadeReadModel?>(null); return Task.FromResult<VariedadeReadModel?>(ReadModels.TryGetValue(id, out var model) ? model with { EspecieId = item.EspecieId, Nome = item.Nome, Ativo = item.Ativo, CreatedAtUtc = item.CreatedAtUtc, UpdatedAtUtc = item.UpdatedAtUtc } : new VariedadeReadModel(item.Id, item.EspecieId, item.Nome, item.Ativo, item.CreatedAtUtc, item.UpdatedAtUtc, new VariedadeEspecieResumo(item.EspecieId, string.Empty, true))); }
        public Task<Variedade?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
        public Task<VariedadeListPage> ListAsync(VariedadeListQuery query, CancellationToken cancellationToken = default) { ListCalls++; LastListQuery = query; return Task.FromResult(ListPage ?? new VariedadeListPage([], 0)); }
        public Task<bool> HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId = null, CancellationToken cancellationToken = default) { LastSpeciesIdChecked = especieId; LastNameChecked = nome; return Task.FromResult(NameConflict || ConflictingSpeciesId == especieId); }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
