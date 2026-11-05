using System.Linq.Expressions;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;

namespace GenSW.Application.MaterialCosts;

public sealed class MaterialCostException(int status, string code, string message) : Exception(message)
{ public int Status { get; } = status; public string Code { get; } = code; }
public sealed record MaterialEvidenceCommand(string Fonte, string ReferenciaDocumento, DateOnly DataFonte, string Moeda,
    string TotalDocumentado, IReadOnlyList<string> Componentes, IReadOnlyList<string> Lacunas, string? Hash = null);
public sealed record MaterialReceiptCommand(int Versao, string Estado, string Motivo, MaterialEvidenceCommand? Evidencia = null, string? SequenciaEsperada = null);
public sealed record MaterialShareCommand(Guid DestinoId, string Tipo, string Percentual);
public sealed record MaterialAllocationCommand(int Versao, bool PendenteRateio, string Motivo, IReadOnlyList<MaterialShareCommand> Percentuais, string? SequenciaEsperada = null);
public sealed record MaterialReferenceCommand(Guid Id, Guid ItemId, string Fonte, string ReferenciaDocumento,
    DateOnly DataFonte, string Moeda, string Unidade, string QuantidadeBase, string ValorUnitario,
    DateOnly VigenteDesde, DateOnly? VigenteAte, string Aplicabilidade, string Motivo, bool Revogada = false);
public sealed record MaterialEvaluationCommand(Guid Id, Guid LoteId, Guid LocalId, string Quantidade,
    int RevisaoEsperada, string Motivo, MaterialEvidenceCommand Evidencia, Guid? ApuracaoId = null);
public sealed record MaterialCompensationCommand(Guid Id, string Motivo, string Evidencia, int RevisaoEsperada);
public sealed record MaterialResolutionCommand(Guid Id, int RevisaoEsperada, MaterialAllocationCommand CustoMaterial, string Motivo, string Evidencia);
public sealed record MaterialEstimateLine(Guid Id, Guid ItemId, string Quantidade, string Unidade, Guid? ReferenciaId = null, Guid? ConversaoId = null);
public sealed record MaterialEstimateCommand(Guid Id, string Motivo, IReadOnlyList<MaterialEstimateLine> Linhas);
public sealed record MaterialCostQuery(int Page = 1, int PageSize = 25, string? Search = null, Guid? ItemId = null,
    Guid? LoteId = null, Guid? LocalId = null, string? Estado = null, string? AteSequencia = null, DateOnly? Data = null)
{
    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page-1)*PageSize > int.MaxValue || Search?.Length > 200) throw new ArgumentException("Paginação/filtros inválidos.");
        if (AteSequencia is not null && (!long.TryParse(AteSequencia, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var cut) || cut < 0)) throw new ArgumentException("Corte monetário inválido.");
    }
}
public sealed record MaterialCostPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record MaterialReferenceView(Guid Id, Guid ItemId, string ItemCodigo, string ItemNome, string Fonte,
    string ReferenciaDocumento, DateOnly DataFonte, string Moeda, string Unidade, string QuantidadeBase,
    string ValorUnitario, DateOnly VigenteDesde, DateOnly? VigenteAte, string Aplicabilidade, string Estado,
    int Revisao, Guid? SubstituiId, DateTimeOffset CreatedAtUtc);
public sealed record MaterialComponentView(Guid Id, Guid CamadaId, Guid ItemId, string Unidade, string Quantidade,
    string? ValorConhecido, IReadOnlyList<string> Lacunas, Guid? EvidenciaId, string FracaoNumerador = "1", string FracaoDenominador = "1");
public sealed record MaterialCostSummary(string Estado, string ValorConhecido, string? ValorTotal,
    string? CustoUnitarioCompleto, IReadOnlyList<MaterialComponentView> Componentes, IReadOnlyList<string> Lacunas,
    string SequenciaAte, int Revisao);
public sealed record MaterialLayerView(Guid Id, Guid LoteId, Guid ItemId, string Unidade, string TipoOrigem,
    string QuantidadeOriginal, string ValorConhecidoOriginal, string Estado, string SequenciaOrigem,
    Guid? MovimentoOrigemId, Guid? SaidaProducaoId, Guid? EvidenciaId, Guid? CamadaReferenciaId,
    DateTimeOffset CreatedAtUtc, IReadOnlyList<MaterialPositionView> Posicoes);
public sealed record MaterialPositionView(Guid CamadaId, Guid LocalId, string Unidade, string Quantidade,
    MaterialCostSummary Custo, int Revisao);
public sealed record MaterialLotView(Guid LoteId, string Codigo, Guid ItemId, string ItemCodigo, string ItemNome,
    string Unidade, MaterialCostSummary Custo, IReadOnlyList<MaterialLayerView> Camadas, string CorteFisico, string CorteMonetario);
public sealed record MaterialDestinationView(Guid Id, Guid DestinoId, string Tipo, Guid? SaidaProducaoId, Guid? PerdaId,
    string Quantidade, string Unidade, string Percentual, MaterialCostSummary Custo, string? UnitarioConhecido,
    bool UnitarioArredondado, string? ResiduoNumerador, string? ResiduoDenominador, string Motivo,Guid? LoteId=null);
public sealed record MaterialExecutionOutput(Guid Id,Guid LoteId,Guid LocalId,string Quantidade,string Unidade);
public sealed record MaterialExecutionView(Guid Id, Guid ExecucaoId, string Estado, MaterialCostSummary Custo,
    IReadOnlyList<MaterialDestinationView> Destinos, string CorteFisico, string CorteMonetario,
    string Motivo, DateTimeOffset CreatedAtUtc, string SnapshotJson, Guid? ApuracaoOriginalId,Guid? OrdemId=null,
    IReadOnlyList<MaterialExecutionOutput>? Saidas=null);
public sealed record MaterialHistoryView(Guid Id, string Tipo, string Sequencia, string Motivo,
    DateTimeOffset CreatedAtUtc, string SnapshotJson, Guid? EventoOriginalId);
public sealed record MaterialEvidenceView(Guid Id,Guid ItemId,string Fonte,string ReferenciaDocumento,DateOnly DataFonte,string Moeda,string Unidade,
    string Quantidade,string TotalDocumentado,IReadOnlyList<string> Componentes,IReadOnlyList<string> Lacunas,string Metodo,
    Guid AutorId,Guid ResponsavelId,DateTimeOffset CreatedAtUtc,string Motivo,string? HashDocumento);
public sealed record MaterialReconciliationView(Guid LoteId, Guid LocalId, bool Coerente, IReadOnlyList<string> Divergencias);
public sealed record MaterialMutationResult(int StatusHttp, string? Location, string RespostaJson, bool Replayed);
public sealed record MaterialInventoryPreview(MaterialCostSummary Custo, string SequenciaEsperada, IReadOnlyList<string> Avisos);
public sealed record MaterialProductionPreview(string Estado, MaterialCostSummary Custo, IReadOnlyList<MaterialDestinationView> Destinos, string SequenciaEsperada);
public sealed record MaterialOutputQuantity(Guid Id, string Unidade, decimal Quantidade);

public interface IMaterialCostRepository
{
    string TransactionIdentity { get; }
    Task<IInventoryScope> BeginMutationAsync(CancellationToken ct);
    Task<IInventoryScope> BeginReadAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    void ClearTracking();
    void Add<T>(T value) where T : class;
    Task<T?> GetAsync<T>(Guid id, bool tracking, CancellationToken ct) where T : class;
    Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T,bool>> predicate, bool tracking, CancellationToken ct) where T : class;
    Task<long> LastSequenceAsync(CancellationToken ct);
    Task<long> NextSequenceAsync(CancellationToken ct);
    Task<IReadOnlyList<PosicaoCustoMaterial>> LayerPositionsAsync(Guid lot, bool tracking, CancellationToken ct);
    Task<bool> LayerWasUsedAsync(Guid layer, CancellationToken ct);
    Task<bool> LotWasUsedSinceAsync(Guid lot, DateTimeOffset since, CancellationToken ct);
}
public interface IMaterialCostFlow
{
    Task<MaterialInventoryPreview> PreviewInventoryAsync(string operation, InventoryMovementCommand command, InventoryPreview preview, CancellationToken ct);
    Task ApplyInventoryAsync(EventoEstoque stockEvent, IReadOnlyList<MovimentoEstoque> movements, MaterialReceiptCommand? cost, CancellationToken ct);
    Task<MaterialProductionPreview> PreviewProductionAsync(ConfirmCommand command, IReadOnlyList<MovimentoEstoque> syntheticMovements, IReadOnlyList<MaterialOutputQuantity> outputs, CancellationToken ct);
    Task ApplyProductionAsync(ExecucaoProducao execution, IReadOnlyList<MovimentoEstoque> movements, IReadOnlyList<SaidaProducao> outputs, ConfirmCommand command, CancellationToken ct);
    Task ReversePhysicalAsync(EventoEstoque stockEvent, IReadOnlyList<MovimentoEstoque> compensations, CancellationToken ct);
    Task<Guid> EvaluateAsync(MaterialEvaluationCommand command, Guid actor, CancellationToken ct);
    Task<Guid> CompensateEvaluationAsync(Guid evaluation, MaterialCompensationCommand command, Guid actor, CancellationToken ct);
    Task<MaterialProductionPreview> PreviewResolutionAsync(Guid execution, MaterialResolutionCommand command, CancellationToken ct);
    Task<Guid> ResolveAsync(Guid execution, MaterialResolutionCommand command, Guid actor, CancellationToken ct);
}
