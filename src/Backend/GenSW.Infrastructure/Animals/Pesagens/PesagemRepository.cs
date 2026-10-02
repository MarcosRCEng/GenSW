using GenSW.Application.Animals;
using GenSW.Application.Animals.Pesagens;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Animals.Pesagens;

public sealed class PesagemRepository(GenSWDbContext context) : IPesagemRepository
{
    public async Task<IAnimalMutationScope> BeginAsync(Guid id, CancellationToken ct) => await AnimalMutationScope.BeginAsync(context, id, false, ct);
    public Task<Animal?> AnimalAsync(Guid id, CancellationToken ct) => context.Animais.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PesagemAnimal?> GetAsync(Guid animalId, Guid id, CancellationToken ct) => context.PesagensAnimal.SingleOrDefaultAsync(x => x.AnimalId == animalId && x.Id == id, ct);
    public async Task<(IReadOnlyList<PesagemAnimal> Items, int Total)> ListAsync(Guid id, PesagemQuery query, CancellationToken ct)
    {
        var items = context.PesagensAnimal.AsNoTracking().Where(x => x.AnimalId == id);
        if (query.DataInicial is { } start) items = items.Where(x => x.DataMedicao >= start);
        if (query.DataFinal is { } end) items = items.Where(x => x.DataMedicao <= end);
        if (query.TipoMarco is { } milestone) items = items.Where(x => x.TipoMarco == milestone);
        var total = await items.CountAsync(ct);
        return (await items.OrderByDescending(x => x.DataMedicao).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct), total);
    }
    public Task AddAsync(PesagemAnimal item, CancellationToken ct) => context.PesagensAnimal.AddAsync(item, ct).AsTask();
    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
