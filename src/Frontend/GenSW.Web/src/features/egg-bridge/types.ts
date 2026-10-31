export interface EggPage<T> { items: T[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface EggSource { id: string; animalId: string; dataPostura: string; pesoGramas: string | number; observacao: string | null; updatedAtUtc: string; createdAtUtc: string }
export interface EggCandidate { origem: EggSource; ponteId: string | null; grupoId: string | null }
export interface EggAnchor { id: string; itemId: string; revisao: string; autorId: string; createdAtUtc: string }
export interface EggScope { id: string; itemId: string; revisaoAncora: string; sequenciaAte: string; fronteira: string; fontesConsultadas: string; lacunas: string; motivo: string; responsavelId: string; autorId: string; createdAtUtc: string }
export type EggDecision = 'NaoRecebidoComprovado' | 'RecebidoComEquivalencia' | 'Pendente'
export interface EggProof { id: string; movimentoEstoqueId: string; eventoId: string; itemId: string; contagem: string; evidencia: string; motivo: string; responsavelId: string; autorId: string; createdAtUtc: string }
export interface EggSelection { producaoOvoId: string; ordinal?: string | null }
export type EggOperation = 'Entrada' | 'Associacao'
export type EggTreatment = 'Anotacao' | 'Invalidacao' | 'Compensacao'
export type EggCommandOperation = EggOperation | EggTreatment | 'Inscricao' | 'Recorte' | 'Prova'
export interface EggPreviewCommand { operacao: EggOperation; recorteId: string; responsavelId: string; motivo: string; evidencia: string; selecao: EggSelection[]; loteId: string | null; localId: string | null; provaId: string | null }
export interface EggSourceExpectation { producaoOvoId: string; hashOrigem: string; updatedAtUtc: string; decisao: string; impedimento: string | null }
export interface EggPreview { token: string; operacao: EggOperation; origens: EggSourceExpectation[]; quantidade: string; deltaEstoque: string; saldoAnterior: string | null; saldoPosterior: string | null; capacidadeRestante: string | null; unidade: 'un'; revisaoAncora: string; sequenciaAte: string; avisos: string[]; podeConfirmar: boolean; expiraEmUtc: string }
export interface EggGroup { id: string; operacao: EggOperation; itemId: string; loteId: string; localId: string; recorteId: string; provaId: string | null; eventoId: string; movimentoEstoqueId: string; quantidade: string; revisaoLoteOriginal: number; situacaoOriginal: string; validadeOriginal: string | null; snapshotLoteJson: string; autorId: string; responsavelId: string; motivo: string; evidencia: string; createdAtUtc: string }
export interface EggBridge { id: string; producaoOvoId: string; grupoId: string; snapshotJson: string; hashOrigem: string; origemUpdatedAtUtc: string; algoritmo: string }
export interface EggTreatmentFact { id: string; grupoId: string; tipo: EggTreatment; eventoId: string | null; autorId: string; responsavelId: string; motivo: string; evidencia: string; createdAtUtc: string }
export interface EggReconciliation { grupoId: string; coerente: boolean; divergencias: string[] }
export interface EggDetail { grupo: EggGroup; pontes: EggBridge[]; tratamentos: EggTreatmentFact[]; origensAtuais: EggSourceExpectation[]; conciliacao: EggReconciliation; avisos: string[] }
export interface EggTreatmentPreview { token: string; grupoId: string; tipo: EggTreatment; deltaEstoque: string; impedimentos: string[]; podeConfirmar: boolean; expiraEmUtc: string }
export type EggCommandResult = { kind: 'grupo'; operation: EggCommandOperation; detail: EggDetail } | { kind: 'inscricao'; anchor: EggAnchor } | { kind: 'recorte'; scope: EggScope } | { kind: 'prova'; proof: EggProof }
