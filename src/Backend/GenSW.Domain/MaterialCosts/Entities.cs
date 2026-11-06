namespace GenSW.Domain.MaterialCosts;

// Monetary facts are append-only. Totals are BRL; reference quotations never
// become evidence or alter an existing material cost by themselves.
public sealed record PrecoReferenciaMaterial(Guid Id, Guid ItemId, string Fonte, string ReferenciaProtegida,
    DateOnly DataFonte, string Moeda, string Unidade, decimal QuantidadeBase, decimal ValorUnitario,
    DateOnly VigenciaInicio, DateOnly? VigenciaFim, string Aplicabilidade, string Metodo,
    Guid AutorId, Guid ResponsavelId, DateTimeOffset CreatedAtUtc, DateOnly DataOperacional,
    string Motivo, string Correlacao, string Algoritmo, Guid? SubstituiId = null, bool Revogada = false);

public sealed record EvidenciaCustoMaterial(Guid Id, Guid ItemId, string Fonte, string ReferenciaProtegida,
    DateOnly DataFonte, string Moeda, string Unidade, decimal Quantidade, decimal Total,
    string ComponentesJson, string LacunasJson, string Metodo,
    Guid AutorId, Guid ResponsavelId, DateTimeOffset CreatedAtUtc, DateOnly DataOperacional,
    string Motivo, string Correlacao, string Algoritmo, string? HashDocumento = null);

public sealed record CamadaCustoMaterial(Guid Id, Guid LoteId, Guid ItemId, string Unidade,
    decimal QuantidadeOriginal, decimal ValorConhecido, string Estado, string TipoOrigem,
    long SequenciaOrigem, string ComponentesJson, string LacunasJson,
    Guid AutorId, Guid ResponsavelId, DateTimeOffset CreatedAtUtc, DateOnly DataOperacional,
    string Motivo, string Correlacao, string Algoritmo,
    Guid? MovimentoOrigemId = null, Guid? SaidaProducaoId = null, Guid? EvidenciaId = null,
    Guid? CamadaReferenciaId = null);

public sealed record EventoCustoMaterial(Guid Id, long Sequencia, string Tipo,
    Guid AutorId, Guid ResponsavelId, DateTimeOffset CreatedAtUtc, DateOnly DataOperacional,
    string Motivo, string Correlacao, string Algoritmo, string SnapshotJson,
    Guid? EventoEstoqueId = null, Guid? ExecucaoId = null, Guid? EventoOriginalId = null);

public sealed record ParcelaCustoMaterial(Guid Id, Guid EventoCustoId, Guid CamadaId, Guid LocalId,
    string Sentido, decimal Quantidade, decimal ValorConhecido, string ComponentesJson,
    string LacunasJson, Guid? MovimentoEstoqueId = null, Guid? ParcelaOriginalId = null);

public sealed class PosicaoCustoMaterial
{
    private PosicaoCustoMaterial() { }
    public Guid CamadaId { get; private set; }
    public Guid LocalId { get; private set; }
    public string Unidade { get; private set; } = "";
    public decimal Quantidade { get; private set; }
    public decimal ValorConhecido { get; private set; }
    public string ComponentesJson { get; private set; } = "[]";
    public string LacunasJson { get; private set; } = "[]";
    public string Estado { get; private set; } = "Desconhecido";
    public int Revisao { get; private set; }
    public static PosicaoCustoMaterial Create(Guid layer, Guid local, string unit)
    {
        MaterialCostRules.Id(layer); MaterialCostRules.Id(local);
        MaterialCostRules.CanonicalUnit(unit);
        return new() { CamadaId = layer, LocalId = local, Unidade = unit };
    }
    public void SetBalance(decimal quantity, decimal knownAmount, string componentsJson, string gapsJson, string state, int expected)
    {
        if (Revisao != expected) throw new InvalidOperationException("Revisão monetária divergente.");
        MaterialCostRules.ScaledQuantity(quantity, Unidade);
        MaterialCostRules.Cents(knownAmount);
        MaterialCostRules.CoverageState(state);
        if (quantity == 0m && knownAmount != 0m) throw new ArgumentException("Posição sem quantidade não pode conservar centavos.");
        Quantidade = quantity; ValorConhecido = knownAmount; ComponentesJson = componentsJson;
        LacunasJson = gapsJson; Estado = state; Revisao = checked(Revisao + 1);
    }
}

public sealed record ApuracaoCustoExecucao(Guid Id, Guid ExecucaoId, string Estado,
    decimal ValorConhecido, decimal? ValorTotal, string Cobertura, string ComponentesJson,
    string LacunasJson, long CorteFisico, long CorteMonetario, string SnapshotJson,
    Guid AutorId, Guid ResponsavelId, DateTimeOffset CreatedAtUtc, DateOnly DataOperacional,
    string Motivo, string Correlacao, string Algoritmo, Guid? ApuracaoOriginalId = null);

public sealed record DestinoCustoExecucao(Guid Id, Guid ApuracaoId, Guid DestinoId, string Tipo,
    decimal Quantidade, string Unidade, decimal Percentual, decimal ValorConhecido, decimal? ValorTotal,
    decimal? CustoUnitarioCompleto, string Cobertura, string ComponentesJson, string LacunasJson,
    int OrdemRateio, string RestoRateio, string Motivo,
    Guid? SaidaProducaoId = null, Guid? PerdaId = null, Guid? CamadaSaidaId = null);

public sealed record ComandoCustoMaterial(Guid Id, string Operacao, Guid RecursoId, string Chave,
    string HashPayload, Guid AutorId, bool ExigeAdmin, int StatusHttp, string? Location,
    string RespostaJson, DateTimeOffset CreatedAtUtc, Guid? EventoId = null, Guid? RegistroId = null);
