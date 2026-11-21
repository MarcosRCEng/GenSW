import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listRacas } from '../../breeds/services/breedsService'
import { listEspecies } from '../../species/services/speciesService'
import { listVariedades } from '../../varieties/services/varietiesService'
import { getAnimalById, listAnimals } from '../services/animalsService'
import type { Animal } from '../types/animals'
import { CompareAnimalsPage } from './CompareAnimalsPage'

vi.mock('../../species/services/speciesService', () => ({ listEspecies: vi.fn() }))
vi.mock('../../breeds/services/breedsService', () => ({ listRacas: vi.fn() }))
vi.mock('../../varieties/services/varietiesService', () => ({ listVariedades: vi.fn() }))
vi.mock('../services/animalsService', () => ({ getAnimalById: vi.fn(), listAnimals: vi.fn() }))
vi.mock('./AnimalAnalyticsPanel', () => ({
  AnimalAnalyticsPanel: ({ animals }: { animals: readonly Animal[] }) =>
    <div aria-label="Aves em análise" role="region">{animals.map(animal => animal.nome).join(', ')}</div>,
}))

const aveA: Animal = {
  id: 'ave-a', codigoInterno: 'AN-1', nome: 'Ave A', especieId: 'codorna',
  racaId: null, variedadeId: null, sexo: 2, dataNascimento: '2026-10-01',
  escopo: 1, ativo: true, createdAtUtc: '2026-10-01T12:00:00Z', updatedAtUtc: '2026-10-01T12:00:00Z',
  especie: { id: 'codorna', nomeComum: 'Codorna', ativo: true }, raca: null, variedade: null,
}
const aveB: Animal = { ...aveA, id: 'ave-b', codigoInterno: 'AN-2', nome: 'Ave B' }
const aveC: Animal = { ...aveA, id: 'ave-c', codigoInterno: 'AN-3', nome: 'Ave C' }

beforeEach(() => {
  const empty = { items: [], page: 1, pageSize: 100, totalItems: 0, totalPages: 0 }
  vi.mocked(listEspecies).mockResolvedValue({
    ...empty, items: [{ ...aveA.especie, nomeCientifico: null, createdAtUtc: aveA.createdAtUtc, updatedAtUtc: aveA.updatedAtUtc }],
    totalItems: 1, totalPages: 1,
  })
  vi.mocked(listRacas).mockResolvedValue(empty)
  vi.mocked(listVariedades).mockResolvedValue(empty)
  vi.mocked(getAnimalById).mockResolvedValue(aveA)
  vi.mocked(listAnimals).mockImplementation(async ({ page = 1 } = {}) => ({
    items: [page === 1 ? aveB : aveC], page, pageSize: 25, totalItems: 26, totalPages: 2,
  }))
})

describe('CompareAnimalsPage', () => {
  it('keeps selections across pages and taxonomy filters and allows removal from the selected list', async () => {
    render(<MemoryRouter><CompareAnimalsPage /></MemoryRouter>)
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Ave B (AN-2)' }))
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Ave C (AN-3)' }))
    expect(screen.getByRole('region', { name: 'Aves em análise' })).toHaveTextContent('Ave B, Ave C')

    fireEvent.change(screen.getByRole('combobox', { name: 'Espécie' }), { target: { value: 'codorna' } })
    await waitFor(() => expect(listAnimals).toHaveBeenLastCalledWith(expect.objectContaining({ especieId: 'codorna', page: 1 })))
    expect(await screen.findByRole('checkbox', { name: 'Ave B (AN-2)' })).toBeChecked()
    const selected = screen.getByRole('list', { name: 'Aves selecionadas' })
    fireEvent.click(within(selected).getByRole('button', { name: 'Remover Ave C (AN-3)' }))
    expect(screen.queryByRole('region', { name: 'Aves em análise' })).not.toBeInTheDocument()
    expect(screen.getByText(/1 ave selecionada/)).toHaveFocus()
  })

  it('preselects the bird from its record even when that bird is absent from candidate pages', async () => {
    render(<MemoryRouter initialEntries={['/animais/comparar?animalId=ave-a']}><CompareAnimalsPage /></MemoryRouter>)
    expect(screen.getByRole('link', { name: 'Voltar para a ficha' })).toHaveAttribute('href', '/animais/ave-a')
    const selected = await screen.findByRole('list', { name: 'Aves selecionadas' })
    expect(within(selected).getByText('Ave A (AN-1)')).toBeInTheDocument()
    expect(getAnimalById).toHaveBeenCalledWith('ave-a')
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Ave B (AN-2)' }))
    expect(screen.getByRole('region', { name: 'Aves em análise' })).toHaveTextContent('Ave A, Ave B')
  })
})
