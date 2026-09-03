using GenSW.Application.Varieties;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Persistence;
using GenSW.Infrastructure.Varieties;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class VariedadeRepositoryTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql postgreSql = null!;

    public async Task InitializeAsync() => postgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
    public Task DisposeAsync() => postgreSql.DisposeAsync().AsTask();

    [Fact]
    public async Task Repository_persists_reads_and_lists_species_scoped_varieties()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var dog = await AddSpeciesAsync(context, "Cão");
        var cat = await AddSpeciesAsync(context, "Gato");
        var first = Create(dog.Id, "Pelo curto", 0);
        var second = Create(dog.Id, "Pelo longo", 0);
        var otherSpecies = Create(cat.Id, "Pelo curto", 2);
        otherSpecies.Inativar(DateTimeOffset.UtcNow);
        var repository = new VariedadeRepository(context);
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await repository.AddAsync(otherSpecies);
        await repository.SaveChangesAsync();

        Assert.Equal(first.Id, (await repository.GetByIdReadOnlyAsync(first.Id))!.Id);
        Assert.Null(await repository.GetByIdReadOnlyAsync(Guid.NewGuid()));
        var page = await repository.ListAsync(new VariedadeListQuery(Page: 1, PageSize: 1, EspecieId: dog.Id));
        Assert.Equal(2, page.TotalItems);
        Assert.Equal("Pelo curto", Assert.Single(page.Items).Nome);
        Assert.Single((await repository.ListAsync(new VariedadeListQuery(Search: "CURTO", EspecieId: dog.Id))).Items);
        Assert.Single((await repository.ListAsync(new VariedadeListQuery(Ativo: false))).Items);
        Assert.Equal(otherSpecies.Id, Assert.Single((await repository.ListAsync(new VariedadeListQuery(PageSize: 1, SortBy: VariedadeSortField.Ativo))).Items).Id);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id),
            (await repository.ListAsync(new VariedadeListQuery(EspecieId: dog.Id, SortBy: VariedadeSortField.CreatedAtUtc))).Items.Select(item => item.Id));
    }

    [Fact]
    public async Task SaveChanges_converts_species_scoped_normalized_duplicates_and_migration_creates_functional_index()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var dog = await AddSpeciesAsync(context, "Cão");
        var cat = await AddSpeciesAsync(context, "Gato");
        var repository = new VariedadeRepository(context);
        await repository.AddAsync(Create(dog.Id, "Pelo curto", 0));
        await repository.SaveChangesAsync();
        await repository.AddAsync(Create(dog.Id, "PELO CURTO", 1));
        var duplicate = await Assert.ThrowsAsync<VariedadeDuplicateException>(() => repository.SaveChangesAsync());
        Assert.IsType<DbUpdateException>(duplicate.InnerException);
        context.ChangeTracker.Clear();
        await repository.AddAsync(Create(cat.Id, "PELO CURTO", 2));
        await repository.SaveChangesAsync();

        var indexes = await context.Database.SqlQueryRaw<string>("""
            SELECT indexname AS "Value" FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = 'Variedades'
            """).ToListAsync();
        Assert.Contains("UX_Variedades_EspecieId_Nome_CaseInsensitive", indexes);
    }

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(postgreSql.ConnectionString).Options);
    private static Variedade Create(Guid speciesId, string name, int days) => Variedade.Criar(speciesId, name, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days));
    private static async Task<Especie> AddSpeciesAsync(GenSWDbContext context, string name)
    {
        var species = Especie.Criar(name, null, DateTimeOffset.UtcNow);
        context.Especies.Add(species);
        await context.SaveChangesAsync();
        return species;
    }
}
