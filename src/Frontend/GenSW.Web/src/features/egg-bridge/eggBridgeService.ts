import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError } from '../../shared/http/httpErrors'
import { queryPath } from '../inventory/inventoryService'
import type { EggAnchor, EggBridge, EggCandidate, EggCommandOperation, EggCommandResult, EggDetail, EggGroup, EggPage, EggPreview, EggProof, EggReconciliation, EggScope, EggSource, EggSourceExpectation, EggTreatmentFact, EggTreatmentPreview } from './types'

export const eggApi = '/producao/ponte-ovos'
export const eggRoot = '/producao/ponte-ovos'
type Check = (value: unknown) => boolean
const text: Check = value => typeof value === 'string'
const nonempty: Check = value => typeof value === 'string' && value.length > 0
const bool: Check = value => typeof value === 'boolean'
const integer: Check = value => Number.isSafeInteger(value) && Number(value) >= 0
const sequence: Check = value => typeof value === 'string' && /^(0|[1-9]\d*)$/.test(value) && BigInt(value) <= 9223372036854775807n
const count: Check = value => sequence(value) && BigInt(String(value)) <= 99999999999999n
const delta: Check = value => typeof value === 'string' && /^-?\d{1,14}$/.test(value)
const timestamp: Check = value => typeof value === 'string' && Number.isFinite(Date.parse(value))
const nullable = (check: Check): Check => value => value === null || check(value)
const strings: Check = value => Array.isArray(value) && value.every(text)
const operation: Check = value => value === 'Entrada' || value === 'Associacao'
const treatment: Check = value => value === 'Anotacao' || value === 'Invalidacao' || value === 'Compensacao'
function fields(value: unknown, checks: Record<string, Check>): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value) || Object.entries(checks).some(([key, check]) => !check((value as Record<string, unknown>)[key]))) throw new InvalidApiResponseError('A API retornou dados da ponte de ovos inválidos. Preserve o pedido e reconsulte o resultado.')
  return value as Record<string, unknown>
}
export function parseSource(value: unknown): EggSource {
  return fields(value, { id: nonempty, animalId: nonempty, dataPostura: nonempty, pesoGramas: v => (typeof v === 'number' && Number.isFinite(v) && v > 0) || (typeof v === 'string' && /^\d+(\.\d+)?$/.test(v) && !/^0+(\.0+)?$/.test(v)), observacao: nullable(text), updatedAtUtc: timestamp, createdAtUtc: timestamp }) as unknown as EggSource
}
export function parseEggCandidate(value: unknown): EggCandidate {
  const row = fields(value, { ponteId: nullable(nonempty), grupoId: nullable(nonempty) })
  return { ...row, origem: parseSource(row.origem) } as unknown as EggCandidate
}
export function parseEggAnchor(value: unknown): EggAnchor { return fields(value, { id: nonempty, itemId: nonempty, revisao: sequence, autorId: nonempty, createdAtUtc: timestamp }) as unknown as EggAnchor }
export function parseEggScope(value: unknown): EggScope { return fields(value, { id: nonempty, itemId: nonempty, revisaoAncora: sequence, sequenciaAte: sequence, fronteira: text, fontesConsultadas: text, lacunas: text, motivo: text, responsavelId: nonempty, autorId: nonempty, createdAtUtc: timestamp }) as unknown as EggScope }
export function parseEggProof(value: unknown): EggProof { return fields(value, { id: nonempty, movimentoEstoqueId: nonempty, eventoId: nonempty, itemId: nonempty, contagem: count, evidencia: nonempty, motivo: nonempty, responsavelId: nonempty, autorId: nonempty, createdAtUtc: timestamp }) as unknown as EggProof }
export function parseEggExpectation(value: unknown): EggSourceExpectation { return fields(value, { producaoOvoId: nonempty, hashOrigem: nonempty, updatedAtUtc: timestamp, decisao: text, impedimento: nullable(text) }) as unknown as EggSourceExpectation }
export function parseEggPreview(value: unknown): EggPreview {
  const row = fields(value, { token: nonempty, operacao: operation, origens: Array.isArray, quantidade: count, deltaEstoque: delta, saldoAnterior: nullable(count), saldoPosterior: nullable(count), capacidadeRestante: nullable(count), unidade: v => v === 'un', revisaoAncora: sequence, sequenciaAte: sequence, avisos: strings, podeConfirmar: bool, expiraEmUtc: timestamp })
  return { ...row, origens: (row.origens as unknown[]).map(parseEggExpectation) } as unknown as EggPreview
}
export function parseEggGroup(value: unknown): EggGroup { return fields(value, { id: nonempty, operacao: operation, itemId: nonempty, loteId: nonempty, localId: nonempty, recorteId: nonempty, provaId: nullable(nonempty), eventoId: nonempty, movimentoEstoqueId: nonempty, quantidade: count, revisaoLoteOriginal: integer, situacaoOriginal: nonempty, validadeOriginal: nullable(text), snapshotLoteJson: text, autorId: nonempty, responsavelId: nonempty, motivo: nonempty, evidencia: text, createdAtUtc: timestamp }) as unknown as EggGroup }
export function parseEggBridge(value: unknown): EggBridge { return fields(value, { id: nonempty, producaoOvoId: nonempty, grupoId: nonempty, snapshotJson: nonempty, hashOrigem: nonempty, origemUpdatedAtUtc: timestamp, algoritmo: nonempty }) as unknown as EggBridge }
export function parseEggTreatment(value: unknown): EggTreatmentFact { return fields(value, { id: nonempty, grupoId: nonempty, tipo: treatment, eventoId: nullable(nonempty), autorId: nonempty, responsavelId: nonempty, motivo: nonempty, evidencia: text, createdAtUtc: timestamp }) as unknown as EggTreatmentFact }
export function parseEggReconciliation(value: unknown): EggReconciliation { return fields(value, { grupoId: nonempty, coerente: bool, divergencias: strings }) as unknown as EggReconciliation }
export function parseEggDetail(value: unknown): EggDetail {
  const row = fields(value, { pontes: Array.isArray, tratamentos: Array.isArray, origensAtuais: Array.isArray, avisos: strings })
  return { grupo: parseEggGroup(row.grupo), pontes: (row.pontes as unknown[]).map(parseEggBridge), tratamentos: (row.tratamentos as unknown[]).map(parseEggTreatment), origensAtuais: (row.origensAtuais as unknown[]).map(parseEggExpectation), conciliacao: parseEggReconciliation(row.conciliacao), avisos: row.avisos as string[] }
}
export function parseEggTreatmentPreview(value: unknown): EggTreatmentPreview { return fields(value, { token: nonempty, grupoId: nonempty, tipo: treatment, deltaEstoque: delta, impedimentos: strings, podeConfirmar: bool, expiraEmUtc: timestamp }) as unknown as EggTreatmentPreview }
export function parseEggPage<T>(value: unknown, parse: (value: unknown) => T): EggPage<T> {
  const row = fields(value, { items: Array.isArray, page: v => integer(v) && Number(v) > 0, pageSize: v => integer(v) && Number(v) >= 1 && Number(v) <= 100, totalItems: integer, totalPages: integer })
  return { ...row, items: (row.items as unknown[]).map(parse) } as unknown as EggPage<T>
}
export function parseEggCommandResult(operation: EggCommandOperation, raw: unknown): EggCommandResult {
  if (operation === 'Inscricao') return { kind: 'inscricao', anchor: parseEggAnchor(raw) }
  if (operation === 'Recorte') { const envelope = fields(raw, { decisoes: Array.isArray }); return { kind: 'recorte', scope: parseEggScope(envelope.recorte) } }
  if (operation === 'Prova') return { kind: 'prova', proof: parseEggProof(raw) }
  return { kind: 'grupo', operation, detail: parseEggDetail(raw) }
}
export async function eggRead<T>(path: string, parse: (value: unknown) => T): Promise<T> { return parse(await httpRequest<unknown>(`${eggApi}${path}`, { authenticated: true })) }
export async function eggList<T>(path: string, query: Record<string, string | number | boolean | undefined>, parse: (value: unknown) => T): Promise<EggPage<T>> { return parseEggPage(await httpRequest<unknown>(queryPath(`${eggApi}${path}`, query), { authenticated: true }), parse) }
export async function eggPreview<T>(path: string, body: unknown, parse: (value: unknown) => T): Promise<T> { return parse(await httpRequest<unknown>(`${eggApi}${path}`, { authenticated: true, method: 'POST', body })) }
export function integerCount(value: string): string {
  if (!/^[1-9]\d{0,13}$/.test(value)) throw new Error('Informe contagem inteira positiva em un, com até 14 dígitos, sem conversão de peso.')
  return value
}
