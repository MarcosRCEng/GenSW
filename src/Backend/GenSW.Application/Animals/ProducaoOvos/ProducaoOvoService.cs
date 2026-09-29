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
    public async Task<PagedProducaoOvoResult> ListAsync(Guid animalId, ProducaoOvoListQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.DataFinal < query.DataInicial) throw new ArgumentException("List query is invalid.");
        await EnsureEligible(animalId, ct); var page = await repository.ListAsync(animalId, query, ct); var all = await repository.ListAllAsync(animalId, query, ct); var standard = await repository.GetPesoPadraoAsync(animalId, ct);
        var ordered = all.OrderBy(x => x.DataPostura).ThenBy(x => x.CreatedAtUtc).ToArray();
        var days = standard is null || ordered.Length == 0 ? null : ordered.FirstOrDefault(x => x.PesoGramas >= standard)?.DataPostura.DayNumber - ordered[0].DataPostura.DayNumber;
        var metrics = new ProducaoOvoMetricasResult(ordered.Length, ordered.Length == 0 ? null : decimal.Round(ordered.Average(x => x.PesoGramas), 2), ordered.Length == 0 ? null : ordered.Min(x => x.PesoGramas), ordered.Length == 0 ? null : ordered.Max(x => x.PesoGramas), standard, days, ordered);
        return new(page.Items, query.Page, query.PageSize, page.TotalItems, page.TotalItems == 0 ? 0 : (int)Math.Ceiling(page.TotalItems / (double)query.PageSize), metrics);
    }
    private async Task EnsureEligible(Guid animalId, CancellationToken ct) { if (!await repository.IsEligibleAsync(animalId, ct)) throw new ProducaoOvoNotEligibleException(animalId); }
}
