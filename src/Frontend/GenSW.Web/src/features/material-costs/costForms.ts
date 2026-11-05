import { today } from '../inventory/inventoryService'
import { moneyInput, validatePercentages } from './decimals'
import type { AllocationCost, ReceiptCost } from './types'

export interface EvidenceForm { fonte: string; referenciaDocumento: string; dataFonte: string; totalDocumentado: string; componentes: string; lacunas: string; hash: string }
export interface ReceiptForm extends EvidenceForm { choice: '' | 'Conhecido' | 'Desconhecido'; motivo: string }
export const evidenceForm = (value?: ReceiptCost['evidencia']): EvidenceForm => ({ fonte: value?.fonte ?? '', referenciaDocumento: value?.referenciaDocumento ?? '', dataFonte: value?.dataFonte ?? today(), totalDocumentado: value?.totalDocumentado ?? '', componentes: value?.componentes.join('\n') ?? '', lacunas: value?.lacunas.join('\n') ?? '', hash: value?.hash ?? '' })
export const receiptForm = (value?: ReceiptCost | null): ReceiptForm => ({ ...evidenceForm(value?.evidencia), choice: value?.estado ?? '', motivo: value?.motivo ?? '' })
export function evidenceBody(value: EvidenceForm) {
  const lines = (text: string) => text.split('\n').map(x => x.trim()).filter(Boolean)
  if (!value.fonte.trim() || !value.referenciaDocumento.trim() || !value.dataFonte || !lines(value.componentes).length) throw new Error('Informe fonte, referência documental, data e componentes atribuídos à quantidade.')
  return { fonte: value.fonte.trim(), referenciaDocumento: value.referenciaDocumento.trim(), dataFonte: value.dataFonte, moeda: 'BRL' as const, totalDocumentado: moneyInput(value.totalDocumentado), componentes: lines(value.componentes), lacunas: lines(value.lacunas), hash: value.hash.trim() || null }
}
export function receiptBody(value: ReceiptForm): ReceiptCost {
  if (!value.choice || !value.motivo.trim()) throw new Error('Escolha custo conhecido ou desconhecido e registre o motivo monetário.')
  return { versao: 1, estado: value.choice, motivo: value.motivo.trim(), evidencia: value.choice === 'Conhecido' ? evidenceBody(value) : null }
}

export interface AllocationDestination { id: string; tipo: 'Saida' | 'Perda'; label: string; zero: boolean }
export interface AllocationForm { choice: '' | 'apurar' | 'pendente'; motivo: string; percentages: Record<string, string> }
export const allocationForm = (value?: AllocationCost | null): AllocationForm => ({ choice: value ? value.pendenteRateio ? 'pendente' : 'apurar' : '', motivo: value?.motivo ?? '', percentages: Object.fromEntries(value?.percentuais.map(x => [x.destinoId, x.percentual]) ?? []) })
export function allocationBody(value: AllocationForm, destinations: AllocationDestination[]): AllocationCost {
  if (!value.choice || !value.motivo.trim()) throw new Error('Escolha rateio manual ou pendência consciente, com motivo.')
  if (value.choice === 'pendente') return { versao: 1, pendenteRateio: true, motivo: value.motivo.trim(), percentuais: [] }
  if (!destinations.some(x => !x.zero)) throw new Error('Rateio exige saída positiva ou destino explícito de perda total.')
  const percentages = validatePercentages(destinations.map(destination => destination.zero ? '0' : value.percentages[destination.id] ?? ''))
  return { versao: 1, pendenteRateio: false, motivo: value.motivo.trim(), percentuais: destinations.map((destination, i) => ({ destinoId: destination.id, tipo: destination.tipo, percentual: percentages[i] })) }
}
