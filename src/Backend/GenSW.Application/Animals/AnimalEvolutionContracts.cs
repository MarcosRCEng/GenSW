namespace GenSW.Application.Animals;

public sealed class AnimalEvolutionException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public interface IAnimalMutationScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}

public interface IAnimalMutationGuard
{
    Task<IAnimalMutationScope> BeginAsync(Guid id, CancellationToken ct);
    Task ValidateAsync(Domain.Animals.Animal animal, UpdateAnimalCommand command, CancellationToken ct);
}

public sealed record AnimalEvolutionPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
