import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listRacas } from '../../breeds/services/breedsService'
import { listEspecies } from '../../species/services/speciesService'
import { listVariedades } from '../../varieties/services/varietiesService'
import { listAnimals, setAnimalAtivo } from '../services/animalsService'
import { AnimalsListPage } from './AnimalsListPage'

vi.mock('../../species/services/speciesService', () => ({ listEspecies: vi.fn() }))
vi.mock('../../breeds/services/breedsService', () => ({ listRacas: vi.fn() }))
vi.mock('../../varieties/services/varietiesService', () => ({ listVariedades: vi.fn() }))
vi.mock('../services/animalsService', () => ({ listAnimals: vi.fn(), setAnimalAtivo: vi.fn() }))

const species = { id: 'species-1', nomeComum: 'Canina', nomeCientifico: null, ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z' }
const inactiveSpecies = { ...species, id: 'species-old', nomeComum: 'Equina', ativo: false }
const breed = { id: 'breed-1', especieId: species.id, nome: 'Pastor', ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: species.id, nomeComum: species.nomeComum, ativo: true } }
const inactiveBreed = { ...breed, id: 'breed-old', nome: 'Antiga', ativo: false }
const variety = { id: 'variety-1', especieId: species.id, nome: 'Curto', ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: species.id, nomeComum: species.nomeComum, ativo: true } }
const inactiveVariety = { ...variety, id: 'variety-old', nome: 'Antiga', ativo: false }
const animal = { id: 'animal-1', codigoInterno: 'AN-000001', nome: 'Bela', especieId: species.id, racaId: breed.id, variedadeId: variety.id, sexo: 2 as const, dataNascimento: '2020-02-29', escopo: 1 as const, ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: species.id, nomeComum: species.nomeComum, ativo: true }, raca: { id: breed.id, nome: breed.nome, ativo: true }, variedade: { id: variety.id, nome: variety.nome, ativo: true } }

function renderPage() { return render(<MemoryRouter><AnimalsListPage /></MemoryRouter>) }

beforeEach(() => {
  vi.mocked(listEspecies).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [species], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [inactiveSpecies], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(listRacas).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [breed], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [inactiveBreed], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(listVariedades).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [variety], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [inactiveVariety], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(listAnimals).mockResolvedValue({ items: [animal], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 })
  vi.mocked(setAnimalAtivo).mockResolvedValue({ ...animal, ativo: false })
})

describe('AnimalsListPage', () => {
  it('renders all animal columns and loads every historical catalog page without an active filter', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: 'Animais' })).toBeInTheDocument()
    for (const column of ['Código interno', 'Nome', 'Espécie', 'Raça', 'Variedade', 'Sexo', 'Escopo', 'Status']) expect(screen.getByRole('columnheader', { name: column })).toBeInTheDocument()
    await waitFor(() => expect(listEspecies).toHaveBeenCalledWith({ page: 2, pageSize: 100 }))
    expect(listRacas).toHaveBeenCalledWith({ page: 1, pageSize: 100 })
    expect(listVariedades).toHaveBeenCalledWith({ page: 2, pageSize: 100 })
    expect(screen.getByRole('option', { name: 'Equina (inativa)' })).toBeInTheDocument()
    expect(screen.getAllByRole('option', { name: 'Antiga (inativa)' })).toHaveLength(2)
  })

  it('submits all filters, sorting, page size and page navigation to the animal list', async () => {
    vi.mocked(listAnimals).mockResolvedValueOnce({ items: [animal], page: 1, pageSize: 25, totalItems: 30, totalPages: 2 }).mockResolvedValue({ items: [animal], page: 2, pageSize: 50, totalItems: 30, totalPages: 2 })
    renderPage()
    await screen.findByText('AN-000001')
    await waitFor(() => expect(screen.getByLabelText('Buscar por código ou nome')).not.toBeDisabled())
    fireEvent.change(screen.getByLabelText('Buscar por código ou nome'), { target: { value: ' Bela ' } })
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: species.id } })
    fireEvent.change(screen.getByLabelText('Raça'), { target: { value: breed.id } })
    fireEvent.change(screen.getByLabelText('Variedade'), { target: { value: variety.id } })
    fireEvent.change(screen.getByLabelText('Sexo'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Escopo'), { target: { value: '1' } })
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'false' } })
    fireEvent.change(screen.getByLabelText('Ordenar por'), { target: { value: 'createdAtUtc' } })
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'desc' } })
    fireEvent.change(screen.getByLabelText('Registros por página'), { target: { value: '50' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Buscar' })).not.toBeDisabled())
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(listAnimals).toHaveBeenCalledWith({ page: 1, pageSize: 50, search: 'Bela', especieId: species.id, racaId: breed.id, variedadeId: variety.id, sexo: 2, escopo: 1, ativo: false, sortBy: 'createdAtUtc', sortDirection: 'desc' }))
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(listAnimals).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, pageSize: 50 })))
  })

  it('keeps the rendered table after a lifecycle error and prevents a duplicate submission', async () => {
    vi.mocked(setAnimalAtivo).mockReturnValue(new Promise(() => undefined))
    renderPage()
    await screen.findByText('AN-000001')
    const action = screen.getByRole('button', { name: 'Inativar' })
    fireEvent.click(action)
    fireEvent.click(action)
    expect(setAnimalAtivo).toHaveBeenCalledOnce()
    expect(screen.getByText('AN-000001')).toBeInTheDocument()
  })

  it('shows empty, retryable list failure, and keeps catalog failure separate from list data', async () => {
    vi.mocked(listAnimals).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 })
    vi.mocked(listEspecies).mockRejectedValue(new Error('catalog unavailable'))
    renderPage()
    expect(await screen.findByText('Não foi possível carregar os animais.')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText('Nenhum animal encontrado.')).toBeInTheDocument()
    expect(screen.getByText('Não foi possível carregar os catálogos para os filtros.')).toBeInTheDocument()
  })
})
