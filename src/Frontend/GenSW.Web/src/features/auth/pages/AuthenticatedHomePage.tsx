import { useAuth } from '../hooks/useAuth'
import { ModuleNavigation } from '../navigation/ModuleNavigation'
import { PageHeader } from '../../../shared/layout/PageHeader'

export function AuthenticatedHomePage() {
  const { user } = useAuth()

  if (!user) {
    return null
  }

  return (
    <main className="mx-auto min-h-screen max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <PageHeader title="ERP agropecuário modular" description="Escolha um módulo para continuar o trabalho." />

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
