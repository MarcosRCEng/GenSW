using GenSW.Application.Animals.Cruzamentos;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace GenSW.Infrastructure.Animals.Cruzamentos;
public sealed class CruzamentoRepository(GenSWDbContext context) : ICruzamentoRepository
{
 public Task AddAsync(Cruzamento item,CancellationToken ct=default)=>context.Cruzamentos.AddAsync(item,ct).AsTask();
 public Task<Cruzamento?> GetForUpdateAsync(Guid id,CancellationToken ct=default)=>context.Cruzamentos.SingleOrDefaultAsync(x=>x.Id==id,ct);
 public Task<CruzamentoResult?> GetAsync(Guid id,CancellationToken ct=default)=>Read(context.Cruzamentos.AsNoTracking().Where(x=>x.Id==id)).SingleOrDefaultAsync(ct);
 public async Task<(IReadOnlyList<CruzamentoResult> Items,int TotalItems)> ListAsync(CruzamentoListQuery q,CancellationToken ct=default) { var items=context.Cruzamentos.AsNoTracking(); if(q.Status is { } s)items=items.Where(x=>x.Status==s);if(q.MachoId is { } m)items=items.Where(x=>x.MachoId==m);if(q.FemeaId is { } f)items=items.Where(x=>x.FemeaId==f);var total=await items.CountAsync(ct);var list=await Read(items.OrderByDescending(x=>x.DataInicio).ThenByDescending(x=>x.CreatedAtUtc)).Skip((q.Page-1)*q.PageSize).Take(q.PageSize).ToListAsync(ct);return(list,total); }
 public Task<Animal?> GetAnimalAsync(Guid id,CancellationToken ct=default)=>context.Animais.SingleOrDefaultAsync(x=>x.Id==id,ct);
 public Task SaveChangesAsync(CancellationToken ct=default)=>context.SaveChangesAsync(ct);
 private IQueryable<CruzamentoResult> Read(IQueryable<Cruzamento> source) => from x in source join macho in context.Animais.AsNoTracking() on x.MachoId equals macho.Id join femea in context.Animais.AsNoTracking() on x.FemeaId equals femea.Id select new CruzamentoResult(x.Id,x.MachoId,x.FemeaId,x.Status,x.DataInicio,x.DataFim,x.Objetivo,x.Observacao,x.CreatedAtUtc,x.UpdatedAtUtc,new CruzamentoAnimalResumo(macho.Id,macho.CodigoInterno,macho.Nome),new CruzamentoAnimalResumo(femea.Id,femea.CodigoInterno,femea.Nome));
}
