using GenSW.Application.Animals;
using GenSW.Application.Properties;
using GenSW.Domain.Properties;
using GenSW.Infrastructure.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.Infrastructure.Properties;

public sealed class PropriedadeRepository(GenSWDbContext context) : IPropriedadeRepository
{
    public Task AddAsync(Propriedade item, CancellationToken ct) => context.Propriedades.AddAsync(item, ct).AsTask();

    public Task<Propriedade?> GetAsync(Guid id, bool tracking, CancellationToken ct) =>
        (tracking ? context.Propriedades : context.Propriedades.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PropriedadePage> ListAsync(PropriedadeListQuery query, CancellationToken ct)
    {
        IQueryable<Propriedade> filtered = context.Propriedades.AsNoTracking();
        if (query.Ativo is { } active) filtered = filtered.Where(x => x.Ativo == active);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            var namePattern = pattern.ToUpperInvariant();
            filtered = filtered.Where(x => EF.Functions.Like(x.NomeNormalizado, namePattern, "\\") ||
                (x.Localizacao != null && EF.Functions.ILike(x.Localizacao, pattern, "\\")));
        }
        var count = await filtered.CountAsync(ct);
        var descending = query.SortDirection == "desc";
        var ordered = (query.SortBy, descending) switch
        {
            ("nome", false) => filtered.OrderBy(x => x.Nome),
            ("nome", true) => filtered.OrderByDescending(x => x.Nome),
            ("ativo", false) => filtered.OrderBy(x => x.Ativo),
            ("ativo", true) => filtered.OrderByDescending(x => x.Ativo),
            ("createdAtUtc", false) => filtered.OrderBy(x => x.CreatedAtUtc),
            ("createdAtUtc", true) => filtered.OrderByDescending(x => x.CreatedAtUtc),
            _ => throw new ArgumentException("Ordenação inválida."),
        };
        return new(await ordered.ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct), count);
    }

    public Task<bool> NomeExistsAsync(string nome, Guid? excludingId, CancellationToken ct)
    {
        var normalized = nome.ToUpperInvariant();
        return context.Propriedades.AsNoTracking().AnyAsync(x => x.Id != excludingId && x.NomeNormalizado == normalized, ct);
    }

    public async Task<IAnimalMutationScope> BeginMutationAsync(Guid? animalId, CancellationToken ct) =>
        await AnimalMutationScope.BeginAsync(context, animalId, false, ct);

    public Task<Propriedade?> LockPropriedadeAsync(Guid id, CancellationToken ct) =>
        context.Propriedades.FromSqlInterpolated($"SELECT * FROM \"Propriedades\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);

    public Task<bool> AnimalExistsAsync(Guid id, CancellationToken ct) => context.Animais.AsNoTracking().AnyAsync(x => x.Id == id, ct);

    public Task<VinculoAnimalPropriedade?> GetCurrentAsync(Guid animalId, CancellationToken ct) =>
        context.VinculosAnimalPropriedade.SingleOrDefaultAsync(x => x.AnimalId == animalId && x.DataFim == null, ct);

    public Task<DateOnly?> GetLastEndAsync(Guid animalId, CancellationToken ct) =>
        context.VinculosAnimalPropriedade.Where(x => x.AnimalId == animalId && x.DataFim != null).MaxAsync(x => x.DataFim, ct);

    public async Task<IReadOnlyList<VinculoAnimalPropriedadeResult>> HistoryAsync(Guid animalId, CancellationToken ct) =>
        await (from link in context.VinculosAnimalPropriedade.AsNoTracking()
               join property in context.Propriedades.AsNoTracking() on link.PropriedadeId equals property.Id
               where link.AnimalId == animalId
               orderby link.DataInicio descending, link.CreatedAtUtc descending, link.Id descending
               select new VinculoAnimalPropriedadeResult(link.Id, link.AnimalId, link.PropriedadeId, property.Nome,
                   property.Ativo, link.DataInicio, link.DataFim, link.Observacao, link.CreatedAtUtc, link.UpdatedAtUtc)).ToListAsync(ct);

    public Task AddVinculoAsync(VinculoAnimalPropriedade item, CancellationToken ct) =>
        context.VinculosAnimalPropriedade.AddAsync(item, ct).AsTask();

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_Propriedades_Nome_CaseInsensitive" })
        { throw new AnimalEvolutionException(409, "propriedade_duplicada", "Já existe uma Propriedade com este nome."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_VinculosAnimalPropriedade_Animal_Atual" })
        { throw new AnimalEvolutionException(409, "vinculo_desatualizado", "A Propriedade atual foi alterada. Atualize o histórico antes de tentar novamente."); }
    }
}
