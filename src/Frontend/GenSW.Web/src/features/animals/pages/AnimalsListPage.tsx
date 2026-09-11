import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { listRacas } from '../../breeds/services/breedsService'
import type { Raca } from '../../breeds/types/breeds'
import { listEspecies } from '../../species/services/speciesService'
import type { Especie } from '../../species/types/species'
import { listVariedades } from '../../varieties/services/varietiesService'
import type { Variedade } from '../../varieties/types/varieties'
import { listAnimals, setAnimalAtivo } from '../services/animalsService'
import type { Animal, AnimalSortBy, AnimalsPage, EscopoAnimal, ListAnimalsParams, SexoAnimal, SortDirection } from '../types/animals'

const INITIAL_PAGE_SIZE = 25
const CATALOG_PAGE_SIZE = 100

async function loadAllPages<T>(load: (params: { page: number; pageSize: number }) => Promise<{ items: T[]; totalPages: number }>): Promise<T[]> {
  const loaded: T[] = []
  let page = 1
  let totalPages = 1
  do {
    const response = await load({ page, pageSize: CATALOG_PAGE_SIZE })
    loaded.push(...response.items)
    totalPages = response.totalPages
    page += 1
  } while (page <= totalPages)
  return loaded
}

function optionLabel(name: string, active: boolean) { return active ? name : `${name} (inativa)` }
function sexoLabel(sexo: SexoAnimal) { return sexo === 1 ? 'Macho' : sexo === 2 ? 'Fêmea' : 'Indeterminado' }
function escopoLabel(escopo: EscopoAnimal) { return escopo === 1 ? 'Operacional' : 'Referência' }

function AnimalsTable({ items, changingStatusId, onChangeStatus }: { items: Animal[]; changingStatusId: string | null; onChangeStatus: (animal: Animal) => void }) {
  return <div className="overflow-x-auto"><table className="min-w-full divide-y divide-slate-200 text-left text-sm"><thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600"><tr>
    <th className="px-4 py-3 font-semibold" scope="col">Código interno</th><th className="px-4 py-3 font-semibold" scope="col">Nome</th><th className="px-4 py-3 font-semibold" scope="col">Espécie</th><th className="px-4 py-3 font-semibold" scope="col">Raça</th><th className="px-4 py-3 font-semibold" scope="col">Variedade</th><th className="px-4 py-3 font-semibold" scope="col">Sexo</th><th className="px-4 py-3 font-semibold" scope="col">Escopo</th><th className="px-4 py-3 font-semibold" scope="col">Status</th><th className="px-4 py-3 font-semibold" scope="col">Ações</th>
  </tr></thead><tbody className="divide-y divide-slate-100 bg-white text-slate-700">{items.map((animal) => <tr key={animal.id}>
    <td className="whitespace-nowrap px-4 py-4 font-medium text-slate-900">{animal.codigoInterno}</td><td className="whitespace-nowrap px-4 py-4">{animal.nome ?? '—'}</td><td className="whitespace-nowrap px-4 py-4">{animal.especie.nomeComum}</td><td className="whitespace-nowrap px-4 py-4">{animal.raca?.nome ?? '—'}</td><td className="whitespace-nowrap px-4 py-4">{animal.variedade?.nome ?? '—'}</td><td className="whitespace-nowrap px-4 py-4">{sexoLabel(animal.sexo)}</td><td className="whitespace-nowrap px-4 py-4">{escopoLabel(animal.escopo)}</td><td className="whitespace-nowrap px-4 py-4"><span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${animal.ativo ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'}`}>{animal.ativo ? 'Ativo' : 'Inativo'}</span></td>
    <td className="whitespace-nowrap px-4 py-4"><div className="flex gap-3"><Link className="font-semibold text-emerald-700 hover:text-emerald-900" to={`/animais/${animal.id}/editar`}>Editar</Link><button className="font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-60" disabled={changingStatusId === animal.id} onClick={() => onChangeStatus(animal)} type="button">{changingStatusId === animal.id ? (animal.ativo ? 'Inativando…' : 'Reativando…') : (animal.ativo ? 'Inativar' : 'Reativar')}</button></div></td>
  </tr>)}</tbody></table></div>
}

export function AnimalsListPage() {
  const [result, setResult] = useState<AnimalsPage | null>(null)
  const [species, setSpecies] = useState<Especie[]>([])
  const [breeds, setBreeds] = useState<Raca[]>([])
  const [varieties, setVarieties] = useState<Variedade[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [hasError, setHasError] = useState(false)
  const [catalogError, setCatalogError] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [changingStatusId, setChangingStatusId] = useState<string | null>(null)
  const [statusMutationError, setStatusMutationError] = useState(false)
  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState<string | undefined>()
  const [especieId, setEspecieId] = useState<string | undefined>()
  const [racaId, setRacaId] = useState<string | undefined>()
  const [variedadeId, setVariedadeId] = useState<string | undefined>()
  const [sexo, setSexo] = useState<SexoAnimal | undefined>()
  const [escopo, setEscopo] = useState<EscopoAnimal | undefined>()
  const [ativo, setAtivo] = useState<boolean | undefined>()
  const [sortBy, setSortBy] = useState<AnimalSortBy>('codigoInterno')
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(INITIAL_PAGE_SIZE)

  useEffect(() => {
    let current = true
    setCatalogError(false)
    void Promise.all([loadAllPages(listEspecies), loadAllPages(listRacas), loadAllPages(listVariedades)]).then(([loadedSpecies, loadedBreeds, loadedVarieties]) => {
      if (!current) return
      setSpecies(loadedSpecies); setBreeds(loadedBreeds); setVarieties(loadedVarieties)
    }).catch(() => { if (current) setCatalogError(true) })
    return () => { current = false }
  }, [])

  useEffect(() => {
    let current = true
    const params: ListAnimalsParams = { page, pageSize, sortBy, sortDirection, ...(search !== undefined ? { search } : {}), ...(especieId !== undefined ? { especieId } : {}), ...(racaId !== undefined ? { racaId } : {}), ...(variedadeId !== undefined ? { variedadeId } : {}), ...(sexo !== undefined ? { sexo } : {}), ...(escopo !== undefined ? { escopo } : {}), ...(ativo !== undefined ? { ativo } : {}) }
    setIsLoading(true); setHasError(false)
    void listAnimals(params).then((response) => {
      if (!current) return
      if (response.totalPages > 0 && page > response.totalPages) { setPage(response.totalPages); return }
      setResult(response)
    }).catch(() => { if (current) setHasError(true) }).finally(() => { if (current) setIsLoading(false) })
    return () => { current = false }
  }, [ativo, escopo, especieId, page, pageSize, racaId, reloadKey, search, sexo, sortBy, sortDirection, variedadeId])

  const resetPage = () => setPage(1)
  const applySearch = (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); if (isLoading) return; resetPage(); setSearch(searchDraft.trim() || undefined); setReloadKey((key) => key + 1) }
  const changeStatus = async (animal: Animal) => {
    if (changingStatusId === animal.id) return
    setChangingStatusId(animal.id); setStatusMutationError(false)
    try { await setAnimalAtivo(animal.id, !animal.ativo); setReloadKey((key) => key + 1) }
    catch { setStatusMutationError(true) }
    finally { setChangingStatusId(null) }
  }
  const control = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 disabled:cursor-not-allowed disabled:bg-slate-100'

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className="mx-auto max-w-7xl"><header className="flex flex-wrap items-start justify-between gap-4"><div><p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p><h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">Animais</h1><p className="mt-2 text-slate-600">Consulta do cadastro base de animais</p></div><div className="flex gap-3"><Link className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white" to="/animais/nova">Novo animal</Link><Link className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700" to="/">Voltar ao início</Link></div></header>
    <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><form className="grid gap-4 md:grid-cols-2 xl:grid-cols-4" onSubmit={applySearch}>
      <div className="md:col-span-2"><label className="text-sm font-medium text-slate-700" htmlFor="animals-search">Buscar por código ou nome</label><input className={control} disabled={isLoading} id="animals-search" onChange={(event) => setSearchDraft(event.target.value)} type="search" value={searchDraft} /></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-species">Espécie</label><select className={control} disabled={isLoading || catalogError} id="animals-species" onChange={(event) => { resetPage(); setEspecieId(event.target.value || undefined) }} value={especieId ?? ''}><option value="">Todas</option>{species.map((item) => <option key={item.id} value={item.id}>{optionLabel(item.nomeComum, item.ativo)}</option>)}</select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-breed">Raça</label><select className={control} disabled={isLoading || catalogError} id="animals-breed" onChange={(event) => { resetPage(); setRacaId(event.target.value || undefined) }} value={racaId ?? ''}><option value="">Todas</option>{breeds.map((item) => <option key={item.id} value={item.id}>{optionLabel(item.nome, item.ativo)}</option>)}</select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-variety">Variedade</label><select className={control} disabled={isLoading || catalogError} id="animals-variety" onChange={(event) => { resetPage(); setVariedadeId(event.target.value || undefined) }} value={variedadeId ?? ''}><option value="">Todas</option>{varieties.map((item) => <option key={item.id} value={item.id}>{optionLabel(item.nome, item.ativo)}</option>)}</select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-sex">Sexo</label><select className={control} disabled={isLoading} id="animals-sex" onChange={(event) => { resetPage(); setSexo(event.target.value ? Number(event.target.value) as SexoAnimal : undefined) }} value={sexo ?? ''}><option value="">Todos</option><option value="1">Macho</option><option value="2">Fêmea</option><option value="3">Indeterminado</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-scope">Escopo</label><select className={control} disabled={isLoading} id="animals-scope" onChange={(event) => { resetPage(); setEscopo(event.target.value ? Number(event.target.value) as EscopoAnimal : undefined) }} value={escopo ?? ''}><option value="">Todos</option><option value="1">Operacional</option><option value="2">Referência</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-active">Status</label><select className={control} disabled={isLoading} id="animals-active" onChange={(event) => { resetPage(); setAtivo(event.target.value === '' ? undefined : event.target.value === 'true') }} value={ativo === undefined ? '' : String(ativo)}><option value="">Todos</option><option value="true">Ativos</option><option value="false">Inativos</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-sort">Ordenar por</label><select className={control} disabled={isLoading} id="animals-sort" onChange={(event) => { resetPage(); setSortBy(event.target.value as AnimalSortBy) }} value={sortBy}><option value="codigoInterno">Código interno</option><option value="nome">Nome</option><option value="sexo">Sexo</option><option value="escopo">Escopo</option><option value="ativo">Status</option><option value="createdAtUtc">Cadastro</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-direction">Direção</label><select className={control} disabled={isLoading} id="animals-direction" onChange={(event) => { resetPage(); setSortDirection(event.target.value as SortDirection) }} value={sortDirection}><option value="asc">Crescente</option><option value="desc">Decrescente</option></select></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="animals-page-size">Registros por página</label><select className={control} disabled={isLoading} id="animals-page-size" onChange={(event) => { resetPage(); setPageSize(Number(event.target.value)) }} value={pageSize}><option value={25}>25</option><option value={50}>50</option><option value={100}>100</option></select></div><div className="flex flex-wrap items-end gap-3"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-60" disabled={isLoading} type="submit">Buscar</button></div>
    </form></section>
    {catalogError ? <p className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700" role="alert">Não foi possível carregar os catálogos para os filtros.</p> : null}
    {statusMutationError ? <p className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700" role="alert">Não foi possível alterar o status do animal.</p> : null}
    <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">{isLoading && result === null ? <p className="p-8 text-center text-slate-600" role="status">Carregando animais…</p> : hasError ? <div className="p-8 text-center" role="alert"><p className="font-medium text-red-700">Não foi possível carregar os animais.</p><button className="mt-4 rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-semibold text-red-700" onClick={() => setReloadKey((key) => key + 1)} type="button">Tentar novamente</button></div> : result?.items.length === 0 ? <div className="p-8 text-center"><p className="font-medium text-slate-800">Nenhum animal encontrado.</p></div> : result ? <AnimalsTable changingStatusId={changingStatusId} items={result.items} onChangeStatus={changeStatus} /> : null}
      {result && !hasError ? <footer className="flex flex-wrap items-center justify-between gap-4 border-t border-slate-200 px-5 py-4 text-sm text-slate-600"><div>{result.totalPages > 0 ? <span>Página {result.page} de {result.totalPages}</span> : null}<span className="ml-3">{result.totalItems} registros</span>{isLoading ? <span className="ml-3" role="status">Atualizando…</span> : null}</div><div className="flex gap-2"><button disabled={isLoading || page <= 1} onClick={() => setPage((current) => current - 1)} type="button">Anterior</button><button disabled={isLoading || result.totalPages === 0 || page >= result.totalPages} onClick={() => setPage((current) => current + 1)} type="button">Próxima</button></div></footer> : null}
    </section></div></main>
}
