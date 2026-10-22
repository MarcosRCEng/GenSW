import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { listReturnState, parentReturnState, readParentReturnState, useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { PageHeader } from '../../shared/layout/PageHeader'
import { TableRegion } from '../../shared/layout/TableRegion'
import { listOffspring } from './offspringService'
import type { OffspringPage } from './types'

const PAGE_SIZE = 25
const actionClass = 'font-semibold text-emerald-700 hover:text-emerald-900 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2'
const buttonClass = 'rounded-lg border border-slate-300 px-3 py-2 font-semibold text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50'
type RequestState = { key: string; status: 'loading' | 'ready' | 'error'; result?: OffspringPage }

export function OffspringListPage() {
  const [query] = useSearchParams()
  const cycle = query.get('cicloReprodutivoId') ?? undefined
  const listPath = cycle ? `/proles?cicloReprodutivoId=${encodeURIComponent(cycle)}` : '/proles'
  // A changed parent is a new list context, including when the router reuses this route.
  return <OffspringList key={listPath} cycle={cycle} listPath={listPath} />
}

function OffspringList({ cycle, listPath }: { cycle?: string; listPath: string }) {
  const location = useLocation()
  const initial = useRestoredListState<{ page: number }>(listPath, { page: 1 })
  const [page, setPage] = useState(() => listReturnState(location.state, listPath)?.list.path === listPath && Number.isSafeInteger(initial.page) && initial.page > 0 ? initial.page : 1)
  const [refresh, setRefresh] = useState(0)
  const paginationSummary = useRef<HTMLParagraphElement>(null)
  const key = `${listPath}:${page}:${refresh}`
  const [request, setRequest] = useState<RequestState>({ key, status: 'loading' })
  const current = request.key === key ? request : undefined
  const result = current?.status === 'ready' ? current.result : undefined
  const loading = !current || current.status === 'loading'
  const error = current?.status === 'error'
  const parentPath = `/ciclos-reprodutivos/${cycle}`
  const parentState = cycle ? readParentReturnState(location.state, parentPath) ?? parentReturnState(undefined, parentPath, { section: 'proles' }) : undefined
  const listState = useRememberListState(listPath, { page, pageSize: PAGE_SIZE })

  useEffect(() => {
    let active = true
    setRequest({ key, status: 'loading' })
    void listOffspring({ cicloReprodutivoId: cycle, page, pageSize: PAGE_SIZE }).then(response => {
      if (!active) return
      const validPage = response.totalPages === 0 ? 1 : Math.min(page, response.totalPages)
      if (page !== validPage) { setPage(validPage); return }
      setRequest({ key, status: 'ready', result: response })
    }).catch(() => { if (active) setRequest({ key, status: 'error' }) })
    return () => { active = false }
  }, [cycle, key, page])

  const navigatePage = (target: number) => {
    if (!result || target < 1 || target > result.totalPages) return
    // Native disabled buttons lose focus in browsers; move it before loading starts.
    paginationSummary.current?.focus()
    setPage(target)
  }

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className="mx-auto max-w-5xl">
    <PageHeader back={parentState ? { to: parentPath, label: 'Voltar para Ciclo reprodutivo', state: parentState.parentReturn.origin ?? parentState } : undefined}
      title="Proles" description="Registros individuais e por lote, sem alterar o resultado histórico do ciclo."
      actions={cycle ? <Link className="gensw-primary-action" state={listState} to={`/proles/nova?cicloReprodutivoId=${encodeURIComponent(cycle)}`}>Registrar prole</Link> : undefined} />
    <section className="mt-6 min-w-0 overflow-hidden rounded-xl border bg-white" aria-busy={loading}>
      {loading ? <p className="p-6" role="status">Carregando proles…</p> : error ? <div className="p-6" role="alert">
        <p className="text-red-700">Não foi possível carregar as proles.</p>
        <button className={`${buttonClass} mt-4`} type="button" onClick={() => { paginationSummary.current?.focus(); setRefresh(value => value + 1) }}>Tentar novamente</button>
      </div> : result?.items.length === 0 ? <p className="p-6">Nenhuma prole encontrada.</p> : result ? <TableRegion label="Tabela de proles" className="overflow-x-auto">
        <table className="min-w-full text-left text-sm"><thead className="bg-slate-50"><tr>
          <th className="p-3" scope="col">Tipo</th><th className="p-3" scope="col">Origem/data</th><th className="p-3" scope="col">Quantidade</th><th className="p-3" scope="col">Animal</th><th className="p-3" scope="col">Ações</th>
        </tr></thead><tbody>{result.items.map(item => <tr className="border-t" key={item.id}>
          <td className="p-3">{item.tipoRegistro === 1 ? 'Individual' : 'Lote'}</td>
          <td className="p-3">{item.origem === 1 ? 'Nascimento' : 'Eclosão'} — {item.data}</td>
          <td className="p-3">{item.quantidade}{item.tipoRegistro === 2 ? ` (${item.quantidadeDesdobrada} desdobrada)` : ''}</td>
          <td className="p-3">{item.animalId ? <Link className={actionClass} to={`/animais/${item.animalId}`}>Animal resultante</Link> : '—'}</td>
          <td className="p-3"><div className="flex gap-3"><Link className={actionClass} state={listState} to={`/proles/${item.id}`}>Visualizar</Link><Link className={actionClass} state={listState} to={`/proles/${item.id}/editar`}>Editar</Link></div></td>
        </tr>)}</tbody></table>
      </TableRegion> : null}
      <nav aria-label="Paginação de proles" className="flex flex-wrap items-center justify-between gap-4 border-t px-4 py-4 text-sm text-slate-600">
        <p ref={paginationSummary} role="group" tabIndex={-1} aria-label="Resumo da paginação de proles" className="flex flex-wrap gap-3 rounded focus:outline-none focus:ring-2 focus:ring-emerald-600" aria-live="polite" aria-atomic="true">{result && <>{result.totalPages > 0 && <span>Página {result.page} de {result.totalPages}</span>}<span>{result.totalItems} registros</span></>}</p>
        <div className="flex gap-2"><button className={buttonClass} disabled={!result || result.page <= 1 || result.totalPages === 0} onClick={() => { if (result) navigatePage(result.page - 1) }} type="button">Anterior</button>
          <button className={buttonClass} disabled={!result || result.totalPages === 0 || result.page >= result.totalPages} onClick={() => { if (result) navigatePage(result.page + 1) }} type="button">Próxima</button></div>
      </nav>
    </section>
  </div></main>
}
