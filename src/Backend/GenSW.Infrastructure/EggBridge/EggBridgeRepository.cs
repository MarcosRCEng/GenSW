using GenSW.Application.EggBridge;
using GenSW.Application.Inventory;
using GenSW.Domain.Animals;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using GenSW.Domain.Production;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.EggBridge;

public sealed class EggBridgeRepository(GenSWDbContext db, IInventoryRepository inventory) : IEggBridgeRepository
{
    private bool Postgres => db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
    public async Task<IInventoryScope> BeginMutationAsync(bool productionLock, CancellationToken ct)
    {
        var scope = await inventory.BeginMutationAsync(ct);
        try
        {
            if (productionLock && Postgres) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(430,2)", ct);
            return scope;
        }
        catch { await scope.DisposeAsync(); throw; }
    }
    public Task<IInventoryScope> BeginReadAsync(CancellationToken ct) => inventory.BeginReadAsync(ct);
    public void Add<T>(T value) where T : class => db.Add(value);
    public Task SaveAsync(CancellationToken ct) => inventory.SaveAsync(ct);
    public void ClearTracking() => inventory.ClearTracking();
    public Task<AncoraConciliacaoOvos?> AnchorAsync(Guid item, bool forUpdate, CancellationToken ct) =>
        (forUpdate && Postgres ? db.Set<AncoraConciliacaoOvos>().FromSqlInterpolated($"SELECT * FROM \"AncorasConciliacaoOvos\" WHERE \"ItemId\"={item} FOR UPDATE") : db.Set<AncoraConciliacaoOvos>()).AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == item, ct);
    public Task<RecorteConciliacaoOvos?> ScopeAsync(Guid id, CancellationToken ct) => db.Set<RecorteConciliacaoOvos>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<DecisaoConciliacaoOvo>> DecisionsAsync(Guid scope, CancellationToken ct) => await db.Set<DecisaoConciliacaoOvo>().AsNoTracking().Where(x => x.RecorteId == scope).OrderBy(x => x.ProducaoOvoId).ToArrayAsync(ct);
    public async Task<IReadOnlyList<Guid>> SupersededSourcesAsync(Guid scope, IReadOnlyList<Guid> sourceIds, CancellationToken ct)
    {
        var original = await ScopeAsync(scope, ct);
        if (original is null) return sourceIds.Distinct().ToArray();
        return await (from decision in db.Set<DecisaoConciliacaoOvo>().AsNoTracking()
                      join approval in db.Set<RecorteConciliacaoOvos>().AsNoTracking() on decision.RecorteId equals approval.Id
                      where approval.OrdemAprovacao > original.OrdemAprovacao && sourceIds.Contains(decision.ProducaoOvoId)
                      select decision.ProducaoOvoId).Distinct().ToArrayAsync(ct);
    }
    public Task<ProvaEntradaOvos?> ProofAsync(Guid id, bool forUpdate, CancellationToken ct) =>
        (forUpdate && Postgres ? db.Set<ProvaEntradaOvos>().FromSqlInterpolated($"SELECT * FROM \"ProvasEntradaOvos\" WHERE \"Id\"={id} FOR UPDATE") : db.Set<ProvaEntradaOvos>()).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ProvaEntradaOvos?> ProofByMovementAsync(Guid movement, CancellationToken ct) => db.Set<ProvaEntradaOvos>().AsNoTracking().SingleOrDefaultAsync(x => x.MovimentoEstoqueId == movement, ct);
    public async Task<IReadOnlyList<AlocacaoProvaOvos>> AllocationsAsync(Guid proof, CancellationToken ct) => await db.Set<AlocacaoProvaOvos>().AsNoTracking().Where(x => x.ProvaId == proof).OrderBy(x => x.Ordinal).ToArrayAsync(ct);
    public Task<MovimentoEstoque?> MovementAsync(Guid id, CancellationToken ct) => db.MovimentosEstoque.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<EventoEstoque?> EventAsync(Guid id, CancellationToken ct) => db.EventosEstoque.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<ProducaoOvo>> SourcesAsync(IReadOnlyList<Guid> ids, bool forShare, CancellationToken ct)
    {
        // AsNoTracking is deliberate: a tracked source from before the row lock must never serve confirmation.
        if (forShare && Postgres)
        {
            var result = new List<ProducaoOvo>();
            foreach (var id in ids.Distinct().Order())
            {
                var row = await db.ProducoesOvos.FromSqlInterpolated($"SELECT * FROM \"ProducoesOvos\" WHERE \"Id\"={id} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
                if (row is not null) result.Add(row);
            }
            return result;
        }
        return await db.ProducoesOvos.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<PonteProducaoOvo>> BridgesAsync(IReadOnlyList<Guid> sourceIds, CancellationToken ct) => await db.Set<PonteProducaoOvo>().AsNoTracking().Where(x => sourceIds.Contains(x.ProducaoOvoId)).ToArrayAsync(ct);
    public async Task<EggBridgePage<EggCandidate>> CandidatesAsync(EggBridgeQuery q, CancellationToken ct)
    {
        q.Validate();
        var rows = from source in db.ProducoesOvos.AsNoTracking()
                   join bridge in db.Set<PonteProducaoOvo>().AsNoTracking() on source.Id equals bridge.ProducaoOvoId into bridges
                   from bridge in bridges.DefaultIfEmpty()
                   where (!q.AnimalId.HasValue || source.AnimalId == q.AnimalId) &&
                       (q.Estado == null || (q.Estado == "SemPonte" ? bridge == null : bridge != null)) &&
                       (!q.GrupoId.HasValue || bridge != null && bridge.GrupoId == q.GrupoId)
                   select new { Source = source, BridgeId = bridge == null ? (Guid?)null : bridge.Id, GroupId = bridge == null ? (Guid?)null : bridge.GrupoId };
        // Keep record construction as the terminal projection; EF cannot order through its constructor.
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderBy(x => x.Source.DataPostura).ThenBy(x => x.Source.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new EggCandidate(x.Source, x.BridgeId, x.GroupId)).ToArrayAsync(ct);
        return new(page, q.Page, q.PageSize, count, (int)Math.Ceiling(count / (double)q.PageSize));
    }
    public Task<GrupoPonteOvos?> GroupAsync(Guid id, CancellationToken ct) => db.Set<GrupoPonteOvos>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<PonteProducaoOvo>> GroupBridgesAsync(Guid group, CancellationToken ct) => await db.Set<PonteProducaoOvo>().AsNoTracking().Where(x => x.GrupoId == group).OrderBy(x => x.ProducaoOvoId).ToArrayAsync(ct);
    public async Task<IReadOnlyList<TratamentoPonteOvos>> TreatmentsAsync(Guid group, CancellationToken ct) => await db.Set<TratamentoPonteOvos>().AsNoTracking().Where(x => x.GrupoId == group).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
    public Task<EggBridgePage<GrupoPonteOvos>> GroupsAsync(EggBridgeQuery q, CancellationToken ct) => Page(db.Set<GrupoPonteOvos>().AsNoTracking().Where(x => (!q.ItemId.HasValue || x.ItemId == q.ItemId) && (!q.GrupoId.HasValue || x.Id == q.GrupoId)).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id), q, ct);
    public Task<EggBridgePage<RecorteConciliacaoOvos>> ScopesAsync(EggBridgeQuery q, CancellationToken ct) => Page(db.Set<RecorteConciliacaoOvos>().AsNoTracking().Where(x => !q.ItemId.HasValue || x.ItemId == q.ItemId).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id), q, ct);
    public Task<EggBridgePage<ProvaEntradaOvos>> ProofsAsync(EggBridgeQuery q, CancellationToken ct) => Page(db.Set<ProvaEntradaOvos>().AsNoTracking().Where(x => !q.ItemId.HasValue || x.ItemId == q.ItemId).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id), q, ct);
    public Task<ComandoPonteOvos?> ReplayAsync(string operation, string key, CancellationToken ct) => db.Set<ComandoPonteOvos>().AsNoTracking().SingleOrDefaultAsync(x => x.Operacao == operation && x.Chave == key, ct);
    private static async Task<EggBridgePage<T>> Page<T>(IQueryable<T> rows, EggBridgeQuery q, CancellationToken ct)
    {
        q.Validate(); var count = await rows.CountAsync(ct);
        return new(await rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct), q.Page, q.PageSize, count, (int)Math.Ceiling(count / (double)q.PageSize));
    }
    public async Task<bool> LotHasAnyHistoryAsync(Guid lot, CancellationToken ct) =>
        await db.MovimentosEstoque.AnyAsync(x => x.LoteId == lot, ct) || await db.Set<ReservaProducao>().AnyAsync(x => x.LoteId == lot, ct) || await db.PosicoesEstoque.AnyAsync(x => x.LoteId == lot, ct);
    public Task<bool> IsBridgeLotAsync(Guid lot, CancellationToken ct) => db.Set<OrigemLoteOvos>().AnyAsync(x => x.LoteId == lot, ct);
    public async Task<bool> WasUsedAfterEntryAsync(GrupoPonteOvos group, CancellationToken ct)
    {
        if (await db.Set<ReservaProducao>().AnyAsync(x => x.LoteId == group.LoteId, ct) ||
            await db.MovimentosEstoque.AnyAsync(x => x.LoteId == group.LoteId && x.EventoId != group.EventoId, ct)) return true;
        var lot = await db.LotesMateriais.AsNoTracking().SingleAsync(x => x.Id == group.LoteId, ct);
        var current = System.Text.Json.JsonSerializer.Serialize(lot, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        return lot.Revisao != group.RevisaoLoteOriginal || lot.Situacao != group.SituacaoOriginal || lot.Validade != group.ValidadeOriginal ||
            !System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(current), System.Text.Json.Nodes.JsonNode.Parse(group.SnapshotLoteJson)) ||
            await db.PosicoesEstoque.AnyAsync(x => x.LoteId == group.LoteId && (x.LocalId != group.LocalId && x.Quantidade != 0 || x.LocalId == group.LocalId && x.Quantidade != group.Quantidade), ct);
    }
    private async Task<IReadOnlyList<string>> LotErrors(Guid lot, CancellationToken ct)
    {
        var errors = new List<string>();
        var positions = await db.PosicoesEstoque.AsNoTracking().Where(x => x.LoteId == lot).ToArrayAsync(ct);
        var movements = await db.MovimentosEstoque.AsNoTracking().Where(x => x.LoteId == lot).ToArrayAsync(ct);
        var reservations = await db.Set<ReservaProducao>().AsNoTracking().Where(x => x.LoteId == lot).ToArrayAsync(ct);
        foreach (var local in positions.Select(x => x.LocalId).Union(movements.Select(x => x.LocalId)).Union(reservations.Select(x => x.LocalId)))
        {
            var position = positions.SingleOrDefault(x => x.LocalId == local);
            var ledger = movements.Where(x => x.LocalId == local).Sum(x => x.Sentido == "Entrada" ? x.Quantidade : -x.Quantidade);
            if (position is null && movements.Any(x => x.LocalId == local) || (position?.Quantidade ?? 0) != ledger) errors.Add("Livro e projeção divergentes.");
            var reserve = reservations.Where(x => x.LocalId == local).ToArray();
            if (reserve.Sum(x => x.QuantidadeAtiva) > (position?.Quantidade ?? 0) || reserve.Any(x => x.QuantidadeAtiva < 0 || x.QuantidadeOriginal != x.QuantidadeAtiva + x.QuantidadeConsumida + x.QuantidadeLiberada)) errors.Add("Reserva física ou conservação divergente.");
        }
        return errors.Distinct().ToArray();
    }
    public async Task EnsureLotConsistentAsync(Guid lot, CancellationToken ct)
    {
        if ((await LotErrors(lot, ct)).Count > 0) throw new EggBridgeException(500, "invariante_f03", "Livro, projeção ou reservas divergentes. Preserve evidências e solicite conciliação antes de operar.");
    }
    public async Task<EggGroupReconciliation> ReconcileAsync(Guid id, CancellationToken ct)
    {
        var errors = new List<string>(); var group = await GroupAsync(id, ct);
        if (group is null) return new(id, false, ["Grupo inexistente."]);
        var bridges = await GroupBridgesAsync(id, ct);
        if (bridges.Count != group.Quantidade) errors.Add("Contagem de pontes diverge do grupo.");
        if ((await SupersededSourcesAsync(group.RecorteId, bridges.Select(x => x.ProducaoOvoId).ToArray(), ct)).Count > 0)
            errors.Add("Decisão posterior requer reconferência; identidade e histórico preservados.");
        var sources = await SourcesAsync(bridges.Select(x => x.ProducaoOvoId).ToArray(), false, ct);
        foreach (var bridge in bridges)
        {
            var source = sources.SingleOrDefault(x => x.Id == bridge.ProducaoOvoId);
            if (source is null || EggBridgeService.SourceHash(source) != bridge.HashOrigem || source.UpdatedAtUtc != bridge.OrigemUpdatedAtUtc) errors.Add("Origem atual diverge do snapshot histórico; saldo preservado.");
        }
        var movement = await MovementAsync(group.MovimentoEstoqueId, ct);
        if (movement is null || movement.ItemId != group.ItemId || movement.LoteId != group.LoteId || movement.LocalId != group.LocalId || movement.EventoId != group.EventoId || movement.Unidade != "un" || movement.Sentido != "Entrada") errors.Add("Origem física divergente.");
        if (group.Operacao == "Entrada" && (movement?.Quantidade != group.Quantidade || !await db.Set<OrigemLoteOvos>().AnyAsync(x => x.GrupoId == id && x.LoteId == group.LoteId, ct))) errors.Add("Entrada ou lote dedicado diverge da contagem individual.");
        var bridgeIds = bridges.Select(b => b.Id).ToArray();
        var allocations = await db.Set<AlocacaoProvaOvos>().AsNoTracking().Where(x => bridgeIds.Contains(x.PonteId)).ToArrayAsync(ct);
        if (group.Operacao == "Associacao")
        {
            var proof = group.ProvaId.HasValue ? await ProofAsync(group.ProvaId.Value, false, ct) : null;
            if (proof is null || allocations.Length != bridges.Count || allocations.Any(x => x.ProvaId != proof.Id || x.MovimentoEstoqueId != proof.MovimentoEstoqueId || x.Ordinal < 1 || x.Ordinal > proof.Contagem)) errors.Add("Prova ou capacidade histórica divergente.");
            if (proof is not null)
            {
                var allSlots = await AllocationsAsync(proof.Id, ct);
                if (allSlots.Count > proof.Contagem || allSlots.Select(x => x.Ordinal).Distinct().Count() != allSlots.Count || allSlots.Any(x => x.Ordinal < 1 || x.Ordinal > proof.Contagem)) errors.Add("Capacidade vitalícia excedida.");
                if (movement is null || movement.TipoEvento is not ("Entrada" or "Abertura") || movement.UnidadeDeclarada != "un" || movement.Quantidade < proof.Contagem || await IsBridgeLotAsync(group.LoteId, ct)) errors.Add("Prova não corresponde a entrada manual contada elegível.");
            }
        }
        else if (allocations.Length > 0) errors.Add("Entrada nova não usa capacidade de prova manual.");
        var treatments = await TreatmentsAsync(id, ct);
        var compensation = treatments.Where(x => x.Tipo == "Compensacao").ToArray();
        if (compensation.Length > 1 || treatments.Count(x => x.Tipo == "Invalidacao") > 1) errors.Add("Tratamento terminal duplicado.");
        foreach (var treatment in compensation)
        {
            var debit = await db.MovimentosEstoque.AsNoTracking().Where(x => x.EventoId == treatment.EventoId).ToArrayAsync(ct);
            if (group.Operacao != "Entrada" || debit.Length != 1 || debit[0].Sentido != "Saida" || debit[0].TipoEvento != "CompensacaoEntradaOvos" || debit[0].Quantidade != group.Quantidade || debit[0].LoteId != group.LoteId || debit[0].LocalId != group.LocalId) errors.Add("Compensação não inverte exatamente o grupo.");
        }
        errors.AddRange(await LotErrors(group.LoteId, ct));
        return new(id, errors.Count == 0, errors.Distinct().ToArray());
    }
}