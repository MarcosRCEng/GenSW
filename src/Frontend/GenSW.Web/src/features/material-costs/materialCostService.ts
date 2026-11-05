import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError } from '../../shared/http/httpErrors'
import { queryPath } from '../inventory/inventoryService'
import type { CostComponent, CostDestination, CostEvidence, CostExecution, CostHistory, CostInventoryPreview, CostLayer, CostLot, CostPage, CostProductionPreview, CostReconciliation, CostReference, CostSummary, CostEstimate } from './types'

export const costRoot = '/custos-materiais'
export const costError = () => new InvalidApiResponseError('A API retornou dados monetários inválidos. Não interprete a falha como custo zero ou desconhecido.')
export function costRecord(value: unknown): Record<string, unknown> { if (!value || typeof value !== 'object' || Array.isArray(value)) throw costError(); return value as Record<string, unknown> }
const string = (value: unknown): value is string => typeof value === 'string'
const strings = (value: unknown): value is string[] => Array.isArray(value) && value.every(string)
const uuid = (value: unknown) => string(value) && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
const nullableUuid = (value: unknown) => value === null || uuid(value)
const decimal = (value: unknown, scale: number, integers: number) => string(value) && new RegExp(`^(0|[1-9][0-9]{0,${integers - 1}})(\\.[0-9]{1,${scale}})?$`).test(value)
const money = (value: unknown) => decimal(value, 2, 16)
const nullableMoney = (value: unknown) => value === null || money(value)
const quantity = (value: unknown) => decimal(value, 6, 14)
const unitary = (value: unknown) => value === null || decimal(value, 6, 14)
const sequence = (value: unknown) => string(value) && /^(0|[1-9][0-9]*)$/.test(value)
const date = (value: unknown) => string(value) && /^\d{4}-\d{2}-\d{2}$/.test(value)
const array = <T,>(value: unknown, parse: (value: unknown) => T): T[] => { if (!Array.isArray(value)) throw costError(); return value.map(parse) }
export function parseCostComponent(value: unknown): CostComponent {
  const v = costRecord(value)
  if (![v.id, v.camadaId, v.itemId].every(uuid) || !['kg', 'L', 'un'].includes(String(v.unidade)) || !quantity(v.quantidade) || !nullableMoney(v.valorConhecido) || !strings(v.lacunas) || !nullableUuid(v.evidenciaId) || !sequence(v.fracaoNumerador) || !sequence(v.fracaoDenominador) || v.fracaoDenominador === '0') throw costError()
  if (BigInt(v.fracaoNumerador as string) > BigInt(v.fracaoDenominador as string)) throw costError()
  return v as unknown as CostComponent
}
export function parseCostSummary(value: unknown): CostSummary {
  const v = costRecord(value)
  if (!['Completo', 'Parcial', 'Desconhecido', 'NaoAplicavel'].includes(String(v.estado)) || !money(v.valorConhecido) || !nullableMoney(v.valorTotal) || !unitary(v.custoUnitarioCompleto) || !strings(v.lacunas) || !sequence(v.sequenciaAte) || !Number.isSafeInteger(v.revisao) || Number(v.revisao) < 0) throw costError()
  const components = array(v.componentes, parseCostComponent)
  if (v.estado !== 'Completo' && (v.valorTotal !== null || v.custoUnitarioCompleto !== null) || v.estado === 'Completo' && v.valorTotal === null) throw costError()
  if (v.estado === 'Desconhecido' && components.some(c => c.valorConhecido !== null)) throw costError()
  if (v.estado === 'NaoAplicavel' && (components.length > 0 || (v.lacunas as string[]).length > 0)) throw costError()
  const cents = (value: string) => { const [whole, fraction = ''] = value.split('.'); return BigInt(whole) * 100n + BigInt(fraction.padEnd(2, '0')) }
  if (components.reduce((sum, c) => sum + (c.valorConhecido === null ? 0n : cents(c.valorConhecido)), 0n) !== cents(v.valorConhecido as string)) throw costError()
  if (v.estado === 'Completo' && (components.some(c => c.valorConhecido === null || c.lacunas.length > 0) || v.lacunas.length > 0 || cents(v.valorTotal as string) !== cents(v.valorConhecido as string))) throw costError()
  return { ...v, componentes: components } as unknown as CostSummary
}
export function parseCostReference(value: unknown): CostReference {
  const v = costRecord(value)
  if (![v.id, v.itemId].every(uuid) || ![v.itemCodigo, v.itemNome, v.fonte, v.referenciaDocumento, v.aplicabilidade, v.createdAtUtc].every(string) || !date(v.dataFonte) || !date(v.vigenteDesde) || !(v.vigenteAte === null || date(v.vigenteAte)) || v.moeda !== 'BRL' || !['kg', 'L', 'un'].includes(String(v.unidade)) || !quantity(v.quantidadeBase) || !decimal(v.valorUnitario, 6, 14) || !['Publicada', 'Substituida', 'Revogada'].includes(String(v.estado)) || !Number.isSafeInteger(v.revisao) || !nullableUuid(v.substituiId)) throw costError()
  return v as unknown as CostReference
}
function parsePosition(value: unknown) {
  const v = costRecord(value)
  if (!uuid(v.camadaId) || !uuid(v.localId) || !quantity(v.quantidade) || !string(v.unidade) || !Number.isSafeInteger(v.revisao)) throw costError()
  return { ...v, custo: parseCostSummary(v.custo) } as CostLayer['posicoes'][number]
}
export function parseCostLayer(value: unknown): CostLayer {
  const v = costRecord(value)
  if (![v.id, v.loteId, v.itemId].every(uuid) || ![v.unidade, v.tipoOrigem, v.estado, v.createdAtUtc].every(string) || !quantity(v.quantidadeOriginal) || !money(v.valorConhecidoOriginal) || !sequence(v.sequenciaOrigem) || ![v.movimentoOrigemId, v.saidaProducaoId, v.evidenciaId, v.camadaReferenciaId].every(nullableUuid)) throw costError()
  return { ...v, posicoes: array(v.posicoes, parsePosition) } as unknown as CostLayer
}
export function parseCostLot(value: unknown): CostLot {
  const v = costRecord(value)
  if (!uuid(v.loteId) || !uuid(v.itemId) || ![v.codigo, v.itemCodigo, v.itemNome, v.unidade].every(string) || !sequence(v.corteFisico) || !sequence(v.corteMonetario)) throw costError()
  return { ...v, custo: parseCostSummary(v.custo), camadas: array(v.camadas, parseCostLayer) } as unknown as CostLot
}
export function parseCostDestination(value: unknown): CostDestination {
  const v = costRecord(value)
  if (v.loteId !== undefined && !nullableUuid(v.loteId)) throw costError()
  if (!uuid(v.id) || !uuid(v.destinoId) || ![v.saidaProducaoId, v.perdaId].every(nullableUuid) || !['Saida', 'Perda'].includes(String(v.tipo)) || !quantity(v.quantidade) || !string(v.unidade) || !decimal(v.percentual, 6, 3) || !unitary(v.unitarioConhecido) || typeof v.unitarioArredondado !== 'boolean' || !(v.residuoNumerador === null || string(v.residuoNumerador) && /^-?\d+$/.test(v.residuoNumerador)) || !(v.residuoDenominador === null || sequence(v.residuoDenominador)) || !string(v.motivo)) throw costError()
  return { ...v, custo: parseCostSummary(v.custo) } as unknown as CostDestination
}
export function parseCostProductionPreview(value: unknown): CostProductionPreview {
  const v = costRecord(value)
  if (!string(v.estado) || !sequence(v.sequenciaEsperada)) throw costError()
  return { ...v, custo: parseCostSummary(v.custo), destinos: array(v.destinos, parseCostDestination) } as unknown as CostProductionPreview
}
export function parseCostInventoryPreview(value: unknown): CostInventoryPreview {
  const v = costRecord(value)
  if (!sequence(v.sequenciaEsperada) || !strings(v.avisos)) throw costError()
  return { ...v, custo: parseCostSummary(v.custo) } as unknown as CostInventoryPreview
}
export function parseCostExecution(value: unknown): CostExecution {
  const v = costRecord(value)
  if (v.ordemId !== undefined && !nullableUuid(v.ordemId)) throw costError()
  if (v.saidas !== undefined) array(v.saidas, value => { const output = costRecord(value); if (![output.id, output.loteId, output.localId].every(uuid) || !quantity(output.quantidade) || !string(output.unidade)) throw costError(); return output })
  if (!uuid(v.id) || !uuid(v.execucaoId) || ![v.estado, v.motivo, v.createdAtUtc, v.snapshotJson].every(string) || !sequence(v.corteFisico) || !sequence(v.corteMonetario) || !nullableUuid(v.apuracaoOriginalId)) throw costError()
  costRecord(JSON.parse(v.snapshotJson as string))
  return { ...v, custo: parseCostSummary(v.custo), destinos: array(v.destinos, parseCostDestination) } as unknown as CostExecution
}
export function parseCostHistory(value: unknown): CostHistory {
  const v = costRecord(value)
  if (!uuid(v.id) || ![v.tipo, v.motivo, v.createdAtUtc, v.snapshotJson].every(string) || !sequence(v.sequencia) || !nullableUuid(v.eventoOriginalId)) throw costError()
  costRecord(JSON.parse(v.snapshotJson as string))
  return v as unknown as CostHistory
}
export function parseCostEvidence(value: unknown): CostEvidence {
  const v = costRecord(value)
  if (![v.id, v.itemId, v.autorId, v.responsavelId].every(uuid) || ![v.fonte, v.referenciaDocumento, v.metodo, v.motivo, v.createdAtUtc].every(string) || !date(v.dataFonte) || v.moeda !== 'BRL' || !string(v.unidade) || !quantity(v.quantidade) || !money(v.totalDocumentado) || !strings(v.componentes) || !strings(v.lacunas) || !(v.hashDocumento === null || string(v.hashDocumento))) throw costError()
  return v as unknown as CostEvidence
}
export function parseCostReconciliation(value: unknown): CostReconciliation {
  const v = costRecord(value); if (!uuid(v.loteId) || !uuid(v.localId) || typeof v.coerente !== 'boolean' || !strings(v.divergencias)) throw costError(); return v as unknown as CostReconciliation
}
export function parseCostPage<T>(value: unknown, parse: (value: unknown) => T): CostPage<T> {
  const v = costRecord(value)
  if (![v.page, v.pageSize, v.totalItems, v.totalPages].every(Number.isSafeInteger) || Number(v.page) < 1 || Number(v.pageSize) < 1 || Number(v.pageSize) > 100 || Number(v.totalItems) < 0 || Number(v.totalPages) < 0) throw costError()
  return { ...v, items: array(v.items, parse) } as unknown as CostPage<T>
}
export function parseCostEstimate(value: unknown): CostEstimate {
  const v = costRecord(value)
  if (!uuid(v.id) || v.tipo !== 'EstimativaPorReferencia' || ![v.rotulo, v.createdAtUtc, v.algoritmo, v.motivo].every(string) || !date(v.dataOperacional) || !money(v.valorEstimadoConhecido) || !nullableMoney(v.valorEstimadoTotal) || !strings(v.lacunas)) throw costError()
  const linhas = array(v.linhas, value => {
    const line = costRecord(value)
    if (!uuid(line.id) || !uuid(line.itemId) || ![line.itemCodigo, line.itemNome, line.unidade].every(string) || !quantity(line.quantidade) || !nullableUuid(line.conversaoId) || !nullableMoney(line.valorEstimadoConhecido) || !strings(line.lacunas) || typeof line.arredondado !== 'boolean') throw costError()
    return { ...line, referencia: line.referencia === null ? null : parseCostReference(line.referencia) } as CostEstimate['linhas'][number]
  })
  return { ...v, linhas } as unknown as CostEstimate
}
export async function costRead<T>(path: string, parse: (value: unknown) => T, params: Record<string, string | number | boolean | undefined> = {}) {
  return parse(await httpRequest<unknown>(queryPath(costRoot + path, params), { authenticated: true }))
}
export async function costList<T>(path: string, params: Record<string, string | number | boolean | undefined>, parse: (value: unknown) => T) {
  return parseCostPage(await httpRequest<unknown>(queryPath(costRoot + path, params), { authenticated: true }), parse)
}
