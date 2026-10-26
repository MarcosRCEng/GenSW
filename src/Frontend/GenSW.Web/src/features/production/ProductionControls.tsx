import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/layout/PageHeader'
import { httpRequest } from '../../shared/http/httpClient'
import { inventoryError, parseLocal, parseLote, parseResponsible } from '../inventory/inventoryService'
import { buttonClass, Panel } from '../inventory/InventoryControls'
import type { ProductionCommandControl } from './useProductionCommand'
import type { ProductionOrder } from './types'
import { actionNames } from './productionActions'
export function ProductionFrame({ title, children }: { title: string; children: ReactNode }) {
  return <main className="gensw-page min-w-0 px-4 py-6 sm:px-6"><div className="mx-auto max-w-6xl space-y-6"><PageHeader title={title} context="Produção — transformações" /><nav aria-label="Produção" className="flex flex-wrap gap-2"><Link className={buttonClass} to="/producao/ordens">Ordens</Link><Link className={buttonClass} to="/producao/receitas">Receitas</Link><Link className={buttonClass} to="/estoque/saldos">Saldos</Link></nav>{children}</div></main>
}
export function ReferenceLabel({ kind, id, link = true }: { kind: 'lote' | 'local' | 'responsavel' | 'ordem'; id: string; link?: boolean }) {
  const [label, setLabel] = useState('Consultando referência…')
  const [error, setError] = useState(false)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setLabel('Consultando referência…'); setError(false)
    const path = kind === 'lote' ? `/estoque/lotes/${id}` : kind === 'local' ? `/estoque/locais/${id}` : kind === 'responsavel' ? `/estoque/responsaveis/${id}` : `/ordens-producao/${id}`
    void httpRequest<unknown>(path, { authenticated: true }).then(raw => {
      if (kind === 'lote') { const r = parseLote(raw); return `Lote ${r.codigo} · ${r.itemNome}` }
      if (kind === 'local') { const r = parseLocal(raw); return `${r.codigo} — ${r.nome}` }
      if (kind === 'responsavel') return parseResponsible(raw).nome
      if (!raw || typeof raw !== 'object' || !('codigo' in raw) || typeof raw.codigo !== 'string') throw new Error('Referência inválida')
      return `Ordem ${raw.codigo}`
    }).then(value => { if (live) setLabel(value) }).catch(() => { if (live) { setLabel('Referência indisponível'); setError(true) } })
    return () => { live = false }
  }, [kind, id, retry])
  const path = kind === 'lote' ? `/estoque/lotes/${id}` : kind === 'local' ? `/estoque/locais/${id}` : kind === 'ordem' ? `/producao/ordens/${id}` : undefined
  return <span className="break-words">{link && path ? <Link className="text-emerald-700 underline" to={path}>{label}</Link> : label}{error && <button type="button" className="ml-2 underline" onClick={() => setRetry(x => x + 1)}>Reconsultar referência</button>}</span>
}
export function PendingCommand({ control, complete, setError }: { control: ProductionCommandControl; complete: (order: ProductionOrder) => void; setError: (error: string | null) => void }) {
  if (control.recoveryError) return <Panel title="Operação pendente indisponível"><p role="alert">{control.recoveryError}</p></Panel>
  if (!control.pending) return null
  const run = async (action: 'retry' | 'consult') => {
    setError(null)
    try { const order = await control[action](); if (order) complete(order) } catch (error) { setError(inventoryError(error)) }
  }
  return <Panel title="Operação aguardando confirmação"><p role="status">{actionNames[control.pending.operation] ?? 'Operação'}: o pedido original foi preservado. Consulte o resultado ou repita exatamente esse pedido antes de editar.</p><div className="flex flex-wrap gap-3"><button className={buttonClass} type="button" disabled={control.busy} onClick={() => void run('consult')}>Consultar resultado</button><button className={buttonClass} type="button" disabled={control.busy} onClick={() => void run('retry')}>Repetir pedido original</button></div>{control.absent && <div className="space-y-3"><p>{control.releaseAllowed ? 'A API recusou o pedido e a consulta não encontrou resultado registrado. Você pode revisar os dados conscientemente antes de enviar outro pedido.' : 'Ainda não há resultado registrado para esta chave. O pedido pode continuar em processamento; repita o pedido original, mantendo a mesma chave e os mesmos dados.'}</p>{control.releaseAllowed && <button className={buttonClass} type="button" onClick={control.release}>Liberar edição após consulta</button>}</div>}</Panel>
}
