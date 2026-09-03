export interface RacaEspecieResumo { id: string; nomeComum: string; ativo: boolean }
export interface Raca { id: string; especieId: string; nome: string; ativo: boolean; createdAtUtc: string; updatedAtUtc: string; especie: RacaEspecieResumo }
export type RacaSortBy = 'nome' | 'ativo' | 'createdAtUtc'
export type SortDirection = 'asc' | 'desc'
export interface CreateRacaRequest { especieId: string; nome: string }
export interface UpdateRacaRequest { especieId: string; nome: string }
export interface UpdateRacaStatusRequest { ativo: boolean }
export interface RacasPage { items: Raca[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface ListRacasParams { page?: number; pageSize?: number; search?: string; especieId?: string; ativo?: boolean; sortBy?: RacaSortBy; sortDirection?: SortDirection }
