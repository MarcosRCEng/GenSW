namespace GenSW.Domain.EggBridge;

// F03 facts are append-only. Revision is owned by the database credit protocol.
public sealed record AncoraConciliacaoOvos(Guid Id, Guid ItemId, long Revisao, Guid AutorId, DateTimeOffset CreatedAtUtc, long SequenciaInicial = 0);
public sealed record RecorteConciliacaoOvos(Guid Id, Guid ItemId, long RevisaoAncora, long SequenciaAte,
    string Fronteira, string FontesConsultadas, string Lacunas, string Motivo, Guid AutorId, Guid ResponsavelId, DateTimeOffset CreatedAtUtc, long OrdemAprovacao = 0);
public sealed record DecisaoConciliacaoOvo(Guid Id, Guid RecorteId, Guid ProducaoOvoId, string Decisao,
    bool IndividualComprovado, string Evidencia, string HashOrigem, DateTimeOffset OrigemUpdatedAtUtc);
public sealed record ProvaEntradaOvos(Guid Id, Guid MovimentoEstoqueId, Guid EventoId, Guid ItemId, long Contagem,
    string Evidencia, string Motivo, Guid AutorId, Guid ResponsavelId, DateTimeOffset CreatedAtUtc);
public sealed record GrupoPonteOvos(Guid Id, string Operacao, Guid ItemId, Guid LoteId, Guid LocalId, Guid RecorteId,
    Guid? ProvaId, Guid EventoId, Guid MovimentoEstoqueId, long Quantidade, int RevisaoLoteOriginal,
    string SituacaoOriginal, DateOnly? ValidadeOriginal, string SnapshotLoteJson, Guid AutorId, Guid ResponsavelId,
    string Motivo, string Evidencia, DateTimeOffset CreatedAtUtc);
public sealed record PonteProducaoOvo(Guid Id, Guid ProducaoOvoId, Guid GrupoId, string SnapshotJson,
    string HashOrigem, DateTimeOffset OrigemUpdatedAtUtc, string Algoritmo);
public sealed record AlocacaoProvaOvos(Guid Id, Guid PonteId, Guid ProvaId, Guid MovimentoEstoqueId, long Ordinal);
public sealed record OrigemLoteOvos(Guid Id, Guid LoteId, Guid GrupoId);
public sealed record TratamentoPonteOvos(Guid Id, Guid GrupoId, string Tipo, Guid? EventoId,
    Guid AutorId, Guid ResponsavelId, string Motivo, string Evidencia, DateTimeOffset CreatedAtUtc);
public sealed record ComandoPonteOvos(Guid Id, string Operacao, string Chave, string HashPayload, Guid AutorId,
    Guid? GrupoId, bool ExigeAdmin, int StatusHttp, string? Location, string RespostaJson, DateTimeOffset CreatedAtUtc);
public sealed record OrigemRecebimentoOvos(Guid Id, Guid EventoId, Guid ItemId, Guid RecorteId,
    string Evidencia, Guid AutorId, DateTimeOffset CreatedAtUtc);
