namespace GenSW.Application.Animals.Filiacoes;
public interface IFiliacaoAnimalService
{
 Task<FiliacaoAnimalResult> CreateOrReplaceAsync(Guid animalId, CreateFiliacaoAnimalCommand command, CancellationToken cancellationToken = default);
 Task<IReadOnlyList<FiliacaoAnimalResult>> ListAsync(Guid animalId, CancellationToken cancellationToken = default);
 Task<PedigreeAnimalResult> GetPedigreeAsync(Guid animalId, int generations, CancellationToken cancellationToken = default);
}
