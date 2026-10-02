using System.Data;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Tree;
using GenSW.Application.Images;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace GenSW.Infrastructure.Animals;

public sealed class AnimalTreeQuery(GenSWDbContext context, IPrivateImageStorage storage) : IAnimalTreeQuery
{
    private sealed class EdgeRow
    {
        public Guid Id { get; init; }
        public Guid Parent { get; init; }
        public Guid Child { get; init; }
        public TipoFiliacaoAnimal Type { get; init; }
        public string Code { get; init; } = "";
        public Guid Neighbor { get; init; }
        public bool Inconsistent { get; init; }
    }
    private IQueryable<EdgeRow> Edges(Guid[] frontier, bool up) =>
        from f in context.FiliacoesAnimal.AsNoTracking()
        join parent in context.Animais.AsNoTracking() on f.ProgenitorId equals parent.Id
        join child in context.Animais.AsNoTracking() on f.AnimalId equals child.Id
        where f.Ativa && (up ? frontier.Contains(f.AnimalId) : frontier.Contains(f.ProgenitorId))
        select new EdgeRow { Id = f.Id, Parent = f.ProgenitorId, Child = f.AnimalId, Type = f.TipoFiliacao,
            Code = up ? parent.CodigoInterno : child.CodigoInterno, Neighbor = up ? parent.Id : child.Id,
            Inconsistent = parent.EspecieId != child.EspecieId || (child.RacaId != null && parent.RacaId != child.RacaId) ||
                (f.TipoFiliacao == TipoFiliacaoAnimal.Pai ? parent.Sexo != SexoAnimal.Macho : parent.Sexo != SexoAnimal.Femea) };

    public async Task<TreeResult> GetAsync(Guid root, int ancestors, int descendants, CancellationToken ct)
    {
        if (ancestors is < 0 or > 4 || descendants is < 0 or > 2) throw new ArgumentException("Profundidades permitidas: ascendentes 0–4 e descendentes 0–2.");
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '3s'", ct);
        await RequireRoot(root, ct);
        var ids = new HashSet<Guid> { root }; var edges = new Dictionary<Guid, EdgeRow>(); var truncated = false;
        async Task Traverse(bool up, int depth)
        {
            var frontier = new[] { root }; var expanded = new HashSet<Guid>();
            for (var level = 0; level < depth && frontier.Length > 0; level++)
            {
                foreach (var id in frontier) expanded.Add(id);
                var rows = await Edges(frontier, up).OrderBy(x => x.Code).ThenBy(x => x.Neighbor).ThenBy(x => x.Id)
                    .Take(201).ToListAsync(ct);
                if (rows.Count > 200) truncated = true;
                var next = new HashSet<Guid>();
                foreach (var row in rows.Take(200))
                {
                    if (edges.ContainsKey(row.Id)) continue;
                    if (edges.Count >= 200 || (!ids.Contains(row.Neighbor) && ids.Count >= 100)) { truncated = true; continue; }
                    ids.Add(row.Neighbor); edges.Add(row.Id, row);
                    if (!expanded.Contains(row.Neighbor)) next.Add(row.Neighbor);
                }
                frontier = next.ToArray();
            }
        }
        await Traverse(true, ancestors); await Traverse(false, descendants);
        var rows = edges.Values.ToArray();
        var nodes = await Nodes(ids.ToArray(), rows, ct);
        await tx.CommitAsync(ct);
        return new(root, nodes, rows.Select(ToEdge).ToArray(), new(ancestors, descendants), truncated, Warnings(rows));
    }
    public async Task<TreeRelationsResult> RelationsAsync(Guid root, string direction, int page, int pageSize, CancellationToken ct)
    {
        if (direction is not ("ascendentes" or "descendentes") || page < 1 || pageSize is < 1 or > 50 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("Direção ou página inválida.");
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '3s'", ct); await RequireRoot(root, ct);
        var query = Edges([root], direction == "ascendentes");
        var count = await query.CountAsync(ct);
        var ordered = direction == "ascendentes" ? query.OrderBy(x => x.Type).ThenBy(x => x.Id) : query.OrderBy(x => x.Code).ThenBy(x => x.Neighbor).ThenBy(x => x.Id);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var nodes = await Nodes(rows.Select(x => x.Neighbor).Append(root).Distinct().ToArray(), rows, ct);
        await tx.CommitAsync(ct);
        return new(root, nodes, rows.Select(ToEdge).ToArray(), page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize), Warnings(rows));
    }
    private async Task RequireRoot(Guid id, CancellationToken ct)
    {
        if (!await context.Animais.AnyAsync(x => x.Id == id, ct)) throw new AnimalEvolutionException(404, "animal_nao_encontrado", "Animal não encontrado.");
    }
    private async Task<IReadOnlyList<TreeNode>> Nodes(Guid[] ids, EdgeRow[] edges, CancellationToken ct)
    {
        var edgeIds = edges.Select(x => x.Id).ToArray();
        var data = await context.Animais.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.CodigoInterno).ThenBy(x => x.Id)
            .Select(x => new {
                Animal = x,
                Father = context.FiliacoesAnimal.Any(f => f.Ativa && f.AnimalId == x.Id && f.TipoFiliacao == TipoFiliacaoAnimal.Pai),
                Mother = context.FiliacoesAnimal.Any(f => f.Ativa && f.AnimalId == x.Id && f.TipoFiliacao == TipoFiliacaoAnimal.Mae),
                MoreParents = context.FiliacoesAnimal.Any(f => f.Ativa && f.AnimalId == x.Id && !edgeIds.Contains(f.Id)),
                MoreChildren = context.FiliacoesAnimal.Any(f => f.Ativa && f.ProgenitorId == x.Id && !edgeIds.Contains(f.Id))
            }).ToListAsync(ct);
        var images = await context.ImagensAnimal.AsNoTracking().Where(x => ids.Contains(x.AnimalId) && x.Ativa).ToListAsync(ct);
        var lookup = images.ToLookup(x => x.AnimalId);
        return data.Select(x => {
            var photo = ImageService.SelectPreferred(ImageOwner.Animal, x.Animal.Id, lookup[x.Animal.Id], storage);
            return new TreeNode(x.Animal.Id, x.Animal.CodigoInterno, x.Animal.Nome, x.Animal.Sexo, x.Animal.Ativo,
                x.Animal.DataNascimento, photo.Imagem, photo.Origem, x.Father, x.Mother, x.MoreParents, x.MoreChildren);
        }).ToArray();
    }
    private static TreeEdge ToEdge(EdgeRow row) => new(row.Id, row.Parent, row.Child, row.Type);
    private static IReadOnlyList<string> Warnings(EdgeRow[] rows)
    {
        var warnings = new List<string>();
        if (rows.Any(x => x.Inconsistent)) warnings.Add("filiacao_legada_inconsistente");
        var links = rows.ToLookup(x => x.Parent, x => x.Child);
        var done = new HashSet<Guid>(); var path = new HashSet<Guid>();
        bool Cycle(Guid id)
        {
            if (path.Contains(id)) return true;
            if (!done.Add(id)) return false;
            path.Add(id); foreach (var child in links[id]) if (Cycle(child)) return true;
            path.Remove(id); return false;
        }
        if (rows.Any(x => Cycle(x.Parent))) warnings.Add("ciclo_legado_detectado");
        return warnings;
    }
}
