import type { Conversion, Profile, RecipeData, RecipeHeader, RecipeVersion, Page } from '../formulation/types'
import type { Event, QuantizationAcceptance } from '../inventory/types'

export type OrderState = 'Rascunho' | 'Planejada' | 'Iniciada' | 'Confirmada' | 'Cancelada' | 'AbortadaSemConsumo' | 'Revertida'
export interface PlanSnapshot {
  receita: RecipeVersion; cabecalho: RecipeHeader; escalonada: RecipeData
  itens: { id: string; codigo: string; nome: string; unidade: string; classe: string; revisao: number }[]
  conversoes: Conversion[]; perfis: Profile[]; dependencias: RecipeVersion[]; algoritmo: string
  responsavel?: { id: string; nome: string; ativo: boolean } | null
}
export interface Reservation {
  id: string; linhaId: string; loteId: string; localId: string; unidade: string
  quantidadeOriginal: string; quantidadeAtiva: string; quantidadeConsumida: string; quantidadeLiberada: string
  conversaoItemId?: string | null; quantidadeDeclarada?: string; unidadeDeclarada?: string
  aceiteQuantizacao?: QuantizationAcceptance | null
}
export interface Execution {
  id: string; ordemId: string; resultado: string; autorId: string; responsavelId: string
  createdAtUtc: string; dataOperacional: string; dataObservada: string | null; snapshotJson: string
  movimentos: { id: string; movimentoEstoqueId: string; papel: string; linhaId: string }[]
  lotesSaida: { id: string; loteId: string; localId: string; linhaId: string; quantidade: string; unidade: string; codigoLote?: string; localCodigo?: string; localNome?: string }[]
  ajustesPresentes?: Event[]
}
export interface ProductionOrder {
  id: string; codigo: string; estado: OrderState; revisao: number; receitaVersaoId: string
  tamanho: string; unidade: string; responsavelId: string; propriedadeId: string | null; propriedadeNome: string | null
  createdAtUtc: string; planejadaEmUtc: string | null; iniciadaEmUtc: string | null; confirmadaEmUtc: string | null
  planejamento: PlanSnapshot | null; reservas: Reservation[]; execucao: Execution | null
  acoesPermitidas: string[]; impedimentos: string[]
}
export interface OrderCommand {
  id: string; codigo: string; receitaVersaoId: string; tamanho: string; unidade: string
  responsavelId: string; propriedadeId: string | null; versaoEsperada: number; motivo: string
}
export interface Allocation {
  id: string; loteId: string; localId: string; quantidade: string; unidade: string
  conversaoItemId: string | null; aceiteQuantizacao: QuantizationAcceptance | null
}
export interface ProductionInput { linhaId: string; alocacoes: Allocation[] }
export interface ProductionOutput {
  id: string; linhaId: string; quantidade: string; unidade: string; novoCodigoLote: string; localId: string
  dataOrigem: string | null; fabricacao: string | null; coleta: string | null; validade: string | null
  fonteValidade: string | null; responsavelValidadeId: string | null; perfilNutricionalId: string | null
  conversaoItemId: string | null; aplicabilidade: string | null; aceiteQuantizacao: QuantizationAcceptance | null; bloqueado: boolean
}
export interface ProductionLoss { quantidade: string; unidade: string; motivo: string; linhaId: string | null }
export interface Confirmation {
  versaoEsperada: number; responsavelId: string; entradas: ProductionInput[]; saidas: ProductionOutput[]
  perdas: ProductionLoss[]; motivo: string; observacao: string | null; dataObservada: string | null
  evidencia: string | null; aptidaoNoFato: string | null
}
export interface ProductionHistory {
  id: string; ordemId: string; operacao: string; motivo: string; autorId: string; createdAtUtc: string
  antesJson: string; depoisJson: string
}
export interface GenealogyEdge { execucaoId: string; ordemId: string; loteEntradaId: string; loteSaidaId: string }
export interface Reconciliation { ordemId: string; codigo: string; estado: string; coerente: boolean; divergencias: string[] }
export interface MutationResult { statusHttp: number; location: string | null; respostaJson: string; replayed: boolean }
export type ProductionPage<T> = Page<T>
