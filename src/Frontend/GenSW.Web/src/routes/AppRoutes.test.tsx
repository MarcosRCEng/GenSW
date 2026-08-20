import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import {
  bootstrapSession,
  login as loginSession,
  logout as logoutSession,
  subscribeToSessionInvalidation,
} from '../features/auth/services/authService'
import type { CurrentUser } from '../features/auth/types/auth'
import { AuthProvider } from '../features/auth/providers/AuthProvider'
import { AppRoutes } from './AppRoutes'

vi.mock('../features/auth/services/authService', () => ({
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

function renderApplication(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('AppRoutes', () => {
  beforeEach(() => {
    vi.mocked(loginSession).mockResolvedValue(currentUser)
    vi.mocked(logoutSession).mockResolvedValue()
    vi.mocked(subscribeToSessionInvalidation).mockReturnValue(vi.fn())
  })

  it('mostra loading e não renderiza a rota protegida durante o bootstrap', () => {
    vi.mocked(bootstrapSession).mockReturnValue(new Promise<CurrentUser | null>(() => undefined))

    renderApplication('/')

    expect(screen.getByRole('status')).toHaveTextContent('Carregando sessão…')
    expect(screen.queryByText('ERP agropecuário modular')).not.toBeInTheDocument()
  })

  it('redireciona uma sessão anônima da raiz para o login', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(null)

    renderApplication('/')

    expect(await screen.findByRole('heading', { name: 'Acessar o sistema' })).toBeInTheDocument()
  })

  it('redireciona um usuário autenticado do login para a área protegida', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)

    renderApplication('/login')

    expect(await screen.findByText('Olá, Marina Silva')).toBeInTheDocument()
    expect(screen.getByText('Usuário: marina')).toBeInTheDocument()
  })

  it('encerra a sessão local e navega para o login', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)

    renderApplication('/')

    fireEvent.click(await screen.findByRole('button', { name: 'Sair' }))

    await waitFor(() => expect(logoutSession).toHaveBeenCalledOnce())
    expect(await screen.findByRole('heading', { name: 'Acessar o sistema' })).toBeInTheDocument()
  })
})
