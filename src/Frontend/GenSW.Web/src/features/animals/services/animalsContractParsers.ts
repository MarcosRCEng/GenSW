import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import type { Animal, AnimalEspecieResumo, AnimalRacaResumo, AnimalVariedadeResumo, AnimalsPage, EscopoAnimal, SexoAnimal } from '../types/animals'

function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value) }
function isNonEmptyString(value: unknown): value is string { return typeof value === 'string' && value.trim().length > 0 }
function isNullableString(value: unknown): value is string | null { return typeof value === 'string' || value === null }
function isIntegerAtLeast(value: unknown, minimum: number): value is number { return typeof value === 'number' && Number.isInteger(value) && value >= minimum }
function isCalendarDate(value: unknown): value is string {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day))
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day
}
function isIsoTimestamp(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$/.test(value) && !Number.isNaN(Date.parse(value))
}
function invalid(message: string): never { throw new InvalidApiResponseError(message) }

function parseEspecieResumo(value: unknown): AnimalEspecieResumo {
  if (!isRecord(value) || !isNonEmptyString(value.id) || typeof value.nomeComum !== 'string' || typeof value.ativo !== 'boolean') return invalid('A API retornou um resumo de espécie inválido.')
  return { id: value.id, nomeComum: value.nomeComum, ativo: value.ativo }
}
function parseRacaResumo(value: unknown): AnimalRacaResumo {
  if (!isRecord(value) || !isNonEmptyString(value.id) || typeof value.nome !== 'string' || typeof value.ativo !== 'boolean') return invalid('A API retornou um resumo de raça inválido.')
  return { id: value.id, nome: value.nome, ativo: value.ativo }
}
function parseVariedadeResumo(value: unknown): AnimalVariedadeResumo {
  if (!isRecord(value) || !isNonEmptyString(value.id) || typeof value.nome !== 'string' || typeof value.ativo !== 'boolean') return invalid('A API retornou um resumo de variedade inválido.')
  return { id: value.id, nome: value.nome, ativo: value.ativo }
}

export function parseAnimal(value: unknown): Animal {
  if (!isRecord(value) || !isNonEmptyString(value.id) || !isNonEmptyString(value.codigoInterno) || !isNullableString(value.nome) || !isNonEmptyString(value.especieId) || !(isNonEmptyString(value.racaId) || value.racaId === null) || !(isNonEmptyString(value.variedadeId) || value.variedadeId === null) || (value.sexo !== 1 && value.sexo !== 2 && value.sexo !== 3) || !(isCalendarDate(value.dataNascimento) || value.dataNascimento === null) || (value.escopo !== 1 && value.escopo !== 2) || typeof value.ativo !== 'boolean' || !isIsoTimestamp(value.createdAtUtc) || !isIsoTimestamp(value.updatedAtUtc)) {
    return invalid('A API retornou dados inválidos para o animal.')
  }

  return {
    id: value.id, codigoInterno: value.codigoInterno, nome: value.nome, especieId: value.especieId, racaId: value.racaId, variedadeId: value.variedadeId,
    sexo: value.sexo as SexoAnimal, dataNascimento: value.dataNascimento, escopo: value.escopo as EscopoAnimal, ativo: value.ativo,
    createdAtUtc: value.createdAtUtc, updatedAtUtc: value.updatedAtUtc,
    especie: parseEspecieResumo(value.especie), raca: value.raca === null ? null : parseRacaResumo(value.raca), variedade: value.variedade === null ? null : parseVariedadeResumo(value.variedade),
  }
}

export function parseAnimalsPage(value: unknown): AnimalsPage {
  if (!isRecord(value) || !Array.isArray(value.items) || !isIntegerAtLeast(value.page, 1) || !isIntegerAtLeast(value.pageSize, 1) || !isIntegerAtLeast(value.totalItems, 0) || !isIntegerAtLeast(value.totalPages, 0)) return invalid('A API retornou uma página de animais inválida.')
  return { items: value.items.map(parseAnimal), page: value.page, pageSize: value.pageSize, totalItems: value.totalItems, totalPages: value.totalPages }
}
