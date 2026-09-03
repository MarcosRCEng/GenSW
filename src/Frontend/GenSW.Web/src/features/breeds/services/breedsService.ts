import { httpRequest } from '../../../shared/http/httpClient'
import type { CreateRacaRequest, ListRacasParams, Raca, RacasPage, UpdateRacaRequest, UpdateRacaStatusRequest } from '../types/breeds'
import { parseRaca, parseRacasPage } from './breedsContractParsers'

const endpoint = '/racas'
function buildListPath(params?: ListRacasParams): string { const query = new URLSearchParams(); if (params?.page !== undefined) query.set('page', String(params.page)); if (params?.pageSize !== undefined) query.set('pageSize', String(params.pageSize)); if (params?.search !== undefined) query.set('search', params.search); if (params?.especieId !== undefined) query.set('especieId', params.especieId); if (params?.ativo !== undefined) query.set('ativo', String(params.ativo)); if (params?.sortBy !== undefined) query.set('sortBy', params.sortBy); if (params?.sortDirection !== undefined) query.set('sortDirection', params.sortDirection); const text = query.toString(); return text ? `${endpoint}?${text}` : endpoint }
export async function createRaca(request: CreateRacaRequest): Promise<Raca> { return parseRaca(await httpRequest<unknown>(endpoint, { method: 'POST', authenticated: true, body: request })) }
export async function getRacaById(id: string): Promise<Raca> { return parseRaca(await httpRequest<unknown>(`${endpoint}/${id}`, { authenticated: true })) }
export async function listRacas(params?: ListRacasParams): Promise<RacasPage> { return parseRacasPage(await httpRequest<unknown>(buildListPath(params), { authenticated: true })) }
export async function updateRaca(id: string, request: UpdateRacaRequest): Promise<Raca> { return parseRaca(await httpRequest<unknown>(`${endpoint}/${id}`, { method: 'PUT', authenticated: true, body: request })) }
export async function setRacaAtivo(id: string, ativo: boolean): Promise<Raca> { const body: UpdateRacaStatusRequest = { ativo }; return parseRaca(await httpRequest<unknown>(`${endpoint}/${id}/ativo`, { method: 'PATCH', authenticated: true, body })) }
