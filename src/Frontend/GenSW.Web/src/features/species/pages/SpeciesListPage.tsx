import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { listEspecies, setEspecieAtivo } from '../services/speciesService'
import type { Especie, EspeciesPage, EspecieSortBy, ListEspeciesParams, SortDirection } from '../types/species'

const INITIAL_PAGE_SIZE = 25
const dateFormatter = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeZone: 'UTC' })

interface EspeciesTableProps {
  changingStatusId: string | null
  items: Especie[]
  onChangeStatus: (especie: Especie) => void
}

function EspeciesTable({ changingStatusId, items, onChangeStatus }: EspeciesTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600"><tr>
          <th className="px-4 py-3 font-semibold" scope="col">Nome comum</th>
          <th className="px-4 py-3 font-semibold" scope="col">Nome científico</th>
          <th className="px-4 py-3 font-semibold" scope="col">Status</th>
          <th className="px-4 py-3 font-semibold" scope="col">Cadastro</th>
          <th className="px-4 py-3 font-semibold" scope="col">Ações</th>
        </tr></thead>
        <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
          {items.map((especie) => (
            <tr key={especie.id}>
              <td className="whitespace-nowrap px-4 py-4 font-medium text-slate-900">{especie.nomeComum}</td>
              <td className="whitespace-nowrap px-4 py-4">{especie.nomeCientifico ?? '—'}</td>
              <td className="whitespace-nowrap px-4 py-4"><span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${especie.ativo ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'}`}>{especie.ativo ? 'Ativo' : 'Inativo'}</span></td>
              <td className="whitespace-nowrap px-4 py-4">{dateFormatter.format(new Date(especie.createdAtUtc))}</td>
              <td className="whitespace-nowrap px-4 py-4"><div className="flex items-center gap-3">
                <Link className="font-semibold text-emerald-700 hover:text-emerald-900 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2" to={`/especies/${especie.id}/editar`}>Editar</Link>
                <button className="font-semibold text-slate-700 hover:text-slate-950 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60" disabled={changingStatusId === especie.id} onClick={() => onChangeStatus(especie)} type="button">
                  {changingStatusId === especie.id ? (especie.ativo ? 'Inativando…' : 'Reativando…') : (especie.ativo ? 'Inativar' : 'Reativar')}
                </button>
              </div></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function SpeciesListPage() {
  const [result, setResult] = useState<EspeciesPage | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [hasError, setHasError] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [changingStatusId, setChangingStatusId] = useState<string | null>(null)
  const [statusMutationError, setStatusMutationError] = useState(false)
  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState<string | undefined>()
  const [ativo, setAtivo] = useState<boolean | undefined>()
  const [sortBy, setSortBy] = useState<EspecieSortBy>('nomeComum')
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(INITIAL_PAGE_SIZE)

  useEffect(() => {
    let isCurrent = true
    const params: ListEspeciesParams = { page, pageSize, sortBy, sortDirection, ...(search !== undefined ? { search } : {}), ...(ativo !== undefined ? { ativo } : {}) }
    setIsLoading(true)
    setHasError(false)
    void listEspecies(params).then((response) => {
      if (!isCurrent) return
      if (response.totalPages > 0 && page > response.totalPages) {
        setPage(response.totalPages)
        return
      }
      setResult(response)
    }).catch(() => { if (isCurrent) setHasError(true) }).finally(() => { if (isCurrent) setIsLoading(false) })
    return () => { isCurrent = false }
  }, [ativo, page, pageSize, reloadKey, search, sortBy, sortDirection])

  const applySearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isLoading) return
    setPage(1)
    setSearch(searchDraft.trim() ? searchDraft : undefined)
    setReloadKey((current) => current + 1)
  }
  const clearFilters = () => {
    if (isLoading) return
    setSearchDraft(''); setSearch(undefined); setAtivo(undefined); setSortBy('nomeComum'); setSortDirection('asc'); setPage(1); setReloadKey((current) => current + 1)
  }
  const changeStatus = async (especie: Especie) => {
    if (changingStatusId === especie.id) return
    setChangingStatusId(especie.id); setStatusMutationError(false)
    try { await setEspecieAtivo(especie.id, !especie.ativo); setReloadKey((current) => current + 1) }
    catch { setStatusMutationError(true) }
    finally { setChangingStatusId(null) }
  }
  const controlClassName = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className="mx-auto max-w-7xl">
    <header className="flex flex-wrap items-start justify-between gap-4"><div><p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p><h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">Espécies</h1><p className="mt-2 text-slate-600">Consulta do cadastro mestre de espécies</p></div><div className="flex flex-wrap gap-3"><Link className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800" to="/especies/nova">Nova espécie</Link><Link className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100" to="/">Voltar ao início</Link></div></header>
    <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><form className="grid gap-4 md:grid-cols-2 xl:grid-cols-4" onSubmit={applySearch}>
      <div className="md:col-span-2"><label className="text-sm font-medium text-slate-700" htmlFor="species-search">Buscar por nome</label><input className={controlClassName} disabled={isLoading} id="species-search" onChange={(event) => setSearchDraft(event.target.value)} type="search" value={searchDraft} /></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="species-active">Status</label><select className={controlClassName} disabled={isLoading} id="species-active" onChange={(event) => { setPage(1); setAtivo(event.target.value === '' ? undefined : event.target.value === 'true') }} value={ativo === undefined ? '' : String(ativo)}><option value="">Todos</option><option value="true">Ativos</option><option value="false">Inativos</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="species-sort">Ordenar por</label><select className={controlClassName} disabled={isLoading} id="species-sort" onChange={(event) => { setPage(1); setSortBy(event.target.value as EspecieSortBy) }} value={sortBy}><option value="nomeComum">Nome comum</option><option value="nomeCientifico">Nome científico</option><option value="ativo">Status</option><option value="createdAtUtc">Cadastro</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="species-direction">Direção</label><select className={controlClassName} disabled={isLoading} id="species-direction" onChange={(event) => { setPage(1); setSortDirection(event.target.value as SortDirection) }} value={sortDirection}><option value="asc">Crescente</option><option value="desc">Decrescente</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="species-page-size">Registros por página</label><select className={controlClassName} disabled={isLoading} id="species-page-size" onChange={(event) => { setPage(1); setPageSize(Number(event.target.value)) }} value={pageSize}><option value={25}>25</option><option value={50}>50</option><option value={100}>100</option></select></div>
      <div className="flex flex-wrap items-end gap-3"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-60" disabled={isLoading} type="submit">Buscar</button><button className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-60" disabled={isLoading} onClick={clearFilters} type="button">Limpar filtros</button></div>
    </form></section>
    {statusMutationError ? <p className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700" role="alert">Não foi possível alterar o status da espécie.</p> : null}
    <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
      {isLoading && result === null ? <p className="p-8 text-center text-slate-600" role="status">Carregando espécies…</p> : hasError ? <div className="p-8 text-center" role="alert"><p className="font-medium text-red-700">Não foi possível carregar as espécies.</p><button className="mt-4 rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-semibold text-red-700" onClick={() => setReloadKey((current) => current + 1)} type="button">Tentar novamente</button></div> : result?.items.length === 0 ? <div className="p-8 text-center"><p className="font-medium text-slate-800">Nenhuma espécie encontrada.</p><p className="mt-2 text-sm text-slate-600">Ajuste os filtros ou realize outra busca.</p></div> : result ? <EspeciesTable changingStatusId={changingStatusId} items={result.items} onChangeStatus={changeStatus} /> : null}
      {result && !hasError ? <footer className="flex flex-wrap items-center justify-between gap-4 border-t border-slate-200 px-5 py-4 text-sm text-slate-600"><div className="flex flex-wrap gap-3">{result.totalPages > 0 ? <span> Página {result.page} de {result.totalPages} </span> : null}<span>{result.totalItems} registros</span>{isLoading ? <span role="status">Atualizando…</span> : null}</div><div className="flex gap-2"><button className="rounded-lg border border-slate-300 px-3 py-2 font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-50" disabled={isLoading || page <= 1} onClick={() => setPage((current) => current - 1)} type="button">Anterior</button><button className="rounded-lg border border-slate-300 px-3 py-2 font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-50" disabled={isLoading || result.totalPages === 0 || page >= result.totalPages} onClick={() => setPage((current) => current + 1)} type="button">Próxima</button></div></footer> : null}
    </section>
  </div></main>
}
