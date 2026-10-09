using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;

namespace GenSW.Application.Inventory;

public sealed class InventoryException(int status, string code, string message) : Exception(message)
{ public int Status { get; } = status; public string Code { get; } = code; }
public sealed record InventoryQuery(int Page = 1, int PageSize = 25, string? Search = null, bool? Ativo = null,
    string SortBy = "codigo", string SortDirection = "asc", Guid? ItemId = null, Guid? LoteId = null,
    Guid? LocalId = null, Guid? PropriedadeId = null, string? Situacao = null, string? Finalidade = null,
    bool? ComSaldo = null, bool? Elegivel = null, DateOnly? ValidadeAte = null, bool? SemValidade = null,
    string? Tipo = null, Guid? AutorId = null, DateOnly? DataDe = null, DateOnly? DataAte = null,
    string? SeqDe = null, string? SeqAte = null)
{
    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue || Search?.Length > 200)
            throw new ArgumentException("Paginação/busca inválida.");
        if (SortBy is not ("codigo" or "nome" or "createdAtUtc" or "sequencia" or "itemCodigo" or "loteCodigo" or "localCodigo") || SortDirection is not ("asc" or "desc")) throw new ArgumentException("Ordenação inválida.");
        if (Situacao is not (null or "Liberado" or "Bloqueado" or "Encerrado") || Finalidade is not (null or "Ordinario" or "Segregacao") || DataDe > DataAte) throw new ArgumentException("Filtro inválido.");
        foreach (var seq in new[] { SeqDe, SeqAte }) if (seq is not null && (!System.Text.RegularExpressions.Regex.IsMatch(seq, @"^(0|[1-9][0-9]*)$") || !long.TryParse(seq, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))) throw new ArgumentException("Sequência inválida.");
        if (SeqDe is not null && SeqAte is not null && long.Parse(SeqDe, System.Globalization.CultureInfo.InvariantCulture) > long.Parse(SeqAte, System.Globalization.CultureInfo.InvariantCulture)) throw new ArgumentException("Intervalo de sequência inválido.");
    }
}
public sealed record InventoryPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages,
    DateTimeOffset ObservadoEmUtc, string SequenciaAte);
public sealed record LocalView(Guid Id, string Codigo, string Nome, string? Descricao, Guid? PropriedadeId,
    string? PropriedadeNome, string Finalidade, bool Ativo, int Revisao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record LoteView(Guid Id, Guid ItemId, string ItemCodigo, string ItemNome, bool ItemAtivo,
    string Codigo, string? CodigoExterno, string Origem, string Fonte, Guid ResponsavelId, string ResponsavelNome,
    DateOnly? DataOrigem, DateOnly? Fabricacao, DateOnly? Coleta, DateOnly? Validade, string? FonteValidade,
    Guid? ResponsavelValidadeId, string Situacao, bool Ativo, string Unidade, Guid? PerfilNutricionalId,
    Guid? ConversaoItemId, string? Aplicabilidade, int Revisao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record SaldoView(Guid LoteId, Guid LocalId, Guid ItemId, string ItemCodigo, string ItemNome,
    string LoteCodigo, string LocalCodigo, string LocalNome, string Unidade, string Quantidade,
    string QuantidadeElegivel, bool Elegivel, IReadOnlyList<string> MotivosIndisponibilidade, int Revisao,
    int ItemRevisao, int LoteRevisao, int LocalRevisao, DateTimeOffset ObservadoEmUtc, string SequenciaAte,
    IReadOnlyList<string>? Avisos = null);
public sealed record ResponsavelView(Guid Id, string Nome, bool Ativo);
public sealed record ReconciliacaoView(Guid LoteId, Guid LocalId, string Unidade, string QuantidadeLivro, string QuantidadeProjecao, string Diferenca);
public sealed record HistoricoView(Guid Id, Guid RegistroId, string Tipo, string Operacao, string Sequencia,
    Guid AutorId, DateTimeOffset CreatedAtUtc, string Motivo, string AntesJson, string DepoisJson);
public sealed record MovimentoView(Guid Id, int Ordinal, Guid LoteId, Guid LocalId, Guid ItemId, string Unidade,
    string Sentido, string Quantidade, string QuantidadeDeclarada, string UnidadeDeclarada, string Residuo,
    string SaldoAnterior, string SaldoPosterior, Guid? ConversaoItemId, Guid? PerfilNutricionalId, string SnapshotJson);
public sealed record EventoView(Guid Id, string Sequencia, string Tipo, Guid AutorId, Guid ResponsavelId,
    DateTimeOffset CreatedAtUtc, DateOnly DataOperacional, DateOnly? DataObservada, string Motivo,
    string? Documento, Guid? EventoReferenciaId, string Algoritmo, string SnapshotJson, IReadOnlyList<MovimentoView> Movimentos);
public sealed record LocalCommand(string Codigo, string Nome, string? Descricao = null, Guid? PropriedadeId = null,
    string Finalidade = "Ordinario", bool Ativo = true, int VersaoEsperada = 0, string Motivo = "Cadastro de local");
public sealed record LoteCommand(Guid ItemId, string Codigo, string Origem, string Fonte, Guid ResponsavelId,
    string? CodigoExterno = null, DateOnly? DataOrigem = null, DateOnly? Fabricacao = null, DateOnly? Coleta = null,
    DateOnly? Validade = null, string? FonteValidade = null, Guid? ResponsavelValidadeId = null,
    Guid? PerfilNutricionalId = null, Guid? ConversaoItemId = null, string? Aplicabilidade = null,
    bool Pendente = false, int VersaoEsperada = 0, int ItemVersaoEsperada = 0, string Motivo = "Cadastro de lote");
public sealed record InventoryStateCommand(int VersaoEsperada, string Motivo, string Evidencia,
    bool? Ativo = null, string? Finalidade = null, DateOnly? Validade = null, string? FonteValidade = null,
    Guid? ResponsavelId = null, Guid? PerfilNutricionalId = null, Guid? ConversaoItemId = null, string? Aplicabilidade = null);
public sealed record InventoryVersions(int Item, int Lote, int Local = 0, int Posicao = 0,
    int OrigemLocal = 0, int DestinoLocal = 0, int OrigemPosicao = 0, int DestinoPosicao = 0);
public sealed record InventoryMovementCommand(Guid LoteId, string Quantidade, string Unidade, Guid ResponsavelId,
    string Motivo, InventoryVersions VersoesEsperadas, Guid? LocalId = null, Guid? OrigemLocalId = null,
    Guid? DestinoLocalId = null, Guid? ConversaoItemId = null, string? QuantidadeContada = null,
    QuantizationAcceptance? AceiteQuantizacao = null, DateOnly? DataObservada = null, string? Origem = null,
    string? Fonte = null, string? Documento = null, string? Destino = null, string? Evidencia = null,
    Guid? EventoReferenciaId = null);
public sealed record InventoryPreviewCommand(string Operacao, InventoryMovementCommand Comando);
public sealed record PreviewPosition(Guid LoteId, Guid LocalId, string SaldoAnterior, string SaldoPosterior, int Revisao, int RevisaoResultante);
public sealed record InventoryPreview(string Operacao, Guid LoteId, string Unidade, PhysicalQuantity Quantidade,
    IReadOnlyList<PreviewPosition> Posicoes, InventoryVersions VersoesEsperadas, IReadOnlyList<string> Avisos,
    DateTimeOffset ObservadoEmUtc, string SequenciaAte);
public sealed record InventoryMutationResult(int StatusHttp, string? Location, string RespostaJson, bool Replayed);

public interface IInventoryScope : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
public interface IInventoryRepository
{
    Task<IInventoryScope> BeginMutationAsync(CancellationToken ct);
    Task<IInventoryScope> BeginReadAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<long> NextSequenceAsync(CancellationToken ct);
    Task<long> LastSequenceAsync(CancellationToken ct);
    void ClearTracking();
    void Add(LocalEstoque value); void Add(LoteMaterial value); void Add(PosicaoEstoque value);
    void Add(EventoEstoque value); void Add(MovimentoEstoque value); void Add(HistoricoEstoque value);
    void Add(ComandoEstoque value); void Add(CatalogAudit value);
    Task<Item?> ItemAsync(Guid id, bool tracking, CancellationToken ct);
    Task<LocalEstoque?> LocalAsync(Guid id, bool tracking, CancellationToken ct);
    Task<LoteMaterial?> LoteAsync(Guid id, bool tracking, CancellationToken ct);
    Task<PosicaoEstoque?> PositionAsync(Guid lot, Guid local, bool tracking, CancellationToken ct);
    Task<ConversaoItem?> ConversionAsync(Guid id, CancellationToken ct);
    Task<NutritionProfile?> ProfileAsync(Guid id, CancellationToken ct);
    Task<ResponsavelView?> ResponsibleAsync(Guid id, bool forUpdate, CancellationToken ct);
    Task<bool> PropertyActiveAsync(Guid id, CancellationToken ct);
    Task<bool> HasBalanceAsync(Guid? lot, Guid? local, CancellationToken ct);
    Task<bool> HasMovementsAsync(Guid lot, Guid local, CancellationToken ct);
    Task<ComandoEstoque?> ReplayAsync(Guid actor, string operation, Guid resource, string key, CancellationToken ct);
    Task<InventoryPage<LocalView>> LocaisAsync(InventoryQuery query, CancellationToken ct);
    Task<InventoryPage<LoteView>> LotesAsync(InventoryQuery query, CancellationToken ct);
    Task<LocalView?> LocalViewAsync(Guid id, CancellationToken ct);
    Task<LoteView?> LoteViewAsync(Guid id, CancellationToken ct);
    Task<InventoryPage<SaldoView>> SaldosAsync(InventoryQuery query, DateOnly today, CancellationToken ct);
    Task<SaldoView?> SaldoAsync(Guid lot, Guid local, DateOnly today, CancellationToken ct);
    Task<InventoryPage<EventoView>> EventosAsync(InventoryQuery query, CancellationToken ct);
    Task<EventoView?> EventoAsync(Guid id, CancellationToken ct);
    Task<InventoryPage<HistoricoView>> HistoryAsync(Guid id, InventoryQuery query, CancellationToken ct);
    Task<InventoryPage<ReconciliacaoView>> ReconcileAsync(InventoryQuery query, CancellationToken ct);
    Task<InventoryPage<ResponsavelView>> ResponsaveisAsync(InventoryQuery query, CancellationToken ct);
}
