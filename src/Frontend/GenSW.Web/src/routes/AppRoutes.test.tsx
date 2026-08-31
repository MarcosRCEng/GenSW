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
import {
  createPessoa,
  getPessoaById,
  listPessoas,
  setPessoaAtivo,
  updatePessoa,
} from '../features/people/services/peopleService'
import { TipoPessoa, type Pessoa } from '../features/people/types/people'
import { AppRoutes } from './AppRoutes'

vi.mock('../features/auth/services/authService', () => ({
  bootstrapSession: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
  subscribeToSessionInvalidation: vi.fn(),
}))

vi.mock('../features/people/services/peopleService', () => ({
  createPessoa: vi.fn(),
  getPessoaById: vi.fn(),
  listPessoas: vi.fn(),
  setPessoaAtivo: vi.fn(),
  updatePessoa: vi.fn(),
}))

const currentUser: CurrentUser = {
  userId: 'b7f14f7b-a8ff-499e-885f-a62c693de76c',
  pessoaId: '8a11a958-982d-49f4-8ba9-cbe5db840cd4',
  nome: 'Marina Silva',
  userName: 'marina',
  roles: [],
}

const activePerson: Pessoa = {
  id: 'person-1',
  tipoPessoa: TipoPessoa.Fisica,
  nome: 'Marina Silva',
  nomeFantasia: null,
  ativo: true,
  createdAtUtc: '2026-08-20T12:00:00Z',
  updatedAtUtc: '2026-08-20T12:00:00Z',
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
    vi.mocked(createPessoa).mockResolvedValue(activePerson)
    vi.mocked(getPessoaById).mockResolvedValue(activePerson)
    vi.mocked(setPessoaAtivo).mockResolvedValue(activePerson)
    vi.mocked(updatePessoa).mockResolvedValue(activePerson)
    vi.mocked(listPessoas).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 25,
      totalItems: 0,
      totalPages: 0,
    })
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

  it('protege a rota de pessoas para usuário anônimo', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(null)

    renderApplication('/pessoas')

    expect(await screen.findByRole('heading', { name: 'Acessar o sistema' })).toBeInTheDocument()
  })

  it('renderiza a rota de pessoas para usuário autenticado', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)

    renderApplication('/pessoas')

    expect(await screen.findByRole('heading', { name: 'Pessoas' })).toBeInTheDocument()
  })

  it('navega da home autenticada para pessoas', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)

    renderApplication('/')

    fireEvent.click(await screen.findByRole('link', { name: 'Pessoas' }))

    expect(await screen.findByRole('heading', { name: 'Pessoas' })).toBeInTheDocument()
  })

  it('protege a rota de criação para usuário anônimo', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(null)

    renderApplication('/pessoas/nova')

    expect(await screen.findByRole('heading', { name: 'Acessar o sistema' })).toBeInTheDocument()
  })

  it('renderiza a rota de criação para usuário autenticado', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)

    renderApplication('/pessoas/nova')

    expect(await screen.findByRole('heading', { name: 'Nova pessoa' })).toBeInTheDocument()
  })

  it('protege a rota de edição para usuário anônimo', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(null)

    renderApplication('/pessoas/person-1/editar')

    expect(await screen.findByRole('heading', { name: 'Acessar o sistema' })).toBeInTheDocument()
  })

  it('renderiza a rota de edição para usuário autenticado', async () => {
    vi.mocked(bootstrapSession).mockResolvedValue(currentUser)

    renderApplication('/pessoas/person-1/editar')

    expect(await screen.findByRole('heading', { name: 'Editar pessoa' })).toBeInTheDocument()
    expect(getPessoaById).toHaveBeenCalledWith('person-1')
  })
})
