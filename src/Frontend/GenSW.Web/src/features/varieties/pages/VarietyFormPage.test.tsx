import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../../shared/http/httpErrors'
import { listEspecies } from '../../species/services/speciesService'
import type { Especie, EspeciesPage, ListEspeciesParams } from '../../species/types/species'
import { createVariedade, getVariedadeById, setVariedadeAtivo, updateVariedade } from '../services/varietiesService'
import type { Variedade } from '../types/varieties'
import { VarietyFormPage } from './VarietyFormPage'

vi.mock('../../species/services/speciesService', () => ({
  listEspecies: vi.fn(),
}))

vi.mock('../services/varietiesService', () => ({
  createVariedade: vi.fn(),
  getVariedadeById: vi.fn(),
  setVariedadeAtivo: vi.fn(),
  updateVariedade: vi.fn(),
}))

const activeSpeciesFirst: Especie = { id: 'species-active-1', nomeComum: 'Cafeeiro', nomeCientifico: 'Coffea arabica', ativo: true, createdAtUtc: '2026-08-20T12:00:00Z', updatedAtUtc: '2026-08-20T12:00:00Z' }
const activeSpeciesSecond: Especie = { id: 'species-active-2', nomeComum: 'Videira', nomeCientifico: 'Vitis vinifera', ativo: true, createdAtUtc: '2026-08-21T12:00:00Z', updatedAtUtc: '2026-08-21T12:00:00Z' }
const otherInactiveSpecies: Especie = { id: 'species-inactive-other', nomeComum: 'Soja inativa', nomeCientifico: 'Glycine max', ativo: false, createdAtUtc: '2026-08-22T12:00:00Z', updatedAtUtc: '2026-08-22T12:00:00Z' }

const activeSpeciesPages: EspeciesPage[] = [
  { items: [activeSpeciesFirst, otherInactiveSpecies], page: 1, pageSize: 100, totalItems: 3, totalPages: 2 },
  { items: [activeSpeciesSecond], page: 2, pageSize: 100, totalItems: 3, totalPages: 2 },
]

const varietyWithInactiveSpecies: Variedade = {
  id: 'variety-1',
  especieId: 'species-inactive-current',
  nome: 'Bourbon Amarelo',
  ativo: false,
  createdAtUtc: '2026-08-23T12:00:00Z',
  updatedAtUtc: '2026-08-23T12:00:00Z',
  especie: { id: 'species-inactive-current', nomeComum: 'Espécie histórica atual', ativo: false },
}

function renderPage(path = '/variedades/nova') {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route element={<VarietyFormPage />} path="/variedades/nova" />
    <Route element={<VarietyFormPage />} path="/variedades/:id/editar" />
    <Route element={<h1>Variedades destino</h1>} path="/variedades" />
  </Routes></MemoryRouter>)
}

async function waitForActiveSpecies() {
  await screen.findByRole('option', { name: 'Videira' })
}

beforeEach(() => {
  vi.mocked(listEspecies).mockReset()
  vi.mocked(createVariedade).mockReset()
  vi.mocked(getVariedadeById).mockReset()
  vi.mocked(updateVariedade).mockReset()
  vi.mocked(setVariedadeAtivo).mockReset()
  vi.mocked(listEspecies).mockImplementation(async (params?: ListEspeciesParams) => activeSpeciesPages[(params?.page ?? 1) - 1])
  vi.mocked(createVariedade).mockResolvedValue({ ...varietyWithInactiveSpecies, especieId: activeSpeciesSecond.id, especie: { id: activeSpeciesSecond.id, nomeComum: activeSpeciesSecond.nomeComum, ativo: true } })
  vi.mocked(getVariedadeById).mockResolvedValue(varietyWithInactiveSpecies)
  vi.mocked(updateVariedade).mockResolvedValue(varietyWithInactiveSpecies)
})

describe('VarietyFormPage - criação', () => {
  it('pagina todas as espécies ativas e cria com uma opção da página seguinte', async () => {
    renderPage()
    await waitForActiveSpecies()
    expect(listEspecies).toHaveBeenNthCalledWith(1, { page: 1, pageSize: 100, ativo: true })
    expect(listEspecies).toHaveBeenNthCalledWith(2, { page: 2, pageSize: 100, ativo: true })
    expect(screen.queryByRole('option', { name: 'Soja inativa' })).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: activeSpeciesSecond.id } })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: '  Niágara\t Rosada  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createVariedade).toHaveBeenCalledWith({ especieId: activeSpeciesSecond.id, nome: 'Niágara Rosada' }))
    expect(await screen.findByRole('heading', { name: 'Variedades destino' })).toBeInTheDocument()
    expect(setVariedadeAtivo).not.toHaveBeenCalled()
  })

  it('valida espécie e nome entre 1 e 200 caracteres localmente', async () => {
    renderPage()
    await waitForActiveSpecies()
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: ' ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(screen.getByText('Selecione uma espécie ativa.')).toBeInTheDocument()
    expect(screen.getByText('Informe um nome entre 1 e 200 caracteres.')).toBeInTheDocument()
    expect(createVariedade).not.toHaveBeenCalled()
  })

  it.each([
    [400, 'Os dados informados para a variedade são inválidos.'],
    [409, 'Já existe uma variedade com esse nome para a espécie selecionada.'],
  ])('traduz erro HTTP %s ao criar', async (status, message) => {
    vi.mocked(createVariedade).mockRejectedValue(new HttpError(status))
    renderPage()
    await waitForActiveSpecies()
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: activeSpeciesFirst.id } })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Catuaí' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })
})

describe('VarietyFormPage - edição', () => {
  it('mostra loading e carrega a variedade antes das opções de espécie', async () => {
    let resolveVariety!: (variety: Variedade) => void
    vi.mocked(getVariedadeById).mockReturnValue(new Promise<Variedade>((resolve) => { resolveVariety = resolve }))
    renderPage('/variedades/variety-1/editar')
    expect(screen.getByRole('status')).toHaveTextContent('Carregando variedade…')
    expect(listEspecies).not.toHaveBeenCalled()
    resolveVariety(varietyWithInactiveSpecies)
    expect(await screen.findByDisplayValue('Bourbon Amarelo')).toBeInTheDocument()
    await waitForActiveSpecies()
  })

  it('preserva somente a espécie atual inativa e permite editar o nome sem trocar o vínculo', async () => {
    renderPage('/variedades/variety-1/editar')
    expect(await screen.findByRole('option', { name: 'Espécie histórica atual (inativa)' })).toBeInTheDocument()
    expect(screen.getByLabelText('Espécie')).toHaveValue('species-inactive-current')
    expect(screen.queryByRole('option', { name: 'Soja inativa' })).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: ' Bourbon  Vermelho ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(updateVariedade).toHaveBeenCalledWith('variety-1', { especieId: 'species-inactive-current', nome: 'Bourbon Vermelho' }))
    expect(setVariedadeAtivo).not.toHaveBeenCalled()
  })

  it('permite trocar da espécie atual inativa para uma ativa da página seguinte', async () => {
    renderPage('/variedades/variety-1/editar')
    await waitForActiveSpecies()
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: activeSpeciesSecond.id } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(updateVariedade).toHaveBeenCalledWith('variety-1', { especieId: activeSpeciesSecond.id, nome: 'Bourbon Amarelo' }))
  })

  it('mostra variedade não encontrada ao receber 404', async () => {
    vi.mocked(getVariedadeById).mockRejectedValue(new HttpError(404))
    renderPage('/variedades/missing/editar')
    expect(await screen.findByText('Variedade não encontrada.')).toBeInTheDocument()
    expect(listEspecies).not.toHaveBeenCalled()
  })

  it('permite tentar novamente após erro genérico de carregamento', async () => {
    vi.mocked(getVariedadeById).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(varietyWithInactiveSpecies)
    renderPage('/variedades/variety-1/editar')
    expect(await screen.findByText('Não foi possível carregar a variedade.')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByDisplayValue('Bourbon Amarelo')).toBeInTheDocument()
    expect(getVariedadeById).toHaveBeenCalledTimes(2)
  })

  it.each([
    [400, 'Os dados informados para a variedade são inválidos.'],
    [404, 'Variedade não encontrada.'],
    [409, 'Já existe uma variedade com esse nome para a espécie selecionada.'],
  ])('traduz erro HTTP %s ao atualizar', async (status, message) => {
    vi.mocked(updateVariedade).mockRejectedValue(new HttpError(status))
    renderPage('/variedades/variety-1/editar')
    await screen.findByDisplayValue('Bourbon Amarelo')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })
})
