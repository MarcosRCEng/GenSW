using GenSW.Application.Animals.CiclosReprodutivos;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace GenSW.Infrastructure.Animals.CiclosReprodutivos;
public sealed class CicloReprodutivoRepository(GenSWDbContext context) : ICicloReprodutivoRepository
{
 public Task AddAsync(CicloReprodutivo x,CancellationToken ct=default)=>context.CiclosReprodutivos.AddAsync(x,ct).AsTask();
 public Task<CicloReprodutivo?> GetForUpdateAsync(Guid id,CancellationToken ct=default)=>context.CiclosReprodutivos.SingleOrDefaultAsync(x=>x.Id==id,ct);
 public Task<CicloReprodutivoResult?> GetAsync(Guid id,CancellationToken ct=default)=>Read(context.CiclosReprodutivos.AsNoTracking().Where(x=>x.Id==id)).SingleOrDefaultAsync(ct);
 public async Task<(IReadOnlyList<CicloReprodutivoResult> Items,int TotalItems)> ListAsync(CicloReprodutivoListQuery q,CancellationToken ct=default) { var x=context.CiclosReprodutivos.AsNoTracking();if(q.CruzamentoId is {} c)x=x.Where(v=>v.CruzamentoId==c);if(q.Tipo is {} t)x=x.Where(v=>v.Tipo==t);if(q.Status is {} s)x=x.Where(v=>v.Status==s);var total=await x.CountAsync(ct);return(await Read(x.OrderByDescending(v=>v.CreatedAtUtc)).Skip((q.Page-1)*q.PageSize).Take(q.PageSize).ToListAsync(ct),total); }
 public Task<bool> CruzamentoExistsAsync(Guid id,CancellationToken ct=default)=>context.Cruzamentos.AnyAsync(x=>x.Id==id,ct);
 public Task SaveChangesAsync(CancellationToken ct=default)=>context.SaveChangesAsync(ct);
 private static IQueryable<CicloReprodutivoResult> Read(IQueryable<CicloReprodutivo> x)=>x.Select(v=>new CicloReprodutivoResult(v.Id,v.CruzamentoId,v.Tipo,v.Status,v.DataPostura,v.DataInicioIncubacao,v.DataEclosao,v.OvosPostos,v.OvosFerteis,v.OvosIncubados,v.OvosEclodidos,v.OvosInviaveis,v.PesoMedioOvoGramas,v.DataInicioGestacao,v.DataPrevistaParto,v.DataParto,v.Nascidos,v.NascidosVivos,v.NascidosMortos,v.PesoAoNascerGramas,v.Observacao,v.CreatedAtUtc,v.UpdatedAtUtc));
}
