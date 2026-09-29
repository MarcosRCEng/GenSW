using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.CiclosReprodutivos;
public sealed record CicloReprodutivoCommand(TipoCicloReprodutivo Tipo, StatusCicloReprodutivo Status, DateOnly? DataPostura, DateOnly? DataInicioIncubacao, DateOnly? DataEclosao, int? OvosPostos, int? OvosFerteis, int? OvosIncubados, int? OvosEclodidos, int? OvosInviaveis, decimal? PesoMedioOvoGramas, DateOnly? DataInicioGestacao, DateOnly? DataPrevistaParto, DateOnly? DataParto, int? Nascidos, int? NascidosVivos, int? NascidosMortos, decimal? PesoAoNascerGramas, string? Observacao);
public sealed record CicloReprodutivoStatusCommand(StatusCicloReprodutivo Status);
public sealed record CicloReprodutivoResult(Guid Id, Guid CruzamentoId, TipoCicloReprodutivo Tipo, StatusCicloReprodutivo Status, DateOnly? DataPostura, DateOnly? DataInicioIncubacao, DateOnly? DataEclosao, int? OvosPostos, int? OvosFerteis, int? OvosIncubados, int? OvosEclodidos, int? OvosInviaveis, decimal? PesoMedioOvoGramas, DateOnly? DataInicioGestacao, DateOnly? DataPrevistaParto, DateOnly? DataParto, int? Nascidos, int? NascidosVivos, int? NascidosMortos, decimal? PesoAoNascerGramas, string? Observacao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc)
{
    public int? DuracaoIncubacaoDias => DataInicioIncubacao is { } inicio && DataEclosao is { } fim ? fim.DayNumber - inicio.DayNumber : null;
    public int? DuracaoGestacaoDias => DataInicioGestacao is { } inicio && DataParto is { } fim ? fim.DayNumber - inicio.DayNumber : null;
    public decimal? TaxaFertilidade => OvosPostos is > 0 && OvosFerteis is { } ferteis ? decimal.Round(ferteis * 100m / OvosPostos.Value, 2) : null;
    public decimal? TaxaEclosao => OvosFerteis is > 0 && OvosEclodidos is { } eclodidos ? decimal.Round(eclodidos * 100m / OvosFerteis.Value, 2) : null;
}
public sealed record CicloReprodutivoListQuery(int Page = 1, int PageSize = 25, Guid? CruzamentoId = null, TipoCicloReprodutivo? Tipo = null, StatusCicloReprodutivo? Status = null);
public sealed record PagedCicloReprodutivoResult(IReadOnlyList<CicloReprodutivoResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed class CicloReprodutivoNotFoundException(Guid id) : KeyNotFoundException($"Reproductive cycle {id} was not found.");
