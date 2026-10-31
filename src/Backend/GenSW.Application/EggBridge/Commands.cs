using GenSW.Application.Inventory;
using GenSW.Domain.EggBridge;

namespace GenSW.Application.EggBridge;

public sealed class EggBridgeException(int status, string code, string message) : Exception(message)
{ public int Status { get; } = status; public string Code { get; } = code; }

public interface IEggBridgePreviewProtector
{
    string Protect(string value);
    string Unprotect(string token);
}

public sealed record EggEnrollmentCommand(Guid ItemId, string Motivo);
public sealed record EggDecisionCommand(Guid ProducaoOvoId, string Decisao, bool IndividualComprovado, string Evidencia);
public sealed record EggScopeCommand(Guid ItemId, string Fronteira, string FontesConsultadas, string Lacunas,
    Guid ResponsavelId, string Motivo, IReadOnlyList<EggDecisionCommand> Decisoes);
public sealed record EggProofCommand(Guid MovimentoEstoqueId, string Contagem, Guid ResponsavelId, string Motivo, string Evidencia);
public sealed record EggSelection(Guid ProducaoOvoId, string? Ordinal = null);
public sealed record EggPreviewCommand(string Operacao, Guid RecorteId, Guid ResponsavelId, string Motivo,
    string Evidencia, IReadOnlyList<EggSelection> Selecao, Guid? LoteId = null, Guid? LocalId = null, Guid? ProvaId = null);
public sealed record EggConfirmCommand(string Token);
public sealed record EggTreatmentCommand(string Tipo, Guid ResponsavelId, string Motivo, string Evidencia,
    bool CoerenciaFisicaConfirmada = false);
public sealed record EggTreatmentPreviewCommand(Guid GrupoId, EggTreatmentCommand Comando);
public sealed record EggSourceExpectation(Guid ProducaoOvoId, string HashOrigem, DateTimeOffset UpdatedAtUtc,
    string Decisao, string? Impedimento);
public sealed record EggPreview(string Token, string Operacao, IReadOnlyList<EggSourceExpectation> Origens,
    string Quantidade, string DeltaEstoque, string? SaldoAnterior, string? SaldoPosterior,
    string? CapacidadeRestante, string Unidade, string RevisaoAncora, string SequenciaAte,
    IReadOnlyList<string> Avisos, bool PodeConfirmar, DateTimeOffset ExpiraEmUtc);
public sealed record EggTreatmentPreview(string Token, Guid GrupoId, string Tipo, string DeltaEstoque,
    IReadOnlyList<string> Impedimentos, bool PodeConfirmar, DateTimeOffset ExpiraEmUtc);
public sealed record EggBridgeDetail(GrupoPonteOvos Grupo, IReadOnlyList<PonteProducaoOvo> Pontes,
    IReadOnlyList<TratamentoPonteOvos> Tratamentos, IReadOnlyList<EggSourceExpectation> OrigensAtuais,
    EggGroupReconciliation Conciliacao, IReadOnlyList<string> Avisos);
public sealed record EggMutationResult(int StatusHttp, string? Location, string RespostaJson, bool Replayed);
