import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { message, page } from './api'
import type { Page } from './types'

export const control =
  'mt-1 w-full min-w-0 rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600 disabled:bg-slate-100'
export const button =
  'inline-flex items-center rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-emerald-600 disabled:opacity-50'
export const primary = `${button} border-emerald-700 bg-emerald-700 text-white`
export function Frame({
  title,
  children,
}: {
  title: string
  children: ReactNode
}) {
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    heading.current?.focus()
  }, [title])
  return (
    <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6">
      <div className="mx-auto max-w-6xl">
        <header className="mb-6">
          <p className="text-sm font-semibold text-emerald-700">GenSW</p>
          <h1
            ref={heading}
            tabIndex={-1}
            className="my-2 rounded text-3xl font-bold focus:ring-2 focus:ring-emerald-600"
          >
            {title}
          </h1>
          <nav
            aria-label="Catálogo e formulação"
            className="flex flex-wrap gap-3"
          >
            <Link className={button} to="/">
              Início
            </Link>
            <Link className={button} to="/itens">
              Insumos e produtos
            </Link>
            <Link className={button} to="/producao/receitas">
              Receitas
            </Link>
            <Link className={button} to="/producao/formulacao">
              Formulação e comparação
            </Link>
          </nav>
        </header>
        <div className="space-y-6">{children}</div>
      </div>
    </main>
  )
}
export function Section({
  title,
  children,
}: {
  title?: string
  children: ReactNode
}) {
  return (
    <section className="min-w-0 space-y-4 rounded-2xl border bg-white p-4 shadow-sm sm:p-6">
      {title && <h2 className="text-xl font-semibold">{title}</h2>}
      {children}
    </section>
  )
}
export function Field({
  label,
  value,
  onChange,
  required = false,
  multiline = false,
  hint,
  disabled = false,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  required?: boolean
  multiline?: boolean
  hint?: string
  disabled?: boolean
}) {
  const id = useId()
  return (
    <div className="min-w-0">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      {multiline ? (
        <textarea
          id={id}
          className={control}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          required={required}
          disabled={disabled}
          rows={3}
          maxLength={2000}
        />
      ) : (
        <input
          id={id}
          className={control}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          required={required}
          disabled={disabled}
          maxLength={1000}
        />
      )}
      {hint && <p className="mt-1 text-sm text-slate-600">{hint}</p>}
    </div>
  )
}
export function Choice({
  label,
  value,
  onChange,
  options,
  captions = {},
  disabled = false,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: readonly string[]
  captions?: Record<string, string>
  disabled?: boolean
}) {
  const id = useId()
  return (
    <div className="min-w-0">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      <select
        id={id}
        className={control}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
      >
        {options.map((o) => (
          <option key={o} value={o}>
            {captions[o] ?? optionCaption[o] ?? o}
          </option>
        ))}
      </select>
    </div>
  )
}
const optionCaption: Record<string, string> = {
  '': 'Todos / não selecionado',
  true: 'Ativo',
  false: 'Inativo',
  OutroMaterialIncorporado: 'Outro material incorporado',
  Consumivel: 'Consumível',
  entrada: 'Entrada',
  producao: 'Produção',
  interno: 'Uso interno',
  venda: 'Venda',
  nome: 'Nome',
  codigo: 'Código',
  createdAtUtc: 'Data de criação',
  asc: 'Crescente',
  desc: 'Decrescente',
  MisturaSimples: 'Mistura simples',
  Processamento: 'Processamento',
  Variavel: 'Variável',
  Fixa: 'Fixa',
  InformadaUsuario: 'Informada pelo usuário',
  ReferenciaTecnica: 'Referência técnica',
  OutroMaterial: 'Outro material',
}
export function Check({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (value: boolean) => void
}) {
  return (
    <label className="flex items-center gap-2 text-sm">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="h-4 w-4 accent-emerald-700"
      />
      {label}
    </label>
  )
}
export function ErrorNotice({ error }: { error: string | null }) {
  return error ? (
    <p role="alert" className="rounded-lg bg-red-50 p-3 text-red-800">
      {error}
    </p>
  ) : null
}
export function Pager({
  current,
  total,
  onChange,
}: {
  current: number
  total: number
  onChange: (value: number) => void
}) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <button
        className={button}
        disabled={current <= 1}
        onClick={() => onChange(current - 1)}
        type="button"
      >
        Anterior
      </button>
      <span>
        Página {current} de {Math.max(1, total)}
      </span>
      <button
        className={button}
        disabled={current >= total}
        onClick={() => onChange(current + 1)}
        type="button"
      >
        Próxima
      </button>
    </div>
  )
}

// Each selector has complete server pagination. Historical selected IDs are displayed even outside the current page.
export function ServerSelect<T extends { id: string }>({
  label,
  path,
  value,
  onChange,
  caption,
  params = {},
}: {
  label: string
  path: string
  value: string | null
  onChange: (value: string | null, item?: T) => void
  caption: (item: T) => string
  params?: Record<string, string | number | boolean | undefined>
}) {
  const [search, setSearch] = useState('')
  const [current, setCurrent] = useState(1)
  const [result, setResult] = useState<Page<T> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selectedLabel, setSelectedLabel] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [retry, setRetry] = useState(0)
  const encodedParams = JSON.stringify(params)
  const id = useId()
  useEffect(() => {
    let live = true
    setLoading(true)
    setError(null)
    const timer = setTimeout(() => {
      void page<T>(path, {
        ...(JSON.parse(encodedParams) as Record<
          string,
          string | number | boolean
        >),
        search,
        page: current,
        pageSize: 10,
      })
        .then((r) => {
          if (live) {
            setResult(r)
            setLoading(false)
          }
        })
        .catch((e) => {
          if (live) {
            setError(message(e))
            setLoading(false)
          }
        })
    }, 200)
    return () => {
      live = false
      clearTimeout(timer)
    }
  }, [path, encodedParams, search, current, retry])
  const contains = result?.items.some((x) => x.id === value)
  return (
    <fieldset className="min-w-0 space-y-2 rounded-lg border p-3">
      <legend className="px-1 text-sm font-medium">{label}</legend>
      <label className="sr-only" htmlFor={`${id}-search`}>
        Buscar {label}
      </label>
      <input
        id={`${id}-search`}
        className={control}
        placeholder="Buscar no servidor"
        value={search}
        onChange={(e) => {
          setSearch(e.target.value)
          setCurrent(1)
        }}
      />
      <label className="sr-only" htmlFor={id}>
        Selecionar {label}
      </label>
      <select
        id={id}
        className={control}
        value={value ?? ''}
        onChange={(e) => {
          const item = result?.items.find((x) => x.id === e.target.value)
          setSelectedLabel(item ? caption(item) : null)
          onChange(e.target.value || null, item)
        }}
      >
        <option value="">Não selecionado</option>
        {value && !contains && (
          <option value={value}>
            {selectedLabel ?? `Referência selecionada: ${value}`}
          </option>
        )}
        {result?.items.map((item) => (
          <option key={item.id} value={item.id}>
            {caption(item)}
          </option>
        ))}
      </select>
      {loading && <p role="status">Buscando…</p>}
      <ErrorNotice error={error} />
      {error && (
        <button
          type="button"
          className={button}
          onClick={() => setRetry((v) => v + 1)}
        >
          Tentar novamente
        </button>
      )}
      {result && (
        <Pager
          current={current}
          total={result.totalPages}
          onChange={setCurrent}
        />
      )}
    </fieldset>
  )
}
