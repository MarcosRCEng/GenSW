using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Domain.Inventory;
using GenSW.Domain.Production;

namespace GenSW.Application.Production;

public sealed class ProductionException(int status, string code, string message) : Exception(message)
{ public int Status { get; } = status; public string Code { get; } = code; }
public sealed record ProductionQuery(int Page = 1, int PageSize = 25, string? Search = null, string? Estado = null,
    Guid? ReceitaVersaoId = null, Guid? ResponsavelId = null, Guid? PropriedadeId = null,
    DateOnly? DataDe = null, DateOnly? DataAte = null, string SortBy = "codigo", string SortDirection = "asc")
{
    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page-1)*PageSize > int.MaxValue || Search?.Length > 200 || DataDe > DataAte) throw new ArgumentException("Paginação/filtros inválidos.");
        if (Estado is not (null or "Rascunho" or "Planejada" or "Iniciada" or "Confirmada" or "Cancelada" or "AbortadaSemConsumo" or "Revertida") || SortBy is not ("codigo" or "createdAtUtc") || SortDirection is not ("asc" or "desc")) throw new ArgumentException("Estado/ordenação inválidos.");
    }
}
public sealed record ProductionPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record OrderCommand(Guid Id, string Codigo, Guid ReceitaVersaoId, string Tamanho, string Unidade,
    Guid ResponsavelId, Guid? PropriedadeId = null, int VersaoEsperada = 0, string Motivo = "Cadastro de ordem");
public sealed record ProductionStateCommand(int VersaoEsperada, string Motivo);
public sealed record AllocationCommand(Guid Id, Guid LoteId, Guid LocalId, string Quantidade, string Unidade,
    Guid? ConversaoItemId = null, QuantizationAcceptance? AceiteQuantizacao = null);
public sealed record InputCommand(Guid LinhaId, IReadOnlyList<AllocationCommand> Alocacoes);
public sealed record StartCommand(int VersaoEsperada, Guid ResponsavelId, IReadOnlyList<InputCommand> Entradas, string Motivo);
public sealed record OutputCommand(Guid Id, Guid LinhaId, string Quantidade, string Unidade, string NovoCodigoLote, Guid LocalId,
    DateOnly? DataOrigem = null, DateOnly? Fabricacao = null, DateOnly? Coleta = null, DateOnly? Validade = null,
    string? FonteValidade = null, Guid? ResponsavelValidadeId = null, Guid? PerfilNutricionalId = null,
    Guid? ConversaoItemId = null, string? Aplicabilidade = null, QuantizationAcceptance? AceiteQuantizacao = null,
    bool Bloqueado = false);
public sealed record LossCommand(string Quantidade, string Unidade, string Motivo, Guid? LinhaId = null);
public sealed record ConfirmCommand(int VersaoEsperada, Guid ResponsavelId, IReadOnlyList<InputCommand> Entradas,
    IReadOnlyList<OutputCommand> Saidas, IReadOnlyList<LossCommand> Perdas, string Motivo, string? Observacao = null,
    DateOnly? DataObservada = null, string? Evidencia = null, string? AptidaoNoFato = null);
public sealed record AbortCommand(int VersaoEsperada, bool NenhumConsumoFisico, string Motivo, string Evidencia);
public sealed record ReverseCommand(int VersaoEsperada, bool CoerenciaFisicaConfirmada, string Motivo, string Evidencia);
public sealed record OccurrenceCommand(int VersaoEsperada, string Tipo, string Motivo, string Evidencia,
    DateOnly? DataObservada = null, bool HouveConsumoFisico = false);
public sealed record ItemSnapshot(Guid Id, string Codigo, string Nome, string Unidade, string Classe, int Revisao);
public sealed record PlanSnapshot(RecipeVersionView Receita, GenSW.Domain.Formulation.RecipeHeader Cabecalho,
    GenSW.Domain.Formulation.RecipeData Escalonada, IReadOnlyList<ItemSnapshot> Itens,
    IReadOnlyList<ConversionView> Conversoes, IReadOnlyList<ProfileView> Perfis,
    IReadOnlyList<RecipeVersionView> Dependencias, string Algoritmo, ResponsavelView? Responsavel = null);
public sealed record ReservationView(Guid Id, Guid LinhaId, Guid LoteId, Guid LocalId, string Unidade,
    string QuantidadeOriginal, string QuantidadeAtiva, string QuantidadeConsumida, string QuantidadeLiberada,
    Guid? ConversaoItemId = null, string? QuantidadeDeclarada = null, string? UnidadeDeclarada = null,
    QuantizationAcceptance? AceiteQuantizacao = null);
public sealed record SaidaProducaoView(Guid Id, Guid ExecucaoId, Guid LinhaId, Guid LoteId, Guid LocalId, string Quantidade,
    string Unidade, string CodigoLote, string LocalCodigo, string LocalNome);
public sealed record ExecutionView(Guid Id, Guid OrdemId, string Resultado, Guid AutorId, Guid ResponsavelId,
    DateTimeOffset CreatedAtUtc, DateOnly DataOperacional, DateOnly? DataObservada, string SnapshotJson,
    IReadOnlyList<ExecucaoMovimento> Movimentos, IReadOnlyList<SaidaProducaoView> LotesSaida,
    IReadOnlyList<EventoView>? AjustesPresentes = null);
public sealed record OrderView(Guid Id, string Codigo, string Estado, int Revisao, Guid ReceitaVersaoId,
    string Tamanho, string Unidade, Guid ResponsavelId, Guid? PropriedadeId, string? PropriedadeNome,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? PlanejadaEmUtc, DateTimeOffset? IniciadaEmUtc,
    DateTimeOffset? ConfirmadaEmUtc, PlanSnapshot? Planejamento, IReadOnlyList<ReservationView> Reservas,
    ExecutionView? Execucao, IReadOnlyList<string> AcoesPermitidas, IReadOnlyList<string> Impedimentos);
public sealed record GenealogyQuery(string Direcao = "ambas", int Profundidade = 3, int Page = 1, int PageSize = 25);
public sealed record GenealogyEdge(Guid ExecucaoId, Guid OrdemId, Guid LoteEntradaId, Guid LoteSaidaId);
public sealed record ProductionReconciliation(Guid OrdemId, string Codigo, string Estado, bool Coerente, IReadOnlyList<string> Divergencias);
public sealed record ProductionMutationResult(int StatusHttp, string? Location, string RespostaJson, bool Replayed);
public sealed record ProductionPreviewCommand(string Operacao, StartCommand? Inicio = null, ConfirmCommand? Confirmacao = null);
public sealed record ProductionQuantityPreview(string Papel, Guid LinhaId, Guid Id, PhysicalQuantity Quantidade);
public sealed record ProductionPreview(IReadOnlyList<ProductionQuantityPreview> Quantidades, IReadOnlyList<string> Avisos);

public interface IProductionRepository
{
    Task<IInventoryScope> BeginMutationAsync(CancellationToken ct);
    void Add<T>(T value) where T : class;
    Task SaveAsync(CancellationToken ct);
    Task<OrdemProducao?> OrderAsync(Guid id, bool tracking, CancellationToken ct);
    Task<ProductionPage<OrdemProducao>> OrdersAsync(ProductionQuery query, CancellationToken ct);
    Task<ComandoProducao?> ReplayAsync(Guid order, string operation, string key, CancellationToken ct);
    Task<IReadOnlyList<ReservaProducao>> ReservationsAsync(Guid order, bool tracking, CancellationToken ct);
    Task<decimal> ReservedAsync(Guid lot, Guid local, Guid? excludingOrder, CancellationToken ct);
    Task<ExecucaoProducao?> ExecutionAsync(Guid order, CancellationToken ct);
    Task<IReadOnlyList<ExecucaoMovimento>> MovementsAsync(Guid execution, CancellationToken ct);
    Task<IReadOnlyList<SaidaProducao>> OutputsAsync(Guid execution, CancellationToken ct);
    Task<IReadOnlyList<MovimentoEstoque>> StockMovementsAsync(Guid execution, CancellationToken ct);
    Task<bool> OutputWasUsedAsync(SaidaProducao output, CancellationToken ct);
    Task<bool> HasDeclaredConsumptionAsync(Guid order, CancellationToken ct);
    Task<ProductionPage<HistoricoProducao>> HistoryAsync(Guid order, int page, int size, CancellationToken ct);
    Task<ProductionPage<OcorrenciaProducao>> OccurrencesAsync(Guid order, int page, int size, CancellationToken ct);
    Task<ProductionPage<GenealogyEdge>> GenealogyAsync(Guid lot, GenealogyQuery query, CancellationToken ct);
    Task<string?> PropertyLabelAsync(Guid id, CancellationToken ct);
    Task<ProductionPage<ProductionReconciliation>> ReconcileAsync(ProductionQuery query, CancellationToken ct);
    Task EnsureOrderConsistentAsync(Guid order, CancellationToken ct);
    Task EnsurePositionConsistentAsync(Guid lot, Guid local, CancellationToken ct);
    Task<IReadOnlyList<Guid>> AdjustmentEventsAsync(Guid execution, CancellationToken ct);
}
