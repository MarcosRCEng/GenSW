namespace GenSW.Application.Animals.ProducaoOvos;

public sealed record ProducaoOvoCommand(DateOnly DataPostura, decimal PesoGramas, string? Observacao);
public sealed record ProducaoOvoResult(Guid Id, Guid AnimalId, DateOnly DataPostura, decimal PesoGramas, string? Observacao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record ProducaoOvoListQuery(int Page = 1, int PageSize = 25, DateOnly? DataInicial = null, DateOnly? DataFinal = null, bool IncludeHistory = true);
public sealed record ProducaoOvoMetricasResult(int TotalLancamentos, decimal? PesoMedioGramas, decimal? PesoMinimoGramas, decimal? PesoMaximoGramas, decimal? PesoPadraoGramas, int? DiasAtePesoPadrao, IReadOnlyList<ProducaoOvoResult>? Evolucao);
public sealed record ProducaoOvoResumo(
    int TotalLancamentos, decimal PesoTotalGramas, decimal PesoMinimoGramas, decimal PesoMaximoGramas,
    DateOnly PrimeiraDataPostura, DateOnly? PrimeiraDataPesoPadrao);
public sealed record PagedProducaoOvoResult(IReadOnlyList<ProducaoOvoResult> Items, int Page, int PageSize, int TotalItems, int TotalPages, ProducaoOvoMetricasResult Metricas);
public sealed class ProducaoOvoNotFoundException(Guid id) : KeyNotFoundException($"Egg production entry {id} was not found.");
public sealed class ProducaoOvoNotEligibleException(Guid animalId) : InvalidOperationException($"Animal {animalId} is not eligible for egg production.");
public sealed class ProducaoOvoLinkedException() : InvalidOperationException("Egg production entry is linked to egg reconciliation or inventory.");
