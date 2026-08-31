import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listPessoas, setPessoaAtivo } from '../services/peopleService'
import { TipoPessoa, type ListPessoasParams, type PessoasPage } from '../types/people'
import { PeopleListPage } from './PeopleListPage'

vi.mock('../services/peopleService', () => ({
  listPessoas: vi.fn(),
  setPessoaAtivo: vi.fn(),
}))

const peoplePage: PessoasPage = {
  items: [
    {
      id: 'person-1',
      tipoPessoa: TipoPessoa.Fisica,
      nome: 'Marina Silva',
      nomeFantasia: null,
      ativo: true,
      createdAtUtc: '2026-08-20T12:00:00Z',
      updatedAtUtc: '2026-08-20T12:00:00Z',
    },
    {
      id: 'person-2',
      tipoPessoa: TipoPessoa.Juridica,
      nome: 'Silva & Filhos Ltda.',
      nomeFantasia: 'Silva & Filhos',
      ativo: false,
      createdAtUtc: '2026-08-21T12:00:00Z',
      updatedAtUtc: '2026-08-21T12:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  totalItems: 2,
  totalPages: 1,
}

function renderPage() {
  return render(
    <MemoryRouter>
      <PeopleListPage />
    </MemoryRouter>,
  )
}

async function waitForInitialLoad() {
  await screen.findByText('Marina Silva')
}

beforeEach(() => {
  vi.mocked(listPessoas).mockReset()
  vi.mocked(setPessoaAtivo).mockReset()
  vi.mocked(listPessoas).mockResolvedValue(peoplePage)
  vi.mocked(setPessoaAtivo).mockResolvedValue(peoplePage.items[0])
})

describe('PeopleListPage', () => {
  it('carrega os defaults e representa pessoas físicas e jurídicas', async () => {
    renderPage()

    await waitForInitialLoad()

    expect(listPessoas).toHaveBeenCalledWith({
      page: 1,
      pageSize: 25,
      sortBy: 'nome',
      sortDirection: 'asc',
    })
    expect(screen.getByText('Silva & Filhos Ltda.')).toBeInTheDocument()
    const table = screen.getByRole('table')
    expect(within(table).getByText('Pessoa física')).toBeInTheDocument()
    expect(within(table).getByText('Pessoa jurídica')).toBeInTheDocument()
    expect(within(table).getByText('Ativo')).toBeInTheDocument()
    expect(within(table).getByText('Inativo')).toBeInTheDocument()
    expect(within(table).getByText('—')).toBeInTheDocument()
    expect(within(table).getByText('Silva & Filhos')).toBeInTheDocument()
    expect(within(table).getByText('20/08/2026')).toBeInTheDocument()
    expect(within(table).getAllByRole('columnheader')).toHaveLength(6)
  })

  it('aplica busca explícita e volta para a primeira página', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.change(screen.getByLabelText('Buscar por nome ou nome fantasia'), {
      target: { value: 'Silva & Filhos' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))

    await waitFor(() =>
      expect(listPessoas).toHaveBeenLastCalledWith({
        page: 1,
        pageSize: 25,
        search: 'Silva & Filhos',
        sortBy: 'nome',
        sortDirection: 'asc',
      }),
    )
  })

  it('aplica tipo pessoa jurídica e preserva ativo=false', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.change(screen.getByLabelText('Tipo de pessoa'), {
      target: { value: String(TipoPessoa.Juridica) },
    })
    await waitFor(() => expect(screen.getByLabelText('Status')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'false' } })

    await waitFor(() =>
      expect(listPessoas).toHaveBeenLastCalledWith({
        page: 1,
        pageSize: 25,
        tipoPessoa: TipoPessoa.Juridica,
        ativo: false,
        sortBy: 'nome',
        sortDirection: 'asc',
      }),
    )
  })

  it('limpa pesquisa e filtros e restaura a ordenação padrão', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.change(screen.getByLabelText('Buscar por nome ou nome fantasia'), {
      target: { value: 'Marina' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Ordenar por'), { target: { value: 'createdAtUtc' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'desc' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))

    await waitFor(() =>
      expect(listPessoas).toHaveBeenLastCalledWith({
        page: 1,
        pageSize: 25,
        sortBy: 'nome',
        sortDirection: 'asc',
      }),
    )
    expect(screen.getByLabelText('Buscar por nome ou nome fantasia')).toHaveValue('')
  })

  it('aplica ordenação por cadastro decrescente', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.change(screen.getByLabelText('Ordenar por'), { target: { value: 'createdAtUtc' } })
    await waitFor(() => expect(screen.getByLabelText('Direção')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'desc' } })

    await waitFor(() =>
      expect(listPessoas).toHaveBeenLastCalledWith({
        page: 1,
        pageSize: 25,
        sortBy: 'createdAtUtc',
        sortDirection: 'desc',
      }),
    )
  })

  it('altera page size e reinicia na página um', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.change(screen.getByLabelText('Registros por página'), { target: { value: '50' } })

    await waitFor(() =>
      expect(listPessoas).toHaveBeenLastCalledWith({
        page: 1,
        pageSize: 50,
        sortBy: 'nome',
        sortDirection: 'asc',
      }),
    )
  })

  it('navega para próxima e anterior preservando os parâmetros', async () => {
    vi.mocked(listPessoas).mockImplementation(async (params?: ListPessoasParams) => ({
      ...peoplePage,
      page: params?.page ?? 1,
      totalItems: 75,
      totalPages: 3,
    }))
    renderPage()
    await waitForInitialLoad()

    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(screen.getByText('Página 2 de 3')).toBeInTheDocument())
    expect(listPessoas).toHaveBeenLastCalledWith({
      page: 2,
      pageSize: 25,
      sortBy: 'nome',
      sortDirection: 'asc',
    })

    fireEvent.click(screen.getByRole('button', { name: 'Anterior' }))
    await waitFor(() => expect(screen.getByText('Página 1 de 3')).toBeInTheDocument())
    expect(listPessoas).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 25,
      sortBy: 'nome',
      sortDirection: 'asc',
    })
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
  })

  it('mostra estado vazio sem página um de zero', async () => {
    vi.mocked(listPessoas).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 25,
      totalItems: 0,
      totalPages: 0,
    })
    renderPage()

    expect(await screen.findByText('Nenhuma pessoa encontrada.')).toBeInTheDocument()
    expect(screen.getByText('0 registros')).toBeInTheDocument()
    expect(screen.queryByText('Página 1 de 0')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('mostra erro genérico e permite tentar novamente', async () => {
    vi.mocked(listPessoas).mockRejectedValueOnce(new Error('internal details'))
    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível carregar as pessoas.',
    )
    expect(screen.queryByText('internal details')).not.toBeInTheDocument()

    vi.mocked(listPessoas).mockResolvedValueOnce(peoplePage)
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))

    expect(await screen.findByText('Marina Silva')).toBeInTheDocument()
    expect(listPessoas).toHaveBeenCalledTimes(2)
  })

  it('mostra loading enquanto a primeira consulta está pendente', async () => {
    let resolve!: (value: PessoasPage) => void
    vi.mocked(listPessoas).mockReturnValue(
      new Promise<PessoasPage>((promiseResolve) => {
        resolve = promiseResolve
      }),
    )
    renderPage()

    expect(screen.getByRole('status')).toHaveTextContent('Carregando pessoas…')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()

    resolve(peoplePage)

    expect(await screen.findByRole('table')).toBeInTheDocument()
  })

  it('oferece ação para cadastrar uma nova pessoa', async () => {
    renderPage()
    await waitForInitialLoad()

    expect(screen.getByRole('link', { name: 'Nova pessoa' })).toHaveAttribute(
      'href',
      '/pessoas/nova',
    )
  })

  it('mostra ações adequadas para pessoas ativas e inativas sem oferecer exclusão', async () => {
    renderPage()
    await waitForInitialLoad()

    const activeRow = screen.getByText('Marina Silva').closest('tr')
    const inactiveRow = screen.getByText('Silva & Filhos Ltda.').closest('tr')
    expect(activeRow).not.toBeNull()
    expect(inactiveRow).not.toBeNull()

    expect(within(activeRow!).getByRole('link', { name: 'Editar' })).toHaveAttribute(
      'href',
      '/pessoas/person-1/editar',
    )
    expect(within(activeRow!).getByRole('button', { name: 'Inativar' })).toBeEnabled()
    expect(within(inactiveRow!).getByRole('button', { name: 'Editar' })).toBeDisabled()
    expect(within(inactiveRow!).getByRole('button', { name: 'Editar' })).toHaveAttribute(
      'title',
      'Pessoa inativa não pode ser editada.',
    )
    expect(within(inactiveRow!).getByRole('button', { name: 'Reativar' })).toBeEnabled()
    expect(screen.queryByRole('button', { name: /excluir|remover/i })).not.toBeInTheDocument()
  })

  it('inativa pessoa e recarrega a consulta atual', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.click(screen.getByRole('button', { name: 'Inativar' }))

    await waitFor(() => expect(setPessoaAtivo).toHaveBeenCalledWith('person-1', false))
    await waitFor(() => expect(listPessoas).toHaveBeenCalledTimes(2))
  })

  it('reativa pessoa e recarrega a consulta atual', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.click(screen.getByRole('button', { name: 'Reativar' }))

    await waitFor(() => expect(setPessoaAtivo).toHaveBeenCalledWith('person-2', true))
    await waitFor(() => expect(listPessoas).toHaveBeenCalledTimes(2))
  })

  it('preserva filtros, ordenação e page size ao recarregar após inativação', async () => {
    renderPage()
    await waitForInitialLoad()

    fireEvent.change(screen.getByLabelText('Tipo de pessoa'), {
      target: { value: String(TipoPessoa.Juridica) },
    })
    await waitFor(() => expect(screen.getByLabelText('Status')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'true' } })
    await waitFor(() => expect(screen.getByLabelText('Ordenar por')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Ordenar por'), { target: { value: 'createdAtUtc' } })
    await waitFor(() => expect(screen.getByLabelText('Direção')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'desc' } })
    await waitFor(() => expect(screen.getByLabelText('Registros por página')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Registros por página'), { target: { value: '50' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Inativar' })).toBeEnabled())

    fireEvent.click(screen.getByRole('button', { name: 'Inativar' }))

    await waitFor(() => expect(setPessoaAtivo).toHaveBeenCalledWith('person-1', false))
    await waitFor(() =>
      expect(listPessoas).toHaveBeenLastCalledWith({
        page: 1,
        pageSize: 50,
        tipoPessoa: TipoPessoa.Juridica,
        ativo: true,
        sortBy: 'createdAtUtc',
        sortDirection: 'desc',
      }),
    )
  })

  it('mantém a tabela e mostra erro quando a alteração de status falha', async () => {
    vi.mocked(setPessoaAtivo).mockRejectedValue(new Error('technical details'))
    renderPage()
    await waitForInitialLoad()

    fireEvent.click(screen.getByRole('button', { name: 'Inativar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível alterar o status da pessoa.',
    )
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(screen.queryByText('technical details')).not.toBeInTheDocument()
  })
})
