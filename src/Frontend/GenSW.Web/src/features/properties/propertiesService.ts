import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError } from '../../shared/http/httpErrors'
import type { AnimalPropriedades, AnimalPropriedadeVinculo, DesvincularPropriedadeRequest, ListPropriedadesParams, Propriedade, PropriedadesPage, SavePropriedadeRequest, TransferirPropriedadeRequest } from './types'

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value)
const text = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0
const optionalText = (value: unknown): value is string | null => value === null || typeof value === 'string'
const timestamp = (value: unknown): value is string => text(value) && Number.isFinite(Date.parse(value))
const date = (value: unknown): value is string => text(value) && /^\d{4}-\d{2}-\d{2}$/.test(value) && timestamp(value) && new Date(value).toISOString().slice(0, 10) === value
const integer = (value: unknown, min: number): value is number => typeof value === 'number' && Number.isInteger(value) && value >= min

export function parsePropriedade(value: unknown): Propriedade {
  if (!isRecord(value) || !text(value.id) || !text(value.nome) || !optionalText(value.localizacao) || !optionalText(value.observacao) || typeof value.ativo !== 'boolean' || !timestamp(value.createdAtUtc) || !timestamp(value.updatedAtUtc)) throw new InvalidApiResponseError('A API retornou dados inválidos para a propriedade.')
  return { id: value.id, nome: value.nome, localizacao: value.localizacao, observacao: value.observacao, ativo: value.ativo, createdAtUtc: value.createdAtUtc, updatedAtUtc: value.updatedAtUtc }
}

export function parsePropriedadesPage(value: unknown): PropriedadesPage {
  if (!isRecord(value) || !Array.isArray(value.items) || !integer(value.page, 1) || !integer(value.pageSize, 1) || !integer(value.totalItems, 0) || !integer(value.totalPages, 0)) throw new InvalidApiResponseError('A API retornou uma página de propriedades inválida.')
  return { items: value.items.map(parsePropriedade), page: value.page, pageSize: value.pageSize, totalItems: value.totalItems, totalPages: value.totalPages }
}

function parseVinculo(value: unknown): AnimalPropriedadeVinculo {
  if (!isRecord(value) || !text(value.id) || !text(value.animalId) || !text(value.propriedadeId) || !text(value.propriedadeNome) || typeof value.propriedadeAtiva !== 'boolean' || !date(value.dataInicio) || !(value.dataFim === null || date(value.dataFim)) || !optionalText(value.observacao) || !timestamp(value.createdAtUtc) || !timestamp(value.updatedAtUtc) || (value.dataFim !== null && value.dataFim < value.dataInicio)) throw new InvalidApiResponseError('A API retornou um vínculo com propriedade inválido.')
  return { id: value.id, animalId: value.animalId, propriedadeId: value.propriedadeId, propriedadeNome: value.propriedadeNome, propriedadeAtiva: value.propriedadeAtiva, dataInicio: value.dataInicio, dataFim: value.dataFim, observacao: value.observacao, createdAtUtc: value.createdAtUtc, updatedAtUtc: value.updatedAtUtc }
}

export function parseAnimalPropriedades(value: unknown): AnimalPropriedades {
  if (!isRecord(value) || !Array.isArray(value.historico)) throw new InvalidApiResponseError('A API retornou um histórico de propriedades inválido.')
  const atual = value.atual === null ? null : parseVinculo(value.atual)
  const historico = value.historico.map(parseVinculo)
  const abertos = historico.filter((item) => item.dataFim === null)
  if ((atual && (atual.dataFim !== null || abertos.length !== 1 || abertos[0].id !== atual.id)) || (!atual && abertos.length !== 0)) throw new InvalidApiResponseError('A API retornou vínculos atuais inconsistentes.')
  return { atual, historico }
}

export async function listPropriedades(params: ListPropriedadesParams = {}): Promise<PropriedadesPage> {
  const query = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => { if (value !== undefined) query.set(key, String(value)) })
  return parsePropriedadesPage(await httpRequest<unknown>(`/propriedades${query.size ? `?${query}` : ''}`, { authenticated: true }))
}

export async function getPropriedadeById(id: string): Promise<Propriedade> {
  return parsePropriedade(await httpRequest<unknown>(`/propriedades/${id}`, { authenticated: true }))
}

export async function createPropriedade(body: SavePropriedadeRequest): Promise<Propriedade> {
  return parsePropriedade(await httpRequest<unknown>('/propriedades', { method: 'POST', authenticated: true, body }))
}

export async function updatePropriedade(id: string, body: SavePropriedadeRequest): Promise<Propriedade> {
  return parsePropriedade(await httpRequest<unknown>(`/propriedades/${id}`, { method: 'PUT', authenticated: true, body }))
}

export async function setPropriedadeAtivo(id: string, ativo: boolean): Promise<Propriedade> {
  return parsePropriedade(await httpRequest<unknown>(`/propriedades/${id}/ativo`, { method: 'PATCH', authenticated: true, body: { ativo } }))
}

export async function getAnimalPropriedades(animalId: string): Promise<AnimalPropriedades> {
  return parseAnimalPropriedades(await httpRequest<unknown>(`/animais/${animalId}/propriedades`, { authenticated: true }))
}

export async function transferirAnimalPropriedade(animalId: string, body: TransferirPropriedadeRequest): Promise<AnimalPropriedades> {
  return parseAnimalPropriedades(await httpRequest<unknown>(`/animais/${animalId}/propriedade/transferir`, { method: 'POST', authenticated: true, body }))
}

export async function desvincularAnimalPropriedade(animalId: string, body: DesvincularPropriedadeRequest): Promise<AnimalPropriedades> {
  return parseAnimalPropriedades(await httpRequest<unknown>(`/animais/${animalId}/propriedade/desvincular`, { method: 'POST', authenticated: true, body }))
}
