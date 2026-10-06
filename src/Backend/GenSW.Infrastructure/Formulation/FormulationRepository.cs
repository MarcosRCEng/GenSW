using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GenSW.Infrastructure.Formulation;

public sealed class FormulationRepository(GenSWDbContext db) : IFormulationRepository
{
    public async Task<IFormulationMutation> BeginAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'; SELECT pg_advisory_xact_lock(413, 421);", ct);
            return new Mutation(transaction);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private sealed class Mutation(IDbContextTransaction transaction) : IFormulationMutation
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new FormulationException(409, "registro_duplicado", "Código, nome ou versão já utilizado. Recarregue o registro."); }
    }
    public void Add(Item v) => db.Add(v);
    public void Add(CategoriaItem v) => db.Add(v);
    public void Add(ConversaoItem v) => db.Add(v);
    public void Add(CatalogAudit v) => db.Add(v);
    public void Add(NutritionProfile v) => db.Add(v);
    public void Add(Recipe v) => db.Add(v);
    public void Add(RecipeVersion v) => db.Add(v);
    public void Add(RecipeReference v) => db.Add(v);
    public void Add(FormulationSnapshot v) => db.Add(v);
    private IQueryable<T> Query<T>(bool tracking = false) where T : class => tracking ? db.Set<T>() : db.Set<T>().AsNoTracking();
    public Task<Item?> ItemAsync(Guid id, bool tracking, CancellationToken ct) => Query<Item>(tracking).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<CategoriaItem?> CategoryAsync(Guid id, bool tracking, CancellationToken ct) => Query<CategoriaItem>(tracking).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ConversaoItem?> ConversionAsync(Guid id, CancellationToken ct) => Query<ConversaoItem>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<NutritionProfile?> ProfileAsync(Guid id, bool tracking, CancellationToken ct) => Query<NutritionProfile>(tracking).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Recipe?> RecipeAsync(Guid id, bool tracking, CancellationToken ct) => Query<Recipe>(tracking).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<RecipeVersion?> VersionAsync(Guid id, bool tracking, CancellationToken ct) => Query<RecipeVersion>(tracking).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> SpeciesExistsAsync(Guid id, CancellationToken ct) => db.Especies.AnyAsync(x => x.Id == id, ct);
    public async Task<int> NextConversionAsync(Guid item, CancellationToken ct) => (await Query<ConversaoItem>().Where(x => x.ItemId == item).MaxAsync(x => (int?)x.Numero, ct) ?? 0) + 1;
    public async Task<int> NextProfileAsync(Guid item, CancellationToken ct) => (await Query<NutritionProfile>().Where(x => x.ItemId == item).MaxAsync(x => (int?)x.Numero, ct) ?? 0) + 1;
    public async Task<int> NextVersionAsync(Guid recipe, CancellationToken ct) => (await Query<RecipeVersion>().Where(x => x.ReceitaId == recipe).MaxAsync(x => (int?)x.Numero, ct) ?? 0) + 1;
    private static string Pattern(string search) => "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    private static async Task<CatalogPage<T>> Page<T>(IQueryable<T> query, CatalogQuery q, CancellationToken ct)
    {
        q.Validate(); var count = await query.CountAsync(ct);
        return new(await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct), q.Page, q.PageSize, count, (int)Math.Ceiling(count / (double)q.PageSize));
    }
    public Task<CatalogPage<Item>> ItemsAsync(CatalogQuery q, CancellationToken ct)
    {
        q.Validate(); var rows = Query<Item>();
        if (q.Ativo is { } active) rows = rows.Where(x => x.Ativo == active);
        if (q.Classe is { } material) rows = rows.Where(x => x.Classe == material);
        if (q.CategoriaId is { } category) rows = rows.Where(x => x.CategoriaId == category);
        rows = q.Capacidade switch { "entrada" => rows.Where(x => x.PodeEntrar), "producao" => rows.Where(x => x.PodeProduzir), "interno" => rows.Where(x => x.UsoInterno), "venda" => rows.Where(x => x.Venda), _ => rows };
        if (!string.IsNullOrWhiteSpace(q.Search)) { var p = Pattern(q.Search); rows = rows.Where(x => EF.Functions.ILike(x.Nome, p, "\\") || EF.Functions.ILike(x.Codigo, p, "\\")); }
        IOrderedQueryable<Item> ordered = (q.SortBy, q.SortDirection) switch
        {
            ("codigo", "asc") => rows.OrderBy(x => x.Codigo),
            ("codigo", "desc") => rows.OrderByDescending(x => x.Codigo),
            ("createdAtUtc", "asc") => rows.OrderBy(x => x.CreatedAtUtc),
            ("createdAtUtc", "desc") => rows.OrderByDescending(x => x.CreatedAtUtc),
            (_, "desc") => rows.OrderByDescending(x => x.Nome),
            _ => rows.OrderBy(x => x.Nome)
        };
        return Page(ordered.ThenBy(x => x.Id), q, ct);
    }
    public Task<CatalogPage<CategoriaItem>> CategoriesAsync(CatalogQuery q, CancellationToken ct)
    {
        var rows = Query<CategoriaItem>();
        if (q.Ativo is { } a) rows = rows.Where(x => x.Ativo == a);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var p = Pattern(q.Search); rows = rows.Where(x => EF.Functions.ILike(x.Nome, p, "\\")); }
        return Page(q.SortDirection == "desc" ? rows.OrderByDescending(x => x.Nome).ThenBy(x => x.Id) : rows.OrderBy(x => x.Nome).ThenBy(x => x.Id), q, ct);
    }
    public Task<CatalogPage<ConversaoItem>> ConversionsAsync(Guid item, CatalogQuery q, CancellationToken ct) => Page(Query<ConversaoItem>().Where(x => x.ItemId == item).OrderByDescending(x => x.Numero).ThenBy(x => x.Id), q, ct);
    public Task<CatalogPage<NutritionProfile>> ProfilesAsync(Guid item, CatalogQuery q, CancellationToken ct)
    {
        var rows = Query<NutritionProfile>().Where(x => x.ItemId == item);
        if (q.Estado is not null) rows = rows.Where(x => x.Estado == q.Estado);
        return Page(rows.OrderByDescending(x => x.Numero).ThenBy(x => x.Id), q, ct);
    }
    public Task<CatalogPage<Recipe>> RecipesAsync(CatalogQuery q, CancellationToken ct)
    {
        var rows = Query<Recipe>();
        if (q.Ativo is { } a) rows = rows.Where(x => x.Ativo == a);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var p = Pattern(q.Search); rows = rows.Where(x => EF.Functions.ILike(x.Nome, p, "\\") || EF.Functions.ILike(x.Codigo, p, "\\")); }
        IOrderedQueryable<Recipe> ordered = (q.SortBy, q.SortDirection) switch
        {
            ("codigo", "asc") => rows.OrderBy(x => x.Codigo),
            ("codigo", "desc") => rows.OrderByDescending(x => x.Codigo),
            ("createdAtUtc", "asc") => rows.OrderBy(x => x.CreatedAtUtc),
            ("createdAtUtc", "desc") => rows.OrderByDescending(x => x.CreatedAtUtc),
            (_, "desc") => rows.OrderByDescending(x => x.Nome),
            _ => rows.OrderBy(x => x.Nome)
        };
        return Page(ordered.ThenBy(x => x.Id), q, ct);
    }
    public Task<CatalogPage<RecipeVersion>> VersionsAsync(Guid? recipe, CatalogQuery q, CancellationToken ct)
    {
        var rows = Query<RecipeVersion>();
        if (recipe.HasValue) rows = rows.Where(x => x.ReceitaId == recipe);
        if (q.Estado is not null) rows = rows.Where(x => x.Estado == q.Estado);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var p = Pattern(q.Search); rows = rows.Where(v => db.Set<Recipe>().Any(r => r.Id == v.ReceitaId && EF.Functions.ILike(r.Nome, p, "\\"))); }
        if (q.Ativo is { } active) rows = rows.Where(v => db.Set<Recipe>().Any(r => r.Id == v.ReceitaId && r.Ativo == active));
        return Page(rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id), q, ct);
    }
    public Task<CatalogPage<CatalogAudit>> HistoryAsync(Guid id, CatalogQuery q, CancellationToken ct) => Page(Query<CatalogAudit>().Where(x => x.RegistroId == id).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id), q, ct);
    public Task<FormulationSnapshot?> SnapshotAsync(Guid id, CancellationToken ct) => Query<FormulationSnapshot>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<FormulationSnapshot?> ReplayAsync(Guid actor, string type, string key, CancellationToken ct) => Query<FormulationSnapshot>().SingleOrDefaultAsync(x => x.AutorId == actor && x.Tipo == type && x.Chave == key, ct);
    public Task<CatalogPage<SnapshotSummary>> SnapshotsAsync(string type, CatalogQuery q, CancellationToken ct) => Page(Query<FormulationSnapshot>().Where(x => x.Tipo == type).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Select(x => new SnapshotSummary(x.Id, x.Tipo, x.CreatedAtUtc)), q, ct);
}
