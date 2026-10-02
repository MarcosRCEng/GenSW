using System.Data;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Filiacoes;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace GenSW.Infrastructure.Animals.Filiacoes;

public sealed class ProgenitorQuery(GenSWDbContext context) : IProgenitorQuery
{
    public async Task<AnimalEvolutionPage<ProgenitorCandidate>> SearchAsync(Guid animalId, TipoFiliacaoAnimal type, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (!Enum.IsDefined(type) || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue || search?.Length > 200)
            throw new ArgumentException("Consulta de progenitores inválida.");
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '3s'", ct);
        var child = await context.Animais.AsNoTracking().SingleOrDefaultAsync(x => x.Id == animalId, ct)
            ?? throw new AnimalEvolutionException(404, "animal_nao_encontrado", "Animal não encontrado.");
        var descendants = Descendants(context, animalId);
        var candidates = context.Animais.AsNoTracking().Where(ProgenitorEligibility.Predicate(child, type))
            .Where(x => !descendants.Contains(x.Id) && !context.FiliacoesAnimal.Any(f => f.Ativa &&
                f.AnimalId == animalId && f.TipoFiliacao != type && f.ProgenitorId == x.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            candidates = candidates.Where(x => EF.Functions.ILike(x.CodigoInterno, pattern, "\\") || (x.Nome != null && EF.Functions.ILike(x.Nome, pattern, "\\")));
        }
        var count = await candidates.CountAsync(ct);
        var items = await candidates.OrderBy(x => x.CodigoInterno).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProgenitorCandidate(x.Id, x.CodigoInterno, x.Nome, x.Sexo, x.EspecieId, x.RacaId, x.Ativo)).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(items, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }
    internal static IQueryable<Guid> Descendants(GenSWDbContext context, Guid id) => context.Database.SqlQuery<Guid>($"""
        WITH RECURSIVE descendants AS (
            SELECT {id}::uuid AS "Value"
            UNION
            SELECT f."AnimalId" FROM "FiliacoesAnimal" f
            JOIN descendants d ON f."ProgenitorId" = d."Value" WHERE f."Ativa"
        ) SELECT "Value" FROM descendants
        """);
}
