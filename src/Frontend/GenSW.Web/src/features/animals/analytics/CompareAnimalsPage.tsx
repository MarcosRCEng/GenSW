import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useLocation, useSearchParams } from 'react-router-dom'
import { listReturnState, readParentReturnState, useRestoredListState } from '../../../shared/details/listNavigation'
import { PageHeader } from '../../../shared/layout/PageHeader'
import { AnimalTaxonomyFilters, type AnimalTaxonomyFilterValues } from '../components/AnimalTaxonomyFilters'
import { getAnimalById, listAnimals } from '../services/animalsService'
import type { Animal, AnimalsPage, ListAnimalsParams } from '../types/animals'
import { AnimalAnalyticsPanel } from './AnimalAnalyticsPanel'

const PAGE_SIZE = 25
const sectionClass = 'mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6'
const secondaryButton = 'gensw-secondary-action disabled:cursor-not-allowed disabled:opacity-50'

interface CandidateQuery {
  filters: AnimalTaxonomyFilterValues
  search: string
  page: number
}

interface CandidateResponse {
  query: CandidateQuery
  page: AnimalsPage | null
}

interface SeedLookup {
  attempt: number
  status: 'loading' | 'ready' | 'failed'
}

function animalLabel(animal: Animal) {
  return animal.nome ? `${animal.nome} (${animal.codigoInterno})` : animal.codigoInterno
}

function Comparison({ initialAnimalId }: { initialAnimalId: string | null }) {
  const location = useLocation()
  const initial = useRestoredListState<Pick<ListAnimalsParams, 'search'> & AnimalTaxonomyFilterValues>('/animais', {})
  const [query, setQuery] = useState<CandidateQuery>(() => ({
    filters: { especieId: initial.especieId, racaId: initial.racaId, variedadeId: initial.variedadeId, sexo: initial.sexo },
    search: initial.search ?? '',
    page: 1,
  }))
  const [searchDraft, setSearchDraft] = useState(query.search)
  const [selectedAnimals, setSelectedAnimals] = useState<Animal[]>([])
  const [loaded, setLoaded] = useState<CandidateResponse | null>(null)
  const [seedLookup, setSeedLookup] = useState<SeedLookup>({ attempt: 0, status: 'loading' })
  const seedEdited = useRef(false)
  const selectionSummary = useRef<HTMLParagraphElement>(null)
  const pageSummary = useRef<HTMLParagraphElement>(null)
  const seedId = initialAnimalId && /^[a-zA-Z0-9-]+$/.test(initialAnimalId) ? initialAnimalId : null
  const current = loaded?.query === query ? loaded : null
  const results = current?.page

  useEffect(() => {
    let active = true
    void listAnimals({
      ...query.filters,
      search: query.search || undefined,
      page: query.page,
      pageSize: PAGE_SIZE,
      sortBy: 'codigoInterno',
      sortDirection: 'asc',
    }).then(page => {
      if (!active) return
      const lastPage = Math.max(1, page.totalPages)
      if (query.page > lastPage) {
        setQuery({ ...query, page: lastPage })
        return
      }
      setLoaded({ query, page })
    }).catch(() => { if (active) setLoaded({ query, page: null }) })
    return () => { active = false }
  }, [query])

  useEffect(() => {
    if (!seedId) return
    let active = true
    void getAnimalById(seedId).then(animal => {
      if (!active) return
      if (!seedEdited.current) {
        setSelectedAnimals(previous => previous.some(item => item.id === animal.id) ? previous : [animal, ...previous])
      }
      setSeedLookup(previous => ({ ...previous, status: 'ready' }))
    }).catch(() => {
      if (active) setSeedLookup(previous => ({ ...previous, status: 'failed' }))
    })
    return () => { active = false }
  }, [seedId, seedLookup.attempt])

  const listState = listReturnState(location.state, '/animais')
  const detailPath = seedId ? `/animais/${seedId}` : undefined
  const back = detailPath
    ? { to: detailPath, label: 'Voltar para a ficha', state: readParentReturnState(location.state, detailPath) ?? listState }
    : { to: '/animais', label: 'Voltar para Animais', state: listState }

  function updateSelection(animal: Animal, checked: boolean) {
    if (seedId?.toLowerCase() === animal.id.toLowerCase()) seedEdited.current = true
    setSelectedAnimals(previous => {
      if (!checked) return previous.filter(item => item.id !== animal.id)
      return previous.some(item => item.id === animal.id) ? previous : [...previous, animal]
    })
  }

  function navigatePage(page: number) {
    pageSummary.current?.focus()
    setQuery({ ...query, page })
  }

  function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setQuery({ ...query, search: searchDraft.trim(), page: 1 })
  }

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8">
    <div className="mx-auto max-w-7xl">
      <PageHeader title="Comparar aves" back={back} />
      <section className={sectionClass} aria-labelledby="compare-selection-heading">
        <h2 className="text-lg font-bold text-slate-900" id="compare-selection-heading">Selecionar aves</h2>
        <p className="mt-2 rounded text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-600"
          ref={selectionSummary} tabIndex={-1} aria-live="polite">
          {selectedAnimals.length === 1 ? '1 ave selecionada' : `${selectedAnimals.length} aves selecionadas`}
          {selectedAnimals.length < 2 && '. Selecione pelo menos duas aves para comparar.'}
        </p>
        {seedId && seedLookup.status === 'loading' && <p className="mt-2 text-sm text-slate-700" role="status">Carregando ave da ficha…</p>}
        {initialAnimalId && (!seedId || seedLookup.status === 'failed') && <div className="mt-3 text-sm text-red-700" role="alert">
          <p>Não foi possível carregar a ave da ficha.</p>
          {seedId && <button className={`${secondaryButton} mt-2`} type="button"
            onClick={() => setSeedLookup(previous => ({ attempt: previous.attempt + 1, status: 'loading' }))}>Tentar carregar ave da ficha</button>}
        </div>}
        {selectedAnimals.length > 0 && <ul aria-label="Aves selecionadas" className="mt-3 flex flex-wrap gap-2">
          {selectedAnimals.map(animal => <li key={animal.id} className="flex max-w-full flex-wrap items-center gap-x-3 rounded-lg border border-emerald-200 bg-emerald-50 px-3 text-sm">
            <span className="min-w-0 break-words font-medium text-slate-900">{animalLabel(animal)}</span>
            <button aria-label={`Remover ${animalLabel(animal)}`} className="min-h-11 rounded px-2 font-semibold text-emerald-800 underline focus:outline-none focus:ring-2 focus:ring-emerald-600"
              type="button" onClick={() => { selectionSummary.current?.focus(); updateSelection(animal, false) }}>Remover</button>
          </li>)}
        </ul>}
        <form className="mt-4 grid items-end gap-3 sm:grid-cols-2 xl:grid-cols-4" onSubmit={search}>
          <div className="sm:col-span-2">
            <label className="text-sm font-medium text-slate-700" htmlFor="compare-animals-search">Buscar por código ou nome</label>
            <input className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" id="compare-animals-search"
              onChange={event => setSearchDraft(event.target.value)} type="search" value={searchDraft} />
          </div>
          <div className="sm:col-span-2"><button className={secondaryButton} type="submit">Buscar</button></div>
          <AnimalTaxonomyFilters idPrefix="compare-animals" value={query.filters}
            onChange={filters => setQuery({ ...query, filters, page: 1 })} />
        </form>
        <div className="mt-4">
          {current === null && <p className="text-sm text-slate-700" role="status">Carregando animais…</p>}
          {results === null && <div role="alert" className="text-sm text-red-700">
            <p>Não foi possível carregar os animais.</p>
            <button className={`${secondaryButton} mt-2`} type="button" onClick={() => navigatePage(query.page)}>Tentar novamente</button>
          </div>}
          {results && (results.items.length === 0
            ? <p className="text-sm text-slate-700">Nenhum animal encontrado.</p>
            : <ul aria-label="Animais encontrados" className="grid gap-2 sm:grid-cols-2">
              {results.items.map(animal => <li className="min-w-0" key={animal.id}>
                <label className="flex min-h-11 items-start gap-3 rounded-lg border border-slate-200 px-3 py-2 focus-within:ring-2 focus-within:ring-emerald-600">
                  <input className="mt-1 size-4 shrink-0 accent-emerald-700" aria-label={animalLabel(animal)} type="checkbox"
                    checked={selectedAnimals.some(item => item.id === animal.id)} onChange={event => updateSelection(animal, event.target.checked)} />
                  <span className="min-w-0 break-words">
                    <span className="block text-sm font-semibold text-slate-900">{animalLabel(animal)}</span>
                    <span className="block text-xs text-slate-600">
                      {[animal.especie.nomeComum, animal.raca?.nome, animal.variedade?.nome, animal.ativo ? null : 'Inativo'].filter(Boolean).join(' · ')}
                    </span>
                  </span>
                </label>
              </li>)}
            </ul>)}
        </div>
        <nav aria-label="Paginação de animais" className="mt-4 flex flex-wrap items-center justify-between gap-2 text-sm text-slate-700">
          <p className="rounded focus:outline-none focus:ring-2 focus:ring-emerald-600" ref={pageSummary} tabIndex={-1} aria-live="polite">
            {results && results.totalPages > 0 ? `Página ${results.page} de ${results.totalPages} · ${results.totalItems} animais` : `Página ${query.page}`}
          </p>
          <div className="flex flex-wrap gap-2">
            <button className={secondaryButton} type="button" disabled={!results || results.page <= 1}
              onClick={() => navigatePage(query.page - 1)}>Anterior</button>
            <button className={secondaryButton} type="button" disabled={!results || results.page >= results.totalPages}
              onClick={() => navigatePage(query.page + 1)}>Próxima</button>
          </div>
        </nav>
      </section>
      {selectedAnimals.length >= 2 && <div className="mt-6"><AnimalAnalyticsPanel animals={selectedAnimals} comparison /></div>}
    </div>
  </main>
}

export function CompareAnimalsPage() {
  const [params] = useSearchParams()
  const initialAnimalId = params.get('animalId')
  return <Comparison key={initialAnimalId} initialAnimalId={initialAnimalId} />
}
