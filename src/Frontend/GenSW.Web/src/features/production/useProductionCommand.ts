import { useRef, useState } from 'react'
import { httpRequest } from '../../shared/http/httpClient'
import { isHttpError } from '../../shared/http/httpErrors'
import { parseOrder } from './productionService'
import type { ProductionOrder } from './types'

export interface PendingProductionCommand { orderId: string; operation: string; path: string; method: 'POST' | 'PUT'; key: string; body: unknown; definitiveFailureStatus?: 400 | 409 }
const prefix = 'gensw.production.retry.v1:'
const operations = ['criacao', 'edicao', 'planejamento', 'revisao-planejamento', 'cancelamento', 'inicio', 'confirmacao', 'confirmacao-ocorrencia', 'aborto-sem-consumo', 'reversao', 'ocorrencias']
const data = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const strings = (value: Record<string, unknown>, names: string[]) => names.every(name => typeof value[name] === 'string')
function validInputs(value: unknown) {
  return Array.isArray(value) && value.every(line => data(line) && typeof line.linhaId === 'string' && Array.isArray(line.alocacoes) && line.alocacoes.every(allocation => data(allocation) && strings(allocation, ['id', 'loteId', 'localId', 'quantidade', 'unidade'])))
}
function validBody(command: PendingProductionCommand) {
  const body = command.body
  if (!data(body) || !Number.isSafeInteger(body.versaoEsperada) || Number(body.versaoEsperada) < 0 || typeof body.motivo !== 'string') return false
  if (['criacao', 'edicao'].includes(command.operation)) return body.id === command.orderId && strings(body, ['id', 'codigo', 'receitaVersaoId', 'tamanho', 'unidade', 'responsavelId'])
  if (command.operation === 'inicio') return typeof body.responsavelId === 'string' && validInputs(body.entradas)
  if (['confirmacao', 'confirmacao-ocorrencia'].includes(command.operation)) return typeof body.responsavelId === 'string' && validInputs(body.entradas) && Array.isArray(body.saidas) && body.saidas.every(output => data(output) && strings(output, ['id', 'linhaId', 'quantidade', 'unidade', 'novoCodigoLote', 'localId'])) && Array.isArray(body.perdas) && body.perdas.every(loss => data(loss) && strings(loss, ['quantidade', 'unidade', 'motivo']))
  if (command.operation === 'aborto-sem-consumo') return typeof body.nenhumConsumoFisico === 'boolean' && typeof body.evidencia === 'string'
  if (command.operation === 'reversao') return typeof body.coerenciaFisicaConfirmada === 'boolean' && typeof body.evidencia === 'string'
  if (command.operation === 'ocorrencias') return strings(body, ['tipo', 'evidencia']) && typeof body.houveConsumoFisico === 'boolean'
  return true
}
function restore(scope: string): PendingProductionCommand | null {
  const value = sessionStorage.getItem(prefix + scope)
  if (!value) return null
  try {
    const v = JSON.parse(value) as PendingProductionCommand
    if (!data(v) || typeof v.orderId !== 'string' || typeof v.operation !== 'string' || !operations.includes(v.operation) || typeof v.key !== 'string' || !v.key.length || v.key.length > 100 || (scope !== 'nova' && scope !== v.orderId)) throw new Error()
    const path = v.operation === 'criacao' ? '/ordens-producao' : v.operation === 'edicao' ? `/ordens-producao/${v.orderId}` : `/ordens-producao/${v.orderId}/${v.operation}`
    if (v.path !== path || v.method !== (v.operation === 'edicao' ? 'PUT' : 'POST') || (v.definitiveFailureStatus !== undefined && v.definitiveFailureStatus !== 400 && v.definitiveFailureStatus !== 409) || !validBody(v)) throw new Error()
    return v
  } catch { throw new Error('O contexto da operação pendente está inválido. Preserve esta sessão e solicite diagnóstico antes de repetir a operação.') }
}
function restoreSafely(scope: string) {
  try { return { pending: restore(scope), error: null } }
  catch { return { pending: null, error: 'Não foi possível recuperar o contexto da operação pendente. Preserve esta sessão e solicite diagnóstico antes de repetir a operação.' } }
}
export function hasProductionRetryContext(scope: string) {
  const restored = restoreSafely(scope)
  return !!restored.pending || !!restored.error
}

// Only the exact business request and retry key are retained. No session/token is stored.
export function useProductionCommand(scope: string) {
  const [restored] = useState(() => restoreSafely(scope))
  const [pending, setPending] = useState<PendingProductionCommand | null>(restored.pending)
  const current = useRef(pending)
  const sending = useRef(false)
  const [busy, setBusy] = useState(false)
  const [absent, setAbsent] = useState(false)
  const save = (value: PendingProductionCommand | null) => {
    // Persistence is required before sending: a reload must never invent a new key.
    if (value) sessionStorage.setItem(prefix + scope, JSON.stringify(value))
    else sessionStorage.removeItem(prefix + scope)
    current.current = value; setPending(value); setAbsent(false)
  }
  const send = async (command: PendingProductionCommand): Promise<ProductionOrder | undefined> => {
    if (sending.current) return undefined
    sending.current = true; setBusy(true)
    try {
      // A previous rejection cannot resolve a new ambiguous attempt of the same request.
      if (command.definitiveFailureStatus) { command = { ...command, definitiveFailureStatus: undefined }; save(command) }
      const result = parseOrder(await httpRequest<unknown>(command.path, { authenticated: true, method: command.method, body: command.body, headers: { 'Idempotency-Key': command.key } }))
      save(null)
      return result
    } catch (error) {
      if (isHttpError(error) && (error.status === 400 || error.status === 409)) save({ ...command, definitiveFailureStatus: error.status })
      throw error
    } finally { sending.current = false; setBusy(false) }
  }
  const submit = async (orderId: string, operation: string, body: unknown, method: 'POST' | 'PUT' = 'POST') => {
    if (restored.error) throw new Error(restored.error)
    if (current.current) throw new Error('Consulte ou repita a operação pendente antes de alterar o pedido.')
    const path = operation === 'criacao' ? '/ordens-producao' : operation === 'edicao' ? `/ordens-producao/${orderId}` : `/ordens-producao/${orderId}/${operation}`
    const command: PendingProductionCommand = { orderId, operation, path, method, key: crypto.randomUUID(), body }
    save(command)
    return send(command)
  }
  const retry = () => current.current ? send(current.current) : Promise.resolve(undefined)
  const consult = async () => {
    const command = current.current
    if (!command || sending.current) return undefined
    sending.current = true; setBusy(true); setAbsent(false)
    try {
      const order = parseOrder(await httpRequest<unknown>(`/ordens-producao/${command.orderId}/comandos/${encodeURIComponent(command.operation)}/${encodeURIComponent(command.key)}`, { authenticated: true }))
      save(null); return order
    } catch (error) {
      if (isHttpError(error) && error.status === 404) { setAbsent(true); return undefined }
      throw error
    } finally { sending.current = false; setBusy(false) }
  }
  const release = () => { if (absent && current.current?.definitiveFailureStatus && !sending.current) save(null) }
  return { submit, retry, consult, release, pending, busy: busy || !!restored.error, absent, releaseAllowed: absent && !!pending?.definitiveFailureStatus, recoveryError: restored.error }
}
export type ProductionCommandControl = ReturnType<typeof useProductionCommand>
