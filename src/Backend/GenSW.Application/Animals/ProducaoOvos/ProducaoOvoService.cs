using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.ProducaoOvos;
public sealed class ProducaoOvoService(IProducaoOvoRepository repository, TimeProvider timeProvider) : IProducaoOvoService
{
    public async Task<ProducaoOvoResult> CreateAsync(Guid animalId, ProducaoOvoCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command); await EnsureEligible(animalId, ct);
        var item = ProducaoOvo.Criar(animalId, command.DataPostura, command.PesoGramas, command.Observacao, timeProvider.GetUtcNow());
        await repository.AddAsync(item, ct); await repository.SaveChangesAsync(ct); return (await repository.GetAsync(item.Id, ct))!;
    }
    public async Task<ProducaoOvoResult> UpdateAsync(Guid animalId, Guid id, ProducaoOvoCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command); var item = await repository.GetForUpdateAsync(id, ct) ?? throw new ProducaoOvoNotFoundException(id); if (item.AnimalId != animalId) throw new ProducaoOvoNotFoundException(id);
        await EnsureEligible(item.AnimalId, ct); item.Atualizar(command.DataPostura, command.PesoGramas, command.Observacao, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct); return (await repository.GetAsync(id, ct))!;
    }
    public async Task DeleteAsync(Guid animalId, Guid id, CancellationToken ct = default)
    {
        var item = await repository.GetForUpdateAsync(id, ct) ?? throw new ProducaoOvoNotFoundException(id);
        if (item.AnimalId != animalId) throw new ProducaoOvoNotFoundException(id);
        await repository.DeleteAsync(item, ct);
    }
    public async Task<PagedProducaoOvoResult> ListAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.DataFinal < query.DataInicial) throw new ArgumentException("List query is invalid.");
        await EnsureEligible(animalId, ct);
        var standard = await repository.GetPesoPadraoAsync(animalId, ct);
        var metrics = query.IncludeHistory
            ? CalculateHistoryMetrics(await repository.ListAllAsync(animalId, query, ct), standard)
            : CalculateSummaryMetrics(await repository.GetResumoAsync(animalId, query, standard, ct), standard);

        var total = metrics.TotalLancamentos;
        var totalPages = (int)Math.Ceiling(total / (double)query.PageSize);
        var items = query.Page <= totalPages ? await repository.ListAsync(animalId, query, ct) : [];
        return new(items, query.Page, query.PageSize, total, totalPages, metrics);
    }

    private static ProducaoOvoMetricasResult CalculateHistoryMetrics(IReadOnlyList<ProducaoOvoResult> history, decimal? standard)
    {
        if (history.Count == 0) return new(0, null, null, null, standard, null, history);

        var days = history.FirstOrDefault(x => x.PesoGramas >= standard)?.DataPostura.DayNumber - history[0].DataPostura.DayNumber;
        return new(history.Count, decimal.Round(history.Average(x => x.PesoGramas), 2),
            history.Min(x => x.PesoGramas), history.Max(x => x.PesoGramas), standard, days, history);
    }

    private static ProducaoOvoMetricasResult CalculateSummaryMetrics(ProducaoOvoResumo? summary, decimal? standard)
    {
        if (summary is null) return new(0, null, null, null, standard, null, null);

        var average = decimal.Round(summary.PesoTotalGramas / summary.TotalLancamentos, 2);
        var days = summary.PrimeiraDataPesoPadrao?.DayNumber - summary.PrimeiraDataPostura.DayNumber;
        return new(summary.TotalLancamentos, average, summary.PesoMinimoGramas, summary.PesoMaximoGramas, standard, days, null);
    }

    private async Task EnsureEligible(Guid animalId, CancellationToken ct) { if (!await repository.IsEligibleAsync(animalId, ct)) throw new ProducaoOvoNotEligibleException(animalId); }
}
