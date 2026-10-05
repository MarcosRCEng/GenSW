export interface Propriedade {
  id: string
  nome: string
  localizacao: string | null
  observacao: string | null
  ativo: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface PropriedadesPage {
  items: Propriedade[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export type PropriedadeSortBy = 'nome' | 'ativo' | 'createdAtUtc'
export type SortDirection = 'asc' | 'desc'
export interface ListPropriedadesParams {
  page?: number
  pageSize?: number
  search?: string
  ativo?: boolean
  sortBy?: PropriedadeSortBy
  sortDirection?: SortDirection
}

export interface SavePropriedadeRequest {
  nome: string
  localizacao: string | null
  observacao: string | null
}

export interface AnimalPropriedadeVinculo {
  id: string
  animalId: string
  propriedadeId: string
  propriedadeNome: string
  propriedadeAtiva: boolean
  dataInicio: string
  dataFim: string | null
  observacao: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface AnimalPropriedades {
  atual: AnimalPropriedadeVinculo | null
  historico: AnimalPropriedadeVinculo[]
}

export interface TransferirPropriedadeRequest {
  propriedadeId: string
  dataInicio: string
  vinculoAtualIdEsperado: string | null
  observacao: string | null
}

export interface DesvincularPropriedadeRequest {
  dataFim: string
  vinculoAtualIdEsperado: string
}
