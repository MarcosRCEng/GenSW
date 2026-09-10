using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.Infrastructure.Tests;

[Collection(nameof(AnimalRepositoryPostgreSqlCollection))]
public sealed class AnimalRepositoryTests(AnimalRepositoryPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Repository_reads_no_tracking_projection_with_optional_breed_and_variety_and_all_filters()
    {
        await using var context = CreateContext();
        var species = await AddSpeciesAsync(context, "Cão");
        var breed = Raca.Criar(species.Id, "Pastor", UtcNow);
        var variety = Variedade.Criar(species.Id, "Curto", UtcNow);
        context.AddRange(breed, variety);
        var matching = Create("AN-100%_A", "Bela", species.Id, breed.Id, variety.Id, SexoAnimal.Femea, EscopoAnimal.Referencia, true, 1);
        var noClassifications = Create("AN-200", "Outro", species.Id, null, null, SexoAnimal.Macho, EscopoAnimal.Operacional, false, 2);
        var wildcardOnly = Create("AN-100XA", "Quase", species.Id, null, null, SexoAnimal.Femea, EscopoAnimal.Referencia, true, 3);
        context.AddRange(matching, noClassifications, wildcardOnly);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new AnimalRepository(context);

        var read = await repository.GetByIdReadOnlyAsync(matching.Id);
        var filtered = await repository.ListAsync(new AnimalListQuery(
            Search: "100%_", EspecieId: species.Id, RacaId: breed.Id, VariedadeId: variety.Id,
            Sexo: SexoAnimal.Femea, Escopo: EscopoAnimal.Referencia, Ativo: true));
        var optional = await repository.GetByIdReadOnlyAsync(noClassifications.Id);

        Assert.Equal("Cão", read!.Especie.NomeComum);
        Assert.Equal("Pastor", read.Raca!.Nome);
        Assert.Equal("Curto", read.Variedade!.Nome);
        Assert.Equal(matching.Id, Assert.Single(filtered.Items).Id);
        Assert.Null(optional!.Raca);
        Assert.Null(optional.Variedade);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(AnimalSortField.CodigoInterno)]
    [InlineData(AnimalSortField.Nome)]
    [InlineData(AnimalSortField.Sexo)]
    [InlineData(AnimalSortField.Escopo)]
    [InlineData(AnimalSortField.Ativo)]
    [InlineData(AnimalSortField.CreatedAtUtc)]
    public async Task ListAsync_applies_each_allowed_sort_with_id_tie_breaker(AnimalSortField sortBy)
    {
        await using var context = CreateContext();
        var species = await AddSpeciesAsync(context, "Cão");
        var first = Create("AN-002", "B", species.Id, null, null, SexoAnimal.Femea, EscopoAnimal.Referencia, true, 0);
        var second = Create("AN-001", "A", species.Id, null, null, SexoAnimal.Macho, EscopoAnimal.Operacional, false, 0);
        context.AddRange(first, second);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new AnimalRepository(context);

        var page = await repository.ListAsync(new AnimalListQuery(PageSize: 10, SortBy: sortBy));
        var descending = await repository.ListAsync(new AnimalListQuery(PageSize: 10, SortBy: sortBy, SortDescending: true));

        IEnumerable<Guid> expectedAscending = sortBy == AnimalSortField.CreatedAtUtc
            ? new[] { first.Id, second.Id }.OrderBy(id => id)
            : new[] { second.Id, first.Id };
        IEnumerable<Guid> expectedDescending = sortBy == AnimalSortField.CreatedAtUtc
            ? new[] { first.Id, second.Id }.OrderByDescending(id => id)
            : new[] { first.Id, second.Id };

        Assert.Equal(2, page.TotalItems);
        Assert.Equal(expectedAscending, page.Items.Select(item => item.Id));
        Assert.Equal(expectedDescending, descending.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task HasCodigoInternoConflictAsync_is_case_insensitive_and_excludes_the_requested_id()
    {
        await using var context = CreateContext();
        var species = await AddSpeciesAsync(context, "Cão");
        var animal = Create("An-000001", null, species.Id, null, null, SexoAnimal.Indeterminado, EscopoAnimal.Operacional, true, 0);
        context.Animais.Add(animal);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new AnimalRepository(context);

        Assert.True(await repository.HasCodigoInternoConflictAsync("aN-000001"));
        Assert.False(await repository.HasCodigoInternoConflictAsync("AN-000001", animal.Id));
    }

    [Fact]
    public async Task SaveChangesAsync_translates_only_the_named_postgresql_codigo_unique_violation()
    {
        await using var context = CreateContext();
        var species = await AddSpeciesAsync(context, "Cão");
        var repository = new AnimalRepository(context);
        await repository.AddAsync(Create("AN-DUP", null, species.Id, null, null, SexoAnimal.Indeterminado, EscopoAnimal.Operacional, true, 0));
        await repository.SaveChangesAsync();
        await repository.AddAsync(Create("an-dup", null, species.Id, null, null, SexoAnimal.Indeterminado, EscopoAnimal.Operacional, true, 1));

        var exception = await Assert.ThrowsAsync<AnimalDuplicateException>(() => repository.SaveChangesAsync());

        Assert.Equal("an-dup", exception.CodigoInterno);
        Assert.Equal(AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint, exception.ConflictSource);
        Assert.IsType<DbUpdateException>(exception.InnerException);
    }

    [Fact]
    public async Task SaveChangesAsync_does_not_translate_another_postgresql_constraint_or_sqlstate()
    {
        await using var context = CreateContext();
        var repository = new AnimalRepository(context);
        await repository.AddAsync(Create("AN-FK", null, Guid.NewGuid(), null, null, SexoAnimal.Indeterminado, EscopoAnimal.Operacional, true, 0));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());

        Assert.IsType<PostgresException>(exception.InnerException);
        Assert.NotEqual(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception.InnerException!).SqlState);
    }

    [Fact]
    public async Task SaveChangesAsync_does_not_translate_a_test_only_other_unique_constraint()
    {
        const string otherConstraint = "UX_Animais_TestOnly_Nome";
        await using var context = CreateContext();
        var species = await AddSpeciesAsync(context, "Cão");
        var repository = new AnimalRepository(context);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX \"UX_Animais_TestOnly_Nome\" ON \"Animais\" (\"Nome\");");

        try
        {
            await repository.AddAsync(Create("AN-OTHER-1", "Duplicado", species.Id, null, null, SexoAnimal.Indeterminado, EscopoAnimal.Operacional, true, 0));
            await repository.SaveChangesAsync();
            await repository.AddAsync(Create("AN-OTHER-2", "Duplicado", species.Id, null, null, SexoAnimal.Indeterminado, EscopoAnimal.Operacional, true, 1));

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());

            var postgres = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal(otherConstraint, postgres.ConstraintName);
            Assert.Single(context.ChangeTracker.Entries<Animal>(), entry => entry.State == EntityState.Added);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(
                "DROP INDEX IF EXISTS \"UX_Animais_TestOnly_Nome\";");
        }
    }

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(fixture.PostgreSql.ConnectionString).Options);
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private static Animal Create(string code, string? name, Guid speciesId, Guid? breedId, Guid? varietyId, SexoAnimal sex, EscopoAnimal scope, bool active, int offset)
    {
        var animal = Animal.Criar(code, name, speciesId, breedId, varietyId, sex, null, scope, DateOnly.FromDateTime(UtcNow.UtcDateTime), UtcNow.AddDays(offset));
        if (!active) animal.Inativar(UtcNow.AddDays(offset));
        return animal;
    }

    private static async Task<Especie> AddSpeciesAsync(GenSWDbContext context, string name)
    {
        var species = Especie.Criar(name, null, UtcNow);
        context.Especies.Add(species);
        await context.SaveChangesAsync();
        return species;
    }
}

[CollectionDefinition(nameof(AnimalRepositoryPostgreSqlCollection))]
public sealed class AnimalRepositoryPostgreSqlCollection : ICollectionFixture<AnimalRepositoryPostgreSqlFixture>
{
}

public sealed class AnimalRepositoryPostgreSqlFixture : IAsyncLifetime
{
    internal GenSW.API.Tests.EphemeralPostgreSql PostgreSql { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        PostgreSql = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Animais\", \"Racas\", \"Variedades\", \"Especies\" CASCADE;");
    }

    public Task DisposeAsync() => PostgreSql.DisposeAsync().AsTask();

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(PostgreSql.ConnectionString).Options);
}
