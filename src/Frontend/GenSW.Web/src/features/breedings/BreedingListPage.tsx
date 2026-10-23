import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { PageHeader } from '../../shared/layout/PageHeader'
import { TableRegion } from '../../shared/layout/TableRegion'
import { listAnimals } from '../animals/services/animalsService'
import type { Animal } from '../animals/types/animals'
import { listBreedings, setBreedingStatus } from './breedingsService'
import type { Breeding, BreedingsPage, BreedingStatus } from './types'

const PAGE_SIZE = 25
const label = (status: BreedingStatus) => ['', 'Planejado', 'Em andamento', 'Concluído', 'Cancelado'][status]
const next = (status: BreedingStatus): BreedingStatus | undefined => status === 1 ? 2 : status === 2 ? 3 : undefined
const actionClass = 'rounded px-2 py-1 font-semibold focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50'

export function BreedingListPage() {
  const initial = useRestoredListState<{ page: number; status?: BreedingStatus; machoId?: string; femeaId?: string }>('/cruzamentos', { page: 1 })
  const [loaded, setLoaded] = useState<{ key: string; result?: BreedingsPage; error?: boolean } | null>(null)
  const [animals, setAnimals] = useState<Animal[]>([])
  const [page, setPage] = useState(Number.isSafeInteger(initial.page) && initial.page > 0 ? initial.page : 1)
  const [status, setStatus] = useState(initial.status)
  const [machoId, setMachoId] = useState(initial.machoId)
  const [femeaId, setFemeaId] = useState(initial.femeaId)
  const [catalogError, setCatalogError] = useState(false)
  const [mutationError, setMutationError] = useState(false)
  const [refresh, setRefresh] = useState(0)
  const [changing, setChanging] = useState<string | null>(null)
  const paginationSummary = useRef<HTMLParagraphElement>(null)
  const listState = useRememberListState('/cruzamentos', { page, status, machoId, femeaId })
  const queryKey = JSON.stringify([page, status, machoId, femeaId, refresh])
  const current = loaded?.key === queryKey ? loaded : null
  const result = current?.result
  const error = current?.error === true
  const loading = !current

  useEffect(() => {
    let current = true
    void listAnimals({ pageSize: 100 }).then(response => { if (current) setAnimals(response.items) }).catch(() => { if (current) setCatalogError(true) })
    return () => { current = false }
  }, [])

  useEffect(() => {
    let current = true
    void listBreedings({ page, pageSize: PAGE_SIZE, status, machoId, femeaId }).then(response => {
      if (!current) return
      const lastPage = Math.max(1, response.totalPages)
      if (page > lastPage) { setPage(lastPage); return }
      setLoaded({ key: queryKey, result: response })
    }).catch(() => { if (current) setLoaded({ key: queryKey, error: true }) })
    return () => { current = false }
  }, [status, machoId, femeaId, page, refresh, queryKey])

  const navigatePage = (target: number) => {
    if (loading || !result || target < 1 || target > result.totalPages) return
    // Native disabled buttons lose focus in browsers; move it before loading starts.
    paginationSummary.current?.focus()
    setRefresh(value => value + 1)
    setPage(target)
  }
  const resetPage = () => { setPage(1); setRefresh(value => value + 1) }
  const change = async (item: Breeding) => {
    const to = next(item.status)
    if (!to || changing) return
    setChanging(item.id)
    setMutationError(false)
    try { await setBreedingStatus(item.id, to); setRefresh(value => value + 1) }
    catch { setMutationError(true) }
    finally { setChanging(null) }
  }

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className="mx-auto max-w-7xl">
    <PageHeader title="Cruzamentos" description="Planejamento e acompanhamento reprodutivo. A filiação continua sendo a fonte de verdade do pedigree." actions={<Link className="gensw-primary-action" to="/cruzamentos/novo" state={listState}>Novo cruzamento</Link>} />
    <section className="mt-8 grid gap-4 rounded-2xl border bg-white p-5 sm:grid-cols-3">
      <div><label className="text-sm font-medium" htmlFor="breeding-status">Status</label><select className="mt-1 w-full rounded border p-2" id="breeding-status" value={status ?? ''} onChange={event => { resetPage(); setStatus(event.target.value ? Number(event.target.value) as BreedingStatus : undefined) }}><option value="">Todos</option><option value="1">Planejado</option><option value="2">Em andamento</option><option value="3">Concluído</option><option value="4">Cancelado</option></select></div>
      <div><label className="text-sm font-medium" htmlFor="breeding-male">Macho</label><select className="mt-1 w-full rounded border p-2" disabled={catalogError} id="breeding-male" value={machoId ?? ''} onChange={event => { resetPage(); setMachoId(event.target.value || undefined) }}><option value="">Todos</option>{animals.filter(item => item.sexo === 1).map(item => <option key={item.id} value={item.id}>{item.codigoInterno}</option>)}</select></div>
      <div><label className="text-sm font-medium" htmlFor="breeding-female">Fêmea</label><select className="mt-1 w-full rounded border p-2" disabled={catalogError} id="breeding-female" value={femeaId ?? ''} onChange={event => { resetPage(); setFemeaId(event.target.value || undefined) }}><option value="">Todas</option>{animals.filter(item => item.sexo === 2).map(item => <option key={item.id} value={item.id}>{item.codigoInterno}</option>)}</select></div>
    </section>
    {catalogError && <p className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-red-700" role="alert">Não foi possível carregar os animais para os filtros.</p>}
    {mutationError && <p className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-red-700" role="alert">Não foi possível alterar o status do cruzamento.</p>}
    <section className="mt-6 overflow-hidden rounded-2xl border bg-white" aria-busy={loading}>
      {loading ? <p className="p-8" role="status">Carregando cruzamentos…</p> : error ? <div className="p-8" role="alert"><p className="text-red-700">Não foi possível carregar os cruzamentos.</p><button className={`${actionClass} mt-4 border`} onClick={() => { paginationSummary.current?.focus(); setRefresh(value => value + 1) }} type="button">Tentar novamente</button></div> : result?.items.length === 0 ? <p className="p-8">Nenhum cruzamento encontrado.</p> : result ? <TableRegion label="Tabela de cruzamentos" className="overflow-x-auto"><table className="min-w-full text-left text-sm">
        <thead className="bg-slate-50 text-xs uppercase"><tr>{['Macho', 'Fêmea', 'Início', 'Status', 'Ações'].map(column => <th className="p-4" key={column} scope="col">{column}</th>)}</tr></thead>
        <tbody>{result.items.map(item => <tr className="border-t" key={item.id}>
          <td className="p-4">{item.macho.codigoInterno}{item.macho.nome ? ` — ${item.macho.nome}` : ''}</td><td className="p-4">{item.femea.codigoInterno}{item.femea.nome ? ` — ${item.femea.nome}` : ''}</td><td className="p-4">{item.dataInicio ?? '—'}</td><td className="p-4">{label(item.status)}</td>
          <td className="p-4"><div className="flex gap-3"><Link className="font-semibold text-emerald-700" state={listState} to={`/cruzamentos/${item.id}`}>Visualizar</Link><Link className="font-semibold text-emerald-700" state={listState} to={`/cruzamentos/${item.id}/editar`}>Editar</Link>{next(item.status) ? <button className={actionClass} disabled={changing !== null} onClick={() => void change(item)} type="button">{item.status === 1 ? 'Iniciar' : 'Concluir'}</button> : null}</div></td>
        </tr>)}</tbody>
      </table></TableRegion> : null}
      <nav className="flex flex-wrap items-center justify-between gap-4 border-t px-5 py-4 text-sm text-slate-600" aria-label="Paginação de cruzamentos">
        <p ref={paginationSummary} role="group" tabIndex={-1} aria-label="Resumo da paginação de cruzamentos" className="flex flex-wrap gap-3 rounded focus:outline-none focus:ring-2 focus:ring-emerald-600" aria-live="polite" aria-atomic="true">{result && <>{result.totalPages > 0 ? <span>Página {result.page} de {result.totalPages}</span> : null}<span>{result.totalItems} registros</span></>}</p>
        <div className="flex gap-2"><button className={actionClass} disabled={!result || result.page <= 1} onClick={() => { if (result) navigatePage(result.page - 1) }} type="button">Anterior</button><button className={actionClass} disabled={!result || result.totalPages === 0 || result.page >= result.totalPages} onClick={() => { if (result) navigatePage(result.page + 1) }} type="button">Próxima</button></div>
      </nav>
    </section>
  </div></main>
}
