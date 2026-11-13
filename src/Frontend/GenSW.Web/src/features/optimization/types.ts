import type { Goal, NutrientValue, Profile, RecipeData, RecipeHeader } from '../formulation/types'

export type Outcome = 'SolucaoViavel' | 'InviabilidadeDemonstrada' | 'DadosIncompletos' | 'DadosIncompativeis' | 'LimiteTempo' | 'ErroAlgoritmo' | 'Inconclusivo'

export interface OptimizationGoal extends Goal {
  id: string
  justificativa: string | null
}

export interface Candidate {
  id: string
  itemId: string
  itemRevisao: number
  perfilId: string | null
  perfilRevisao: number | null
  referenciaKg: string
  inclusaoMinima: string | null
  inclusaoMaxima: string | null
  justificativa: string | null
}

export interface OptimizationRequest {
  versaoModelo: 'f05-n1/1'
  modo: 'N1'
  disponibilidade: false
  massaKg: string
  referenciaDescricao: string | null
  receitaId: string | null
  receitaRevisao: number | null
  receitaVersaoId: string | null
  receitaVersaoRevisao: number | null
  anteriorId: string | null
  especieId: string | null
  fase: string | null
  preparo: string | null
  candidatos: Candidate[]
  metas: OptimizationGoal[]
}

export interface Coefficient {
  metaId: string
  valorNormalizado: string | null
  unidade: string | null
  estado: string
  codigo?: string | null
  fonte?: string | null
  origem?: string | null
  hipotese?: string | null
  observacao?: NutrientValue | null
  materiaSeca?: NutrientValue | null
  valorExato?: string | null
  fracaoSeca?: string | null
}

export interface ResolvedCandidate {
  id: string
  itemId: string
  nome: string
  excluido: boolean
  perfil?: Profile | null
  coeficientes: Coefficient[]
}

export interface OptimizationInputs {
  corteUtc: string
  candidatos: ResolvedCandidate[]
  metas: OptimizationGoal[]
  diagnosticos: { codigo: string; mensagem: string; desfecho: Outcome; candidatoId?: string; metaId?: string }[]
  composicaoReferencia?: Composition | null
  definicoes?: { codigo: string; nome: string; versao: number }[] | null
}

export interface Composition {
  massaKg: string | null
  materiaSecaKg: string | null
  umidadePercentual: string | null
  componentes: {
    componente: string; metodo: string; contexto: string; unidade: string; estado: string
    valorBN: string | null; valorMS: string | null; contribuicaoConhecida: string | null; coberturaPercentual: string | null
    estimado: boolean; estimadoMS: boolean
  }[]
  avisos: string[]
}

export interface OptimizationSummary {
  id: string
  autorId: string
  createdAtUtc: string
  estado: 'EmCalculo' | 'Finalizada'
  desfecho?: Outcome | null
  especieId?: string | null
  fase?: string | null
}

export interface Constraint {
  id: string
  kind: string
  referenceId?: string | null
  unit: string
  value: string
  minimum?: string | null
  maximum?: string | null
  lowerSlack?: string | null
  upperSlack?: string | null
  active: boolean
  nearBoundary: boolean
  violated: boolean
  activityTolerance?: string | null
}

export interface Solution {
  rawMasses: Record<string, string>
  quantizedMasses?: Record<string, string> | null
  residualsKg?: Record<string, string> | null
  rawDeviationKg: string
  quantizedDeviationKg?: string | null
  redistributedKg?: string | null
  constraints: Constraint[]
  contributions: { goalId: string; ingredientId: string; value: string; unit: string }[]
  objectiveQuality: string
}

export interface SolverResult {
  outcome: Outcome
  code: string
  candidate?: Solution | null
  proof?: { code: string; constraintIds: string[]; details: string[]; multipliers?: Record<string, string> | null } | null
  model?: { version: string } | null
  native?: {
    status: string
    code?: string | null
    rawStatus?: string | number | null
    objective?: number | string | null
    metadata?: {
      version?: string | null
      binarySha256?: string | null
      runtime?: string | null
      seconds?: number | null
      budgetSeconds?: number | null
      algorithm?: string | null
      threads?: number | null
      seed?: number | null
      primalTolerance?: number | null
      dualTolerance?: number | null
      smallMatrixValue?: number | null
      highsIntBytes?: number | null
      nativeTimeLimitSeconds?: number | null
    } | null
  } | null
}

export interface OptimizationExecution extends OptimizationSummary {
  revisao: number
  pedido: OptimizationRequest
  entradas: OptimizationInputs
  hashPedido?: string | null
  hashResultado?: string | null
  resultado: {
    desfecho: Outcome
    codigo: string
    motor?: SolverResult | null
    composicao?: Composition | null
    versaoAplicacao?: string | null
    hashEntradas?: string | null
    hashModelo?: string | null
    hashModeloNativo?: string | null
    correlacao?: string | null
  } | null
}

export interface DraftDestination {
  receitaId: string | null
  receitaRevisao: number | null
  saidaItemId: string
  saidaItemRevisao: number
  novaReceita: RecipeHeader | null
}

export interface DraftPreview {
  hash: string
  conteudo: RecipeData
  avisos: string[]
  exigeAceiteResiduos: boolean
}

export interface DraftCommand {
  destino: DraftDestination
  hashPrevia: string
  aceitaResiduos: boolean
  aceitaAvisos: boolean
}

export interface DraftReceipt {
  id: string
  otimizacaoId: string
  receitaId: string
  versaoId: string
  createdAtUtc: string
}
