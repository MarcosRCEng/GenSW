export interface VariedadeEspecieResumo { id: string; nomeComum: string; ativo: boolean }
export interface Variedade { id: string; especieId: string; nome: string; ativo: boolean; createdAtUtc: string; updatedAtUtc: string; especie: VariedadeEspecieResumo }
export type VariedadeSortBy = 'nome' | 'ativo' | 'createdAtUtc'
export type SortDirection = 'asc' | 'desc'
export interface CreateVariedadeRequest { especieId: string; nome: string }
export interface UpdateVariedadeRequest { especieId: string; nome: string }
export interface UpdateVariedadeStatusRequest { ativo: boolean }
export interface VariedadesPage { items: Variedade[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface ListVariedadesParams { page?: number; pageSize?: number; search?: string; especieId?: string; ativo?: boolean; sortBy?: VariedadeSortBy; sortDirection?: SortDirection }
