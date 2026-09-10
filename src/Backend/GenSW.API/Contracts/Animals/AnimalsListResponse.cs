namespace GenSW.API.Contracts.Animals;

public sealed record AnimalsListResponse(
    IReadOnlyList<AnimalResponse> Items,
    int Page, int PageSize, int TotalItems, int TotalPages);
