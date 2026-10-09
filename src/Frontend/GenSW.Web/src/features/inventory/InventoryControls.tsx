import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError } from '../../shared/http/httpErrors'
import { inventoryError, queryPath } from './inventoryService'

export const inputClass = 'mt-1 w-full min-w-0 rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600 disabled:bg-slate-100'
const buttonBase = 'inline-flex items-center justify-center rounded-lg border px-4 py-2 text-sm font-semibold focus:outline-none focus:ring-2 disabled:opacity-50'
export const buttonClass = `${buttonBase} border-slate-300 bg-white text-slate-800 hover:bg-slate-100 focus:ring-emerald-600`
export const primaryClass = `${buttonBase} border-emerald-700 bg-emerald-700 text-white hover:bg-emerald-800 focus:ring-emerald-600`

export function InventoryFrame({ title, children }: { title: string; children: ReactNode }) {
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => { heading.current?.focus() }, [title])
  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6"><div className="mx-auto max-w-6xl space-y-6">
    <header className="space-y-3"><p className="font-semibold text-emerald-700">GenSW · Estoque</p><h1 className="rounded text-3xl font-bold focus:ring-2 focus:ring-emerald-600" ref={heading} tabIndex={-1}>{title}</h1>
      <nav className="flex flex-wrap gap-2" aria-label="Estoque"><Link className={buttonClass} to="/">Início</Link>{[['Saldos', 'saldos'], ['Locais', 'locais'], ['Lotes', 'lotes'], ['Movimentos', 'movimentos'], ['Reconciliação', 'reconciliacao']].map(([label, path]) => <Link key={path} className={buttonClass} to={`/estoque/${path}`}>{label}</Link>)}</nav>
    </header>{children}
  </div></main>
}

export function Panel({ title, children }: { title?: string; children: ReactNode }) {
  return <section className="min-w-0 space-y-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">{title && <h2 className="text-xl font-semibold">{title}</h2>}{children}</section>
}

export function Input({ label, value, onChange, type = 'text', required = false, hint, disabled = false, maxLength = 2000, multiline = false }: { label: string; value: string; onChange: (v: string) => void; type?: string; required?: boolean; hint?: string; disabled?: boolean; maxLength?: number; multiline?: boolean }) {
  const id = useId()
  return <div className="min-w-0"><label className="text-sm font-medium" htmlFor={id}>{label}</label>{multiline ? <textarea id={id} className={inputClass} value={value} onChange={e => onChange(e.target.value)} required={required} disabled={disabled} maxLength={maxLength} rows={3} /> : <input id={id} className={inputClass} type={type} value={value} onChange={e => onChange(e.target.value)} required={required} disabled={disabled} maxLength={maxLength} />}{hint && <p className="mt-1 text-sm text-slate-600">{hint}</p>}</div>
}

export function Select({ label, value, onChange, options, disabled = false }: { label: string; value: string; onChange: (v: string) => void; options: readonly (readonly [string, string])[]; disabled?: boolean }) {
  const id = useId()
  return <div className="min-w-0"><label className="text-sm font-medium" htmlFor={id}>{label}</label><select className={inputClass} id={id} value={value} onChange={e => onChange(e.target.value)} disabled={disabled}>{options.map(([key, name]) => <option key={key} value={key}>{name}</option>)}</select></div>
}

export function Notice({ error, retry }: { error: string | null; retry?: () => void }) {
  const focus = useRef<HTMLDivElement>(null)
  useEffect(() => { if (error) focus.current?.focus() }, [error])
  return error ? <div role="alert" ref={focus} tabIndex={-1} className="rounded-lg border border-red-200 bg-red-50 p-3 text-red-800 focus:ring-2 focus:ring-red-600"><p>{error}</p>{retry && <button className={`${buttonClass} mt-3`} type="button" onClick={retry}>Tentar novamente</button>}</div> : null
}

export function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (p: number) => void }) {
  return <nav className="flex flex-wrap items-center gap-3" aria-label="Paginação"><button className={buttonClass} type="button" disabled={page <= 1} onClick={() => onChange(page - 1)}>Anterior</button><span>Página {page} de {Math.max(1, totalPages)}</span><button className={buttonClass} type="button" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>Próxima</button></nav>
}

export function ScrollTable({ children, label }: { children: ReactNode; label: string }) {
  return <div role="region" aria-label={label} tabIndex={0} className="max-w-full overflow-x-auto rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-600"><table className="min-w-full text-left text-sm [&_th]:whitespace-nowrap [&_th]:border-b [&_th]:p-3 [&_td]:border-b [&_td]:p-3">{children}</table></div>
}

// Resolve selected IDs separately: pagination never hides a historical reference.
export function ReferenceSelect<T extends { id: string }>({ label, path, detailPath, value, onChange, caption, params = {}, parse, searchPlaceholder = 'Buscar no servidor' }: { label: string; path: string; detailPath?: (id: string) => string; value: string | null; onChange: (id: string | null, item?: T) => void; caption: (item: T) => string; params?: Record<string, string | number | boolean | undefined>; parse: (v: unknown) => T; searchPlaceholder?: string }) {
  const [search, setSearch] = useState('')
  const [current, setCurrent] = useState(1)
  const [items, setItems] = useState<T[]>([])
  const [pages, setPages] = useState(0)
  const [selected, setSelected] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [retry, setRetry] = useState(0)
  const encoded = JSON.stringify(params)
  const id = useId()
  useEffect(() => { setCurrent(1); setSearch('') }, [path, encoded])
  useEffect(() => {
    let live = true
    setLoading(true); setError(null)
    const timer = setTimeout(() => {
      void httpRequest<unknown>(queryPath(path, { ...JSON.parse(encoded), search, page: current, pageSize: 10 }), { authenticated: true }).then(raw => {
        if (!raw || typeof raw !== 'object' || !('items' in raw) || !Array.isArray(raw.items) || !('totalPages' in raw) || typeof raw.totalPages !== 'number' || !Number.isSafeInteger(raw.totalPages) || raw.totalPages < 0) throw new InvalidApiResponseError()
        const data = raw.items.map(parse)
        if (live) { setItems(data); setPages(raw.totalPages as number); setLoading(false) }
      }).catch(e => { if (live) { setError(inventoryError(e)); setLoading(false) } })
    }, 200)
    return () => { live = false; clearTimeout(timer) }
  }, [path, encoded, search, current, parse, retry])
  const loaded = items.find(x => x.id === value)
  const resolved = loaded ?? (selected?.id === value ? selected : null)
  // Depend on resolved path, not an inline callback, to avoid duplicate requests.
  const selectedPath = value ? detailPath?.(value) ?? `${path}/${value}` : null
  useEffect(() => {
    let live = true
    if (!selectedPath || !value || loaded) return
    void httpRequest<unknown>(selectedPath, { authenticated: true }).then(parse).then(item => { if (live) setSelected(item) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [selectedPath, value, loaded, parse, retry])
  return <fieldset className="min-w-0 space-y-2 rounded-lg border border-slate-200 p-3"><legend className="px-1 font-medium">{label}</legend><label className="sr-only" htmlFor={`${id}-search`}>Buscar {label}</label><input className={inputClass} id={`${id}-search`} value={search} placeholder={searchPlaceholder} onChange={e => { setSearch(e.target.value); setCurrent(1) }} /><label className="sr-only" htmlFor={id}>Selecionar {label}</label><select id={id} className={inputClass} value={value ?? ''} onChange={e => { const item = items.find(x => x.id === e.target.value); if (item) setSelected(item); onChange(e.target.value || null, item) }}><option value="">Não selecionado</option>{value && !loaded && <option value={value}>{resolved ? caption(resolved) : 'Carregando referência selecionada…'}</option>}{items.map(item => <option key={item.id} value={item.id}>{caption(item)}</option>)}</select>{loading && <p role="status">Buscando…</p>}<Notice error={error} retry={() => setRetry(x => x + 1)} /><Pagination page={current} totalPages={pages} onChange={setCurrent} /></fieldset>
}
