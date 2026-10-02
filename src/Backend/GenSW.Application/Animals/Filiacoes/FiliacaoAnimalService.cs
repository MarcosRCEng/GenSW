using GenSW.Application.Animals;
using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.Filiacoes;
public sealed class FiliacaoAnimalService(IFiliacaoAnimalRepository repository, TimeProvider timeProvider) : IFiliacaoAnimalService
{
 public async Task<FiliacaoAnimalResult> CreateOrReplaceAsync(Guid animalId, CreateFiliacaoAnimalCommand command, CancellationToken ct = default)
 {
  ArgumentNullException.ThrowIfNull(command);
  if (!Enum.IsDefined(command.TipoFiliacao)) throw new ArgumentException("Tipo de filiação inválido.");
  await using var tx = await repository.BeginMutationAsync(ct);
  var child = await repository.LockAnimalAsync(animalId, ct) ?? throw new AnimalNotFoundException(animalId);
  var progenitor = await repository.GetAnimalAsync(command.ProgenitorId, ct) ?? throw new AnimalNotFoundException(command.ProgenitorId);
  if (!ProgenitorEligibility.Predicate(child, command.TipoFiliacao).Compile()(progenitor))
      throw new FiliacaoAnimalConflictException("Progenitor deve estar ativo, ter sexo e espécie compatíveis e a mesma raça quando informada no descendente.");
  if (await repository.WouldCreateCycleAsync(animalId, command.ProgenitorId, ct)) throw new FiliacaoAnimalConflictException("Filiation would create a genealogical cycle.");
  var current = await repository.GetActiveAsync(animalId, command.TipoFiliacao, ct);
  var other = await repository.GetActiveAsync(animalId, command.TipoFiliacao == TipoFiliacaoAnimal.Pai ? TipoFiliacaoAnimal.Mae : TipoFiliacaoAnimal.Pai, ct);
  if (other?.ProgenitorId == command.ProgenitorId) throw new FiliacaoAnimalConflictException("The same progenitor cannot be active as father and mother.");
  var now = timeProvider.GetUtcNow(); if (current is not null) { current.Inativar(command.DataRegistro, now); await repository.SaveChangesAsync(ct); }
  var item = FiliacaoAnimal.Criar(animalId, command.ProgenitorId, command.TipoFiliacao, command.DataRegistro, now); await repository.AddAsync(item, ct); await repository.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToResult(item);
 }
 public async Task<IReadOnlyList<FiliacaoAnimalResult>> ListAsync(Guid animalId, CancellationToken ct = default) { if (await repository.GetAnimalAsync(animalId, ct) is null) throw new AnimalNotFoundException(animalId); return await repository.ListAsync(animalId, ct); }
 public async Task<PedigreeAnimalResult> GetPedigreeAsync(Guid animalId, int generations, CancellationToken ct = default) { if (generations is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(generations)); return await repository.GetPedigreeAsync(animalId, generations, ct) ?? throw new AnimalNotFoundException(animalId); }
 private static FiliacaoAnimalResult ToResult(FiliacaoAnimal x) => new(x.Id,x.AnimalId,x.ProgenitorId,x.TipoFiliacao,x.Ativa,x.DataRegistro,x.DataFim,x.CreatedAtUtc,x.UpdatedAtUtc);
}
