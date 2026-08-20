import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { HttpError, NetworkError } from '../../../shared/http/httpErrors'
import {
  bootstrapSession,
  login as loginSession,
  logout as logoutSession,
  subscribeToSessionInvalidation,
} from '../services/authService'
import type { CurrentUser } from '../types/auth'
import { AuthProvider } from '../providers/AuthProvider'
import { AppRoutes } from '../../../routes/AppRoutes'

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

async function renderLogin() {
  render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </MemoryRouter>,
  )

  await screen.findByRole('heading', { name: 'Acessar o sistema' })
}

async function submitCredentials() {
  fireEvent.change(screen.getByLabelText('Usuário'), { target: { value: 'marina' } })
  fireEvent.change(screen.getByLabelText('Senha'), { target: { value: 'secret' } })
  fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))

  await waitFor(() => expect(loginSession).toHaveBeenCalledWith({ userName: 'marina', password: 'secret' }))
}

describe('LoginPage', () => {
  beforeEach(() => {
    vi.mocked(bootstrapSession).mockResolvedValue(null)
    vi.mocked(logoutSession).mockResolvedValue()
    vi.mocked(subscribeToSessionInvalidation).mockReturnValue(vi.fn())
  })

  it('autentica e navega para a área protegida', async () => {
    vi.mocked(loginSession).mockResolvedValue(currentUser)

    await renderLogin()
    await submitCredentials()

    expect(await screen.findByText('Olá, Marina Silva')).toBeInTheDocument()
  })

  it('mostra mensagem genérica para credenciais rejeitadas', async () => {
    vi.mocked(loginSession).mockRejectedValue(new HttpError(401))

    await renderLogin()
    await submitCredentials()

    expect(await screen.findByRole('alert')).toHaveTextContent('Usuário ou senha inválidos.')
  })

  it('distingue o limite de tentativas', async () => {
    vi.mocked(loginSession).mockRejectedValue(new HttpError(429))

    await renderLogin()
    await submitCredentials()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Muitas tentativas. Tente novamente mais tarde.',
    )
  })

  it('mostra indisponibilidade sem expor detalhes de falha de rede', async () => {
    vi.mocked(loginSession).mockRejectedValue(new NetworkError(new Error('connection refused')))

    await renderLogin()
    await submitCredentials()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível acessar o GenSW. Verifique sua conexão e tente novamente.',
    )
    expect(screen.queryByText('connection refused')).not.toBeInTheDocument()
  })
})
