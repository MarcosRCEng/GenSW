import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { isHttpError, NetworkError } from '../../../shared/http/httpErrors'
import { useAuth } from '../hooks/useAuth'

const INVALID_CREDENTIALS_MESSAGE = 'Usuário ou senha inválidos.'
const RATE_LIMIT_MESSAGE = 'Muitas tentativas. Tente novamente mais tarde.'
const UNAVAILABLE_MESSAGE = 'Não foi possível acessar o GenSW. Verifique sua conexão e tente novamente.'

function getLoginErrorMessage(error: unknown): string {
  if (isHttpError(error)) {
    if (error.status === 401) {
      return INVALID_CREDENTIALS_MESSAGE
    }

    if (error.status === 429) {
      return RATE_LIMIT_MESSAGE
    }
  }

  if (error instanceof NetworkError) {
    return UNAVAILABLE_MESSAGE
  }

  return UNAVAILABLE_MESSAGE
}

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    if (isSubmitting) {
      return
    }

    setErrorMessage(null)
    setIsSubmitting(true)

    try {
      await login({ userName, password })
      navigate('/', { replace: true })
    } catch (error: unknown) {
      setErrorMessage(getLoginErrorMessage(error))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-md items-center px-6 py-16">
      <section className="w-full rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
        <h1 className="mt-3 text-3xl font-bold tracking-tight text-slate-900">Acessar o sistema</h1>
        <p className="mt-2 text-sm leading-6 text-slate-600">Entre com suas credenciais para continuar.</p>

        <form className="mt-8 space-y-5" onSubmit={handleSubmit}>
          <div>
            <label className="block text-sm font-medium text-slate-700" htmlFor="userName">
              Usuário
            </label>
            <input
              autoComplete="username"
              autoFocus
              className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-100 disabled:bg-slate-100"
              disabled={isSubmitting}
              id="userName"
              name="userName"
              onChange={(event) => setUserName(event.target.value)}
              required
              type="text"
              value={userName}
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700" htmlFor="password">
              Senha
            </label>
            <input
              autoComplete="current-password"
              className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-100 disabled:bg-slate-100"
              disabled={isSubmitting}
              id="password"
              name="password"
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </div>

          {errorMessage ? (
            <p className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
              {errorMessage}
            </p>
          ) : null}

          <button
            className="flex w-full justify-center rounded-lg bg-emerald-700 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
            disabled={isSubmitting}
            type="submit"
          >
            {isSubmitting ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
      </section>
    </main>
  )
}
