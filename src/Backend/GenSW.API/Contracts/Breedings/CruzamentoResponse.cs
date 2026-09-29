using GenSW.Domain.Animals;
namespace GenSW.API.Contracts.Breedings;
public sealed record CruzamentoAnimalResumoResponse(Guid Id, string CodigoInterno, string? Nome);
public sealed record CruzamentoResponse(Guid Id, Guid MachoId, Guid FemeaId, StatusCruzamento Status, DateOnly? DataInicio, DateOnly? DataFim, string? Objetivo, string? Observacao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, CruzamentoAnimalResumoResponse Macho, CruzamentoAnimalResumoResponse Femea);
public sealed record CruzamentosListResponse(IReadOnlyList<CruzamentoResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
