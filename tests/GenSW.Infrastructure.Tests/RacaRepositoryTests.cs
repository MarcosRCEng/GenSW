using GenSW.Application.Breeds;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Infrastructure.Breeds;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class RacaRepositoryTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql postgreSql = null!;

    public async Task InitializeAsync() => postgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
    public Task DisposeAsync() => postgreSql.DisposeAsync().AsTask();

    [Fact]
    public async Task Repository_persists_reads_and_lists_species_scoped_breeds()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var dog = await AddSpeciesAsync(context, "Cão");
        var cat = await AddSpeciesAsync(context, "Gato");
        var first = Create(dog.Id, "Poodle", 0);
        var second = Create(dog.Id, "Retriever", 1);
        var otherSpecies = Create(cat.Id, "Poodle", 2);
        otherSpecies.Inativar(DateTimeOffset.UtcNow);
        var repository = new RacaRepository(context);
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await repository.AddAsync(otherSpecies);
        await repository.SaveChangesAsync();

        Assert.Equal(first.Id, (await repository.GetByIdReadOnlyAsync(first.Id))!.Id);
        Assert.Null(await repository.GetByIdReadOnlyAsync(Guid.NewGuid()));
        var page = await repository.ListAsync(new RacaListQuery(Page: 1, PageSize: 1, EspecieId: dog.Id));
        Assert.Equal(2, page.TotalItems);
        Assert.Equal("Poodle", Assert.Single(page.Items).Nome);
        Assert.Single((await repository.ListAsync(new RacaListQuery(Search: "retr", EspecieId: dog.Id))).Items);
        Assert.Single((await repository.ListAsync(new RacaListQuery(Ativo: false))).Items);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id),
            (await repository.ListAsync(new RacaListQuery(EspecieId: dog.Id, SortBy: RacaSortField.Ativo))).Items.Select(item => item.Id));
    }

    [Fact]
    public async Task SaveChanges_converts_normalized_duplicate_and_migration_creates_functional_index()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var species = await AddSpeciesAsync(context, "Cão");
        var repository = new RacaRepository(context);
        await repository.AddAsync(Create(species.Id, "Pelo curto", 0));
        await repository.SaveChangesAsync();
        await repository.AddAsync(Create(species.Id, "PELO CURTO", 1));
        await Assert.ThrowsAsync<RacaDuplicateException>(() => repository.SaveChangesAsync());

        var indexes = await context.Database.SqlQueryRaw<string>("""
            SELECT indexname AS "Value" FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = 'Racas'
            """).ToListAsync();
        Assert.Contains("UX_Racas_EspecieId_Nome_CaseInsensitive", indexes);
    }

    [Fact]
    public async Task Referenced_species_cannot_be_deleted()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var species = await AddSpeciesAsync(context, "Cão");
        context.Racas.Add(Create(species.Id, "Poodle", 0));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.Especies.Remove(await context.Especies.SingleAsync(entity => entity.Id == species.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(postgreSql.ConnectionString).Options);
    private static Raca Create(Guid speciesId, string name, int days) => Raca.Criar(speciesId, name, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days));
    private static async Task<Especie> AddSpeciesAsync(GenSWDbContext context, string name)
    {
        var species = Especie.Criar(name, null, DateTimeOffset.UtcNow);
        context.Especies.Add(species);
        await context.SaveChangesAsync();
        return species;
    }
}
