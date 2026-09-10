namespace GenSW.Application.Animals;

public sealed record AnimalListPage(IReadOnlyList<AnimalReadModel> Items, int TotalItems);

public sealed record PagedAnimalResult(
    IReadOnlyList<AnimalResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);
