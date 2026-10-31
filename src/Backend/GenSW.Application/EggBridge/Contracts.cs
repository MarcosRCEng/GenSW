using GenSW.Application.Inventory;
using GenSW.Domain.Animals;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;

namespace GenSW.Application.EggBridge;

public sealed record EggBridgeQuery(int Page = 1, int PageSize = 25, Guid? AnimalId = null, Guid? ItemId = null,
    Guid? GrupoId = null, string? Estado = null)
{
    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue ||
            Estado is not (null or "SemPonte" or "ComPonte")) throw new ArgumentException("Paginação/filtro F03 inválido.");
    }
}
public sealed record EggBridgePage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record EggCandidate(ProducaoOvo Origem, Guid? PonteId, Guid? GrupoId);
public sealed record EggGroupReconciliation(Guid GrupoId, bool Coerente, IReadOnlyList<string> Divergencias);

public interface IEggBridgeRepository
{
    Task<IInventoryScope> BeginMutationAsync(bool productionLock, CancellationToken ct);
    Task<IInventoryScope> BeginReadAsync(CancellationToken ct);
    void Add<T>(T value) where T : class;
    Task SaveAsync(CancellationToken ct);
    void ClearTracking();
    Task<AncoraConciliacaoOvos?> AnchorAsync(Guid item, bool forUpdate, CancellationToken ct);
    Task<RecorteConciliacaoOvos?> ScopeAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DecisaoConciliacaoOvo>> DecisionsAsync(Guid scope, CancellationToken ct);
    Task<IReadOnlyList<Guid>> SupersededSourcesAsync(Guid scope, IReadOnlyList<Guid> sourceIds, CancellationToken ct);
    Task<ProvaEntradaOvos?> ProofAsync(Guid id, bool forUpdate, CancellationToken ct);
    Task<ProvaEntradaOvos?> ProofByMovementAsync(Guid movement, CancellationToken ct);
    Task<IReadOnlyList<AlocacaoProvaOvos>> AllocationsAsync(Guid proof, CancellationToken ct);
    Task<MovimentoEstoque?> MovementAsync(Guid id, CancellationToken ct);
    Task<EventoEstoque?> EventAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProducaoOvo>> SourcesAsync(IReadOnlyList<Guid> ids, bool forShare, CancellationToken ct);
    Task<IReadOnlyList<PonteProducaoOvo>> BridgesAsync(IReadOnlyList<Guid> sourceIds, CancellationToken ct);
    Task<EggBridgePage<EggCandidate>> CandidatesAsync(EggBridgeQuery query, CancellationToken ct);
    Task<GrupoPonteOvos?> GroupAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PonteProducaoOvo>> GroupBridgesAsync(Guid group, CancellationToken ct);
    Task<IReadOnlyList<TratamentoPonteOvos>> TreatmentsAsync(Guid group, CancellationToken ct);
    Task<EggBridgePage<GrupoPonteOvos>> GroupsAsync(EggBridgeQuery query, CancellationToken ct);
    Task<EggBridgePage<RecorteConciliacaoOvos>> ScopesAsync(EggBridgeQuery query, CancellationToken ct);
    Task<EggBridgePage<ProvaEntradaOvos>> ProofsAsync(EggBridgeQuery query, CancellationToken ct);
    Task<ComandoPonteOvos?> ReplayAsync(string operation, string key, CancellationToken ct);
    Task<bool> LotHasAnyHistoryAsync(Guid lot, CancellationToken ct);
    Task<bool> IsBridgeLotAsync(Guid lot, CancellationToken ct);
    Task<bool> WasUsedAfterEntryAsync(GrupoPonteOvos group, CancellationToken ct);
    Task<EggGroupReconciliation> ReconcileAsync(Guid group, CancellationToken ct);
    Task EnsureLotConsistentAsync(Guid lot, CancellationToken ct);
}
public interface IInventoryEggBridgePolicy
{
    Task LockItemAsync(Guid item, bool forUpdate, CancellationToken ct);
    Task ValidateAsync(Guid item, Guid lot, string operation, bool credit, InventoryMovementCommand command, CancellationToken ct);
    Task RecordReceiptAsync(Guid item, EventoEstoque stockEvent, InventoryMovementCommand command, CancellationToken ct);
}
