import { useRef, useState } from 'react'
import { httpRequest } from '../../shared/http/httpClient'
import { isHttpError } from '../../shared/http/httpErrors'
import { eggApi, parseEggCommandResult } from './eggBridgeService'
import type { EggCommandOperation, EggCommandResult } from './types'

export interface PendingEggCommand { operation: EggCommandOperation; path: string; key: string; body: Record<string, unknown>; definitiveFailureStatus?: 400 | 409 }
const storageKey = 'gensw.egg-bridge.retry.v1'
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
function valid(command: PendingEggCommand) {
  if (!object(command) || !object(command.body) || typeof command.key !== 'string' || !command.key.length || command.key.length > 100 || ![undefined, 400, 409].includes(command.definitiveFailureStatus)) return false
  const { operation, path, body } = command
  if (operation === 'Inscricao') return path === '/itens' && typeof body.itemId === 'string' && typeof body.motivo === 'string'
  if (operation === 'Recorte') return path === '/recortes' && ['itemId', 'fronteira', 'fontesConsultadas', 'lacunas', 'responsavelId', 'motivo'].every(key => typeof body[key] === 'string') && Array.isArray(body.decisoes) && body.decisoes.every(v => object(v) && typeof v.producaoOvoId === 'string' && ['NaoRecebidoComprovado', 'RecebidoComEquivalencia', 'Pendente'].includes(String(v.decisao)) && typeof v.individualComprovado === 'boolean' && typeof v.evidencia === 'string')
  if (operation === 'Prova') return path === '/provas' && ['movimentoEstoqueId', 'contagem', 'responsavelId', 'motivo', 'evidencia'].every(key => typeof body[key] === 'string')
  if (operation === 'Entrada' || operation === 'Associacao') return path === '/confirmacoes' && typeof body.token === 'string' && body.token.length > 0
  return ['Anotacao', 'Invalidacao', 'Compensacao'].includes(operation) && /^\/grupos\/[0-9a-f-]{36}\/tratamentos$/i.test(path) && typeof body.token === 'string' && body.token.length > 0
}
function restore() {
  try {
    const saved = sessionStorage.getItem(storageKey)
    if (!saved) return { pending: null, error: null }
    const command = JSON.parse(saved) as PendingEggCommand
    if (!valid(command)) throw new Error()
    return { pending: command, error: null }
  } catch { return { pending: null, error: 'Não foi possível recuperar o pedido F03 pendente. Preserve esta sessão e solicite diagnóstico antes de enviar outro pedido.' } }
}
export function useEggBridgeCommand() {
  const [restored] = useState(restore)
  const [pending, setPending] = useState<PendingEggCommand | null>(restored.pending)
  const current = useRef(pending)
  const sending = useRef(false)
  const [busy, setBusy] = useState(false)
  const [absent, setAbsent] = useState(false)
  const save = (command: PendingEggCommand | null) => {
    if (command) sessionStorage.setItem(storageKey, JSON.stringify(command))
    else sessionStorage.removeItem(storageKey)
    current.current = command; setPending(command); setAbsent(false)
  }
  const send = async (command: PendingEggCommand): Promise<EggCommandResult | undefined> => {
    if (sending.current) return undefined
    sending.current = true; setBusy(true)
    try {
      if (command.definitiveFailureStatus) { command = { ...command, definitiveFailureStatus: undefined }; save(command) }
      const result = parseEggCommandResult(command.operation, await httpRequest<unknown>(`${eggApi}${command.path}`, { authenticated: true, method: 'POST', body: command.body, headers: { 'Idempotency-Key': command.key } }))
      save(null); return result
    } catch (error) {
      if (isHttpError(error) && (error.status === 400 || error.status === 409)) save({ ...command, definitiveFailureStatus: error.status })
      throw error
    } finally { sending.current = false; setBusy(false) }
  }
  const submit = async (operation: EggCommandOperation, path: string, body: Record<string, unknown>) => {
    if (restored.error) throw new Error(restored.error)
    if (current.current) throw new Error('Consulte ou repita o pedido F03 pendente antes de alterar os dados.')
    const command: PendingEggCommand = { operation, path, body, key: crypto.randomUUID() }
    if (!valid(command)) throw new Error('Pedido F03 inválido. Revise os campos e a prévia.')
    // Preserve the exact token/body before the first request, including after reload/login.
    save(command)
    return send(command)
  }
  const retry = () => current.current ? send(current.current) : Promise.resolve(undefined)
  const consult = async () => {
    const command = current.current
    if (!command || sending.current) return undefined
    sending.current = true; setBusy(true); setAbsent(false)
    try {
      const result = parseEggCommandResult(command.operation, await httpRequest<unknown>(`${eggApi}/comandos/${encodeURIComponent(command.operation)}/${encodeURIComponent(command.key)}`, { authenticated: true }))
      save(null); return result
    } catch (error) {
      if (isHttpError(error) && error.status === 404) { setAbsent(true); return undefined }
      throw error
    } finally { sending.current = false; setBusy(false) }
  }
  const release = () => { if (absent && current.current?.definitiveFailureStatus && !sending.current) save(null) }
  return { submit, retry, consult, release, pending, busy: busy || !!restored.error, absent, releaseAllowed: absent && !!pending?.definitiveFailureStatus, recoveryError: restored.error }
}
export type EggCommandControl = ReturnType<typeof useEggBridgeCommand>
