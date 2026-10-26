import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError } from '../../shared/http/httpErrors'
import { queryPath } from '../inventory/inventoryService'
import type { Recipe, RecipeVersion } from '../formulation/types'
import type { GenealogyEdge, PlanSnapshot, ProductionHistory, ProductionOrder, ProductionPage, Reconciliation } from './types'

type Data = Record<string, unknown>
export function record(value: unknown): Data {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new InvalidApiResponseError('A API retornou dados de produção inválidos.')
  return value as Data
}
const string = (value: unknown) => typeof value === 'string'
const amount = (value: unknown) => typeof value === 'string' && /^\d+(\.\d+)?$/.test(value)
export function parseRecipe(value: unknown): Recipe {
  const v = record(value)
  if (!string(v.id) || !string(v.nome) || !string(v.codigo) || typeof v.ativo !== 'boolean') throw new InvalidApiResponseError()
  return v as unknown as Recipe
}
export function parseVersion(value: unknown): RecipeVersion {
  const v = record(value)
  if (!string(v.id) || !string(v.receitaId) || !Number.isSafeInteger(v.numero) || !string(v.estado)) throw new InvalidApiResponseError()
  const content = record(v.conteudo)
  if (!Array.isArray(content.entradas) || !Array.isArray(content.saidas) || !amount(content.tamanhoReferencia)) throw new InvalidApiResponseError()
  return v as unknown as RecipeVersion
}
export function parseOrder(value: unknown): ProductionOrder {
  const v = record(value)
  if (!string(v.id) || !string(v.codigo) || !['Rascunho', 'Planejada', 'Iniciada', 'Confirmada', 'Cancelada', 'AbortadaSemConsumo', 'Revertida'].includes(String(v.estado)) || !Number.isSafeInteger(v.revisao) || !amount(v.tamanho) || !string(v.unidade) || !string(v.receitaVersaoId) || !string(v.responsavelId) || !Array.isArray(v.reservas) || !Array.isArray(v.acoesPermitidas) || !v.acoesPermitidas.every(string) || !Array.isArray(v.impedimentos) || !v.impedimentos.every(string)) throw new InvalidApiResponseError()
  if (v.planejamento !== null) {
    const plan = record(v.planejamento)
    parseVersion(plan.receita)
    record(plan.cabecalho)
    const scaled = record(plan.escalonada)
    if (!Array.isArray(scaled.entradas) || !Array.isArray(scaled.saidas) || !Array.isArray(plan.itens) || !Array.isArray(plan.conversoes) || !Array.isArray(plan.perfis)) throw new InvalidApiResponseError()
    plan.itens.forEach(item => { const i = record(item); if (!string(i.id) || !string(i.nome) || !string(i.codigo)) throw new InvalidApiResponseError() })
  }
  v.reservas.forEach(reservation => {
    const r = record(reservation)
    if (!string(r.id) || !string(r.linhaId) || !string(r.loteId) || !string(r.localId) || ![r.quantidadeOriginal, r.quantidadeAtiva, r.quantidadeConsumida, r.quantidadeLiberada].every(amount)) throw new InvalidApiResponseError()
  })
  if (v.execucao !== null) {
    const execution = record(v.execucao)
    if (!string(execution.id) || !string(execution.snapshotJson) || !Array.isArray(execution.lotesSaida) || !Array.isArray(execution.movimentos)) throw new InvalidApiResponseError()
  }
  return v as unknown as ProductionOrder
}
export function parseHistory(value: unknown): ProductionHistory {
  const v = record(value)
  if (!string(v.id) || !string(v.operacao) || !string(v.motivo) || !string(v.createdAtUtc)) throw new InvalidApiResponseError()
  return v as unknown as ProductionHistory
}
export function parseGenealogy(value: unknown): GenealogyEdge {
  const v = record(value)
  if (![v.execucaoId, v.ordemId, v.loteEntradaId, v.loteSaidaId].every(string)) throw new InvalidApiResponseError()
  return v as unknown as GenealogyEdge
}
export function parseReconciliation(value: unknown): Reconciliation {
  const v = record(value)
  if (!string(v.ordemId) || !string(v.codigo) || typeof v.coerente !== 'boolean' || !Array.isArray(v.divergencias) || !v.divergencias.every(string)) throw new InvalidApiResponseError()
  return v as unknown as Reconciliation
}
export function parseProductionPage<T>(value: unknown, parse: (item: unknown) => T): ProductionPage<T> {
  const v = record(value)
  if (!Array.isArray(v.items) || ![v.page, v.pageSize, v.totalItems, v.totalPages].every(Number.isSafeInteger) || Number(v.page) < 1 || Number(v.pageSize) < 1 || Number(v.pageSize) > 100 || Number(v.totalItems) < 0 || Number(v.totalPages) < 0) throw new InvalidApiResponseError()
  return { ...v, items: v.items.map(parse) } as unknown as ProductionPage<T>
}
export async function productionList<T>(path: string, params: Record<string, string | number | boolean | undefined>, parse: (item: unknown) => T): Promise<ProductionPage<T>> {
  return parseProductionPage(await httpRequest<unknown>(queryPath(`/ordens-producao${path}`, params), { authenticated: true }), parse)
}
export async function readOrder(id: string) { return parseOrder(await httpRequest<unknown>(`/ordens-producao/${id}`, { authenticated: true })) }
export const itemName = (plan: PlanSnapshot, id: string) => { const item = plan.itens.find(x => x.id === id); return item ? `${item.codigo} — ${item.nome}` : 'Item histórico sem rótulo' }
