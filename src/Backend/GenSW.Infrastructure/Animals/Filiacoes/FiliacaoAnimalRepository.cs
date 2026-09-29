using GenSW.Application.Animals.Filiacoes;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace GenSW.Infrastructure.Animals.Filiacoes;
public sealed class FiliacaoAnimalRepository(GenSWDbContext context) : IFiliacaoAnimalRepository
{
 public async Task<IFiliacaoAnimalMutationScope> BeginMutationAsync(CancellationToken ct=default) => new Scope(await context.Database.BeginTransactionAsync(ct));
 public Task<Animal?> LockAnimalAsync(Guid id,CancellationToken ct=default) => context.Animais.FromSqlInterpolated($"SELECT * FROM \"Animais\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
 public Task<Animal?> GetAnimalAsync(Guid id,CancellationToken ct=default) => context.Animais.SingleOrDefaultAsync(x=>x.Id==id,ct);
 public Task<FiliacaoAnimal?> GetActiveAsync(Guid a,TipoFiliacaoAnimal t,CancellationToken ct=default) => context.FiliacoesAnimal.SingleOrDefaultAsync(x=>x.AnimalId==a && x.TipoFiliacao==t && x.Ativa,ct);
 public Task<FiliacaoAnimal?> GetForUpdateAsync(Guid a,Guid id,CancellationToken ct=default) => context.FiliacoesAnimal.SingleOrDefaultAsync(x=>x.AnimalId==a&&x.Id==id,ct);
 public async Task<IReadOnlyList<FiliacaoAnimalResult>> ListAsync(Guid a,CancellationToken ct=default) => await context.FiliacoesAnimal.AsNoTracking().Where(x=>x.AnimalId==a).OrderByDescending(x=>x.Ativa).ThenByDescending(x=>x.CreatedAtUtc).Select(x=>ToResult(x)).ToListAsync(ct);
 public async Task<bool> WouldCreateCycleAsync(Guid animalId,Guid progenitorId,CancellationToken ct=default)
 {
  var visited=new HashSet<Guid>(); var pending=new Queue<Guid>(); pending.Enqueue(progenitorId);
  while(pending.TryDequeue(out var id)){ if(!visited.Add(id)) continue; if(id==animalId) return true; var parents=await context.FiliacoesAnimal.AsNoTracking().Where(x=>x.AnimalId==id&&x.Ativa).Select(x=>x.ProgenitorId).ToListAsync(ct); foreach(var p in parents) pending.Enqueue(p); } return false;
 }
 public Task AddAsync(FiliacaoAnimal x,CancellationToken ct=default)=>context.FiliacoesAnimal.AddAsync(x,ct).AsTask();
 public async Task<PedigreeAnimalResult?> GetPedigreeAsync(Guid id,int generations,CancellationToken ct=default) => await Build(id,generations,new HashSet<Guid>(),ct);
 private async Task<PedigreeAnimalResult?> Build(Guid id,int remaining,HashSet<Guid> path,CancellationToken ct){var a=await context.Animais.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);if(a is null)return null; if(remaining==0||!path.Add(id))return new(a.Id,a.CodigoInterno,a.Nome,[]);var ids=await context.FiliacoesAnimal.AsNoTracking().Where(x=>x.AnimalId==id&&x.Ativa).OrderBy(x=>x.TipoFiliacao).Select(x=>x.ProgenitorId).ToListAsync(ct);var ps=new List<PedigreeAnimalResult>();foreach(var p in ids){var r=await Build(p,remaining-1,path,ct);if(r is not null)ps.Add(r);}path.Remove(id);return new(a.Id,a.CodigoInterno,a.Nome,ps);}
 public Task SaveChangesAsync(CancellationToken ct=default)=>context.SaveChangesAsync(ct);
 private static FiliacaoAnimalResult ToResult(FiliacaoAnimal x)=>new(x.Id,x.AnimalId,x.ProgenitorId,x.TipoFiliacao,x.Ativa,x.DataRegistro,x.DataFim,x.CreatedAtUtc,x.UpdatedAtUtc);
 private sealed class Scope(IDbContextTransaction tx):IFiliacaoAnimalMutationScope{bool committed;public async Task CommitAsync(CancellationToken ct=default){await tx.CommitAsync(ct);committed=true;}public async ValueTask DisposeAsync(){if(!committed)await tx.RollbackAsync();await tx.DisposeAsync();}}
}
