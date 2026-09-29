using GenSW.Application.Animals.Cruzamentos;
using GenSW.Application.Animals.CiclosReprodutivos;
using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Proles;

public sealed class ProleService(IProleRepository repository, IAnimalService animalService, TimeProvider timeProvider) : IProleService
{
    public async Task<ProleResult> CreateAsync(Guid cicloId, ProleCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var ciclo = await CicloConcluidoAsync(cicloId, ct);
        ValidarOrigem(ciclo, command.Origem);
        var total = await repository.GetQuantidadeRegistradaAsync(cicloId, ct);
        ValidarLimite(ciclo, total, command.Quantidade);
        var item = Prole.Criar(cicloId, command.TipoRegistro, command.Quantidade, command.Origem, command.Data, command.PesoGramas, command.Sexo, command.Condicao, command.Observacao, null, timeProvider.GetUtcNow());
        await repository.AddAsync(item, ct);
        await repository.SaveChangesAsync(ct);
        return await GetAsync(item.Id, ct);
    }

    public async Task<ProleResult> GetAsync(Guid id, CancellationToken ct = default) => await repository.GetAsync(id, ct) ?? throw new ProleNotFoundException(id);
    public async Task<PagedProleResult> ListAsync(ProleListQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (query.TipoRegistro is not null && !Enum.IsDefined(query.TipoRegistro.Value)) || (query.Origem is not null && !Enum.IsDefined(query.Origem.Value))) throw new ArgumentException("List query is invalid.");
        var page = await repository.ListAsync(query, ct); return new(page.Items, query.Page, query.PageSize, page.TotalItems, page.TotalItems == 0 ? 0 : (int)Math.Ceiling(page.TotalItems / (double)query.PageSize));
    }
    public async Task<ProleResult> UpdateAsync(Guid id, ProleUpdateCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command); var item = await repository.GetForUpdateAsync(id, ct) ?? throw new ProleNotFoundException(id); var ciclo = await CicloConcluidoAsync(item.CicloReprodutivoId, ct); ValidarOrigem(ciclo, command.Origem);
        item.Atualizar(command.Origem, command.Data, command.PesoGramas, command.Sexo, command.Condicao, command.Observacao, timeProvider.GetUtcNow()); await repository.SaveChangesAsync(ct); return await GetAsync(id, ct);
    }
    public async Task<ProleResult> SplitAsync(Guid id, ProleUpdateCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command); var lote = await repository.GetForUpdateAsync(id, ct) ?? throw new ProleNotFoundException(id); var ciclo = await CicloConcluidoAsync(lote.CicloReprodutivoId, ct); ValidarOrigem(ciclo, command.Origem);
        lote.RegistrarDesdobramento(timeProvider.GetUtcNow()); var child = Prole.Criar(ciclo.Id, TipoRegistroProle.Individual, 1, command.Origem, command.Data, command.PesoGramas, command.Sexo, command.Condicao, command.Observacao, lote.Id, timeProvider.GetUtcNow()); await repository.AddAsync(child, ct); await repository.SaveChangesAsync(ct); return await GetAsync(child.Id, ct);
    }
    public async Task<ProleConversaoResult> ConvertAsync(Guid id, ProleConversaoCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command); var item = await repository.GetForUpdateAsync(id, ct) ?? throw new ProleNotFoundException(id);
        if (item.TipoRegistro != TipoRegistroProle.Individual) throw new ProleConflictException("A batch must be split into an individual record before conversion.");
        if (item.AnimalId is not null) throw new ProleConflictException("This offspring record has already been converted.");
        var ciclo = await CicloConcluidoAsync(item.CicloReprodutivoId, ct);
        var animal = await animalService.CreateAsync(new(command.CodigoInterno, command.Nome, command.EspecieId, command.RacaId, command.VariedadeId, item.Sexo, item.Data, command.Escopo), ct);
        item.VincularAnimal(animal.Id, timeProvider.GetUtcNow()); await repository.SaveChangesAsync(ct);
        var pai = await repository.GetPaiAsync(ciclo.Id, ct) ?? throw new CruzamentoNotFoundException(ciclo.CruzamentoId); var mae = await repository.GetMaeAsync(ciclo.Id, ct) ?? throw new CruzamentoNotFoundException(ciclo.CruzamentoId);
        return new(await GetAsync(id, ct), animal, pai, mae);
    }
    private async Task<CicloReprodutivo> CicloConcluidoAsync(Guid id, CancellationToken ct) { var ciclo = await repository.GetCicloForUpdateAsync(id, ct) ?? throw new CicloReprodutivoNotFoundException(id); if (ciclo.Status != StatusCicloReprodutivo.Concluido) throw new ProleConflictException("Offspring can only be recorded for a completed cycle."); return ciclo; }
    private static void ValidarOrigem(CicloReprodutivo ciclo, TipoOrigemProle origem) { if ((ciclo.Tipo == TipoCicloReprodutivo.Oviparo && origem != TipoOrigemProle.Eclosao) || (ciclo.Tipo == TipoCicloReprodutivo.Gestacional && origem != TipoOrigemProle.Nascimento)) throw new ProleConflictException("The offspring origin must match the reproductive cycle flow."); }
    private static void ValidarLimite(CicloReprodutivo ciclo, int existing, int requested) { var limit = ciclo.Tipo == TipoCicloReprodutivo.Oviparo ? ciclo.OvosEclodidos : ciclo.Nascidos; if (limit is null) throw new ProleConflictException("The completed cycle must have an assessed total before registering offspring."); if (existing + requested > limit) throw new ProleConflictException("The registered offspring total cannot exceed the assessed cycle total."); }
}
