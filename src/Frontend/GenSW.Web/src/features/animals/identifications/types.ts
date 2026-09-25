export type AnimalIdentificationType = 1 | 2 | 3 | 4 | 5 | 6

export interface AnimalIdentification {
  id: string
  animalId: string
  tipo: AnimalIdentificationType
  descricaoTipo: string | null
  valor: string
  principal: boolean
  dataAplicacao: string | null
  observacao: string | null
  ativo: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface AnimalIdentificationPage {
  items: AnimalIdentification[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface AnimalIdentificationAnimalSummary { id: string; codigoInterno: string; nome: string | null }
export interface GlobalAnimalIdentification { identificacao: AnimalIdentification; animal: AnimalIdentificationAnimalSummary }
export interface GlobalAnimalIdentificationPage { items: GlobalAnimalIdentification[]; page: number; pageSize: number; totalItems: number; totalPages: number }

export interface CreateAnimalIdentificationRequest {
  tipo: AnimalIdentificationType
  descricaoTipo?: string | null
  valor: string
  principal: boolean
  dataAplicacao?: string | null
  observacao?: string | null
}

export interface UpdateAnimalIdentificationMetadataRequest { dataAplicacao?: string | null; observacao?: string | null }
export interface ListAnimalIdentificationsParams { page?: number; pageSize?: number; tipo?: AnimalIdentificationType; valor?: string; ativo?: boolean; principal?: boolean }
