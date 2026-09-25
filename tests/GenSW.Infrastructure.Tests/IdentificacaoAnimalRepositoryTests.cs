using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using GenSW.Infrastructure;
using GenSW.Infrastructure.Animals.Identificacoes;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.Infrastructure.Tests;

[Collection(nameof(AnimalRepositoryPostgreSqlCollection))]
public sealed class IdentificacaoAnimalRepositoryTests(AnimalRepositoryPostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Repository_uses_escaped_case_insensitive_exact_duplicate_check_and_read_only_filtered_pages()
    {
        await using var context = CreateContext();
        var animal = await AddAnimalAsync(context, "AN-001", "Bela");
        var otherAnimal = await AddAnimalAsync(context, "AN-002", "Lua");
        var matching = Create(animal.Id, TipoIdentificacaoAnimal.Brinco, " A%_01 ", true, true, 1);
        var sameCreatedAtLowerId = Create(animal.Id, TipoIdentificacaoAnimal.Anilha, "A-2", false, true, 1);
        var inactive = Create(animal.Id, TipoIdentificacaoAnimal.Brinco, "B-1", false, false, 2);
        var wildcardOnly = Create(otherAnimal.Id, TipoIdentificacaoAnimal.Anilha, "AXX01", false, true, 3);
        context.AddRange(matching, sameCreatedAtLowerId, inactive, wildcardOnly);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new IdentificacaoAnimalRepository(context);

        var page = await repository.ListByAnimalAsync(animal.Id,
            new IdentificacaoAnimalListQuery(PageSize: 10, Tipo: TipoIdentificacaoAnimal.Brinco, Valor: "%_01", Ativo: true));
        var global = await repository.ListGlobalAsync(new IdentificacaoAnimalListQuery(PageSize: 10, Principal: true));

        Assert.True(await repository.HasDuplicateAsync(TipoIdentificacaoAnimal.Brinco, null, "a%_01"));
        Assert.False(await repository.HasDuplicateAsync(TipoIdentificacaoAnimal.Anilha, null, "a%_01"));
        Assert.Equal(new[] { matching.Id }, page.Items.Select(item => item.Id));
        Assert.Equal(new[] { matching.Id, sameCreatedAtLowerId.Id }.OrderByDescending(id => id),
            (await repository.ListByAnimalAsync(animal.Id, new IdentificacaoAnimalListQuery(PageSize: 10, Ativo: true))).Items.Take(2).Select(item => item.Id));
        Assert.All(global.Items, item => Assert.Equal(animal.Id, item.Animal.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Repository_reads_by_animal_and_locks_parent_inside_uncommitted_mutation_that_rolls_back()
    {
        await using var context = CreateContext();
        var animal = await AddAnimalAsync(context, "AN-003", "Nina");
        var identification = Create(animal.Id, TipoIdentificacaoAnimal.Microchip, "MC-1", false, true, 0);
        context.Add(identification);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new IdentificacaoAnimalRepository(context);

        await using (await repository.BeginMutationAsync())
        {
            var locked = await repository.LockAnimalAsync(animal.Id);
            var found = await repository.GetByAnimalForUpdateAsync(animal.Id, identification.Id);
            await repository.AddAsync(Create(animal.Id, TipoIdentificacaoAnimal.Brinco, "B-ROLLBACK", false, true, 1));
            await repository.SaveChangesAsync();
            Assert.Equal(animal.Id, locked!.Id);
            Assert.Equal(identification.Id, found!.Id);
        }

        await using var verification = CreateContext();
        Assert.False(await verification.IdentificacoesAnimal.AnyAsync(item => item.Valor == "B-ROLLBACK"));
        Assert.Null(await repository.GetByAnimalReadOnlyAsync(Guid.NewGuid(), identification.Id));
    }

    [Fact]
    public async Task Uncommitted_mutation_scope_rolls_back_both_successful_flushes()
    {
        await using var context = CreateContext();
        var animal = await AddAnimalAsync(context, "AN-ROLLBACK", null);
        var repository = new IdentificacaoAnimalRepository(context);

        await using (await repository.BeginMutationAsync())
        {
            await repository.AddAsync(Create(animal.Id, TipoIdentificacaoAnimal.Anilha, "FIRST-FLUSH", false, true, 0));
            await repository.SaveChangesAsync();
            await repository.AddAsync(Create(animal.Id, TipoIdentificacaoAnimal.Brinco, "SECOND-FLUSH", false, true, 1));
            await repository.SaveChangesAsync();
        }

        await using var verification = CreateContext();
        Assert.False(await verification.IdentificacoesAnimal.AnyAsync(item => item.Valor == "FIRST-FLUSH" || item.Valor == "SECOND-FLUSH"));
    }

    [Fact]
    public async Task Parent_for_update_lock_blocks_a_second_concurrent_lock_until_the_first_transaction_commits()
    {
        await using var firstContext = CreateContext();
        var animal = await AddAnimalAsync(firstContext, "AN-LOCK", null);
        var firstRepository = new IdentificacaoAnimalRepository(firstContext);
        await using var firstScope = await firstRepository.BeginMutationAsync();
        Assert.Equal(animal.Id, (await firstRepository.LockAnimalAsync(animal.Id))!.Id);

        await using var secondContext = CreateContext();
        var secondRepository = new IdentificacaoAnimalRepository(secondContext);
        await using var secondScope = await secondRepository.BeginMutationAsync();
        var secondLock = secondRepository.LockAnimalAsync(animal.Id);

        Assert.NotSame(secondLock, await Task.WhenAny(secondLock, Task.Delay(TimeSpan.FromMilliseconds(400))));
        await firstScope.CommitAsync();
        Assert.Equal(animal.Id, (await secondLock)!.Id);
    }

    [Fact]
    public async Task Global_page_escapes_valor_filters_and_projects_animal_summary_in_deterministic_order()
    {
        await using var context = CreateContext();
        var firstAnimal = await AddAnimalAsync(context, "AN-GLOBAL-1", "Primeiro");
        var secondAnimal = await AddAnimalAsync(context, "AN-GLOBAL-2", "Segundo");
        var exact = Create(firstAnimal.Id, TipoIdentificacaoAnimal.Brinco, "G%_01", false, true, 1);
        var later = Create(secondAnimal.Id, TipoIdentificacaoAnimal.Brinco, "G-2", false, true, 2);
        var wildcardOnly = Create(secondAnimal.Id, TipoIdentificacaoAnimal.Anilha, "GXX01", false, true, 3);
        var inactive = Create(firstAnimal.Id, TipoIdentificacaoAnimal.Brinco, "G%_01-INACTIVE", false, false, 4);
        context.AddRange(exact, later, wildcardOnly, inactive);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new IdentificacaoAnimalRepository(context);

        var escaped = await repository.ListGlobalAsync(new IdentificacaoAnimalListQuery(PageSize: 10,
            Tipo: TipoIdentificacaoAnimal.Brinco, Valor: "%_01", Ativo: true));
        var firstPage = await repository.ListGlobalAsync(new IdentificacaoAnimalListQuery(Page: 1, PageSize: 1, Ativo: true));
        var secondPage = await repository.ListGlobalAsync(new IdentificacaoAnimalListQuery(Page: 2, PageSize: 1, Ativo: true));
        var orderedPage = await repository.ListGlobalAsync(new IdentificacaoAnimalListQuery(Page: 1, PageSize: 2, Ativo: true));

        var escapedItem = Assert.Single(escaped.Items);
        Assert.Equal(exact.Id, escapedItem.Identificacao.Id);
        Assert.Equal(new IdentificacaoAnimalAnimalResumo(firstAnimal.Id, "AN-GLOBAL-1", "Primeiro"), escapedItem.Animal);
        Assert.Equal(wildcardOnly.Id, firstPage.Items.Single().Identificacao.Id);
        Assert.Equal(later.Id, secondPage.Items.Single().Identificacao.Id);
        Assert.Equal(new[] { wildcardOnly.Id, later.Id }, orderedPage.Items.Select(item => item.Identificacao.Id));
        Assert.Equal(3, firstPage.TotalItems);
        Assert.Equal(3, firstPage.TotalPages);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SaveChangesAsync_translates_only_the_three_named_unique_indexes()
    {
        await using var context = CreateContext();
        var firstAnimal = await AddAnimalAsync(context, "AN-004", null);
        var secondAnimal = await AddAnimalAsync(context, "AN-005", null);
        var repository = new IdentificacaoAnimalRepository(context);
        await repository.AddAsync(Create(firstAnimal.Id, TipoIdentificacaoAnimal.Anilha, "TAG-1", false, true, 0));
        await repository.SaveChangesAsync();
        await repository.AddAsync(Create(secondAnimal.Id, TipoIdentificacaoAnimal.Anilha, "tag-1", false, true, 1));

        var duplicate = await Assert.ThrowsAsync<IdentificacaoAnimalDuplicateException>(() => repository.SaveChangesAsync());
        Assert.Equal(IdentificacaoAnimalDuplicateConflictSource.PersistedNamedTipoValorUniqueConstraint, duplicate.ConflictSource);
        context.ChangeTracker.Clear();
        await repository.AddAsync(Create(firstAnimal.Id, TipoIdentificacaoAnimal.Brinco, "B-1", true, true, 2));
        await repository.SaveChangesAsync();
        await repository.AddAsync(Create(firstAnimal.Id, TipoIdentificacaoAnimal.Microchip, "MC-2", true, true, 3));

        var principal = await Assert.ThrowsAsync<IdentificacaoAnimalPrincipalConflictException>(() => repository.SaveChangesAsync());
        Assert.Equal(IdentificacaoAnimalPrincipalConflictSource.PersistedNamedPrincipalAtivaUniqueConstraint, principal.ConflictSource);
    }

    [Fact]
    public async Task SaveChangesAsync_translates_the_named_outro_description_and_value_index()
    {
        await using var context = CreateContext();
        var firstAnimal = await AddAnimalAsync(context, "AN-006", null);
        var secondAnimal = await AddAnimalAsync(context, "AN-007", null);
        var repository = new IdentificacaoAnimalRepository(context);
        await repository.AddAsync(CreateOutro(firstAnimal.Id, "Colar", "X-1", 0));
        await repository.SaveChangesAsync();
        await repository.AddAsync(CreateOutro(secondAnimal.Id, "colar", "x-1", 1));

        var duplicate = await Assert.ThrowsAsync<IdentificacaoAnimalDuplicateException>(() => repository.SaveChangesAsync());

        Assert.Equal(IdentificacaoAnimalDuplicateConflictSource.PersistedNamedOutroDescricaoTipoValorUniqueConstraint,
            duplicate.ConflictSource);
    }

    [Fact]
    public async Task SaveChangesAsync_translates_a_named_duplicate_when_multiple_identifications_are_pending()
    {
        await using var context = CreateContext();
        var firstAnimal = await AddAnimalAsync(context, "AN-MULTI-1", null);
        var secondAnimal = await AddAnimalAsync(context, "AN-MULTI-2", null);
        var thirdAnimal = await AddAnimalAsync(context, "AN-MULTI-3", null);
        var repository = new IdentificacaoAnimalRepository(context);
        await repository.AddAsync(Create(firstAnimal.Id, TipoIdentificacaoAnimal.Anilha, "MULTI-DUP", false, true, 0));
        await repository.SaveChangesAsync();
        await repository.AddAsync(Create(secondAnimal.Id, TipoIdentificacaoAnimal.Anilha, "multi-dup", false, true, 1));
        await repository.AddAsync(Create(thirdAnimal.Id, TipoIdentificacaoAnimal.Anilha, "MULTI-DUP", false, true, 2));

        var duplicate = await Assert.ThrowsAsync<IdentificacaoAnimalDuplicateException>(() => repository.SaveChangesAsync());

        Assert.Equal(IdentificacaoAnimalDuplicateConflictSource.PersistedNamedTipoValorUniqueConstraint, duplicate.ConflictSource);
    }

    [Fact]
    public async Task SaveChangesAsync_does_not_reclassify_unknown_database_errors()
    {
        await using var context = CreateContext();
        var repository = new IdentificacaoAnimalRepository(context);
        await repository.AddAsync(Create(Guid.NewGuid(), TipoIdentificacaoAnimal.Anilha, "FK-UNKNOWN", false, true, 0));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());

        Assert.IsType<PostgresException>(exception.InnerException);
        Assert.NotEqual(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception.InnerException!).SqlState);
    }

    [Fact]
    public void Infrastructure_is_the_only_repository_binding()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GenSW"] = fixture.PostgreSql.ConnectionString
        }).Build();

        services.AddInfrastructure(configuration);

        var binding = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IIdentificacaoAnimalRepository));
        Assert.Equal(typeof(IdentificacaoAnimalRepository), binding.ImplementationType);
    }

    private GenSWDbContext CreateContext() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(fixture.PostgreSql.ConnectionString).Options);

    private static IdentificacaoAnimal Create(Guid animalId, TipoIdentificacaoAnimal type, string value, bool principal, bool active, int offset)
    {
        var identification = IdentificacaoAnimal.Criar(animalId, type, null, value, principal, null, null, UtcNow.AddDays(offset));
        if (!active) identification.Inativar(UtcNow.AddDays(offset));
        return identification;
    }

    private static IdentificacaoAnimal CreateOutro(Guid animalId, string description, string value, int offset) =>
        IdentificacaoAnimal.Criar(animalId, TipoIdentificacaoAnimal.Outro, description, value, false, null, null,
            UtcNow.AddDays(offset));

    private static async Task<Animal> AddAnimalAsync(GenSWDbContext context, string code, string? name)
    {
        var species = Especie.Criar($"Espécie {code}", null, UtcNow);
        context.Especies.Add(species);
        await context.SaveChangesAsync();
        var animal = Animal.Criar(code, name, species.Id, null, null, SexoAnimal.Indeterminado, null,
            EscopoAnimal.Operacional, DateOnly.FromDateTime(UtcNow.UtcDateTime), UtcNow);
        context.Animais.Add(animal);
        await context.SaveChangesAsync();
        return animal;
    }
}
