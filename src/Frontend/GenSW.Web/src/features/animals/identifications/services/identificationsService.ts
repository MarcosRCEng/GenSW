import { httpRequest } from '../../../../shared/http/httpClient'
import type { AnimalIdentification, AnimalIdentificationPage, CreateAnimalIdentificationRequest, GlobalAnimalIdentificationPage, ListAnimalIdentificationsParams, UpdateAnimalIdentificationMetadataRequest } from '../types'
import { parseAnimalIdentification, parseAnimalIdentificationsPage, parseGlobalAnimalIdentificationsPage } from './identificationsContractParsers'

const GLOBAL_ENDPOINT = '/identificacoes-animal'
function endpoint(animalId: string): string { return `/animais/${animalId}/identificacoes` }
function buildListPath(base: string, params?: ListAnimalIdentificationsParams): string {
  const query = new URLSearchParams()
  if (params?.page !== undefined) query.set('page', String(params.page))
  if (params?.pageSize !== undefined) query.set('pageSize', String(params.pageSize))
  if (params?.tipo !== undefined) query.set('tipo', String(params.tipo))
  if (params?.valor !== undefined) query.set('valor', params.valor)
  if (params?.ativo !== undefined) query.set('ativo', String(params.ativo))
  if (params?.principal !== undefined) query.set('principal', String(params.principal))
  const text = query.toString()
  return text ? `${base}?${text}` : base
}

export async function listAnimalIdentifications(animalId: string, params?: ListAnimalIdentificationsParams): Promise<AnimalIdentificationPage> { return parseAnimalIdentificationsPage(await httpRequest<unknown>(buildListPath(endpoint(animalId), params), { authenticated: true })) }
export async function getAnimalIdentification(animalId: string, identificationId: string): Promise<AnimalIdentification> { return parseAnimalIdentification(await httpRequest<unknown>(`${endpoint(animalId)}/${identificationId}`, { authenticated: true })) }
export async function createAnimalIdentification(animalId: string, request: CreateAnimalIdentificationRequest): Promise<AnimalIdentification> { return parseAnimalIdentification(await httpRequest<unknown>(endpoint(animalId), { method: 'POST', authenticated: true, body: request })) }
export async function updateAnimalIdentificationMetadata(animalId: string, identificationId: string, request: UpdateAnimalIdentificationMetadataRequest): Promise<AnimalIdentification> { return parseAnimalIdentification(await httpRequest<unknown>(`${endpoint(animalId)}/${identificationId}`, { method: 'PATCH', authenticated: true, body: request })) }
export async function setAnimalIdentificationAtivo(animalId: string, identificationId: string, ativo: boolean): Promise<AnimalIdentification> { return parseAnimalIdentification(await httpRequest<unknown>(`${endpoint(animalId)}/${identificationId}/ativo`, { method: 'PATCH', authenticated: true, body: { ativo } })) }
export async function setAnimalIdentificationPrincipal(animalId: string, identificationId: string, principal: boolean): Promise<AnimalIdentification> { return parseAnimalIdentification(await httpRequest<unknown>(`${endpoint(animalId)}/${identificationId}/principal`, { method: 'PATCH', authenticated: true, body: { principal } })) }
export async function listGlobalAnimalIdentifications(params?: ListAnimalIdentificationsParams): Promise<GlobalAnimalIdentificationPage> { return parseGlobalAnimalIdentificationsPage(await httpRequest<unknown>(buildListPath(GLOBAL_ENDPOINT, params), { authenticated: true })) }
