import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { ModuleNavigation } from '../navigation/ModuleNavigation'

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
    <main className="mx-auto min-h-screen max-w-7xl px-4 py-8 sm:px-6 sm:py-12 lg:px-8">
      <header className="flex flex-wrap items-start justify-between gap-6">
        <div className="min-w-0">
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
          <h1 className="mt-3 text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">ERP agropecuário modular</h1>
        </div>
        <button
          className="min-h-11 rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 transition hover:border-slate-400 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
          disabled={isLoggingOut}
          onClick={handleLogout}
          type="button"
        >
          {isLoggingOut ? 'Saindo…' : 'Sair'}
        </button>
      </header>

      <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
        <p className="break-words text-xl font-semibold text-slate-900">Olá, {user.nome}</p>
        <p className="mt-2 break-words text-sm text-slate-600">Usuário: {user.userName}</p>
        <p className="mt-4 text-sm leading-6 text-slate-600">
          Escolha uma funcionalidade disponível para começar. Módulos planejados ainda não estão disponíveis.
        </p>
      </div>

      <ModuleNavigation />
    </main>
  )
}
