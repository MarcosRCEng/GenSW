import { useRef, useState } from 'react'
import { httpRequest } from '../../shared/http/httpClient'
import { isHttpError } from '../../shared/http/httpErrors'
import { parseCommittedEvent, parseVersions } from './inventoryService'
import { operationInfo, type MovementCommand, type Operation } from './types'

interface Pending { actor: string; operation: Operation; key: string; body: MovementCommand; definitiveFailureStatus?: 400 | 409 }
const prefix = 'gensw.inventory.movement.retry.v1:'
const data = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
export function validMovementPending(v: unknown): v is Pending {
  if (!data(v) || typeof v.actor !== 'string' || typeof v.operation !== 'string' || !Object.prototype.hasOwnProperty.call(operationInfo, v.operation) || typeof v.key !== 'string' || !v.key || v.key.length > 100 || !data(v.body) || v.definitiveFailureStatus !== undefined && ![400, 409].includes(Number(v.definitiveFailureStatus))) return false
  const b = v.body
  if (!['loteId', 'quantidade', 'unidade', 'responsavelId', 'motivo'].every(k => typeof b[k] === 'string')) return false
  try { parseVersions(b.versoesEsperadas) } catch { return false }
  if (b.custoMaterial !== undefined && b.custoMaterial !== null) {
    const c = b.custoMaterial
    if (!data(c) || c.versao !== 1 || !['Conhecido', 'Desconhecido'].includes(String(c.estado)) || typeof c.motivo !== 'string') return false
    if (c.estado === 'Conhecido' && (!data(c.evidencia) || !['fonte', 'referenciaDocumento', 'dataFonte', 'totalDocumentado'].every(k => typeof (c.evidencia as Record<string, unknown>)[k] === 'string') || c.evidencia.moeda !== 'BRL' || !Array.isArray(c.evidencia.componentes) || !c.evidencia.componentes.every(x => typeof x === 'string') || !Array.isArray(c.evidencia.lacunas) || !c.evidencia.lacunas.every(x => typeof x === 'string'))) return false
  }
  return true
}
function restore(scope: string) {
  try { const raw = sessionStorage.getItem(prefix + scope); if (!raw) return { pending: null, error: null }; const v: unknown = JSON.parse(raw); if (!validMovementPending(v)) throw new Error(); return { pending: v, error: null } }
  catch { return { pending: null, error: 'Contexto do movimento inválido ou indisponível. Preserve a sessão para diagnóstico antes de criar outro pedido.' } }
}
export function useMovementCommand(scope: string, actor: string) {
  const [restored] = useState(() => restore(scope)); const [pending, setPending] = useState<Pending | null>(restored.pending); const current = useRef(pending); const sending = useRef(false); const [busy, setBusy] = useState(false); const [absent, setAbsent] = useState(false)
  const save = (v: Pending | null) => { if (v) sessionStorage.setItem(prefix + scope, JSON.stringify(v)); else sessionStorage.removeItem(prefix + scope); current.current = v; setPending(v); setAbsent(false) }
  const owner = (v: Pending) => { if (v.actor !== actor) throw new Error('Este movimento pertence ao autor original. Retome com a mesma conta para consultar ou repetir; nenhum novo pedido foi enviado.') }
  const send = async (v: Pending) => {
    owner(v); if (sending.current) return undefined; sending.current = true; setBusy(true)
    try { if (v.definitiveFailureStatus) { v = { ...v, definitiveFailureStatus: undefined }; save(v) } const result = parseCommittedEvent(await httpRequest<unknown>(`/estoque/${operationInfo[v.operation].endpoint}`, { authenticated: true, method: 'POST', body: v.body, headers: { 'Idempotency-Key': v.key } })); save(null); return result }
    catch (e) { if (isHttpError(e) && (e.status === 400 || e.status === 409)) save({ ...v, definitiveFailureStatus: e.status }); throw e } finally { sending.current = false; setBusy(false) }
  }
  const submit = async (operation: Operation, body: MovementCommand) => { if (restored.error) throw new Error(restored.error); if (current.current || sending.current) throw new Error('Consulte ou repita o movimento original antes de editar.'); const v = { actor, operation, body, key: crypto.randomUUID() }; if (!validMovementPending(v)) throw new Error('Movimento inválido; nenhum pedido enviado.'); save(v); return send(v) }
  const consult = async () => { const v = current.current; if (!v || sending.current) return undefined; owner(v); sending.current = true; setBusy(true); setAbsent(false); try { const result = parseCommittedEvent(await httpRequest<unknown>(`/estoque/comandos/${v.operation}/${v.body.loteId}/${encodeURIComponent(v.key)}`, { authenticated: true })); save(null); return result } catch (e) { if (isHttpError(e) && e.status === 404) { setAbsent(true); return undefined } throw e } finally { sending.current = false; setBusy(false) } }
  const releaseAllowed = absent && !!pending?.definitiveFailureStatus
  return { pending, busy, absent, recoveryError: restored.error, submit, retry: () => current.current ? send(current.current) : Promise.resolve(undefined), consult, releaseAllowed, release: () => { if (releaseAllowed && !sending.current) save(null) } }
}
