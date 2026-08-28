using GenSW.Application.People;
using GenSW.Domain.People;
using GenSW.Infrastructure.People;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class PessoaRepositoryTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql postgreSql = null!;

    public async Task InitializeAsync() => postgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
    public Task DisposeAsync() => postgreSql.DisposeAsync().AsTask();

    [Fact]
    public async Task Add_and_get_persist_people_and_missing_get_returns_null()
    {
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, "Ana", null, DateTimeOffset.UtcNow);
        await using (var context = CreateContext())
        {
            var repository = new PessoaRepository(context);
            await context.Database.MigrateAsync();
            await repository.AddAsync(pessoa);
            await repository.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var readRepository = new PessoaRepository(readContext);
        Assert.Equal(pessoa.Id, (await readRepository.GetByIdReadOnlyAsync(pessoa.Id))!.Id);
        Assert.Null(await readRepository.GetByIdReadOnlyAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_applies_filters_search_sorting_and_database_paging()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var people = new[]
        {
            Pessoa.Criar(TipoPessoa.Juridica, "Beta", "Alpha Trade", timestamp.AddDays(1)),
            Pessoa.Criar(TipoPessoa.Fisica, "Alpha", null, timestamp),
            Pessoa.Criar(TipoPessoa.Juridica, "Gamma", "Wildcard %_", timestamp.AddDays(2)),
        };
        people[2].Inativar(timestamp.AddDays(3));
        context.Pessoas.AddRange(people);
        await context.SaveChangesAsync();
        var repository = new PessoaRepository(context);

        var byName = await repository.ListAsync(new PessoaListQuery(Page: 1, PageSize: 1, SortBy: PessoaSortField.Nome));
        Assert.Equal(3, byName.TotalItems);
        Assert.Single(byName.Items);
        Assert.Equal("Alpha", byName.Items[0].Nome);
        Assert.Empty((await repository.ListAsync(new PessoaListQuery(Page: 5, PageSize: 25))).Items);
        Assert.Single((await repository.ListAsync(new PessoaListQuery(Search: " alpha trade "))).Items);
        Assert.Single((await repository.ListAsync(new PessoaListQuery(Search: "WILDCARD %_"))).Items);
        Assert.Single((await repository.ListAsync(new PessoaListQuery(TipoPessoa: TipoPessoa.Fisica))).Items);
        Assert.Equal(2, (await repository.ListAsync(new PessoaListQuery(Ativo: true))).TotalItems);
        Assert.Equal("Gamma", (await repository.ListAsync(new PessoaListQuery(SortBy: PessoaSortField.Nome, SortDescending: true))).Items[0].Nome);
        Assert.Equal(TipoPessoa.Fisica, (await repository.ListAsync(new PessoaListQuery(SortBy: PessoaSortField.TipoPessoa))).Items[0].TipoPessoa);
        Assert.False((await repository.ListAsync(new PessoaListQuery(SortBy: PessoaSortField.Ativo))).Items[0].Ativo);
        Assert.Equal("Alpha", (await repository.ListAsync(new PessoaListQuery(SortBy: PessoaSortField.CreatedAtUtc))).Items[0].Nome);
    }

    [Fact]
    public async Task List_uses_id_as_a_deterministic_secondary_sort_for_equal_names()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var first = Pessoa.Criar(TipoPessoa.Fisica, "Mesmo Nome", null, timestamp);
        var second = Pessoa.Criar(TipoPessoa.Fisica, "Mesmo Nome", null, timestamp);
        context.Pessoas.AddRange(first, second);
        await context.SaveChangesAsync();
        var repository = new PessoaRepository(context);

        var ascending = await repository.ListAsync(new PessoaListQuery(PageSize: 25, SortBy: PessoaSortField.Nome));
        var descending = await repository.ListAsync(new PessoaListQuery(PageSize: 25, SortBy: PessoaSortField.Nome, SortDescending: true));

        var ids = new[] { first.Id, second.Id };
        Assert.Equal(ids.OrderBy(id => id), ascending.Items.Select(pessoa => pessoa.Id));
        Assert.Equal(ids.OrderByDescending(id => id), descending.Items.Select(pessoa => pessoa.Id));
    }

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(postgreSql.ConnectionString).Options);
}
