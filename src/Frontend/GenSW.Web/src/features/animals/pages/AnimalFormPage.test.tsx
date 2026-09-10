import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../../shared/http/httpErrors'
import { listRacas } from '../../breeds/services/breedsService'
import { listEspecies } from '../../species/services/speciesService'
import { listVariedades } from '../../varieties/services/varietiesService'
import { createAnimal, getAnimalById, updateAnimal } from '../services/animalsService'
import { AnimalFormPage } from './AnimalFormPage'

vi.mock('../../species/services/speciesService', () => ({ listEspecies: vi.fn() }))
vi.mock('../../breeds/services/breedsService', () => ({ listRacas: vi.fn() }))
vi.mock('../../varieties/services/varietiesService', () => ({ listVariedades: vi.fn() }))
vi.mock('../services/animalsService', () => ({ createAnimal: vi.fn(), getAnimalById: vi.fn(), updateAnimal: vi.fn() }))

const speciesA = { id: 'species-a', nomeComum: 'Canina', nomeCientifico: null, ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z' }
const speciesB = { ...speciesA, id: 'species-b', nomeComum: 'Felina' }
const breedA = { id: 'breed-a', especieId: speciesA.id, nome: 'Pastor', ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: speciesA.id, nomeComum: speciesA.nomeComum, ativo: true } }
const breedB = { ...breedA, id: 'breed-b', especieId: speciesB.id, nome: 'Siamês', especie: { id: speciesB.id, nomeComum: speciesB.nomeComum, ativo: true } }
const varietyA = { id: 'variety-a', especieId: speciesA.id, nome: 'Curto', ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: speciesA.id, nomeComum: speciesA.nomeComum, ativo: true } }
const varietyB = { ...varietyA, id: 'variety-b', especieId: speciesB.id, nome: 'Longo', especie: { id: speciesB.id, nomeComum: speciesB.nomeComum, ativo: true } }
const savedAnimal = { id: 'animal-1', codigoInterno: 'AN-000001', nome: 'Bela', especieId: speciesA.id, racaId: breedA.id, variedadeId: varietyA.id, sexo: 2 as const, dataNascimento: '2020-02-29', escopo: 1 as const, ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: speciesA.id, nomeComum: speciesA.nomeComum, ativo: true }, raca: { id: breedA.id, nome: breedA.nome, ativo: true }, variedade: { id: varietyA.id, nome: varietyA.nome, ativo: true } }

function renderPage(path = '/animais/nova') {
  return render(<MemoryRouter initialEntries={[path]}><Routes><Route element={<AnimalFormPage />} path="/animais/nova" /><Route element={<AnimalFormPage />} path="/animais/:id/editar" /><Route element={<p>Lista de animais</p>} path="/animais" /></Routes></MemoryRouter>)
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(listEspecies).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [speciesA], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [speciesB], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(listRacas).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [breedA], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [breedB], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(listVariedades).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [varietyA], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [varietyB], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(createAnimal).mockResolvedValue(savedAnimal)
  vi.mocked(getAnimalById).mockResolvedValue(savedAnimal)
  vi.mocked(updateAnimal).mockResolvedValue(savedAnimal)
})

describe('AnimalFormPage', () => {
  it('shows automatic-code help and omits untouched empty code, but rejects a touched empty value locally', async () => {
    renderPage()
    expect(await screen.findByText('O código será gerado automaticamente se permanecer vazio.')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByLabelText('Espécie')).not.toBeDisabled())
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesA.id } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createAnimal).toHaveBeenCalledWith(expect.objectContaining({ especieId: speciesA.id })))
    expect(vi.mocked(createAnimal).mock.calls[0][0]).not.toHaveProperty('codigoInterno')

    renderPage()
    await waitFor(() => expect(screen.getByLabelText('Espécie')).not.toBeDisabled())
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesA.id } })
    fireEvent.focus(screen.getByLabelText('Código interno'))
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('Informe um código interno não vazio.')).toBeInTheDocument()
    expect(createAnimal).toHaveBeenCalledOnce()
  })

  it('normalizes and sends explicit manual code, optional fields, all active catalog pages, and compatible independent classifications', async () => {
    renderPage()
    await screen.findByRole('heading', { name: 'Novo animal' })
    await waitFor(() => expect(listEspecies).toHaveBeenCalledWith({ page: 2, pageSize: 100, ativo: true }))
    expect(listRacas).toHaveBeenCalledWith({ page: 1, pageSize: 100, ativo: true })
    expect(listVariedades).toHaveBeenCalledWith({ page: 2, pageSize: 100, ativo: true })
    fireEvent.change(screen.getByLabelText('Código interno'), { target: { value: '  MANUAL   01 ' } })
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesA.id } })
    fireEvent.change(screen.getByLabelText('Raça'), { target: { value: breedA.id } })
    fireEvent.change(screen.getByLabelText('Variedade'), { target: { value: varietyA.id } })
    fireEvent.change(screen.getByLabelText('Sexo'), { target: { value: '1' } })
    fireEvent.change(screen.getByLabelText('Escopo'), { target: { value: '2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createAnimal).toHaveBeenCalledWith({ codigoInterno: 'MANUAL 01', nome: null, especieId: speciesA.id, racaId: breedA.id, variedadeId: varietyA.id, sexo: 1, dataNascimento: null, escopo: 2 }))
  })

  it('loads the animal before catalogs, retains only inactive current links, and keeps active replacements for an inactive current species', async () => {
    const inactiveAnimal = { ...savedAnimal, racaId: 'breed-old', variedadeId: 'variety-old', especie: { ...savedAnimal.especie, ativo: false }, raca: { id: 'breed-old', nome: 'Antiga', ativo: false }, variedade: { id: 'variety-old', nome: 'Antiga', ativo: false } }
    vi.mocked(getAnimalById).mockResolvedValue(inactiveAnimal)
    renderPage('/animais/animal-1/editar')
    expect(await screen.findByRole('heading', { name: 'Editar animal' })).toBeInTheDocument()
    expect(getAnimalById).toHaveBeenCalledWith('animal-1')
    expect(screen.getByRole('option', { name: 'Canina (inativa)' })).toBeInTheDocument()
    expect(screen.getAllByRole('option', { name: 'Antiga (inativa)' })).toHaveLength(2)
    expect(screen.queryByRole('option', { name: 'Siamês' })).not.toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Pastor' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Curto' })).toBeInTheDocument()
  })

  it('preserves incompatible classifications on a species change, blocks submission until explicit correction, and hides technical server details', async () => {
    renderPage('/animais/animal-1/editar')
    await screen.findByRole('heading', { name: 'Editar animal' })
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesB.id } })
    expect(await screen.findByText('A raça selecionada não pertence à espécie selecionada.')).toBeInTheDocument()
    expect(screen.getByLabelText('Raça')).toHaveValue(breedA.id)
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(updateAnimal).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText('Raça'), { target: { value: breedB.id } })
    fireEvent.change(screen.getByLabelText('Variedade'), { target: { value: varietyB.id } })
    vi.mocked(updateAnimal).mockRejectedValueOnce(new HttpError(409, 'SQLSTATE 23505 PostgreSQL Detail'))
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Já existe um animal com esse código interno.')
    expect(screen.queryByText(/SQLSTATE|PostgreSQL Detail/)).not.toBeInTheDocument()
  })
})
