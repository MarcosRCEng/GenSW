import { InvalidApiResponseError } from '../../../../shared/http/httpErrors'
import type { AnimalIdentification, AnimalIdentificationAnimalSummary, AnimalIdentificationPage, AnimalIdentificationType, GlobalAnimalIdentification, GlobalAnimalIdentificationPage } from '../types'

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
  if (typeof value !== 'string') return false
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?(Z|[+-](\d{2}):(\d{2}))$/.exec(value)
  if (match === null) return false

  const [, yearText, monthText, dayText, hourText, minuteText, secondText, zone, offsetHourText, offsetMinuteText] = match
  const year = Number(yearText)
  const month = Number(monthText)
  const day = Number(dayText)
  const hour = Number(hourText)
  const minute = Number(minuteText)
  const second = Number(secondText)
  if (year < 1 || month < 1 || month > 12 || hour > 23 || minute > 59 || second > 59) return false
  const daysInMonth = month === 2 ? (year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0) ? 29 : 28) : [4, 6, 9, 11].includes(month) ? 30 : 31
  if (day < 1 || day > daysInMonth) return false
  if (zone === 'Z') return true

  const offsetHour = Number(offsetHourText)
  const offsetMinute = Number(offsetMinuteText)
  if (offsetHour > 14 || offsetMinute > 59 || (offsetHour === 14 && offsetMinute !== 0)) return false

  const offsetSeconds = offsetHour * 3_600 + offsetMinute * 60
  const localSeconds = hour * 3_600 + minute * 60 + second
  if (year === 1 && month === 1 && day === 1 && zone.startsWith('+') && localSeconds < offsetSeconds) return false
  if (year === 9999 && month === 12 && day === 31 && zone.startsWith('-') && localSeconds + offsetSeconds >= 86_400) return false
  return true
}
function invalid(message: string): never { throw new InvalidApiResponseError(message) }

export function parseAnimalIdentification(value: unknown): AnimalIdentification {
  if (!isRecord(value) || !isNonEmptyString(value.id) || !isNonEmptyString(value.animalId) || ![1, 2, 3, 4, 5, 6].includes(value.tipo as number) || !isNullableString(value.descricaoTipo) || !isNonEmptyString(value.valor) || typeof value.principal !== 'boolean' || !(isCalendarDate(value.dataAplicacao) || value.dataAplicacao === null) || !isNullableString(value.observacao) || typeof value.ativo !== 'boolean' || !isIsoTimestamp(value.createdAtUtc) || !isIsoTimestamp(value.updatedAtUtc)) return invalid('A API retornou dados inválidos para a identificação do animal.')
  return { id: value.id, animalId: value.animalId, tipo: value.tipo as AnimalIdentificationType, descricaoTipo: value.descricaoTipo, valor: value.valor, principal: value.principal, dataAplicacao: value.dataAplicacao, observacao: value.observacao, ativo: value.ativo, createdAtUtc: value.createdAtUtc, updatedAtUtc: value.updatedAtUtc }
}

function parsePageMetadata(value: unknown): { items: unknown[]; page: number; pageSize: number; totalItems: number; totalPages: number } {
  if (!isRecord(value) || !Array.isArray(value.items) || !isIntegerAtLeast(value.page, 1) || !isIntegerAtLeast(value.pageSize, 1) || !isIntegerAtLeast(value.totalItems, 0) || !isIntegerAtLeast(value.totalPages, 0)) return invalid('A API retornou uma página de identificações inválida.')
  return { items: value.items, page: value.page, pageSize: value.pageSize, totalItems: value.totalItems, totalPages: value.totalPages }
}

export function parseAnimalIdentificationsPage(value: unknown): AnimalIdentificationPage {
  const page = parsePageMetadata(value)
  return { ...page, items: page.items.map(parseAnimalIdentification) }
}

function parseAnimalSummary(value: unknown): AnimalIdentificationAnimalSummary {
  if (!isRecord(value) || !isNonEmptyString(value.id) || !isNonEmptyString(value.codigoInterno) || !isNullableString(value.nome)) return invalid('A API retornou um resumo de animal inválido.')
  return { id: value.id, codigoInterno: value.codigoInterno, nome: value.nome }
}

function parseGlobalAnimalIdentification(value: unknown): GlobalAnimalIdentification {
  if (!isRecord(value)) return invalid('A API retornou uma identificação global inválida.')
  return { identificacao: parseAnimalIdentification(value.identificacao), animal: parseAnimalSummary(value.animal) }
}

export function parseGlobalAnimalIdentificationsPage(value: unknown): GlobalAnimalIdentificationPage {
  const page = parsePageMetadata(value)
  return { ...page, items: page.items.map(parseGlobalAnimalIdentification) }
}
