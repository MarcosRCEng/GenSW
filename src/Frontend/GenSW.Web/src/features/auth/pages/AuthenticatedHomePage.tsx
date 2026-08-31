import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export function AuthenticatedHomePage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [isLoggingOut, setIsLoggingOut] = useState(false)

  if (!user) {
    return null
  }

  const handleLogout = async () => {
    if (isLoggingOut) {
      return
    }

    setIsLoggingOut(true)

    try {
      await logout()
    } catch {
      // O estado local é limpo pelo provider mesmo sem confirmação da revogação remota.
    } finally {
      navigate('/login', { replace: true })
    }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-4xl items-center px-6 py-16">
      <section className="w-full">
        <div className="flex flex-wrap items-start justify-between gap-6">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
            <h1 className="mt-3 text-4xl font-bold tracking-tight text-slate-900">ERP agropecuário modular</h1>
          </div>
          <button
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 transition hover:border-slate-400 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
            disabled={isLoggingOut}
            onClick={handleLogout}
            type="button"
          >
            {isLoggingOut ? 'Saindo…' : 'Sair'}
          </button>
        </div>

        <div className="mt-10 rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
          <p className="text-2xl font-semibold text-slate-900">Olá, {user.nome}</p>
          <p className="mt-2 text-slate-600">Usuário: {user.userName}</p>
          <p className="mt-6 max-w-2xl text-base leading-7 text-slate-600">
            Fundação técnica para evoluir a gestão de produção animal, genética e demais domínios rurais.
          </p>
          <div className="mt-8 border-t border-slate-200 pt-6">
            <p className="text-sm font-semibold uppercase tracking-wide text-slate-500">Cadastros</p>
            <Link
              className="mt-3 inline-flex rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2"
              to="/pessoas"
            >
              Pessoas
            </Link>
          </div>
        </div>
      </section>
    </main>
  )
}
