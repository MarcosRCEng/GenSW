using GenSW.Application.Animals.Cruzamentos;
using GenSW.Application.Animals.Proles;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Animals.Proles;
public sealed class ProleRepository(GenSWDbContext context) : IProleRepository
{
 public Task AddAsync(Prole item,CancellationToken ct=default)=>context.Proles.AddAsync(item,ct).AsTask();
 public Task<Prole?> GetForUpdateAsync(Guid id,CancellationToken ct=default)=>context.Proles.SingleOrDefaultAsync(x=>x.Id==id,ct);
 public Task<ProleResult?> GetAsync(Guid id,CancellationToken ct=default)=>Read(context.Proles.AsNoTracking().Where(x=>x.Id==id)).SingleOrDefaultAsync(ct);
 public async Task<(IReadOnlyList<ProleResult> Items,int TotalItems)> ListAsync(ProleListQuery q,CancellationToken ct=default) { var x=context.Proles.AsNoTracking(); if(q.CicloReprodutivoId is{} c)x=x.Where(v=>v.CicloReprodutivoId==c); if(q.TipoRegistro is{} t)x=x.Where(v=>v.TipoRegistro==t); if(q.Origem is{} o)x=x.Where(v=>v.Origem==o); if(q.Convertida is{} a)x=x.Where(v=>(v.AnimalId!=null)==a); var total=await x.CountAsync(ct); return(await Read(x.OrderByDescending(v=>v.CreatedAtUtc)).Skip((q.Page-1)*q.PageSize).Take(q.PageSize).ToListAsync(ct),total); }
 public Task<CicloReprodutivo?> GetCicloForUpdateAsync(Guid id,CancellationToken ct=default)=>context.CiclosReprodutivos.SingleOrDefaultAsync(x=>x.Id==id,ct);
 public async Task<int> GetQuantidadeRegistradaAsync(Guid cicloId,CancellationToken ct=default)=>await context.Proles.AsNoTracking().Where(x=>x.CicloReprodutivoId==cicloId&&x.LoteOrigemId==null).SumAsync(x=>(int?)x.Quantidade,ct)??0;
 public Task<CruzamentoAnimalResumo?> GetPaiAsync(Guid cicloId,CancellationToken ct=default)=>Pais(cicloId,true).SingleOrDefaultAsync(ct);
 public Task<CruzamentoAnimalResumo?> GetMaeAsync(Guid cicloId,CancellationToken ct=default)=>Pais(cicloId,false).SingleOrDefaultAsync(ct);
 public Task SaveChangesAsync(CancellationToken ct=default)=>context.SaveChangesAsync(ct);
 private IQueryable<CruzamentoAnimalResumo> Pais(Guid cicloId,bool pai)=>from ciclo in context.CiclosReprodutivos.AsNoTracking() join cruzamento in context.Cruzamentos.AsNoTracking() on ciclo.CruzamentoId equals cruzamento.Id join animal in context.Animais.AsNoTracking() on (pai?cruzamento.MachoId:cruzamento.FemeaId) equals animal.Id where ciclo.Id==cicloId select new CruzamentoAnimalResumo(animal.Id,animal.CodigoInterno,animal.Nome);
 private static IQueryable<ProleResult> Read(IQueryable<Prole> x)=>x.Select(v=>new ProleResult(v.Id,v.CicloReprodutivoId,v.LoteOrigemId,v.TipoRegistro,v.Quantidade,v.QuantidadeDesdobrada,v.Origem,v.Data,v.PesoGramas,v.Sexo,v.Condicao,v.Observacao,v.AnimalId,v.CreatedAtUtc,v.UpdatedAtUtc));
}
