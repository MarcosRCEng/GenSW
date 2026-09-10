using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.Infrastructure.Animals;

public sealed class AnimalRepository(GenSWDbContext context) : IAnimalRepository
{
    private const string CodigoInternoIndex = "UX_Animais_CodigoInterno_CaseInsensitive";

    public Task AddAsync(Animal animal, CancellationToken cancellationToken = default) =>
        context.Animais.AddAsync(animal, cancellationToken).AsTask();

    public Task<AnimalReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) =>
        ReadModels(context.Animais.AsNoTracking().Where(animal => animal.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public Task<Animal?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Animais.SingleOrDefaultAsync(animal => animal.Id == id, cancellationToken);

    public async Task<AnimalListPage> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(context.Animais.AsNoTracking(), query);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await ReadModels(ApplyOrdering(filtered, query.SortBy, query.SortDescending))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new AnimalListPage(items, totalItems);
    }

    public Task<bool> HasCodigoInternoConflictAsync(
        string codigoInterno,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        var pattern = EscapeLikePattern(codigoInterno);
        return context.Animais.AsNoTracking().AnyAsync(animal =>
            (excludingId == null || animal.Id != excludingId) &&
            EF.Functions.ILike(animal.CodigoInterno, pattern, "\\"), cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: CodigoInternoIndex
            } &&
            exception.Entries.Select(entry => entry.Entity).OfType<Animal>().SingleOrDefault() is { } failedAnimal)
        {
            throw new AnimalDuplicateException(
                failedAnimal.CodigoInterno,
                AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint,
                exception);
        }
    }

    private IQueryable<AnimalReadModel> ReadModels(IQueryable<Animal>? animals = null) =>
        from animal in animals ?? context.Animais.AsNoTracking()
        join especie in context.Especies.AsNoTracking() on animal.EspecieId equals especie.Id
        join raca in context.Racas.AsNoTracking() on animal.RacaId equals raca.Id into racas
        from raca in racas.DefaultIfEmpty()
        join variedade in context.Variedades.AsNoTracking() on animal.VariedadeId equals variedade.Id into variedades
        from variedade in variedades.DefaultIfEmpty()
        select new AnimalReadModel(
            animal.Id, animal.CodigoInterno, animal.Nome, animal.EspecieId, animal.RacaId, animal.VariedadeId,
            animal.Sexo, animal.DataNascimento, animal.Escopo, animal.Ativo, animal.CreatedAtUtc, animal.UpdatedAtUtc,
            new AnimalEspecieResumo(especie.Id, especie.NomeComum, especie.Ativo),
            raca == null ? null : new AnimalRacaResumo(raca.Id, raca.Nome, raca.Ativo),
            variedade == null ? null : new AnimalVariedadeResumo(variedade.Id, variedade.Nome, variedade.Ativo));

    private static IQueryable<Animal> ApplyFilters(IQueryable<Animal> animals, AnimalListQuery filters)
    {
        if (filters.EspecieId is { } especieId) animals = animals.Where(animal => animal.EspecieId == especieId);
        if (filters.RacaId is { } racaId) animals = animals.Where(animal => animal.RacaId == racaId);
        if (filters.VariedadeId is { } variedadeId) animals = animals.Where(animal => animal.VariedadeId == variedadeId);
        if (filters.Sexo is { } sexo) animals = animals.Where(animal => animal.Sexo == sexo);
        if (filters.Escopo is { } escopo) animals = animals.Where(animal => animal.Escopo == escopo);
        if (filters.Ativo is { } ativo) animals = animals.Where(animal => animal.Ativo == ativo);

        var search = string.IsNullOrWhiteSpace(filters.Search) ? null : filters.Search.Trim();
        if (search is not null)
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            animals = animals.Where(animal =>
                EF.Functions.ILike(animal.CodigoInterno, pattern, "\\") ||
                (animal.Nome != null && EF.Functions.ILike(animal.Nome, pattern, "\\")));
        }

        return animals;
    }

    private static IOrderedQueryable<Animal> ApplyOrdering(IQueryable<Animal> animals, AnimalSortField sortBy, bool descending) =>
        (sortBy, descending) switch
        {
            (AnimalSortField.CodigoInterno, false) => animals.OrderBy(animal => animal.CodigoInterno).ThenBy(animal => animal.Id),
            (AnimalSortField.CodigoInterno, true) => animals.OrderByDescending(animal => animal.CodigoInterno).ThenByDescending(animal => animal.Id),
            (AnimalSortField.Nome, false) => animals.OrderBy(animal => animal.Nome).ThenBy(animal => animal.Id),
            (AnimalSortField.Nome, true) => animals.OrderByDescending(animal => animal.Nome).ThenByDescending(animal => animal.Id),
            (AnimalSortField.Sexo, false) => animals.OrderBy(animal => animal.Sexo).ThenBy(animal => animal.Id),
            (AnimalSortField.Sexo, true) => animals.OrderByDescending(animal => animal.Sexo).ThenByDescending(animal => animal.Id),
            (AnimalSortField.Escopo, false) => animals.OrderBy(animal => animal.Escopo).ThenBy(animal => animal.Id),
            (AnimalSortField.Escopo, true) => animals.OrderByDescending(animal => animal.Escopo).ThenByDescending(animal => animal.Id),
            (AnimalSortField.Ativo, false) => animals.OrderBy(animal => animal.Ativo).ThenBy(animal => animal.Id),
            (AnimalSortField.Ativo, true) => animals.OrderByDescending(animal => animal.Ativo).ThenByDescending(animal => animal.Id),
            (AnimalSortField.CreatedAtUtc, false) => animals.OrderBy(animal => animal.CreatedAtUtc).ThenBy(animal => animal.Id),
            (AnimalSortField.CreatedAtUtc, true) => animals.OrderByDescending(animal => animal.CreatedAtUtc).ThenByDescending(animal => animal.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
        };

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
