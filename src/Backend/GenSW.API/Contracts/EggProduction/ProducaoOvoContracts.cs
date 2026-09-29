namespace GenSW.API.Contracts.EggProduction;
public sealed record ProducaoOvoRequest(DateOnly DataPostura, decimal PesoGramas, string? Observacao);
public sealed record ProducaoOvoResponse(Guid Id, Guid AnimalId, DateOnly DataPostura, decimal PesoGramas, string? Observacao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record ProducaoOvoMetricasResponse(int TotalLancamentos, decimal? PesoMedioGramas, decimal? PesoMinimoGramas, decimal? PesoMaximoGramas, decimal? PesoPadraoGramas, int? DiasAtePesoPadrao, IReadOnlyList<ProducaoOvoResponse> Evolucao);
public sealed record ProducoesOvosListResponse(IReadOnlyList<ProducaoOvoResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages, ProducaoOvoMetricasResponse Metricas);
