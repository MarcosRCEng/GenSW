using GenSW.Application.Species;
using GenSW.Domain.Species;
using GenSW.Infrastructure.Persistence;
using GenSW.Infrastructure.Species;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class EspecieRepositoryTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql postgreSql = null!;

    public async Task InitializeAsync() => postgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
    public Task DisposeAsync() => postgreSql.DisposeAsync().AsTask();

    [Fact]
    public async Task Add_and_get_persist_species_and_missing_get_returns_null()
    {
        var especie = Create("Cão", "Canis familiaris");
        await using (var context = CreateContext())
        {
            var repository = new EspecieRepository(context);
            await context.Database.MigrateAsync();
            await repository.AddAsync(especie);
            await repository.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var readRepository = new EspecieRepository(readContext);
        Assert.Equal(especie.Id, (await readRepository.GetByIdReadOnlyAsync(especie.Id))!.Id);
        Assert.Null(await readRepository.GetByIdReadOnlyAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_applies_search_status_sorting_and_paging()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var first = Create("Alpha", "Alpha scientific", 0);
        var second = Create("Beta", null, 1);
        var inactive = Create("Wildcard %_", "Gamma scientific", 2);
        inactive.Inativar(DateTimeOffset.UtcNow);
        context.Especies.AddRange(first, second, inactive);
        await context.SaveChangesAsync();
        var repository = new EspecieRepository(context);

        var page = await repository.ListAsync(new EspecieListQuery(Page: 1, PageSize: 1));
        Assert.Equal(3, page.TotalItems);
        Assert.Equal("Alpha", Assert.Single(page.Items).NomeComum);
        Assert.Empty((await repository.ListAsync(new EspecieListQuery(Page: 5))).Items);
        Assert.Single((await repository.ListAsync(new EspecieListQuery(Search: " alpha scientific "))).Items);
        Assert.Single((await repository.ListAsync(new EspecieListQuery(Search: "WILDCARD %_"))).Items);
        Assert.Equal(2, (await repository.ListAsync(new EspecieListQuery(Ativo: true))).TotalItems);
        Assert.Equal("Wildcard %_", (await repository.ListAsync(new EspecieListQuery(SortBy: EspecieSortField.NomeComum, SortDescending: true))).Items[0].NomeComum);
        Assert.Equal("Alpha", (await repository.ListAsync(new EspecieListQuery(SortBy: EspecieSortField.NomeCientifico))).Items[0].NomeComum);
        Assert.False((await repository.ListAsync(new EspecieListQuery(SortBy: EspecieSortField.Ativo))).Items[0].Ativo);
        Assert.Equal("Alpha", (await repository.ListAsync(new EspecieListQuery(SortBy: EspecieSortField.CreatedAtUtc))).Items[0].NomeComum);
    }

    [Fact]
    public async Task List_uses_id_as_a_deterministic_secondary_sort()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var first = Create("Cão", "Canis familiaris");
        var second = Create("Gato", "Felis catus");
        context.Especies.AddRange(first, second);
        await context.SaveChangesAsync();
        var repository = new EspecieRepository(context);

        var ascending = await repository.ListAsync(new EspecieListQuery(SortBy: EspecieSortField.Ativo));
        var descending = await repository.ListAsync(new EspecieListQuery(SortBy: EspecieSortField.Ativo, SortDescending: true));

        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), ascending.Items.Select(especie => especie.Id));
        Assert.Equal(new[] { first.Id, second.Id }.OrderByDescending(id => id), descending.Items.Select(especie => especie.Id));
    }

    [Fact]
    public async Task Conflict_checks_are_case_insensitive_and_exclude_the_same_id()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var existing = Create("Cão", "Canis familiaris");
        context.Especies.Add(existing);
        await context.SaveChangesAsync();
        var repository = new EspecieRepository(context);

        Assert.True(await repository.HasNomeComumConflictAsync("cÃO", null));
        Assert.False(await repository.HasNomeComumConflictAsync("cÃO", existing.Id));
        Assert.True(await repository.HasNomeCientificoConflictAsync("CANIS FAMILIARIS", null));
        Assert.False(await repository.HasNomeCientificoConflictAsync("CANIS FAMILIARIS", existing.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_translates_the_corresponding_unique_index_violation(bool commonName)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var repository = new EspecieRepository(context);
        await repository.AddAsync(Create(commonName ? "Dog" : "Cão", "Canis familiaris"));
        await repository.SaveChangesAsync();
        await repository.AddAsync(commonName ? Create("dOG", "Canis lupus") : Create("Lobo", "CANIS FAMILIARIS"));

        var exception = await Assert.ThrowsAsync<EspecieDuplicateException>(() => repository.SaveChangesAsync());
        Assert.Equal(commonName ? EspecieDuplicateField.NomeComum : EspecieDuplicateField.NomeCientifico, exception.Field);
    }

    [Fact]
    public async Task Multiple_null_scientific_names_are_permitted()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var repository = new EspecieRepository(context);
        await repository.AddAsync(Create("Cão", null));
        await repository.AddAsync(Create("Gato", null));
        await repository.SaveChangesAsync();

        Assert.Equal(2, (await repository.ListAsync(new EspecieListQuery())).TotalItems);
    }

    [Fact]
    public async Task Migration_creates_case_insensitive_unique_indexes_in_postgresql()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var indexes = await context.Database.SqlQueryRaw<string>("""
            SELECT indexname AS "Value"
            FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = 'Especies'
            """).ToListAsync();
        var definitions = await context.Database.SqlQueryRaw<string>("""
            SELECT pg_get_indexdef(indexrelid) AS "Value"
            FROM pg_index
            WHERE indrelid = '"Especies"'::regclass
            """).ToListAsync();

        Assert.Contains("UX_Especies_NomeComum_CaseInsensitive", indexes);
        Assert.Contains("UX_Especies_NomeCientifico_CaseInsensitive", indexes);
        Assert.Contains(definitions, definition => definition.Contains("lower", StringComparison.OrdinalIgnoreCase) && definition.Contains("UX_Especies_NomeComum_CaseInsensitive"));
        Assert.Contains(definitions, definition => definition.Contains("lower", StringComparison.OrdinalIgnoreCase) && definition.Contains("UX_Especies_NomeCientifico_CaseInsensitive") && definition.Contains("WHERE (\"NomeCientifico\" IS NOT NULL)"));
    }

    [Theory]
    [MemberData(nameof(NonCanonicalNames))]
    public async Task Database_rejects_noncanonical_whitespace_in_common_names(string invalidName)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        await Assert.ThrowsAsync<PostgresException>(() => InsertDirectlyAsync(context, invalidName, null));
    }

    [Theory]
    [MemberData(nameof(NonCanonicalNames))]
    public async Task Database_rejects_noncanonical_whitespace_in_scientific_names(string invalidName)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        await Assert.ThrowsAsync<PostgresException>(() => InsertDirectlyAsync(context, "Nome comum", invalidName));
    }

    public static TheoryData<string> NonCanonicalNames => new()
    {
        "Nome\tcomum",
        "Nome\ncomum",
        "Nome\u00A0comum",
        "Nome\u2003comum",
    };

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(postgreSql.ConnectionString).Options);

    private static Especie Create(string commonName, string? scientificName, int days = 0) =>
        Especie.Criar(commonName, scientificName, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days));

    private static Task<int> InsertDirectlyAsync(GenSWDbContext context, string commonName, string? scientificName)
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Especies" ("Id", "NomeComum", "NomeCientifico", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({Guid.NewGuid()}, {commonName}, {scientificName}, TRUE, {timestamp}, {timestamp})
            """);
    }
}
