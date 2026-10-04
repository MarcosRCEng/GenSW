import { useEffect, useRef, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { listReturnState } from './listNavigation'
import type { DetailLoadState } from './useRecordDetails'

interface DetailsPageProps {
  title: string
  listPath: string
  listLabel: string
  state: DetailLoadState
  onRetry: () => void
  editPath?: string
  children?: ReactNode
}

const linkClassName = 'inline-flex rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2'

export function DetailsPage({ title, listPath, listLabel, state, onRetry, editPath, children }: DetailsPageProps) {
  const location = useLocation()
  const heading = useRef<HTMLHeadingElement>(null)
  const returnState = listReturnState(location.state, listPath)
  useEffect(() => { heading.current?.focus() }, [location.pathname])

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8">
    <div className="mx-auto max-w-5xl">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
          <h1 className="mt-2 rounded text-3xl font-bold tracking-tight text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600" ref={heading} tabIndex={-1}>{title}</h1>
          <p className="mt-2 text-slate-600">Consulta dos dados do registro</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <Link className={linkClassName} to={returnState?.list.path ?? listPath} state={returnState}>Voltar para {listLabel}</Link>
          {state === 'ready' && editPath ? <Link className={linkClassName} to={editPath} state={returnState}>Editar</Link> : null}
        </div>
      </header>
      {state === 'loading' ? <p className="mt-8 rounded-2xl border border-slate-200 bg-white p-8 text-slate-600" role="status">Carregando detalhes…</p> : null}
      {state === 'not-found' || state === 'forbidden' || state === 'error' ? <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" role="alert">
        <p className="font-medium text-red-700">{state === 'not-found' ? 'Registro não encontrado.' : state === 'forbidden' ? 'Você não tem permissão para visualizar este registro.' : 'Não foi possível carregar os detalhes.'}</p>
        {state !== 'not-found' ? <button className={`${linkClassName} mt-4`} onClick={onRetry} type="button">Tentar novamente</button> : null}
      </section> : null}
      {state === 'ready' ? <div className="mt-8 space-y-6">{children}</div> : null}
    </div>
  </main>
}

export function DetailField({ label, children }: { label: string; children: ReactNode }) {
  return <div className="min-w-0"><dt className="text-sm font-medium text-slate-600">{label}</dt><dd className="mt-1 whitespace-pre-wrap break-words text-slate-900">{children === null || children === undefined || children === '' ? 'Não informado' : children}</dd></div>
}

export function DetailStatus({ active }: { active: boolean }) {
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${active ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'}`}>{active ? 'Ativo' : 'Inativo'}</span>
}
