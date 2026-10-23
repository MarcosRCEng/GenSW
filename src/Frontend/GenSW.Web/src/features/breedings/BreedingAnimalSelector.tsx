import { useEffect, useRef, useState } from 'react'
import { HttpError, SessionExpiredError } from '../../shared/http/httpErrors'
import { getAnimalById, listAnimals } from '../animals/services/animalsService'
import type { Animal, AnimalsPage } from '../animals/types/animals'

interface BreedingAnimalSelectorProps {
  id: string
  label: string
  sexo: 1 | 2
  value: string
  onChange: (id: string) => void
  emptyLabel?: string
  disabled?: boolean
  contextKey?: string
}

const PAGE_SIZE = 10
const controlClass = 'mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm disabled:opacity-50'
const buttonClass = 'rounded border px-2 py-1 text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50'
type CandidateLoad = { key: string; result: AnimalsPage } | { key: string; error: unknown }
type ReferenceLoad = { animal: Animal } | { error: unknown }

function errorMessage(message: string, error: unknown) {
  if (error instanceof HttpError) return `${message} HTTP ${error.status}.`
  if (error instanceof SessionExpiredError) return `${message} Sua sessão expirou; entre novamente.`
  return message
}

function animalLabel(animal: Animal, sexo: 1 | 2) {
  return `${animal.codigoInterno}${animal.nome ? ` — ${animal.nome}` : ''}${animal.ativo ? '' : ' (inativo)'}${animal.sexo === sexo ? '' : ' (sexo diferente do filtro)'}`
}

// A selection generation discards A → B → A responses while preserving the
// native select element, including keyboard focus when the choice changes.
function AnimalSelection({ id, label, sexo, value, onChange, emptyLabel = 'Selecione', disabled, candidates, lookupReady }: BreedingAnimalSelectorProps & { candidates?: Animal[]; lookupReady: boolean }) {
  const [selection, setSelection] = useState({ value, generation: 0 })
  const [loadedReference, setLoadedReference] = useState<{ generation: number; result: ReferenceLoad } | null>(null)
  if (selection.value !== value) setSelection({ value, generation: selection.generation + 1 })
  const reference = selection.value === value && loadedReference?.generation === selection.generation ? loadedReference.result : null
  const [retry, setRetry] = useState(0)
  const selectedCandidate = candidates?.find(item => item.id === value)
  const resolved = reference && 'animal' in reference ? reference.animal : selectedCandidate
  const failed = reference !== null && 'error' in reference
  const retryControl = useRef<HTMLSelectElement>(null)

  useEffect(() => {
    if (!value) return
    if (selectedCandidate) {
      setLoadedReference({ generation: selection.generation, result: { animal: selectedCandidate } })
      return
    }
    if (resolved || failed || !lookupReady) return
    let current = true
    void getAnimalById(value).then(animal => {
      if (current) setLoadedReference({ generation: selection.generation, result: { animal } })
    }).catch((error: unknown) => {
      if (current) setLoadedReference({ generation: selection.generation, result: { error } })
    })
    return () => { current = false }
  }, [value, selection.generation, selectedCandidate, resolved, failed, lookupReady, retry])

  return <>
    <label className="text-sm font-medium" htmlFor={id}>{label}</label>
    <select ref={retryControl} className={controlClass} id={id} value={value} disabled={disabled} onChange={event => onChange(event.target.value)} aria-describedby={failed ? `${id}-reference-error` : undefined}>
      <option value="">{emptyLabel}</option>
      {value && <option value={value}>{resolved ? `${animalLabel(resolved, sexo)} (selecionado)` : `Animal selecionado (ID: ${value})`}</option>}
      {candidates?.filter(item => item.id !== value && item.sexo === sexo).map(item => <option key={item.id} value={item.id}>{animalLabel(item, sexo)}</option>)}
    </select>
    {value && !resolved && !failed && <p className="mt-1 text-sm text-slate-600" aria-live="polite">Carregando identificação do animal selecionado…</p>}
    {failed && <div className="mt-2 text-sm text-red-700" role="alert" id={`${id}-reference-error`}>
      <p>{errorMessage('Não foi possível carregar o animal selecionado.', reference.error)}</p>
      <button className={`${buttonClass} mt-2`} type="button" disabled={disabled} aria-label={`Tentar carregar animal selecionado: ${label}`} onClick={() => {
        retryControl.current?.focus()
        setLoadedReference(null)
        setRetry(previous => previous + 1)
      }}>Tentar carregar animal selecionado</button>
    </div>}
  </>
}

function AnimalSelector(props: BreedingAnimalSelectorProps) {
  const { id, label, sexo, disabled } = props
  const [query, setQuery] = useState({ search: '', page: 1, revision: 0 })
  const [loaded, setLoaded] = useState<CandidateLoad | null>(null)
  const summary = useRef<HTMLParagraphElement>(null)
  const queryKey = JSON.stringify(query)
  const current = loaded?.key === queryKey ? loaded : null
  const result = current && 'result' in current ? current.result : undefined
  const failed = current !== null && 'error' in current
  const loading = current === null

  useEffect(() => {
    let active = true
    void listAnimals({ page: query.page, pageSize: PAGE_SIZE, sexo, search: query.search || undefined, sortBy: 'codigoInterno', sortDirection: 'asc' }).then(response => {
      if (!active) return
      const lastPage = Math.max(1, response.totalPages)
      if (query.page > lastPage) {
        setQuery(previous => ({ ...previous, page: lastPage, revision: previous.revision + 1 }))
        return
      }
      setLoaded({ key: queryKey, result: response })
    }).catch((error: unknown) => { if (active) setLoaded({ key: queryKey, error }) })
    return () => { active = false }
  }, [query.page, query.search, query.revision, queryKey, sexo])

  const navigate = (page: number) => {
    if (!result || page < 1 || page > result.totalPages) return
    // Keep a keyboard focus target when native disabled buttons lose focus.
    summary.current?.focus()
    setQuery(previous => ({ ...previous, page, revision: previous.revision + 1 }))
  }

  return <div className="min-w-0" aria-busy={loading}>
    <AnimalSelection {...props} candidates={result?.items} lookupReady={current !== null} />
    <div className="mt-3">
      <label className="text-sm text-slate-600" htmlFor={`${id}-search`}>Buscar {label.toLocaleLowerCase()} por código ou nome</label>
      <input className={controlClass} id={`${id}-search`} value={query.search} type="search" disabled={disabled} onChange={event => setQuery(previous => ({ search: event.target.value, page: 1, revision: previous.revision + 1 }))} onKeyDown={event => {
        // Searching inside the edit form must not activate its implicit submit.
        if (event.key === 'Enter' && !event.nativeEvent.isComposing) event.preventDefault()
      }} />
    </div>
    {loading ? <p className="mt-2 text-sm text-slate-600" role="status">Carregando animais…</p> : failed ? <div className="mt-2 text-sm text-red-700" role="alert">
      <p>{errorMessage('Não foi possível carregar os animais.', current.error)}</p>
      <button className={`${buttonClass} mt-2`} type="button" disabled={disabled} aria-label={`Tentar novamente: ${label}`} onClick={() => {
        summary.current?.focus()
        setQuery(previous => ({ ...previous, revision: previous.revision + 1 }))
      }}>Tentar novamente</button>
    </div> : result?.items.length === 0 ? <p className="mt-2 text-sm text-slate-600">Nenhum animal encontrado.</p> : null}
    <nav className="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-slate-600" aria-label={`Paginação de animais: ${label}`}>
      <p ref={summary} role="group" tabIndex={-1} aria-label={`Resumo dos animais: ${label}`} className="rounded focus:outline-none focus:ring-2 focus:ring-emerald-600" aria-live="polite" aria-atomic="true">
        {result ? <>{result.totalPages > 0 ? <span>Página {result.page} de {result.totalPages}</span> : null}<span className="ml-2">{result.totalItems} animais</span></> : loading ? `Carregando página ${query.page}…` : `Página ${query.page} não carregada.`}
      </p>
      <div className="flex gap-2">
        <button className={buttonClass} type="button" aria-label={`Animais anteriores: ${label}`} disabled={disabled || !result || result.page <= 1} onClick={() => { if (result) navigate(result.page - 1) }}>Anterior</button>
        <button className={buttonClass} type="button" aria-label={`Próximos animais: ${label}`} disabled={disabled || !result || result.page >= result.totalPages} onClick={() => { if (result) navigate(result.page + 1) }}>Próxima</button>
      </div>
    </nav>
  </div>
}

export function BreedingAnimalSelector(props: BreedingAnimalSelectorProps) {
  // Route/context and sex changes invalidate both candidate and reference requests.
  return <AnimalSelector key={JSON.stringify([props.contextKey, props.sexo])} {...props} />
}
