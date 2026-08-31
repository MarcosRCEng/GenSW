export const TipoPessoa = {
  Fisica: 1,
  Juridica: 2,
} as const

export type TipoPessoa = (typeof TipoPessoa)[keyof typeof TipoPessoa]

export interface Pessoa {
  id: string
  tipoPessoa: TipoPessoa
  nome: string
  nomeFantasia: string | null
  ativo: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreatePessoaRequest {
  tipoPessoa: TipoPessoa
  nome: string
  nomeFantasia?: string | null
}

export interface UpdatePessoaRequest {
  nome: string
  nomeFantasia?: string | null
}

export interface UpdatePessoaStatusRequest {
  ativo: boolean
}

export interface PessoasPage {
  items: Pessoa[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export type PessoaSortBy = 'nome' | 'tipoPessoa' | 'ativo' | 'createdAtUtc'

export type SortDirection = 'asc' | 'desc'

export interface ListPessoasParams {
  page?: number
  pageSize?: number
  search?: string
  tipoPessoa?: TipoPessoa
  ativo?: boolean
  sortBy?: PessoaSortBy
  sortDirection?: SortDirection
}
