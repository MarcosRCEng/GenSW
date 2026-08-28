using GenSW.Application.People;
using GenSW.Domain.People;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class PessoaServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_creates_and_persists_a_physical_person_with_the_clock_time()
    {
        var repository = new FakePessoaRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreatePessoaCommand(TipoPessoa.Fisica, "  Ana Silva  ", null));

        Assert.Equal(TipoPessoa.Fisica, result.TipoPessoa);
        Assert.Equal("Ana Silva", result.Nome);
        Assert.True(result.Ativo);
        Assert.Equal(Now, result.CreatedAtUtc);
        Assert.Equal(Now, result.UpdatedAtUtc);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task Create_creates_a_legal_person_and_keeps_domain_invariants()
    {
        var service = CreateService(new FakePessoaRepository());

        var result = await service.CreateAsync(new CreatePessoaCommand(TipoPessoa.Juridica, "GenSW Ltda", "GenSW"));
        Assert.Equal("GenSW", result.NomeFantasia);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            new CreatePessoaCommand(TipoPessoa.Fisica, "Ana", "Fantasia")));
    }

    [Fact]
    public async Task Get_returns_a_result_or_null()
    {
        var repository = new FakePessoaRepository();
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, "Ana", null, Now);
        repository.Items.Add(pessoa);
        var service = CreateService(repository);

        Assert.Equal(pessoa.Id, (await service.GetByIdAsync(pessoa.Id))!.Id);
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Update_changes_active_person_and_rejects_missing_or_inactive_people()
    {
        var repository = new FakePessoaRepository();
        var active = Pessoa.Criar(TipoPessoa.Juridica, "Old", "Old", Now.AddDays(-1));
        var inactive = Pessoa.Criar(TipoPessoa.Fisica, "Inactive", null, Now.AddDays(-1));
        inactive.Inativar(Now.AddHours(-1));
        repository.Items.AddRange([active, inactive]);
        var service = CreateService(repository);

        var updated = await service.UpdateAsync(active.Id, new UpdatePessoaCommand("New", "New trade"));
        Assert.Equal("New", updated.Nome);
        Assert.Equal(Now, updated.UpdatedAtUtc);
        await Assert.ThrowsAsync<PessoaNotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), new UpdatePessoaCommand("New", null)));
        await Assert.ThrowsAsync<PessoaInactiveException>(() => service.UpdateAsync(inactive.Id, new UpdatePessoaCommand("Never", null)));
        Assert.False(inactive.Ativo);
    }

    [Fact]
    public async Task Set_active_is_idempotent_and_never_touches_a_user()
    {
        var repository = new FakePessoaRepository();
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, "Ana", null, Now.AddDays(-1));
        repository.Items.Add(pessoa);
        var service = CreateService(repository);

        await service.SetActiveAsync(pessoa.Id, false);
        var inactiveTime = pessoa.UpdatedAtUtc;
        await service.SetActiveAsync(pessoa.Id, false);
        Assert.False(pessoa.Ativo);
        Assert.Equal(inactiveTime, pessoa.UpdatedAtUtc);
        await service.SetActiveAsync(pessoa.Id, true);
        Assert.True(pessoa.Ativo);
        await Assert.ThrowsAsync<PessoaNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid(), false));
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_rejects_invalid_paging(int page, int pageSize)
    {
        var service = CreateService(new FakePessoaRepository());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(new PessoaListQuery(page, pageSize)));
    }

    [Fact]
    public async Task List_normalizes_search_and_returns_default_page_size()
    {
        var repository = new FakePessoaRepository { ListPage = new PessoaListPage([], 0) };
        var service = CreateService(repository);

        var result = await service.ListAsync(new PessoaListQuery(Search: "  Ana  "));
        Assert.Equal(25, result.PageSize);
        Assert.Equal("Ana", repository.LastListQuery!.Search);
        await service.ListAsync(new PessoaListQuery(Search: "   "));
        Assert.Null(repository.LastListQuery!.Search);
    }

    [Fact]
    public async Task List_rejects_invalid_person_type_without_calling_the_repository()
    {
        var repository = new FakePessoaRepository();
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(
            new PessoaListQuery(TipoPessoa: (TipoPessoa)99)));

        Assert.Equal(0, repository.ListCalls);
    }

    private static PessoaService CreateService(FakePessoaRepository repository) => new(repository, new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakePessoaRepository : IPessoaRepository
    {
        public List<Pessoa> Items { get; } = [];
        public PessoaListPage? ListPage { get; set; }
        public PessoaListQuery? LastListQuery { get; private set; }
        public int ListCalls { get; private set; }
        public int SaveChangesCalls { get; private set; }

        public Task AddAsync(Pessoa pessoa, CancellationToken cancellationToken = default) { Items.Add(pessoa); return Task.CompletedTask; }
        public Task<Pessoa?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<Pessoa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<PessoaListPage> ListAsync(PessoaListQuery query, CancellationToken cancellationToken = default) { ListCalls++; LastListQuery = query; return Task.FromResult(ListPage ?? new PessoaListPage([], 0)); }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCalls++; return Task.CompletedTask; }
    }
}
