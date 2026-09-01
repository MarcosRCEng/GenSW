export interface Especie {
  id: string
  nomeComum: string
  nomeCientifico: string | null
  ativo: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export type EspecieSortBy = 'nomeComum' | 'nomeCientifico' | 'ativo' | 'createdAtUtc'
export type SortDirection = 'asc' | 'desc'

export interface CreateEspecieRequest {
  nomeComum: string
  nomeCientifico?: string | null
}

export interface UpdateEspecieRequest {
  nomeComum: string
  nomeCientifico?: string | null
}

export interface UpdateEspecieStatusRequest {
  ativo: boolean
}

export interface EspeciesPage {
  items: Especie[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface ListEspeciesParams {
  page?: number
  pageSize?: number
  search?: string
  ativo?: boolean
  sortBy?: EspecieSortBy
  sortDirection?: SortDirection
}
