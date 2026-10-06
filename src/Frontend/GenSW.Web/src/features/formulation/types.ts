export interface Page<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}
export interface Category {
  id: string
  nome: string
  ativo: boolean
  revisao: number
}
export interface ItemData {
  codigo: string
  nome: string
  descricao: string | null
  categoriaId: string | null
  classe: string
  unidade: string
  podeEntrar: boolean
  podeProduzir: boolean
  usoInterno: boolean
  venda: boolean
}
export interface Item extends ItemData {
  id: string
  ativo: boolean
  revisao: number
  unidadeFixada: boolean
  createdAtUtc: string
  updatedAtUtc: string
}
export interface ConversionData {
  origem: string
  destino: string
  fator: string
  fonte: string
  metodo: string
  dataFonte: string
  contexto: string
  referenciaAmostra: string
  proveniencia: string
}
export interface Conversion extends Omit<ConversionData, 'fator'> {
  id: string
  itemId: string
  numero: number
  fator: string
}
export interface Component {
  codigo: string
  nome: string
  grandeza: string
  semantica: string
  versao: number
}
export interface NutrientValue {
  componente: string
  estado: string
  valor: string | null
  origem: string | null
  base: string
  unidade: string
  metodo: string
  contexto: string
  qualificador: string
  motivo: string | null
  fonte: string | null
  hipotese: string | null
}
export interface ProfileData {
  nome: string
  fonte: string
  metodo: string
  preparacao: string
  referenciaAmostra: string
  contexto: string
  especieId: string | null
  fase: string | null
  dataFonte: string | null
  dataColeta: string | null
  valores: NutrientValue[]
}
export interface Profile {
  id: string
  itemId: string
  numero: number
  revisao: number
  estado: string
  createdAtUtc: string
  publishedAtUtc: string | null
  conteudo: ProfileData
}
export interface RecipeHeader {
  codigo: string
  nome: string
  finalidade: string
}
export interface Recipe extends RecipeHeader {
  id: string
  ativo: boolean
  revisao: number
}
export interface RecipeInput {
  id: string
  itemId: string
  papel: string
  quantidade: string
  unidade: string
  escala: string
  perfilId: string | null
  conversaoId: string | null
  subReceitaVersaoId: string | null
  subSaidaId: string | null
  inclusaoMinima: string | null
  inclusaoMaxima: string | null
}
export interface RecipeOutput {
  id: string
  itemId: string
  nome: string
  quantidade: string
  unidade: string
  escala: string
  principal: boolean
  perfilId: string | null
  conversaoId: string | null
  massaLiquidaKg: string | null
  massaDrenadaKg: string | null
  massaBrutaKg: string | null
  metodoMedicao: string | null
}
export interface RecipeData {
  tipo: string
  modo: string
  tamanhoReferencia: string
  unidadeReferencia: string
  entradas: RecipeInput[]
  saidas: RecipeOutput[]
  perdas: {
    quantidade: string
    unidade: string
    motivo: string
    escala: string
  }[]
  etapas: { ordem: number; descricao: string }[]
  observacao: string | null
  retencoes: { componente: string; fator: string; hipotese: string }[]
}
export interface RecipeVersion {
  id: string
  receitaId: string
  numero: number
  revisao: number
  estado: string
  conteudo: RecipeData
}
export interface Goal {
  componente: string
  metodo: string
  contexto: string
  base: string
  unidade: string
  minimo: string | null
  maximo: string | null
  origem: string
  fonte: string | null
  aceitarEstimados: boolean
}
export interface SimulationCommand {
  receitaVersaoId: string
  tamanho: string
  unidade: string
  especieId: string | null
  fase: string | null
  metas: Goal[]
  variacao: RecipeData | null
  anteriorId: string | null
}
export interface Contribution {
  linhaId: string
  item: string
  massaKg: string | null
  quantidadeComponente: string | null
  origem: string | null
  fonte: string | null
  motivo: string | null
}
export interface NutrientResult {
  componente: string
  metodo: string
  contexto: string
  unidade: string
  estado: string
  valorBN: string | null
  valorMS: string | null
  contribuicaoConhecidaBN: string | null
  coberturaPercentual: string | null
  estimado: boolean
  estimadoMS?: boolean
  contribuicoes: Contribution[]
  kcalBN: string | null
}
export interface NutritionResult {
  motor: string
  massaKg: string | null
  materiaSecaKg: string | null
  umidadePercentual: string | null
  componentes: NutrientResult[]
  metas: {
    meta: Goal
    estado: string
    motivo: string
    impossibilidadeLocal: boolean
  }[]
  avisos: string[]
}
export interface Simulation {
  id: string
  createdAtUtc: string
  conteudo: {
    pedido: SimulationCommand
    receita: RecipeVersion
    cabecalho: RecipeHeader
    escalonada: RecipeData
    resultado: NutritionResult
    rendimentoPercentual: string | null
    balancoKg: string | null
    problemas: string[]
    linhas: {
      nome: string
      perfilId: string | null
      perfilNumero: number | null
      perfil: ProfileData | null
      caminho: string | null
    }[]
  }
}
export interface Summary {
  id: string
  tipo: string
  createdAtUtc: string
}
export interface Comparison {
  id: string
  conteudo: {
    pedido: { simulacaoIds: string[]; base: string }
    simulacoes: Simulation[]
    linhas: {
      componente: string
      metodo: string
      contexto: string
      unidade: string
      comparavel: boolean
      valores: {
        simulacaoId: string
        valor: string | null
        estado: string
        estimado: boolean
      }[]
    }[]
  }
}
export interface Audit {
  id: string
  tipo: string
  operacao: string
  autorId: string
  createdAtUtc: string
  antesJson: string
  depoisJson: string
}
