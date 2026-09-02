import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { listEspecies } from '../../species/services/speciesService'
import type { Especie } from '../../species/types/species'
import { listRacas, setRacaAtivo } from '../services/breedsService'
import type { ListRacasParams, Raca, RacasPage, RacaSortBy, SortDirection } from '../types/breeds'

const INITIAL_PAGE_SIZE = 25
const SPECIES_PAGE_SIZE = 100
const dateFormatter = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeZone: 'UTC' })

interface BreedsTableProps {
  changingStatusId: string | null
  items: Raca[]
  onChangeStatus: (raca: Raca) => void
}

function BreedsTable({ changingStatusId, items, onChangeStatus }: BreedsTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600"><tr>
          <th className="px-4 py-3 font-semibold" scope="col">Nome</th>
          <th className="px-4 py-3 font-semibold" scope="col">Espécie</th>
          <th className="px-4 py-3 font-semibold" scope="col">Status</th>
          <th className="px-4 py-3 font-semibold" scope="col">Cadastro</th>
          <th className="px-4 py-3 font-semibold" scope="col">Ações</th>
        </tr></thead>
        <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
          {items.map((raca) => (
            <tr key={raca.id}>
              <td className="whitespace-nowrap px-4 py-4 font-medium text-slate-900" id={`breed-${raca.id}-name`}>{raca.nome}</td>
              <td className="whitespace-nowrap px-4 py-4">{raca.especie.nomeComum}</td>
              <td className="whitespace-nowrap px-4 py-4"><span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${raca.ativo ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'}`}>{raca.ativo ? 'Ativo' : 'Inativo'}</span></td>
              <td className="whitespace-nowrap px-4 py-4">{dateFormatter.format(new Date(raca.createdAtUtc))}</td>
              <td className="whitespace-nowrap px-4 py-4"><div className="flex items-center gap-3">
                <Link aria-describedby={`breed-${raca.id}-name`} className="font-semibold text-emerald-700 hover:text-emerald-900 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2" to={`/racas/${raca.id}/editar`}>Editar</Link>
                <button className="font-semibold text-slate-700 hover:text-slate-950 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60" disabled={changingStatusId === raca.id} onClick={() => onChangeStatus(raca)} type="button">
                  {changingStatusId === raca.id ? (raca.ativo ? 'Inativando…' : 'Reativando…') : (raca.ativo ? 'Inativar' : 'Reativar')}
                </button>
              </div></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function BreedsListPage() {
  const [result, setResult] = useState<RacasPage | null>(null)
  const [species, setSpecies] = useState<Especie[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [hasError, setHasError] = useState(false)
  const [speciesLoadError, setSpeciesLoadError] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [changingStatusId, setChangingStatusId] = useState<string | null>(null)
  const [statusMutationError, setStatusMutationError] = useState(false)
  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState<string | undefined>()
  const [especieId, setEspecieId] = useState<string | undefined>()
  const [ativo, setAtivo] = useState<boolean | undefined>()
  const [sortBy, setSortBy] = useState<RacaSortBy>('nome')
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(INITIAL_PAGE_SIZE)

  useEffect(() => {
    let isCurrent = true
    const loadSpecies = async () => {
      const loaded: Especie[] = []
      let currentPage = 1
      let totalPages = 1
      do {
        const response = await listEspecies({ page: currentPage, pageSize: SPECIES_PAGE_SIZE })
        loaded.push(...response.items)
        totalPages = response.totalPages
        currentPage += 1
      } while (currentPage <= totalPages)
      if (isCurrent) setSpecies(loaded)
    }
    setSpeciesLoadError(false)
    void loadSpecies().catch(() => { if (isCurrent) setSpeciesLoadError(true) })
    return () => { isCurrent = false }
  }, [])

  useEffect(() => {
    let isCurrent = true
    const params: ListRacasParams = {
      page,
      pageSize,
      sortBy,
      sortDirection,
      ...(search !== undefined ? { search } : {}),
      ...(especieId !== undefined ? { especieId } : {}),
      ...(ativo !== undefined ? { ativo } : {}),
    }
    setIsLoading(true)
    setHasError(false)
    void listRacas(params).then((response) => {
      if (!isCurrent) return
      if (response.totalPages > 0 && page > response.totalPages) {
        setPage(response.totalPages)
        return
      }
      setResult(response)
    }).catch(() => { if (isCurrent) setHasError(true) }).finally(() => { if (isCurrent) setIsLoading(false) })
    return () => { isCurrent = false }
  }, [ativo, especieId, page, pageSize, reloadKey, search, sortBy, sortDirection])

  const applySearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isLoading) return
    setPage(1)
    setSearch(searchDraft.trim() ? searchDraft : undefined)
    setReloadKey((current) => current + 1)
  }

  const clearFilters = () => {
    if (isLoading) return
    setSearchDraft('')
    setSearch(undefined)
    setEspecieId(undefined)
    setAtivo(undefined)
    setSortBy('nome')
    setSortDirection('asc')
    setPage(1)
    setReloadKey((current) => current + 1)
  }

  const changeStatus = async (raca: Raca) => {
    if (changingStatusId === raca.id) return
    setChangingStatusId(raca.id)
    setStatusMutationError(false)
    try {
      await setRacaAtivo(raca.id, !raca.ativo)
      setReloadKey((current) => current + 1)
    } catch {
      setStatusMutationError(true)
    } finally {
      setChangingStatusId(null)
    }
  }

  const controlClassName = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className="mx-auto max-w-7xl">
    <header className="flex flex-wrap items-start justify-between gap-4"><div><p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p><h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">Raças</h1><p className="mt-2 text-slate-600">Consulta do cadastro mestre de raças</p></div><div className="flex flex-wrap gap-3"><Link className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800" to="/racas/nova">Nova raça</Link><Link className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100" to="/">Voltar ao início</Link></div></header>
    <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><form className="grid gap-4 md:grid-cols-2 xl:grid-cols-4" onSubmit={applySearch}>
      <div className="md:col-span-2"><label className="text-sm font-medium text-slate-700" htmlFor="breeds-search">Buscar por nome</label><input className={controlClassName} disabled={isLoading} id="breeds-search" onChange={(event) => setSearchDraft(event.target.value)} type="search" value={searchDraft} /></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="breeds-species">Espécie</label><select className={controlClassName} disabled={isLoading || speciesLoadError} id="breeds-species" onChange={(event) => { setPage(1); setEspecieId(event.target.value || undefined) }} value={especieId ?? ''}><option value="">Todas</option>{species.map((item) => <option key={item.id} value={item.id}>{item.nomeComum}{item.ativo ? '' : ' (inativa)'}</option>)}</select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="breeds-active">Status</label><select className={controlClassName} disabled={isLoading} id="breeds-active" onChange={(event) => { setPage(1); setAtivo(event.target.value === '' ? undefined : event.target.value === 'true') }} value={ativo === undefined ? '' : String(ativo)}><option value="">Todos</option><option value="true">Ativos</option><option value="false">Inativos</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="breeds-sort">Ordenar por</label><select className={controlClassName} disabled={isLoading} id="breeds-sort" onChange={(event) => { setPage(1); setSortBy(event.target.value as RacaSortBy) }} value={sortBy}><option value="nome">Nome</option><option value="ativo">Status</option><option value="createdAtUtc">Cadastro</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="breeds-direction">Direção</label><select className={controlClassName} disabled={isLoading} id="breeds-direction" onChange={(event) => { setPage(1); setSortDirection(event.target.value as SortDirection) }} value={sortDirection}><option value="asc">Crescente</option><option value="desc">Decrescente</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="breeds-page-size">Registros por página</label><select className={controlClassName} disabled={isLoading} id="breeds-page-size" onChange={(event) => { setPage(1); setPageSize(Number(event.target.value)) }} value={pageSize}><option value={25}>25</option><option value={50}>50</option><option value={100}>100</option></select></div>
      <div className="flex flex-wrap items-end gap-3"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-60" disabled={isLoading} type="submit">Buscar</button><button className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-60" disabled={isLoading} onClick={clearFilters} type="button">Limpar filtros</button></div>
    </form></section>
    {speciesLoadError ? <p className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700" role="alert">Não foi possível carregar as espécies para o filtro.</p> : null}
    {statusMutationError ? <p className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700" role="alert">Não foi possível alterar o status da raça.</p> : null}
    <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
      {isLoading && result === null ? <p className="p-8 text-center text-slate-600" role="status">Carregando raças…</p> : hasError ? <div className="p-8 text-center" role="alert"><p className="font-medium text-red-700">Não foi possível carregar as raças.</p><button className="mt-4 rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-semibold text-red-700" onClick={() => setReloadKey((current) => current + 1)} type="button">Tentar novamente</button></div> : result?.items.length === 0 ? <div className="p-8 text-center"><p className="font-medium text-slate-800">Nenhuma raça encontrada.</p><p className="mt-2 text-sm text-slate-600">Ajuste os filtros ou realize outra busca.</p></div> : result ? <BreedsTable changingStatusId={changingStatusId} items={result.items} onChangeStatus={changeStatus} /> : null}
      {result && !hasError ? <footer className="flex flex-wrap items-center justify-between gap-4 border-t border-slate-200 px-5 py-4 text-sm text-slate-600"><div className="flex flex-wrap gap-3">{result.totalPages > 0 ? <span> Página {result.page} de {result.totalPages} </span> : null}<span>{result.totalItems} registros</span>{isLoading ? <span role="status">Atualizando…</span> : null}</div><div className="flex gap-2"><button className="rounded-lg border border-slate-300 px-3 py-2 font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-50" disabled={isLoading || page <= 1} onClick={() => setPage((current) => current - 1)} type="button">Anterior</button><button className="rounded-lg border border-slate-300 px-3 py-2 font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-50" disabled={isLoading || result.totalPages === 0 || page >= result.totalPages} onClick={() => setPage((current) => current + 1)} type="button">Próxima</button></div></footer> : null}
    </section>
  </div></main>
}
