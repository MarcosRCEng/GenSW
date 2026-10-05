import { useEffect, useState } from 'react'
import { httpRequest } from '../../shared/http/httpClient'
import { errorMessage } from './financial'

interface Reference {
  id: string
  nome: string | null
  codigoInterno?: string
}
export function ReferencePicker({
  kind,
  value,
  onChange,
}: {
  kind: 'pessoas' | 'animais'
  value: string | null
  onChange: (value: string | null) => void
}) {
  const [search, setSearch] = useState(''),
    [items, setItems] = useState<Reference[]>([]),
    [selected, setSelected] = useState<Reference | null>(null),
    [error, setError] = useState(''),
    [loading, setLoading] = useState(false)
  const label = kind === 'pessoas' ? 'Pessoa' : 'Animal'
  useEffect(() => {
    if (!value) {
      setSelected(null)
      return
    }
    const abort = new AbortController()
    void httpRequest<Reference>(`/${kind}/${value}`, {
      authenticated: true,
      signal: abort.signal,
    })
      .then(setSelected)
      .catch((e) => {
        if (!abort.signal.aborted) setError(errorMessage(e))
      })
    return () => abort.abort()
  }, [kind, value])
  useEffect(() => {
    const abort = new AbortController()
    const timer = window.setTimeout(() => {
      setLoading(true)
      setError('')
      void httpRequest<{ items: Reference[] }>(
        `/${kind}?ativo=true&pageSize=25&search=${encodeURIComponent(search)}`,
        { authenticated: true, signal: abort.signal },
      )
        .then((x) => setItems(x.items))
        .catch((e) => {
          if (!abort.signal.aborted) setError(errorMessage(e))
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false)
        })
    }, 250)
    return () => {
      window.clearTimeout(timer)
      abort.abort()
    }
  }, [kind, search])
  const text = (x: Reference) =>
    `${x.codigoInterno ? `${x.codigoInterno} · ` : ''}${x.nome ?? 'Sem nome'}`
  return (
    <fieldset className="finance-reference">
      <legend>{label} (opcional)</legend>
      <label>
        Pesquisar {label.toLowerCase()}
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </label>
      <label>
        Selecionar {label.toLowerCase()}
        <select
          value={value ?? ''}
          onChange={(e) => onChange(e.target.value || null)}
        >
          <option value="">Sem vínculo</option>
          {selected && !items.some((x) => x.id === selected.id) && (
            <option value={selected.id}>
              {text(selected)} (vínculo existente)
            </option>
          )}
          {items.map((x) => (
            <option key={x.id} value={x.id}>
              {text(x)}
            </option>
          ))}
        </select>
      </label>
      <p aria-live="polite">
        {loading
          ? 'Pesquisando…'
          : `${items.length} opções; refine a pesquisa para localizar outras.`}
      </p>
      {error && <p role="alert">{error}</p>}
    </fieldset>
  )
}
