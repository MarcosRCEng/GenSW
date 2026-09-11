import { httpRequest } from '../../../shared/http/httpClient'
import type { Animal, AnimalsPage, CreateAnimalRequest, ListAnimalsParams, UpdateAnimalRequest } from '../types/animals'
import { parseAnimal, parseAnimalsPage } from './animalsContractParsers'

const ANIMALS_ENDPOINT = '/animais'

function buildListPath(params?: ListAnimalsParams): string {
  const query = new URLSearchParams()
  if (params?.page !== undefined) query.set('page', String(params.page))
  if (params?.pageSize !== undefined) query.set('pageSize', String(params.pageSize))
  if (params?.search !== undefined) query.set('search', params.search)
  if (params?.especieId !== undefined) query.set('especieId', params.especieId)
  if (params?.racaId !== undefined) query.set('racaId', params.racaId)
  if (params?.variedadeId !== undefined) query.set('variedadeId', params.variedadeId)
  if (params?.sexo !== undefined) query.set('sexo', String(params.sexo))
  if (params?.escopo !== undefined) query.set('escopo', String(params.escopo))
  if (params?.ativo !== undefined) query.set('ativo', String(params.ativo))
  if (params?.sortBy !== undefined) query.set('sortBy', params.sortBy)
  if (params?.sortDirection !== undefined) query.set('sortDirection', params.sortDirection)
  const text = query.toString()
  return text ? `${ANIMALS_ENDPOINT}?${text}` : ANIMALS_ENDPOINT
}

export async function createAnimal(request: CreateAnimalRequest): Promise<Animal> { return parseAnimal(await httpRequest<unknown>(ANIMALS_ENDPOINT, { method: 'POST', authenticated: true, body: request })) }
export async function getAnimalById(id: string): Promise<Animal> { return parseAnimal(await httpRequest<unknown>(`${ANIMALS_ENDPOINT}/${id}`, { authenticated: true })) }
export async function listAnimals(params?: ListAnimalsParams): Promise<AnimalsPage> { return parseAnimalsPage(await httpRequest<unknown>(buildListPath(params), { authenticated: true })) }
export async function updateAnimal(id: string, request: UpdateAnimalRequest): Promise<Animal> { return parseAnimal(await httpRequest<unknown>(`${ANIMALS_ENDPOINT}/${id}`, { method: 'PUT', authenticated: true, body: request })) }
export async function setAnimalAtivo(id: string, ativo: boolean): Promise<Animal> { return parseAnimal(await httpRequest<unknown>(`${ANIMALS_ENDPOINT}/${id}/ativo`, { method: 'PATCH', authenticated: true, body: { ativo } })) }
