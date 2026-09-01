import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listEspecies, setEspecieAtivo } from '../services/speciesService'
import type { EspeciesPage, ListEspeciesParams } from '../types/species'
import { SpeciesListPage } from './SpeciesListPage'

vi.mock('../services/speciesService', () => ({
  listEspecies: vi.fn(),
  setEspecieAtivo: vi.fn(),
}))

const speciesPage: EspeciesPage = {
  items: [
    { id: 'species-1', nomeComum: 'Onça-pintada', nomeCientifico: 'Panthera onca', ativo: true, createdAtUtc: '2026-08-20T12:00:00Z', updatedAtUtc: '2026-08-20T12:00:00Z' },
    { id: 'species-2', nomeComum: 'Lobo-guará', nomeCientifico: null, ativo: false, createdAtUtc: '2026-08-21T12:00:00Z', updatedAtUtc: '2026-08-21T12:00:00Z' },
  ],
  page: 1,
  pageSize: 25,
  totalItems: 2,
  totalPages: 1,
}

function renderPage() {
  return render(<MemoryRouter><SpeciesListPage /></MemoryRouter>)
}

async function waitForInitialLoad() {
  await screen.findByText('Onça-pintada')
}

beforeEach(() => {
  vi.mocked(listEspecies).mockReset()
  vi.mocked(setEspecieAtivo).mockReset()
  vi.mocked(listEspecies).mockResolvedValue(speciesPage)
  vi.mocked(setEspecieAtivo).mockResolvedValue(speciesPage.items[0])
})

describe('SpeciesListPage', () => {
  it('mostra loading enquanto a primeira consulta está pendente', () => {
    vi.mocked(listEspecies).mockReturnValue(new Promise<EspeciesPage>(() => undefined))
    renderPage()
    expect(screen.getByRole('status')).toHaveTextContent('Carregando espécies…')
  })

  it('lista espécies e exibe todos os cabeçalhos', async () => {
    renderPage()
    await waitForInitialLoad()
    const table = screen.getByRole('table')
    expect(within(table).getByText('Panthera onca')).toBeInTheDocument()
    expect(within(table).getByText('—')).toBeInTheDocument()
    expect(within(table).getAllByRole('columnheader')).toHaveLength(5)
    for (const header of ['Nome comum', 'Nome científico', 'Status', 'Cadastro', 'Ações']) {
      expect(within(table).getByRole('columnheader', { name: header })).toBeInTheDocument()
    }
  })

  it('aplica busca, status, ordenação e direção', async () => {
    renderPage()
    await waitForInitialLoad()
    fireEvent.change(screen.getByLabelText('Buscar por nome'), { target: { value: 'Onça' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(screen.getByLabelText('Status')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'false' } })
    await waitFor(() => expect(screen.getByLabelText('Ordenar por')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Ordenar por'), { target: { value: 'createdAtUtc' } })
    await waitFor(() => expect(screen.getByLabelText('Direção')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'desc' } })
    await waitFor(() => expect(listEspecies).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, search: 'Onça', ativo: false, sortBy: 'createdAtUtc', sortDirection: 'desc' }))
  })

  it('navega entre páginas e troca o tamanho da página', async () => {
    vi.mocked(listEspecies).mockImplementation(async (params?: ListEspeciesParams) => ({ ...speciesPage, page: params?.page ?? 1, pageSize: params?.pageSize ?? 25, totalItems: 75, totalPages: 3 }))
    renderPage()
    await waitForInitialLoad()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(screen.getByText('Página 2 de 3')).toBeInTheDocument())
    fireEvent.change(screen.getByLabelText('Registros por página'), { target: { value: '50' } })
    await waitFor(() => expect(listEspecies).toHaveBeenLastCalledWith({ page: 1, pageSize: 50, sortBy: 'nomeComum', sortDirection: 'asc' }))
  })

  it('mostra estado vazio', async () => {
    vi.mocked(listEspecies).mockResolvedValue({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 })
    renderPage()
    expect(await screen.findByText('Nenhuma espécie encontrada.')).toBeInTheDocument()
  })

  it('mostra falha e permite tentar novamente', async () => {
    vi.mocked(listEspecies).mockRejectedValueOnce(new Error('internal details'))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar as espécies.')
    vi.mocked(listEspecies).mockResolvedValueOnce(speciesPage)
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText('Onça-pintada')).toBeInTheDocument()
  })

  it('mantém edição disponível para espécie inativa', async () => {
    vi.mocked(listEspecies).mockResolvedValue({ items: [{ ...speciesPage.items[0], ativo: false }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 })
    renderPage()
    expect(await screen.findByRole('link', { name: 'Editar' })).toHaveAttribute('href', '/especies/species-1/editar')
  })

  it('inativa espécie, bloqueia duplo clique e recarrega os filtros atuais', async () => {
    let resolveStatus!: (value: typeof speciesPage.items[0]) => void
    vi.mocked(setEspecieAtivo).mockReturnValue(new Promise((resolve) => { resolveStatus = resolve }))
    renderPage()
    await waitForInitialLoad()
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'true' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Inativar' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Inativar' }))
    fireEvent.click(screen.getByRole('button', { name: 'Inativando…' }))
    expect(setEspecieAtivo).toHaveBeenCalledTimes(1)
    resolveStatus(speciesPage.items[0])
    await waitFor(() => expect(listEspecies).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, ativo: true, sortBy: 'nomeComum', sortDirection: 'asc' }))
  })

  it('recua para a última página válida após inativar o último registro da página final', async () => {
    const lastSpecies = { ...speciesPage.items[0], id: 'species-last' }
    const reloadedSpecies = { ...speciesPage.items[1], id: 'species-reloaded', nomeComum: 'Arara-azul' }
    vi.mocked(listEspecies)
      .mockResolvedValueOnce({ ...speciesPage, totalItems: 26, totalPages: 2 })
      .mockResolvedValueOnce({ ...speciesPage, totalItems: 26, totalPages: 2 })
      .mockResolvedValueOnce({ items: [lastSpecies], page: 2, pageSize: 25, totalItems: 26, totalPages: 2 })
      .mockResolvedValueOnce({ items: [], page: 2, pageSize: 25, totalItems: 25, totalPages: 1 })
      .mockResolvedValueOnce({ items: [reloadedSpecies], page: 1, pageSize: 25, totalItems: 25, totalPages: 1 })
    renderPage()
    await waitForInitialLoad()

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'true' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Próxima' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('Página 2 de 2')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Inativar' }))

    await waitFor(() => expect(listEspecies).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 25,
      ativo: true,
      sortBy: 'nomeComum',
      sortDirection: 'asc',
    }))
    expect(await screen.findByText('Arara-azul')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 1')).toBeInTheDocument()
    expect(screen.queryByText('Página 2 de 1')).not.toBeInTheDocument()
  })

  it('reativa espécie', async () => {
    renderPage()
    await waitForInitialLoad()
    fireEvent.click(screen.getByRole('button', { name: 'Reativar' }))
    await waitFor(() => expect(setEspecieAtivo).toHaveBeenCalledWith('species-2', true))
  })
})
