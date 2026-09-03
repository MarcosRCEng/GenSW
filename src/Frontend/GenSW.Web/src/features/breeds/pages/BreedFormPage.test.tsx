import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../../shared/http/httpErrors'
import { listEspecies } from '../../species/services/speciesService'
import type { Especie, EspeciesPage, ListEspeciesParams } from '../../species/types/species'
import { createRaca, getRacaById, setRacaAtivo, updateRaca } from '../services/breedsService'
import type { Raca } from '../types/breeds'
import { BreedFormPage } from './BreedFormPage'

vi.mock('../../species/services/speciesService', () => ({
  listEspecies: vi.fn(),
}))

vi.mock('../services/breedsService', () => ({
  createRaca: vi.fn(),
  getRacaById: vi.fn(),
  setRacaAtivo: vi.fn(),
  updateRaca: vi.fn(),
}))

const activeSpeciesFirst: Especie = { id: 'species-active-1', nomeComum: 'Bovino', nomeCientifico: 'Bos taurus', ativo: true, createdAtUtc: '2026-08-20T12:00:00Z', updatedAtUtc: '2026-08-20T12:00:00Z' }
const activeSpeciesSecond: Especie = { id: 'species-active-2', nomeComum: 'Caprino', nomeCientifico: 'Capra hircus', ativo: true, createdAtUtc: '2026-08-21T12:00:00Z', updatedAtUtc: '2026-08-21T12:00:00Z' }
const otherInactiveSpecies: Especie = { id: 'species-inactive-other', nomeComum: 'Equino inativo', nomeCientifico: 'Equus caballus', ativo: false, createdAtUtc: '2026-08-22T12:00:00Z', updatedAtUtc: '2026-08-22T12:00:00Z' }

const activeSpeciesPages: EspeciesPage[] = [
  { items: [activeSpeciesFirst, otherInactiveSpecies], page: 1, pageSize: 100, totalItems: 3, totalPages: 2 },
  { items: [activeSpeciesSecond], page: 2, pageSize: 100, totalItems: 3, totalPages: 2 },
]

const breedWithInactiveSpecies: Raca = {
  id: 'breed-1',
  especieId: 'species-inactive-current',
  nome: 'Mangalarga',
  ativo: false,
  createdAtUtc: '2026-08-23T12:00:00Z',
  updatedAtUtc: '2026-08-23T12:00:00Z',
  especie: { id: 'species-inactive-current', nomeComum: 'Espécie histórica atual', ativo: false },
}

function renderPage(path = '/racas/nova') {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route element={<BreedFormPage />} path="/racas/nova" />
    <Route element={<BreedFormPage />} path="/racas/:id/editar" />
    <Route element={<h1>Raças destino</h1>} path="/racas" />
  </Routes></MemoryRouter>)
}

async function waitForActiveSpecies() {
  await screen.findByRole('option', { name: 'Caprino' })
}

beforeEach(() => {
  vi.mocked(listEspecies).mockReset()
  vi.mocked(createRaca).mockReset()
  vi.mocked(getRacaById).mockReset()
  vi.mocked(updateRaca).mockReset()
  vi.mocked(setRacaAtivo).mockReset()
  vi.mocked(listEspecies).mockImplementation(async (params?: ListEspeciesParams) => activeSpeciesPages[(params?.page ?? 1) - 1])
  vi.mocked(createRaca).mockResolvedValue({ ...breedWithInactiveSpecies, especieId: activeSpeciesSecond.id, especie: { id: activeSpeciesSecond.id, nomeComum: activeSpeciesSecond.nomeComum, ativo: true } })
  vi.mocked(getRacaById).mockResolvedValue(breedWithInactiveSpecies)
  vi.mocked(updateRaca).mockResolvedValue(breedWithInactiveSpecies)
})

describe('BreedFormPage - criação', () => {
  it('pagina todas as espécies ativas e cria com uma opção da página seguinte', async () => {
    renderPage()
    await waitForActiveSpecies()
    expect(listEspecies).toHaveBeenNthCalledWith(1, { page: 1, pageSize: 100, ativo: true })
    expect(listEspecies).toHaveBeenNthCalledWith(2, { page: 2, pageSize: 100, ativo: true })
    expect(screen.queryByRole('option', { name: 'Equino inativo' })).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: activeSpeciesSecond.id } })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: '  Boer\t brasileira  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() => expect(createRaca).toHaveBeenCalledWith({ especieId: activeSpeciesSecond.id, nome: 'Boer brasileira' }))
    expect(await screen.findByRole('heading', { name: 'Raças destino' })).toBeInTheDocument()
    expect(setRacaAtivo).not.toHaveBeenCalled()
  })

  it('valida espécie e nome entre 1 e 200 caracteres localmente', async () => {
    renderPage()
    await waitForActiveSpecies()
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: ' ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(screen.getByText('Selecione uma espécie ativa.')).toBeInTheDocument()
    expect(screen.getByText('Informe um nome entre 1 e 200 caracteres.')).toBeInTheDocument()
    expect(createRaca).not.toHaveBeenCalled()
  })

  it.each([
    [400, 'Os dados informados para a raça são inválidos.'],
    [409, 'Já existe uma raça com esse nome para a espécie selecionada.'],
  ])('traduz erro HTTP %s ao criar', async (status, message) => {
    vi.mocked(createRaca).mockRejectedValue(new HttpError(status))
    renderPage()
    await waitForActiveSpecies()
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: activeSpeciesFirst.id } })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Nelore' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })
})

describe('BreedFormPage - edição', () => {
  it('mostra loading e carrega a raça antes das opções de espécie', async () => {
    let resolveBreed!: (breed: Raca) => void
    vi.mocked(getRacaById).mockReturnValue(new Promise<Raca>((resolve) => { resolveBreed = resolve }))
    renderPage('/racas/breed-1/editar')
    expect(screen.getByRole('status')).toHaveTextContent('Carregando raça…')
    expect(listEspecies).not.toHaveBeenCalled()
    resolveBreed(breedWithInactiveSpecies)
    expect(await screen.findByDisplayValue('Mangalarga')).toBeInTheDocument()
    await waitForActiveSpecies()
  })

  it('preserva somente a espécie atual inativa e permite editar o nome sem trocar o vínculo', async () => {
    renderPage('/racas/breed-1/editar')
    expect(await screen.findByRole('option', { name: 'Espécie histórica atual (inativa)' })).toBeInTheDocument()
    expect(screen.getByLabelText('Espécie')).toHaveValue('species-inactive-current')
    expect(screen.queryByRole('option', { name: 'Equino inativo' })).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: ' Mangalarga  Marchador ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(updateRaca).toHaveBeenCalledWith('breed-1', { especieId: 'species-inactive-current', nome: 'Mangalarga Marchador' }))
    expect(setRacaAtivo).not.toHaveBeenCalled()
  })

  it('permite trocar da espécie atual inativa para uma ativa da página seguinte', async () => {
    renderPage('/racas/breed-1/editar')
    await waitForActiveSpecies()
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: activeSpeciesSecond.id } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(updateRaca).toHaveBeenCalledWith('breed-1', { especieId: activeSpeciesSecond.id, nome: 'Mangalarga' }))
  })

  it('mostra raça não encontrada ao receber 404', async () => {
    vi.mocked(getRacaById).mockRejectedValue(new HttpError(404))
    renderPage('/racas/missing/editar')
    expect(await screen.findByText('Raça não encontrada.')).toBeInTheDocument()
    expect(listEspecies).not.toHaveBeenCalled()
  })

  it('permite tentar novamente após erro genérico de carregamento', async () => {
    vi.mocked(getRacaById).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(breedWithInactiveSpecies)
    renderPage('/racas/breed-1/editar')
    expect(await screen.findByText('Não foi possível carregar a raça.')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByDisplayValue('Mangalarga')).toBeInTheDocument()
    expect(getRacaById).toHaveBeenCalledTimes(2)
  })

  it.each([
    [400, 'Os dados informados para a raça são inválidos.'],
    [404, 'Raça não encontrada.'],
    [409, 'Já existe uma raça com esse nome para a espécie selecionada.'],
  ])('traduz erro HTTP %s ao atualizar', async (status, message) => {
    vi.mocked(updateRaca).mockRejectedValue(new HttpError(status))
    renderPage('/racas/breed-1/editar')
    await screen.findByDisplayValue('Mangalarga')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })
})
