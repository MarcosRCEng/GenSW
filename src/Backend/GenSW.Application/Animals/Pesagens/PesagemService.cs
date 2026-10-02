using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Pesagens;

public sealed class PesagemService(IPesagemRepository repository, TimeProvider clock)
{
    public async Task<PesagemResult> SaveAsync(Guid animalId, Guid? id, PesagemCommand command, CancellationToken ct)
    {
        await using var tx = await repository.BeginAsync(animalId, ct);
        var animal = await GetAnimal(animalId, ct);
        PesagemAnimal item;
        if (id is { } existing)
        {
            item = await repository.GetAsync(animalId, existing, ct) ?? throw Missing();
            item.Atualizar(command.DataMedicao, command.PesoGramas, command.TipoMarco, command.DescricaoMarco,
                command.IdadeReferenciaDias, command.Observacao, animal.DataNascimento, clock.GetUtcNow());
        }
        else
        {
            item = PesagemAnimal.Criar(animalId, command.DataMedicao, command.PesoGramas, command.TipoMarco,
                command.DescricaoMarco, command.IdadeReferenciaDias, command.Observacao, animal.DataNascimento, clock.GetUtcNow());
            await repository.AddAsync(item, ct);
        }
        await repository.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToResult(item, animal.DataNascimento);
    }

    public async Task<PesagemResult> GetAsync(Guid animalId, Guid id, CancellationToken ct)
    {
        var animal = await GetAnimal(animalId, ct);
        return ToResult(await repository.GetAsync(animalId, id, ct) ?? throw Missing(), animal.DataNascimento);
    }

    public async Task<AnimalEvolutionPage<PesagemResult>> ListAsync(Guid animalId, PesagemQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue ||
            query.DataInicial > query.DataFinal || (query.TipoMarco.HasValue && !Enum.IsDefined(query.TipoMarco.Value)))
            throw new ArgumentException("Consulta de pesagens inválida.");
        var animal = await GetAnimal(animalId, ct);
        var page = await repository.ListAsync(animalId, query, ct);
        return new(page.Items.Select(x => ToResult(x, animal.DataNascimento)).ToArray(), query.Page, query.PageSize,
            page.Total, (int)Math.Ceiling(page.Total / (double)query.PageSize));
    }

    private async Task<Animal> GetAnimal(Guid id, CancellationToken ct) =>
        await repository.AnimalAsync(id, ct) ?? throw new AnimalEvolutionException(404, "animal_nao_encontrado", "Animal não encontrado.");
    private static AnimalEvolutionException Missing() => new(404, "pesagem_nao_encontrada", "Pesagem não encontrada neste animal.");
    private static PesagemResult ToResult(PesagemAnimal x, DateOnly? birth) => new(x.Id, x.AnimalId, x.DataMedicao,
        x.PesoGramas, x.TipoMarco, x.DescricaoMarco, x.IdadeReferenciaDias,
        birth.HasValue ? x.DataMedicao.DayNumber - birth.Value.DayNumber : null, x.Observacao, x.CreatedAtUtc, x.UpdatedAtUtc);
}
