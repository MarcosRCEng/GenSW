import { useRef, useState } from 'react'
import { httpRequest } from '../../shared/http/httpClient'
import { isHttpError } from '../../shared/http/httpErrors'
import { costRecord, parseCostEstimate, parseCostHistory, parseCostReference } from './materialCostService'

type Operation = 'referencia' | 'substituicao' | 'avaliacao' | 'compensacao' | 'estimativa' | 'resolucao'
export interface PendingCostCommand { resource: string; operation: Operation; path: string; key: string; body: Record<string, unknown>; definitiveFailureStatus?: 400 | 409 }
const prefix = 'gensw.material-cost.retry.v1:'
const uuid = (v: unknown): v is string => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)
const data = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const text = (v: Record<string, unknown>, fields: string[]) => fields.every(field => typeof v[field] === 'string')
const evidence = (v: unknown) => data(v) && text(v, ['fonte', 'referenciaDocumento', 'dataFonte', 'totalDocumentado']) && v.moeda === 'BRL' && Array.isArray(v.componentes) && v.componentes.every(x => typeof x === 'string') && Array.isArray(v.lacunas) && v.lacunas.every(x => typeof x === 'string')
const allocation = (v: unknown) => data(v) && v.versao === 1 && typeof v.pendenteRateio === 'boolean' && typeof v.motivo === 'string' && Array.isArray(v.percentuais) && v.percentuais.every(x => data(x) && uuid(x.destinoId) && ['Saida', 'Perda'].includes(String(x.tipo)) && typeof x.percentual === 'string')
export function validCostPending(v: unknown): v is PendingCostCommand {
  if (!data(v) || !uuid(v.resource) || !data(v.body) || v.body.id !== v.resource || typeof v.operation !== 'string' || typeof v.path !== 'string' || typeof v.key !== 'string' || !v.key.length || v.key.length > 100 || v.definitiveFailureStatus !== undefined && v.definitiveFailureStatus !== 400 && v.definitiveFailureStatus !== 409) return false
  const b = v.body
  if (typeof b.motivo !== 'string') return false
  const guid = '[0-9a-fA-F-]{36}'
  switch (v.operation) {
    case 'referencia': case 'substituicao':
      return (v.operation === 'referencia' ? v.path === '/custos-materiais/referencias' : new RegExp(`^/custos-materiais/referencias/${guid}/substituicoes$`).test(v.path)) && uuid(b.itemId) && text(b, ['fonte', 'referenciaDocumento', 'dataFonte', 'unidade', 'quantidadeBase', 'valorUnitario', 'vigenteDesde', 'aplicabilidade']) && b.moeda === 'BRL' && typeof b.revogada === 'boolean'
    case 'avaliacao':
      return v.path === '/custos-materiais/avaliacoes' && uuid(b.loteId) && uuid(b.localId) && typeof b.quantidade === 'string' && Number.isSafeInteger(b.revisaoEsperada) && evidence(b.evidencia)
    case 'compensacao':
      return new RegExp(`^/custos-materiais/avaliacoes/${guid}/compensacoes$`).test(v.path) && typeof b.evidencia === 'string' && Number.isSafeInteger(b.revisaoEsperada)
    case 'estimativa':
      return v.path === '/custos-materiais/estimativas' && Array.isArray(b.linhas) && b.linhas.length > 0 && b.linhas.every(x => data(x) && uuid(x.id) && uuid(x.itemId) && text(x, ['quantidade', 'unidade']) && (x.referenciaId === null || uuid(x.referenciaId)) && (x.conversaoId === null || uuid(x.conversaoId)))
    case 'resolucao':
      return new RegExp(`^/custos-materiais/execucoes/${guid}/resolucoes$`).test(v.path) && typeof b.evidencia === 'string' && Number.isSafeInteger(b.revisaoEsperada) && allocation(b.custoMaterial)
    default: return false
  }
}
function parseResult(value: unknown, pending: PendingCostCommand) {
  const result = pending.operation === 'referencia' || pending.operation === 'substituicao' ? parseCostReference(value) : pending.operation === 'estimativa' ? parseCostEstimate(value) : pending.operation === 'resolucao' ? costRecord(value) : parseCostHistory(value)
  if (result.id !== pending.resource) throw new Error('A resposta não corresponde à identidade do pedido original. Preserve o contexto para diagnóstico.')
  if (pending.operation === 'resolucao' && (!uuid((result as Record<string, unknown>).apuracaoId) || !uuid((result as Record<string, unknown>).execucaoId) || (result as Record<string, unknown>).originalPreservado !== true)) throw new Error('Resposta de resolução inválida. Preserve o pedido original.')
  return result
}
function restore(scope: string) {
  try {
    const raw = sessionStorage.getItem(prefix + scope)
    if (!raw) return { pending: null, error: null }
    const parsed: unknown = JSON.parse(raw)
    if (!validCostPending(parsed)) throw new Error()
    return { pending: parsed, error: null }
  } catch { return { pending: null, error: 'O contexto monetário pendente está inválido ou indisponível. Preserve esta sessão e solicite diagnóstico antes de enviar outro comando.' } }
}
export function useCostCommand(scope: string) {
  const [restored] = useState(() => restore(scope))
  const [pending, setPending] = useState<PendingCostCommand | null>(restored.pending)
  const current = useRef(pending)
  const sending = useRef(false)
  const [busy, setBusy] = useState(false)
  const [absent, setAbsent] = useState(false)
  const save = (value: PendingCostCommand | null) => {
    if (value) sessionStorage.setItem(prefix + scope, JSON.stringify(value))
    else sessionStorage.removeItem(prefix + scope)
    current.current = value; setPending(value); setAbsent(false)
  }
  const send = async (context: PendingCostCommand) => {
    if (sending.current) return undefined
    sending.current = true; setBusy(true)
    try {
      if (context.definitiveFailureStatus) { context = { ...context, definitiveFailureStatus: undefined }; save(context) }
      const raw = await httpRequest<unknown>(context.path, { authenticated: true, method: 'POST', body: context.body, headers: { 'Idempotency-Key': context.key } })
      const result = parseResult(raw, context); save(null); return result
    } catch (error) {
      if (isHttpError(error) && (error.status === 400 || error.status === 409)) save({ ...context, definitiveFailureStatus: error.status })
      throw error
    } finally { sending.current = false; setBusy(false) }
  }
  const submit = (resource: string, operation: Operation, path: string, body: Record<string, unknown>) => {
    if (restored.error) throw new Error(restored.error)
    if (current.current || sending.current) throw new Error('Consulte ou repita o comando original antes de editar ou reenviar.')
    const context = { resource, operation, path, body, key: crypto.randomUUID() }
    if (!validCostPending(context)) throw new Error('Pedido monetário inválido; nenhum comando foi enviado.')
    save(context); return send(context)
  }
  const retry = () => current.current ? send(current.current) : Promise.resolve(undefined)
  const consult = async () => {
    const context = current.current
    if (!context || sending.current) return undefined
    sending.current = true; setBusy(true); setAbsent(false)
    try {
      const raw = await httpRequest<unknown>(`/custos-materiais/comandos/${context.resource}/${context.operation}/${encodeURIComponent(context.key)}`, { authenticated: true })
      const result = parseResult(raw, context); save(null); return result
    } catch (error) { if (isHttpError(error) && error.status === 404) { setAbsent(true); return undefined } throw error }
    finally { sending.current = false; setBusy(false) }
  }
  const release = () => { if (absent && current.current?.definitiveFailureStatus && !sending.current) save(null) }
  return { pending, busy, absent, submit, retry, consult, release, releaseAllowed: !!pending?.definitiveFailureStatus && absent, recoveryError: restored.error }
}
export type CostCommandControl = ReturnType<typeof useCostCommand>
