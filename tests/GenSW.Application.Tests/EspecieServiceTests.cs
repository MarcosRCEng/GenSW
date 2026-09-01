using GenSW.Application.Species;
using GenSW.Domain.Species;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class EspecieServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_creates_and_persists_a_species_with_the_clock_time()
    {
        var repository = new FakeEspecieRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreateEspecieCommand("  Cão doméstico  ", "  Canis   familiaris  "));

        Assert.Equal("Cão doméstico", result.NomeComum);
        Assert.Equal("Canis familiaris", result.NomeCientifico);
        Assert.True(result.Ativo);
        Assert.Equal(Now, result.CreatedAtUtc);
        Assert.Equal(Now, result.UpdatedAtUtc);
        Assert.Single(repository.Items);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Create_rejects_a_normalized_common_name_conflict()
    {
        var repository = new FakeEspecieRepository { CommonConflict = true };
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<EspecieDuplicateException>(() =>
            service.CreateAsync(new CreateEspecieCommand("  CÃO   DOMÉSTICO ", null)));

        Assert.Equal(EspecieDuplicateField.NomeComum, exception.Field);
        Assert.Equal("CÃO DOMÉSTICO", repository.LastCommonNameChecked);
        Assert.Null(repository.LastExcludedId);
    }

    [Fact]
    public async Task Create_rejects_a_scientific_name_conflict_and_skips_it_when_null()
    {
        var conflictingRepository = new FakeEspecieRepository { ScientificConflict = true };

        var exception = await Assert.ThrowsAsync<EspecieDuplicateException>(() =>
            CreateService(conflictingRepository).CreateAsync(new CreateEspecieCommand("Cão", "Canis familiaris")));

        Assert.Equal(EspecieDuplicateField.NomeCientifico, exception.Field);
        Assert.Equal("Canis familiaris", conflictingRepository.LastScientificNameChecked);

        var nullScientificRepository = new FakeEspecieRepository();
        await CreateService(nullScientificRepository).CreateAsync(new CreateEspecieCommand("Gato", null));
        Assert.Null(nullScientificRepository.LastScientificNameChecked);
    }

    [Fact]
    public async Task Get_returns_a_result_or_null()
    {
        var repository = new FakeEspecieRepository();
        var especie = Especie.Criar("Cão", null, Now);
        repository.Items.Add(especie);
        var service = CreateService(repository);

        Assert.Equal(especie.Id, (await service.GetByIdAsync(especie.Id))!.Id);
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_normalizes_search_and_maps_a_page()
    {
        var especie = Especie.Criar("Cão", null, Now);
        var repository = new FakeEspecieRepository { ListPage = new EspecieListPage([especie], 26) };
        var service = CreateService(repository);

        var result = await service.ListAsync(new EspecieListQuery(Page: 2, Search: "  Cão  "));

        Assert.Equal("Cão", repository.LastListQuery!.Search);
        Assert.Equal(2, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(26, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(especie.Id, Assert.Single(result.Items).Id);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_rejects_invalid_paging(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService(new FakeEspecieRepository()).ListAsync(new EspecieListQuery(page, pageSize)));
    }

    [Fact]
    public async Task List_rejects_an_invalid_sort_without_calling_the_repository()
    {
        var repository = new FakeEspecieRepository();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService(repository).ListAsync(new EspecieListQuery(SortBy: (EspecieSortField)99)));

        Assert.Equal(0, repository.ListCalls);
    }

    [Fact]
    public async Task Update_allows_an_inactive_species_and_excludes_its_id_from_conflict_checks()
    {
        var repository = new FakeEspecieRepository();
        var especie = Especie.Criar("Cão", null, Now.AddDays(-1));
        especie.Inativar(Now.AddHours(-1));
        repository.Items.Add(especie);
        var service = CreateService(repository);

        var result = await service.UpdateAsync(
            especie.Id,
            new UpdateEspecieCommand("Cão doméstico", "Canis familiaris"));

        Assert.False(result.Ativo);
        Assert.Equal(especie.Id, repository.LastExcludedId);
        Assert.Equal("Cão doméstico", result.NomeComum);
    }

    [Fact]
    public async Task Update_throws_when_the_species_is_missing()
    {
        var id = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<EspecieNotFoundException>(() =>
            CreateService(new FakeEspecieRepository()).UpdateAsync(id, new UpdateEspecieCommand("Cão", null)));

        Assert.Equal(id, exception.EspecieId);
    }

    [Fact]
    public async Task Set_active_inactivates_retrieves_inactive_and_reactivates()
    {
        var repository = new FakeEspecieRepository();
        var especie = Especie.Criar("Cão", null, Now.AddDays(-1));
        repository.Items.Add(especie);
        var service = CreateService(repository);

        var inactive = await service.SetActiveAsync(especie.Id, false);
        Assert.False(inactive.Ativo);
        Assert.False((await service.GetByIdAsync(especie.Id))!.Ativo);

        var active = await service.SetActiveAsync(especie.Id, true);
        Assert.True(active.Ativo);
        await Assert.ThrowsAsync<EspecieNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid(), false));
    }

    private static EspecieService CreateService(FakeEspecieRepository repository) => new(repository, new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeEspecieRepository : IEspecieRepository
    {
        public List<Especie> Items { get; } = [];
        public bool CommonConflict { get; set; }
        public bool ScientificConflict { get; set; }
        public string? LastCommonNameChecked { get; private set; }
        public string? LastScientificNameChecked { get; private set; }
        public Guid? LastExcludedId { get; private set; }
        public EspecieListQuery? LastListQuery { get; private set; }
        public EspecieListPage? ListPage { get; set; }
        public int ListCalls { get; private set; }
        public int SaveChangesCalls { get; private set; }

        public Task AddAsync(Especie especie, CancellationToken cancellationToken = default) { Items.Add(especie); return Task.CompletedTask; }
        public Task<Especie?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<Especie?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<EspecieListPage> ListAsync(EspecieListQuery query, CancellationToken cancellationToken = default) { ListCalls++; LastListQuery = query; return Task.FromResult(ListPage ?? new EspecieListPage([], 0)); }
        public Task<bool> HasNomeComumConflictAsync(string nomeComum, Guid? excludingId = null, CancellationToken cancellationToken = default) { LastCommonNameChecked = nomeComum; LastExcludedId = excludingId; return Task.FromResult(CommonConflict); }
        public Task<bool> HasNomeCientificoConflictAsync(string nomeCientifico, Guid? excludingId = null, CancellationToken cancellationToken = default) { LastScientificNameChecked = nomeCientifico; LastExcludedId = excludingId; return Task.FromResult(ScientificConflict); }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCalls++; return Task.CompletedTask; }
    }
}
