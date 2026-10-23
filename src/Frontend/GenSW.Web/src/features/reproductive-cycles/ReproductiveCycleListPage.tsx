import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { listReturnState, parentReturnState, readParentReturnState, useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { PageHeader } from '../../shared/layout/PageHeader'
import { TableRegion } from '../../shared/layout/TableRegion'
import { listReproductiveCycles, setReproductiveCycleStatus } from './reproductiveCyclesService'
import type { ReproductiveCycle, ReproductiveCycleStatus, ReproductiveCycleType, ReproductiveCyclesPage } from './types'

const PAGE_SIZE = 25
interface CycleFilters { page: number; tipo?: ReproductiveCycleType; st?: ReproductiveCycleStatus }
const typeLabel = (value: ReproductiveCycleType) => value === 1 ? 'Ovíparo' : 'Gestacional'
const statusLabel = (value: ReproductiveCycleStatus) => ['', 'Em andamento', 'Concluído', 'Cancelado'][value]
const actionClass = 'font-semibold text-emerald-700 focus:outline-none focus:ring-2 focus:ring-emerald-600 disabled:opacity-50'

export function ReproductiveCycleListPage() {
  const [query] = useSearchParams()
  const crossing = query.get('cruzamentoId') || undefined
  const listPath = crossing ? `/ciclos-reprodutivos?cruzamentoId=${encodeURIComponent(crossing)}` : '/ciclos-reprodutivos'
  // A different parent starts a different list, even when the router reuses this route.
  return <CycleList key={listPath} crossing={crossing} listPath={listPath} />
}

function CycleList({ crossing, listPath }: { crossing?: string; listPath: string }) {
  const location = useLocation()
  const parentPath = `/cruzamentos/${crossing}`
  const parentState = crossing ? readParentReturnState(location.state, parentPath) ?? parentReturnState(undefined, parentPath, { section: 'ciclos' }) : undefined
  const initial = useRestoredListState<CycleFilters>(listPath, { page: 1 })
  const restore = listReturnState(location.state, listPath)?.list.path === listPath
  const [page, setPage] = useState(restore && Number.isSafeInteger(initial.page) && initial.page > 0 ? initial.page : 1)
  const [tipo, setTipo] = useState(restore ? initial.tipo : undefined)
  const [st, setSt] = useState(restore ? initial.st : undefined)
  const [refresh, setRefresh] = useState(0)
  const [loaded, setLoaded] = useState<{ key: string; result?: ReproductiveCyclesPage; error?: boolean } | null>(null)
  const [changingStatusId, setChangingStatusId] = useState<string | null>(null)
  const [mutationError, setMutationError] = useState(false)
  const paginationSummary = useRef<HTMLDivElement>(null)
  const remembered = useRememberListState(listPath, { page, tipo, st })
  const listState = { ...remembered, ...(parentState ? { parentReturn: parentState.parentReturn } : {}) }
  const queryKey = JSON.stringify([crossing, page, tipo, st, refresh])
  const current = loaded?.key === queryKey ? loaded : null
  const result = current?.result
  const error = current?.error === true
  const loading = !current

  useEffect(() => {
    let active = true
    void listReproductiveCycles({ cruzamentoId: crossing, tipo, status: st, page, pageSize: PAGE_SIZE }).then(response => {
      if (!active) return
      const lastPage = Math.max(1, response.totalPages)
      if (page > lastPage) { setPage(lastPage); return }
      setLoaded({ key: queryKey, result: response })
    }).catch(() => { if (active) setLoaded({ key: queryKey, error: true }) })
    return () => { active = false }
  }, [crossing, page, tipo, st, refresh, queryKey])

  const navigatePage = (target: number) => {
    if (!result || target < 1 || target > result.totalPages) return
    // Native disabled buttons lose focus in browsers; move it before loading starts.
    paginationSummary.current?.focus()
    setPage(target)
    setRefresh(value => value + 1)
  }

  const complete = async (item: ReproductiveCycle) => {
    if (changingStatusId) return
    setChangingStatusId(item.id)
    setMutationError(false)
    try {
      await setReproductiveCycleStatus(item.id, 2)
      setRefresh(value => value + 1)
    } catch { setMutationError(true) }
    finally { setChangingStatusId(null) }
  }

  return <main className="min-h-screen bg-slate-50 px-4 py-8"><div className="mx-auto max-w-6xl">
    <PageHeader back={parentState ? { to: parentPath, label: 'Voltar para Cruzamento', state: parentState.parentReturn.origin ?? parentState } : undefined}
      title="Ciclos reprodutivos" description="Resultados posteriores ao cruzamento; não criam filiação nem animais."
      actions={crossing ? <Link className="gensw-primary-action" state={listState} to={`/ciclos-reprodutivos/novo?cruzamentoId=${encodeURIComponent(crossing)}`}>Novo ciclo</Link> : undefined} />
    <section className="mt-6 grid gap-3 rounded-xl border bg-white p-4 sm:grid-cols-2">
      <select aria-label="Fluxo" className="rounded border p-2" value={tipo ?? ''} onChange={event => { setPage(1); setTipo(event.target.value ? Number(event.target.value) as ReproductiveCycleType : undefined); setRefresh(value => value + 1) }}>
        <option value="">Todos os fluxos</option><option value="1">Ovíparo</option><option value="2">Gestacional</option>
      </select>
      <select aria-label="Status do ciclo" className="rounded border p-2" value={st ?? ''} onChange={event => { setPage(1); setSt(event.target.value ? Number(event.target.value) as ReproductiveCycleStatus : undefined); setRefresh(value => value + 1) }}>
        <option value="">Todos os status</option><option value="1">Em andamento</option><option value="2">Concluído</option><option value="3">Cancelado</option>
      </select>
    </section>
    {mutationError ? <p className="mt-4 text-red-700" role="alert">Não foi possível atualizar o ciclo.</p> : null}
    <section className="mt-6 overflow-hidden rounded-xl border bg-white" aria-busy={loading}>
      {loading ? <p className="p-6 text-slate-600" role="status">Carregando ciclos…</p> : error ? <div className="p-6" role="alert">
        <p className="text-red-700">Não foi possível carregar os ciclos.</p>
        <button className={`${actionClass} mt-4`} onClick={() => { paginationSummary.current?.focus(); setRefresh(value => value + 1) }} type="button">Tentar novamente</button>
      </div> : result?.items.length === 0 ? <p className="p-6">Nenhum ciclo encontrado.</p> : result ? <TableRegion label="Tabela de ciclos reprodutivos" className="overflow-x-auto">
        <table className="min-w-full text-left text-sm"><thead className="bg-slate-50"><tr>
          <th scope="col" className="p-3">Fluxo</th><th scope="col" className="p-3">Status</th><th scope="col" className="p-3">Resultado</th><th scope="col" className="p-3">Ações</th>
        </tr></thead><tbody>{result.items.map(item => <tr className="border-t" key={item.id}>
          <td className="p-3">{typeLabel(item.tipo)}</td><td className="p-3">{statusLabel(item.status)}</td><td className="p-3">{item.tipo === 1 ? `${item.ovosEclodidos ?? 0} eclodidos` : `${item.nascidosVivos ?? 0} vivos`}</td>
          <td className="p-3"><div className="flex flex-wrap gap-3">
            <Link className={actionClass} state={listState} to={`/ciclos-reprodutivos/${item.id}`}>Visualizar</Link>
            <Link className={actionClass} state={listState} to={`/ciclos-reprodutivos/${item.id}/editar`}>Editar</Link>
            {item.status === 1 ? <button className={actionClass} disabled={changingStatusId !== null} onClick={() => void complete(item)} type="button">{changingStatusId === item.id ? 'Concluindo…' : 'Concluir'}</button> : null}
          </div></td>
        </tr>)}</tbody></table>
      </TableRegion> : null}
      <nav aria-label="Paginação de ciclos reprodutivos" className="flex flex-wrap items-center justify-between gap-4 border-t px-5 py-4 text-sm text-slate-600">
        <div ref={paginationSummary} role="group" tabIndex={-1} aria-label="Resumo da paginação de ciclos reprodutivos" className="rounded focus:outline-none focus:ring-2 focus:ring-emerald-600" aria-live="polite" aria-atomic="true">{result ? <>{result.totalPages > 0 ? <span>Página {result.page} de {result.totalPages}</span> : null}<span className="ml-3">{result.totalItems} registros</span></> : null}</div>
        <div className="flex gap-4"><button className={actionClass} disabled={!result || result.page <= 1} onClick={() => { if (result) navigatePage(result.page - 1) }} type="button">Anterior</button>
          <button className={actionClass} disabled={!result || result.page >= result.totalPages} onClick={() => { if (result) navigatePage(result.page + 1) }} type="button">Próxima</button></div>
      </nav>
    </section>
  </div></main>
}
