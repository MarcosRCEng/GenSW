import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import {
  bootstrapSession,
  login as loginSession,
  logout as logoutSession,
  subscribeToSessionInvalidation,
} from '../services/authService'
import type { CurrentUser } from '../types/auth'
import { useAuth } from '../hooks/useAuth'
import { AuthProvider } from './AuthProvider'

vi.mock('../services/authService', () => ({
  bootstrapSession: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
  subscribeToSessionInvalidation: vi.fn(),
}))

const currentUser: CurrentUser = {
  userId: 'b7f14f7b-a8ff-499e-885f-a62c693de76c',
  pessoaId: '8a11a958-982d-49f4-8ba9-cbe5db840cd4',
  nome: 'Marina Silva',
  userName: 'marina',
  roles: [],
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((promiseResolve) => {
    resolve = promiseResolve
  })

  return { promise, resolve }
}

function AuthProbe() {
  const { bootstrap, isAuthenticated, isInitializing, login, logout, user } = useAuth()

  return (
    <div>
      <span data-testid="initializing">{String(isInitializing)}</span>
      <span data-testid="authenticated">{String(isAuthenticated)}</span>
      <span data-testid="user">{user?.userName ?? 'anonymous'}</span>
      <button onClick={() => void bootstrap()} type="button">
        Bootstrap
      </button>
      <button onClick={() => void login({ userName: 'marina', password: 'secret' })} type="button">
        Login
      </button>
      <button onClick={() => void logout().catch(() => undefined)} type="button">
        Logout
      </button>
    </div>
  )
}

describe('AuthProvider', () => {
  beforeEach(() => {
    vi.mocked(subscribeToSessionInvalidation).mockReturnValue(vi.fn())
    vi.mocked(loginSession).mockResolvedValue(currentUser)
    vi.mocked(logoutSession).mockResolvedValue()
  })

  it('restaura o usuário e mantém a inicialização ativa até o bootstrap terminar', async () => {
    const bootstrapResult = deferred<CurrentUser | null>()
    vi.mocked(bootstrapSession).mockReturnValue(bootstrapResult.promise)

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(screen.getByTestId('initializing')).toHaveTextContent('true')
    expect(screen.getByTestId('authenticated')).toHaveTextContent('false')

    await act(async () => {
      bootstrapResult.resolve(currentUser)
      await bootstrapResult.promise
    })

    await waitFor(() => expect(screen.getByTestId('initializing')).toHaveTextContent('false'))
    expect(screen.getByTestId('authenticated')).toHaveTextContent('true')
    expect(screen.getByTestId('user')).toHaveTextContent('marina')
  })

  it('finaliza como anônimo quando o bootstrap não encontra sessão', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(null)

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('initializing')).toHaveTextContent('false'))
    expect(screen.getByTestId('authenticated')).toHaveTextContent('false')
    expect(screen.getByTestId('user')).toHaveTextContent('anonymous')
  })

  it('atualiza o usuário no login e limpa o estado local no logout', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(null)

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('initializing')).toHaveTextContent('false'))
    fireEvent.click(screen.getByRole('button', { name: 'Login' }))

    await waitFor(() => expect(screen.getByTestId('authenticated')).toHaveTextContent('true'))
    expect(loginSession).toHaveBeenCalledWith({ userName: 'marina', password: 'secret' })

    fireEvent.click(screen.getByRole('button', { name: 'Logout' }))

    await waitFor(() => expect(screen.getByTestId('authenticated')).toHaveTextContent('false'))
    expect(logoutSession).toHaveBeenCalledOnce()
  })

  it('limpa o estado local mesmo se o logout remoto falhar', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)
    vi.mocked(logoutSession).mockRejectedValue(new Error('network unavailable'))

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('authenticated')).toHaveTextContent('true'))
    fireEvent.click(screen.getByRole('button', { name: 'Logout' }))

    await waitFor(() => expect(screen.getByTestId('authenticated')).toHaveTextContent('false'))
  })

  it('reage à invalidação central da sessão', async () => {
    let invalidateSession: () => void = () => undefined
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)
    vi.mocked(subscribeToSessionInvalidation).mockImplementation((listener) => {
      invalidateSession = listener
      return vi.fn()
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('authenticated')).toHaveTextContent('true'))

    act(() => invalidateSession())

    expect(screen.getByTestId('authenticated')).toHaveTextContent('false')
    expect(screen.getByTestId('user')).toHaveTextContent('anonymous')
  })

  it('compartilha a mesma operação quando o bootstrap já está em andamento', async () => {
    const bootstrapResult = deferred<CurrentUser | null>()
    vi.mocked(bootstrapSession).mockReturnValue(bootstrapResult.promise)

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Bootstrap' }))
    expect(bootstrapSession).toHaveBeenCalledOnce()

    await act(async () => {
      bootstrapResult.resolve(null)
      await bootstrapResult.promise
    })
  })
})
