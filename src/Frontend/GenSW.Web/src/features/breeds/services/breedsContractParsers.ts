import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import type { Raca, RacasPage } from '../types/breeds'

function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value) }
function isNonEmptyString(value: unknown): value is string { return typeof value === 'string' && value.trim().length > 0 }
function isIsoDateString(value: unknown): value is string { return isNonEmptyString(value) && !Number.isNaN(Date.parse(value)) }
function isIntegerAtLeast(value: unknown, minimum: number): value is number { return typeof value === 'number' && Number.isInteger(value) && value >= minimum }

export function parseRaca(value: unknown): Raca {
  if (!isRecord(value) || !isNonEmptyString(value.id) || !isNonEmptyString(value.especieId) || typeof value.nome !== 'string' || typeof value.ativo !== 'boolean' || !isIsoDateString(value.createdAtUtc) || !isIsoDateString(value.updatedAtUtc) || !isRecord(value.especie) || !isNonEmptyString(value.especie.id) || typeof value.especie.nomeComum !== 'string' || typeof value.especie.ativo !== 'boolean') throw new InvalidApiResponseError('A API retornou dados inválidos para a raça.')
  return { id: value.id, especieId: value.especieId, nome: value.nome, ativo: value.ativo, createdAtUtc: value.createdAtUtc, updatedAtUtc: value.updatedAtUtc, especie: { id: value.especie.id, nomeComum: value.especie.nomeComum, ativo: value.especie.ativo } }
}

export function parseRacasPage(value: unknown): RacasPage {
  if (!isRecord(value) || !Array.isArray(value.items) || !isIntegerAtLeast(value.page, 1) || !isIntegerAtLeast(value.pageSize, 1) || !isIntegerAtLeast(value.totalItems, 0) || !isIntegerAtLeast(value.totalPages, 0)) throw new InvalidApiResponseError('A API retornou uma página de raças inválida.')
  return { items: value.items.map(parseRaca), page: value.page, pageSize: value.pageSize, totalItems: value.totalItems, totalPages: value.totalPages }
}
