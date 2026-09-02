import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listEspecies } from '../../species/services/speciesService'
import type { EspeciesPage, ListEspeciesParams } from '../../species/types/species'
import { listRacas, setRacaAtivo } from '../services/breedsService'
import type { ListRacasParams, RacasPage } from '../types/breeds'
import { BreedsListPage } from './BreedsListPage'

vi.mock('../../species/services/speciesService', () => ({
  listEspecies: vi.fn(),
}))

vi.mock('../services/breedsService', () => ({
  listRacas: vi.fn(),
  setRacaAtivo: vi.fn(),
}))

const speciesPages: EspeciesPage[] = [
  {
    items: [
      { id: 'species-active', nomeComum: 'Bovino', nomeCientifico: 'Bos taurus', ativo: true, createdAtUtc: '2026-08-20T12:00:00Z', updatedAtUtc: '2026-08-20T12:00:00Z' },
    ],
    page: 1,
    pageSize: 100,
    totalItems: 2,
    totalPages: 2,
  },
  {
    items: [
      { id: 'species-inactive', nomeComum: 'Equino histórico', nomeCientifico: 'Equus caballus', ativo: false, createdAtUtc: '2026-08-21T12:00:00Z', updatedAtUtc: '2026-08-21T12:00:00Z' },
    ],
    page: 2,
    pageSize: 100,
    totalItems: 2,
    totalPages: 2,
  },
]

const breedsPage: RacasPage = {
  items: [
    { id: 'breed-active', especieId: 'species-active', nome: 'Nelore', ativo: true, createdAtUtc: '2026-08-22T12:00:00Z', updatedAtUtc: '2026-08-22T12:00:00Z', especie: { id: 'species-active', nomeComum: 'Bovino', ativo: true } },
    { id: 'breed-inactive', especieId: 'species-inactive', nome: 'Mangalarga histórico', ativo: false, createdAtUtc: '2026-08-23T12:00:00Z', updatedAtUtc: '2026-08-23T12:00:00Z', especie: { id: 'species-inactive', nomeComum: 'Equino histórico', ativo: false } },
  ],
  page: 1,
  pageSize: 25,
  totalItems: 2,
  totalPages: 1,
}

function renderPage() {
  return render(<MemoryRouter><BreedsListPage /></MemoryRouter>)
}

async function waitForInitialLoad() {
  await screen.findByText('Nelore')
  await screen.findByRole('option', { name: 'Equino histórico (inativa)' })
}

beforeEach(() => {
  vi.mocked(listEspecies).mockReset()
  vi.mocked(listRacas).mockReset()
  vi.mocked(setRacaAtivo).mockReset()
  vi.mocked(listEspecies).mockImplementation(async (params?: ListEspeciesParams) => speciesPages[(params?.page ?? 1) - 1])
  vi.mocked(listRacas).mockResolvedValue(breedsPage)
  vi.mocked(setRacaAtivo).mockResolvedValue(breedsPage.items[0])
})

describe('BreedsListPage', () => {
  it('mostra loading enquanto a primeira consulta está pendente', () => {
    vi.mocked(listRacas).mockReturnValue(new Promise<RacasPage>(() => undefined))
    vi.mocked(listEspecies).mockReturnValue(new Promise<EspeciesPage>(() => undefined))
    renderPage()
    expect(screen.getByRole('status')).toHaveTextContent('Carregando raças…')
  })

  it('lista raças com a espécie resumida e todos os cabeçalhos', async () => {
    renderPage()
    await waitForInitialLoad()
    const table = screen.getByRole('table')
    expect(within(table).getByText('Bovino')).toBeInTheDocument()
    expect(within(table).getByText('Mangalarga histórico')).toBeInTheDocument()
    for (const header of ['Nome', 'Espécie', 'Status', 'Cadastro', 'Ações']) {
      expect(within(table).getByRole('columnheader', { name: header })).toBeInTheDocument()
    }
  })

  it('carrega todas as páginas de espécies sem filtro de status e permite consultar histórico inativo', async () => {
    renderPage()
    await waitForInitialLoad()

    expect(listEspecies).toHaveBeenNthCalledWith(1, { page: 1, pageSize: 100 })
    expect(listEspecies).toHaveBeenNthCalledWith(2, { page: 2, pageSize: 100 })
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: 'species-inactive' } })

    await waitFor(() => expect(listRacas).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 25,
      especieId: 'species-inactive',
      sortBy: 'nome',
      sortDirection: 'asc',
    }))
  })

  it('aplica busca, espécie, status, ordenação e direção', async () => {
    renderPage()
    await waitForInitialLoad()
    fireEvent.change(screen.getByLabelText('Buscar por nome'), { target: { value: 'Mangalarga' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(screen.getByLabelText('Espécie')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: 'species-inactive' } })
    await waitFor(() => expect(screen.getByLabelText('Status')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'false' } })
    await waitFor(() => expect(screen.getByLabelText('Ordenar por')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Ordenar por'), { target: { value: 'createdAtUtc' } })
    await waitFor(() => expect(screen.getByLabelText('Direção')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'desc' } })

    await waitFor(() => expect(listRacas).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 25,
      search: 'Mangalarga',
      especieId: 'species-inactive',
      ativo: false,
      sortBy: 'createdAtUtc',
      sortDirection: 'desc',
    }))
  })

  it('navega entre páginas e troca o tamanho da página', async () => {
    vi.mocked(listRacas).mockImplementation(async (params?: ListRacasParams) => ({ ...breedsPage, page: params?.page ?? 1, pageSize: params?.pageSize ?? 25, totalItems: 75, totalPages: 3 }))
    renderPage()
    await waitForInitialLoad()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(screen.getByText('Página 2 de 3')).toBeInTheDocument())
    fireEvent.change(screen.getByLabelText('Registros por página'), { target: { value: '50' } })
    await waitFor(() => expect(listRacas).toHaveBeenLastCalledWith({ page: 1, pageSize: 50, sortBy: 'nome', sortDirection: 'asc' }))
  })

  it('mostra estado vazio', async () => {
    vi.mocked(listRacas).mockResolvedValue({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 })
    renderPage()
    expect(await screen.findByText('Nenhuma raça encontrada.')).toBeInTheDocument()
  })

  it('mostra falha e permite tentar novamente', async () => {
    vi.mocked(listRacas).mockRejectedValueOnce(new Error('internal details'))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar as raças.')
    vi.mocked(listRacas).mockResolvedValueOnce(breedsPage)
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText('Nelore')).toBeInTheDocument()
  })

  it('mantém edição disponível para raça inativa', async () => {
    renderPage()
    expect(await screen.findByRole('link', { name: 'Editar', description: 'Mangalarga histórico' })).toHaveAttribute('href', '/racas/breed-inactive/editar')
  })

  it('inativa raça, bloqueia duplo clique e recarrega os filtros atuais', async () => {
    let resolveStatus!: (value: typeof breedsPage.items[0]) => void
    vi.mocked(setRacaAtivo).mockReturnValue(new Promise((resolve) => { resolveStatus = resolve }))
    renderPage()
    await waitForInitialLoad()
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: 'species-active' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Inativar' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Inativar' }))
    fireEvent.click(screen.getByRole('button', { name: 'Inativando…' }))
    expect(setRacaAtivo).toHaveBeenCalledTimes(1)
    resolveStatus(breedsPage.items[0])
    await waitFor(() => expect(listRacas).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, especieId: 'species-active', sortBy: 'nome', sortDirection: 'asc' }))
  })

  it('reativa raça e apresenta erro de lifecycle sem perder a listagem', async () => {
    vi.mocked(setRacaAtivo).mockRejectedValueOnce(new Error('internal details'))
    renderPage()
    await waitForInitialLoad()
    fireEvent.click(screen.getByRole('button', { name: 'Reativar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível alterar o status da raça.')
    expect(setRacaAtivo).toHaveBeenCalledWith('breed-inactive', true)
    expect(screen.getByText('Nelore')).toBeInTheDocument()
  })
})
