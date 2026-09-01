import { httpRequest } from '../../../shared/http/httpClient'
import type {
  CreateEspecieRequest,
  Especie,
  EspeciesPage,
  ListEspeciesParams,
  UpdateEspecieRequest,
  UpdateEspecieStatusRequest,
} from '../types/species'
import { parseEspecie, parseEspeciesPage } from './speciesContractParsers'

const SPECIES_ENDPOINT = '/especies'

function buildListPath(params?: ListEspeciesParams): string {
  const query = new URLSearchParams()

  if (params?.page !== undefined) query.set('page', String(params.page))
  if (params?.pageSize !== undefined) query.set('pageSize', String(params.pageSize))
  if (params?.search !== undefined) query.set('search', params.search)
  if (params?.ativo !== undefined) query.set('ativo', String(params.ativo))
  if (params?.sortBy !== undefined) query.set('sortBy', params.sortBy)
  if (params?.sortDirection !== undefined) query.set('sortDirection', params.sortDirection)

  const queryString = query.toString()
  return queryString ? `${SPECIES_ENDPOINT}?${queryString}` : SPECIES_ENDPOINT
}

export async function createEspecie(request: CreateEspecieRequest): Promise<Especie> {
  const response = await httpRequest<unknown>(SPECIES_ENDPOINT, {
    method: 'POST',
    authenticated: true,
    body: request,
  })

  return parseEspecie(response)
}

export async function getEspecieById(id: string): Promise<Especie> {
  const response = await httpRequest<unknown>(`${SPECIES_ENDPOINT}/${id}`, {
    authenticated: true,
  })

  return parseEspecie(response)
}

export async function listEspecies(
  params?: ListEspeciesParams,
): Promise<EspeciesPage> {
  const response = await httpRequest<unknown>(buildListPath(params), {
    authenticated: true,
  })

  return parseEspeciesPage(response)
}

export async function updateEspecie(
  id: string,
  request: UpdateEspecieRequest,
): Promise<Especie> {
  const response = await httpRequest<unknown>(`${SPECIES_ENDPOINT}/${id}`, {
    method: 'PUT',
    authenticated: true,
    body: request,
  })

  return parseEspecie(response)
}

export async function setEspecieAtivo(id: string, ativo: boolean): Promise<Especie> {
  const request: UpdateEspecieStatusRequest = { ativo }
  const response = await httpRequest<unknown>(`${SPECIES_ENDPOINT}/${id}/ativo`, {
    method: 'PATCH',
    authenticated: true,
    body: request,
  })

  return parseEspecie(response)
}
