import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { transferableAbortController } from 'node:util'
import { createMemoryRouter, MemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getAnimalById, listAnimals } from '../animals/services/animalsService'
import type { Animal } from '../animals/types/animals'
import { BreedingFormPage } from './BreedingFormPage'
import { BreedingListPage } from './BreedingListPage'
import { createBreeding, getBreeding, listBreedings, updateBreeding } from './breedingsService'
import type { Breeding } from './types'

vi.mock('../animals/services/animalsService', () => ({ listAnimals: vi.fn(), getAnimalById: vi.fn() }))
vi.mock('./breedingsService', () => ({ listBreedings: vi.fn(), setBreedingStatus: vi.fn(), getBreeding: vi.fn(), createBreeding: vi.fn(), updateBreeding: vi.fn() }))

// Exclusively synthetic: the 101st record overall is not the 101st of either sex.
const animals: Animal[] = Array.from({ length: 204 }, (_, index) => ({
  id: `synthetic-${index + 1}`, codigoInterno: `P1-484-${String(index + 1).padStart(3, '0')}`,
  nome: `Animal de teste ${index + 1}`, sexo: index % 2 === 0 ? 1 : 2,
  especieId: 'synthetic-species', especie: { id: 'synthetic-species', nomeComum: 'Teste', ativo: true },
  racaId: null, variedadeId: null, raca: null, variedade: null, dataNascimento: null,
  escopo: 1, ativo: true, createdAtUtc: '', updatedAtUtc: '',
}))
const male = animals[200], female = animals[201]
const breeding: Breeding = {
  id: 'synthetic-cross', machoId: male.id, femeaId: female.id, status: 1,
  macho: male, femea: female, dataInicio: null, dataFim: null, objetivo: null,
  observacao: null, createdAtUtc: '', updatedAtUtc: '',
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  vi.mocked(listAnimals).mockImplementation(async ({ sexo, search, page = 1, pageSize = 25 } = {}) => {
    const matches = animals.filter(item => (!sexo || item.sexo === sexo) && (!search || `${item.codigoInterno} ${item.nome}`.toLowerCase().includes(search.toLowerCase())))
    return { items: matches.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalItems: matches.length, totalPages: Math.ceil(matches.length / pageSize) }
  })
  vi.mocked(getAnimalById).mockImplementation(async id => {
    const item = animals.find(item => item.id === id)
    if (!item) throw new Error('not found')
    return item
  })
  vi.mocked(listBreedings).mockResolvedValue({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 })
  vi.mocked(getBreeding).mockResolvedValue(breeding)
  vi.mocked(createBreeding).mockResolvedValue(breeding)
  vi.mocked(updateBreeding).mockResolvedValue(breeding)
})
afterEach(() => vi.unstubAllGlobals())

async function chooseBeyond100() {
  for (const [label, item] of [['Macho', male], ['Fêmea', female]] as const) {
    fireEvent.change(screen.getByLabelText(`Buscar ${label.toLocaleLowerCase()} por código ou nome`), { target: { value: item.codigoInterno } })
    const select = screen.getByLabelText(label)
    await within(select).findByRole('option', { name: new RegExp(item.codigoInterno) })
    fireEvent.change(select, { target: { value: item.id } })
    expect(select).toHaveValue(item.id)
  }
}

describe('P1.3 animal selection beyond the first hundred', () => {
  it('finds the 101st animal of each sex in the filters and applies its original ID', async () => {
    // Explicitly prove the API can return what the original pageSize=100 UI omits.
    expect((await listAnimals({ pageSize: 100 })).items).not.toContainEqual(male)
    expect((await listAnimals({ sexo: 1, search: male.codigoInterno, pageSize: 10 })).items).toEqual([male])
    render(<MemoryRouter><BreedingListPage /></MemoryRouter>)
    await screen.findByText('Nenhum cruzamento encontrado.')
    await chooseBeyond100()
    await waitFor(() => expect(listBreedings).toHaveBeenLastCalledWith(expect.objectContaining({ machoId: male.id, femeaId: female.id, page: 1 })))
  })

  it('creates a breeding with the IDs of the 101st male and female', async () => {
    const router = createMemoryRouter([
      { path: '/cruzamentos/novo', element: <BreedingFormPage /> },
      { path: '/cruzamentos/:id', element: <h1>Registro salvo</h1> },
    ], { initialEntries: ['/cruzamentos/novo'] })
    render(<RouterProvider router={router} />)
    await chooseBeyond100()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Registro salvo' })
    expect(createBreeding).toHaveBeenCalledWith(expect.objectContaining({ machoId: male.id, femeaId: female.id }))
  })
})
