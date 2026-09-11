export type SexoAnimal = 1 | 2 | 3
export type EscopoAnimal = 1 | 2
export type AnimalSortBy = 'codigoInterno' | 'nome' | 'sexo' | 'escopo' | 'ativo' | 'createdAtUtc'
export type SortDirection = 'asc' | 'desc'

export interface AnimalEspecieResumo { id: string; nomeComum: string; ativo: boolean }
export interface AnimalRacaResumo { id: string; nome: string; ativo: boolean }
export interface AnimalVariedadeResumo { id: string; nome: string; ativo: boolean }

export interface Animal {
  id: string
  codigoInterno: string
  nome: string | null
  especieId: string
  racaId: string | null
  variedadeId: string | null
  sexo: SexoAnimal
  dataNascimento: string | null
  escopo: EscopoAnimal
  ativo: boolean
  createdAtUtc: string
  updatedAtUtc: string
  especie: AnimalEspecieResumo
  raca: AnimalRacaResumo | null
  variedade: AnimalVariedadeResumo | null
}

export interface CreateAnimalRequest {
  codigoInterno?: string | null
  nome?: string | null
  especieId: string
  racaId?: string | null
  variedadeId?: string | null
  sexo: SexoAnimal
  dataNascimento?: string | null
  escopo: EscopoAnimal
}

export interface UpdateAnimalRequest {
  codigoInterno: string
  nome?: string | null
  especieId: string
  racaId?: string | null
  variedadeId?: string | null
  sexo: SexoAnimal
  dataNascimento?: string | null
  escopo: EscopoAnimal
}

export interface AnimalsPage { items: Animal[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface ListAnimalsParams {
  page?: number; pageSize?: number; search?: string; especieId?: string; racaId?: string; variedadeId?: string
  sexo?: SexoAnimal; escopo?: EscopoAnimal; ativo?: boolean; sortBy?: AnimalSortBy; sortDirection?: SortDirection
}
