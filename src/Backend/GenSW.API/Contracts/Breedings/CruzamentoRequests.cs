using GenSW.Domain.Animals;
namespace GenSW.API.Contracts.Breedings;
public sealed record CreateCruzamentoRequest(Guid MachoId, Guid FemeaId, StatusCruzamento Status, DateOnly? DataInicio, DateOnly? DataFim, string? Objetivo, string? Observacao);
public sealed record UpdateCruzamentoRequest(Guid MachoId, Guid FemeaId, StatusCruzamento Status, DateOnly? DataInicio, DateOnly? DataFim, string? Objetivo, string? Observacao);
public sealed record UpdateCruzamentoStatusRequest(StatusCruzamento Status);
