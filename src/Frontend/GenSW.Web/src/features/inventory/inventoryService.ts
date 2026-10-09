import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError, isHttpError } from '../../shared/http/httpErrors'
import type { Conversion, Item, Profile } from '../formulation/types'
import type { Propriedade } from '../properties/types'
import type { Event, History, InventoryPage, Local, Lote, Movement, PhysicalQuantity, Preview, Reconciliation, Responsible, Saldo, Versions } from './types'

type RecordValue = Record<string, unknown>
type Predicate = (v: unknown) => boolean
const text: Predicate = v => typeof v === 'string'
const nonempty: Predicate = v => typeof v === 'string' && v.trim().length > 0
const nullable = (check: Predicate): Predicate => v => v === null || check(v)
const integer: Predicate = v => typeof v === 'number' && Number.isSafeInteger(v) && v >= 0
const bool: Predicate = v => typeof v === 'boolean'
const timestamp: Predicate = v => typeof v === 'string' && /^\d{4}-\d\d-\d\dT/.test(v) && Number.isFinite(Date.parse(v))
const date: Predicate = v => typeof v === 'string' && /^\d{4}-\d\d-\d\d$/.test(v) && Number.isFinite(Date.parse(v)) && new Date(v).toISOString().slice(0, 10) === v
const decimal: Predicate = v => typeof v === 'string' && /^-?\d{1,29}(\.\d{1,28})?$/.test(v)
const amount: Predicate = v => typeof v === 'string' && /^\d{1,14}(\.\d{1,6})?$/.test(v)
const sequence: Predicate = v => typeof v === 'string' && /^(0|[1-9]\d*)$/.test(v) && BigInt(v) <= 9223372036854775807n
const json: Predicate = v => { if (typeof v !== 'string') return false; try { JSON.parse(v); return true } catch { return false } }
const strings: Predicate = v => Array.isArray(v) && v.every(text)
const unit: Predicate = v => v === 'kg' || v === 'L' || v === 'un'
function canonicalCount(unit: unknown, ...values: unknown[]) {
  if (unit === 'un' && values.some(v => typeof v !== 'string' || !/^-?\d+(\.0+)?$/.test(v))) throw new InvalidApiResponseError('Contagem fracionária nos dados retornados.')
}
function fields(v: unknown, checks: Record<string, Predicate>): RecordValue {
  if (!v || typeof v !== 'object' || Array.isArray(v) || Object.entries(checks).some(([k, check]) => !check((v as RecordValue)[k]))) throw new InvalidApiResponseError('A API retornou dados de estoque inválidos. Recarregue a consulta.')
  return v as RecordValue
}
const entity = { id: nonempty, revisao: integer, createdAtUtc: timestamp, updatedAtUtc: timestamp }
export function parseLocal(v: unknown): Local {
  return fields(v, { ...entity, codigo: nonempty, nome: nonempty, descricao: nullable(text), propriedadeId: nullable(nonempty), propriedadeNome: nullable(text), finalidade: x => x === 'Ordinario' || x === 'Segregacao', ativo: bool }) as unknown as Local
}
export function parseLote(v: unknown): Lote {
  return fields(v, { ...entity, itemId: nonempty, itemCodigo: nonempty, itemNome: nonempty, itemAtivo: bool, codigo: nonempty, codigoExterno: nullable(text), origem: nonempty, fonte: nonempty, responsavelId: nonempty, responsavelNome: nonempty, dataOrigem: nullable(date), fabricacao: nullable(date), coleta: nullable(date), validade: nullable(date), fonteValidade: nullable(text), responsavelValidadeId: nullable(nonempty), situacao: x => ['Liberado', 'Bloqueado', 'Encerrado'].includes(String(x)), ativo: bool, unidade: unit, perfilNutricionalId: nullable(nonempty), conversaoItemId: nullable(nonempty), aplicabilidade: nullable(text) }) as unknown as Lote
}
export function parseSaldo(v: unknown): Saldo {
  const value = fields(v, { loteId: nonempty, localId: nonempty, itemId: nonempty, itemCodigo: nonempty, itemNome: nonempty, loteCodigo: nonempty, localCodigo: nonempty, localNome: nonempty, unidade: unit, quantidade: amount, quantidadeElegivel: amount, elegivel: bool, motivosIndisponibilidade: strings, revisao: integer, itemRevisao: integer, loteRevisao: integer, localRevisao: integer, observadoEmUtc: timestamp, sequenciaAte: sequence }) as unknown as Saldo
  canonicalCount(value.unidade, value.quantidade, value.quantidadeElegivel)
  fields(v, { avisos: x => x === undefined || x === null || strings(x) })
  return { ...value, avisos: value.avisos ?? [] }
}
export function parseResponsible(v: unknown): Responsible { return fields(v, { id: nonempty, nome: nonempty, ativo: bool }) as unknown as Responsible }
export function parseReconciliation(v: unknown): Reconciliation {
  const value = fields(v, { loteId: nonempty, localId: nonempty, unidade: unit, quantidadeLivro: decimal, quantidadeProjecao: decimal, diferenca: decimal })
  canonicalCount(value.unidade, value.quantidadeLivro, value.quantidadeProjecao, value.diferenca)
  return value as unknown as Reconciliation
}
export function parseHistory(v: unknown): History { return fields(v, { id: nonempty, registroId: nonempty, tipo: nonempty, operacao: nonempty, sequencia: sequence, autorId: nonempty, createdAtUtc: timestamp, motivo: text, antesJson: json, depoisJson: json }) as unknown as History }
export function parseMovement(v: unknown): Movement {
  const value = fields(v, { id: nonempty, ordinal: integer, loteId: nonempty, localId: nonempty, itemId: nonempty, unidade: unit, sentido: x => x === 'Entrada' || x === 'Saida', quantidade: amount, quantidadeDeclarada: amount, unidadeDeclarada: nonempty, residuo: decimal, saldoAnterior: amount, saldoPosterior: amount, conversaoItemId: nullable(nonempty), perfilNutricionalId: nullable(nonempty), snapshotJson: json })
  canonicalCount(value.unidade, value.quantidade, value.saldoAnterior, value.saldoPosterior)
  return value as unknown as Movement
}
export function parseEvent(v: unknown): Event {
  const value = fields(v, { id: nonempty, sequencia: sequence, tipo: nonempty, autorId: nonempty, responsavelId: nonempty, createdAtUtc: timestamp, dataOperacional: date, dataObservada: nullable(date), motivo: nonempty, documento: nullable(text), eventoReferenciaId: nullable(nonempty), algoritmo: nonempty, snapshotJson: json, movimentos: Array.isArray })
  return { ...value, movimentos: (value.movimentos as unknown[]).map(parseMovement) } as unknown as Event
}
export function parseVersions(v: unknown): Versions { return fields(v, { item: integer, lote: integer, local: integer, posicao: integer, origemLocal: integer, destinoLocal: integer, origemPosicao: integer, destinoPosicao: integer }) as unknown as Versions }
export function parsePhysicalQuantity(v: unknown): PhysicalQuantity {
  const value = fields(v, { declarada: amount, unidadeDeclarada: nonempty, calculada: decimal, normalizada: amount, residuo: decimal, unidade: unit, fator: nullable(decimal), sentido: nullable(text), proveniencia: nullable(text), exigeAceite: bool })
  canonicalCount(value.unidade, value.normalizada)
  return value as unknown as PhysicalQuantity
}
export function parsePreview(v: unknown): Preview {
  const value = fields(v, { operacao: nonempty, loteId: nonempty, unidade: unit, avisos: strings, observadoEmUtc: timestamp, sequenciaAte: sequence, posicoes: Array.isArray })
  const posicoes = (value.posicoes as unknown[]).map(x => {
    const position = fields(x, { loteId: nonempty, localId: nonempty, saldoAnterior: amount, saldoPosterior: amount, revisao: integer, revisaoResultante: integer })
    canonicalCount(value.unidade, position.saldoAnterior, position.saldoPosterior)
    return position
  })
  return { ...value, posicoes, quantidade: parsePhysicalQuantity(value.quantidade), versoesEsperadas: parseVersions(value.versoesEsperadas) } as unknown as Preview
}
// Write responses preserve the durable envelope; GET /movimentos/:id is a direct event.
export function parseCommittedEvent(v: unknown): Event {
  const value = fields(v, { evento: x => x !== null && typeof x === 'object', previa: x => x !== null && typeof x === 'object' })
  parsePreview(value.previa)
  return parseEvent(value.evento)
}
export function parsePage<T>(v: unknown, parse: (x: unknown) => T): InventoryPage<T> {
  const value = fields(v, { items: Array.isArray, page: x => integer(x) && Number(x) > 0, pageSize: x => integer(x) && Number(x) >= 1 && Number(x) <= 100, totalItems: integer, totalPages: integer, observadoEmUtc: timestamp, sequenciaAte: sequence })
  return { ...value, items: (value.items as unknown[]).map(parse) } as unknown as InventoryPage<T>
}
export function parseItem(v: unknown): Item { return fields(v, { ...entity, codigo: nonempty, nome: nonempty, unidade: unit, ativo: bool, podeEntrar: bool, podeProduzir: bool, usoInterno: bool, venda: bool, unidadeFixada: bool }) as unknown as Item }
export function parseProperty(v: unknown): Propriedade { return fields(v, { id: nonempty, nome: nonempty, ativo: bool, createdAtUtc: timestamp, updatedAtUtc: timestamp }) as unknown as Propriedade }
export function parseConversion(v: unknown): Conversion { return fields(v, { id: nonempty, itemId: nonempty, numero: integer, origem: nonempty, destino: nonempty, fator: decimal, fonte: nonempty, metodo: nonempty, contexto: text, referenciaAmostra: text, proveniencia: text, dataFonte: date }) as unknown as Conversion }
export function parseProfile(v: unknown): Profile {
  const value = fields(v, { id: nonempty, itemId: nonempty, numero: integer, revisao: integer, estado: nonempty })
  fields(value.conteudo, { nome: nonempty, fonte: nonempty })
  return value as unknown as Profile
}
export function queryPath(path: string, params: Record<string, string | number | boolean | undefined>) {
  const query = new URLSearchParams()
  Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== '') query.set(k, String(v)) })
  return `${path}${query.size ? `?${query}` : ''}`
}
export async function inventoryRead<T>(path: string, parse: (v: unknown) => T): Promise<T> { return parse(await httpRequest<unknown>(`/estoque/${path}`, { authenticated: true })) }
export async function inventoryList<T>(path: string, params: Record<string, string | number | boolean | undefined>, parse: (v: unknown) => T): Promise<InventoryPage<T>> { return parsePage(await httpRequest<unknown>(queryPath(`/estoque/${path}`, params), { authenticated: true }), parse) }
export async function inventorySend<T>(path: string, body: unknown, parse: (v: unknown) => T, key?: string, method: 'POST' | 'PUT' | 'PATCH' = 'POST'): Promise<T> { return parse(await httpRequest<unknown>(`/estoque/${path}`, { authenticated: true, method, body, headers: key ? { 'Idempotency-Key': key } : undefined })) }
export function inventoryError(error: unknown): string {
  if (isHttpError(error)) return error.detail ?? (error.status === 403 ? 'Esta operação exige o papel Admin atual.' : error.status === 404 ? 'Registro não encontrado.' : error.status === 409 ? 'Conflito. Recarregue os dados e confira a prévia antes de tentar novamente.' : 'Não foi possível concluir a operação.')
  return error instanceof Error ? error.message : 'Não foi possível conectar à API.'
}
export function decimalInput(value: string, allowZero = false): string {
  const result = value.trim().replace(',', '.')
  if (!/^\d{1,14}(\.\d{1,6})?$/.test(result) || (!allowZero && /^0+(\.0+)?$/.test(result))) throw new Error('Informe quantidade positiva com até 14 inteiros e 6 decimais, sem expoente ou separador de milhares.')
  return result
}
export const showQuantity = (value: string) => value.replace('.', ',')
export const showTime = (value: string) => new Date(value).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })
export function today() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  return ['year', 'month', 'day'].map(type => parts.find(x => x.type === type)?.value).join('-')
}
