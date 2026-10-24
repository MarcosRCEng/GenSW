import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { transferableAbortController } from 'node:util'
import { createMemoryRouter, Outlet, RouterProvider, useParams } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { UnsavedChangesProvider } from '../../shared/navigation/UnsavedChangesProvider'
import { getAnimalById, listAnimals } from '../animals/services/animalsService'
import type { Animal, AnimalsPage } from '../animals/types/animals'
import { BreedingFormPage } from './BreedingFormPage'
import { createBreeding, getBreeding, updateBreeding } from './breedingsService'
import type { Breeding } from './types'

vi.mock('../animals/services/animalsService', () => ({ getAnimalById: vi.fn(), listAnimals: vi.fn() }))
vi.mock('./breedingsService', () => ({ createBreeding: vi.fn(), getBreeding: vi.fn(), updateBreeding: vi.fn() }))

// Synthetic records are exclusive to these form regressions, never to the pilot.
const animal = (id: string, sexo: 1 | 2, ativo = true): Animal => ({
  id, sexo, ativo, codigoInterno: `FORM-${id}`, nome: `Nome ${id}`,
  especieId: 'species', racaId: null, variedadeId: null, dataNascimento: null,
  escopo: 1, createdAtUtc: '', updatedAtUtc: '',
  especie: { id: 'species', nomeComum: 'Espécie teste', ativo: true }, raca: null, variedade: null,
})
const historicalMale = animal('historical-male', 1, false)
const historicalFemale = animal('historical-female', 2, false)
const candidateMale = animal('candidate-male', 1)
const candidateFemale = animal('candidate-female', 2)
const animals = [historicalMale, historicalFemale, candidateMale, candidateFemale]
const record = (id: string, macho = historicalMale, femea = historicalFemale, objetivo = `Objetivo ${id}`): Breeding => ({
  id, machoId: macho.id, femeaId: femea.id, status: 1, dataInicio: null, dataFim: null,
  objetivo, observacao: null, createdAtUtc: '', updatedAtUtc: '', macho, femea,
})
const candidatePage = (sexo: 1 | 2, page = 1): AnimalsPage => ({
  items: [sexo === 1 ? candidateMale : candidateFemale], page, pageSize: 10, totalItems: 11, totalPages: 2,
})
const emptyPage: AnimalsPage = { items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 }
const origin = { list: { path: '/cruzamentos', filters: { page: 2, status: 1, machoId: historicalMale.id } } }
const showDescriptor = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'showModal')
const closeDescriptor = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'close')

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no })
  return { promise, resolve, reject }
}

function DetailDestination() {
  const { id } = useParams()
  return <h1>Destino do cruzamento {id}</h1>
}

function start(id = 'A', state: unknown = origin) {
  const router = createMemoryRouter([{
    element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>,
    children: [
      { path: '/cruzamentos', element: <h1>Lista de cruzamentos</h1> },
      { path: '/cruzamentos/novo', element: <BreedingFormPage /> },
      { path: '/cruzamentos/:id/editar', element: <BreedingFormPage /> },
      { path: '/cruzamentos/:id', element: <DetailDestination /> },
    ],
  }], { initialEntries: [{ pathname: `/cruzamentos/${id}/editar`, state }] })
  render(<RouterProvider router={router} />)
  return router
}

async function loaded() {
  await screen.findByDisplayValue('Objetivo A')
  await screen.findByRole('option', { name: /FORM-historical-male.*inativo.*selecionado/ })
  await screen.findByRole('option', { name: /FORM-historical-female.*inativo.*selecionado/ })
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
  vi.mocked(listAnimals).mockImplementation(async params => params?.search ? emptyPage : candidatePage(params?.sexo === 2 ? 2 : 1, params?.page))
  vi.mocked(getAnimalById).mockImplementation(async id => {
    const found = animals.find(item => item.id === id)
    if (!found) throw new Error('Unknown synthetic animal')
    return found
  })
  vi.mocked(getBreeding).mockImplementation(async id => record(id))
  vi.mocked(updateBreeding).mockImplementation(async id => record(id))
  vi.mocked(createBreeding).mockResolvedValue(record('created', candidateMale, candidateFemale))
})

afterEach(() => {
  vi.unstubAllGlobals()
  if (showDescriptor) Object.defineProperty(HTMLDialogElement.prototype, 'showModal', showDescriptor)
  else Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal')
  if (closeDescriptor) Object.defineProperty(HTMLDialogElement.prototype, 'close', closeDescriptor)
  else Reflect.deleteProperty(HTMLDialogElement.prototype, 'close')
})

describe('Breeding form selection and navigation state', () => {
  it('searches and pages candidates without changing saved IDs or creating an unsaved draft', async () => {
    const router = start()
    await loaded()
    fireEvent.click(screen.getByRole('button', { name: 'Próximos animais: Macho' }))
    await waitFor(() => expect(listAnimals).toHaveBeenCalledWith(expect.objectContaining({ sexo: 1, page: 2 })))
    fireEvent.change(screen.getByLabelText('Buscar macho por código ou nome'), { target: { value: 'sem correspondência' } })
    fireEvent.change(screen.getByLabelText('Buscar fêmea por código ou nome'), { target: { value: 'outra busca' } })
    await waitFor(() => expect(screen.getAllByText('Nenhum animal encontrado.')).toHaveLength(2))
    expect(screen.getByLabelText('Macho')).toHaveValue(historicalMale.id)
    expect(screen.getByLabelText('Fêmea')).toHaveValue(historicalFemale.id)
    const unload = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(unload)
    expect(unload.defaultPrevented).toBe(false)
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    await screen.findByRole('heading', { name: 'Lista de cruzamentos' })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(router.state.location.state).toEqual(origin)
    expect(updateBreeding).not.toHaveBeenCalled()
  })

  it('protects an explicit choice on Cancel, retains it when continuing, and discards only after confirmation', async () => {
    const router = start()
    await loaded()
    fireEvent.change(screen.getByLabelText('Macho'), { target: { value: candidateMale.id } })
    const unload = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(unload)
    expect(unload.defaultPrevented).toBe(true)
    const cancel = screen.getByRole('link', { name: 'Cancelar' })
    cancel.focus()
    fireEvent.click(cancel)
    const stay = await screen.findByRole('button', { name: 'Continuar editando' })
    expect(stay).toHaveFocus()
    fireEvent.click(stay)
    expect(screen.getByLabelText('Macho')).toHaveValue(candidateMale.id)
    expect(screen.getByLabelText('Fêmea')).toHaveValue(historicalFemale.id)
    expect(router.state.location.pathname).toBe('/cruzamentos/A/editar')
    fireEvent.click(cancel)
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar e sair' }))
    await screen.findByRole('heading', { name: 'Lista de cruzamentos' })
    expect(updateBreeding).not.toHaveBeenCalled()
  })

  it.each([false, true])('saves original historical IDs outside candidate pages even with detail lookup failure=%s', async lookupFails => {
    if (lookupFails) vi.mocked(getAnimalById).mockRejectedValue(new Error('Detail unavailable'))
    const router = start()
    await screen.findByDisplayValue('Objetivo A')
    if (lookupFails) await waitFor(() => expect(screen.getAllByText('Não foi possível carregar o animal selecionado.')).toHaveLength(2))
    else await loaded()
    fireEvent.change(screen.getByLabelText('Buscar macho por código ou nome'), { target: { value: 'fora da página' } })
    await screen.findByText('Nenhum animal encontrado.')
    expect(screen.getByLabelText('Macho')).toHaveValue(historicalMale.id)
    expect(screen.getByLabelText('Fêmea')).toHaveValue(historicalFemale.id)
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Lista de cruzamentos' })
    expect(updateBreeding).toHaveBeenCalledExactlyOnceWith('A', expect.objectContaining({ machoId: historicalMale.id, femeaId: historicalFemale.id }))
    expect(router.state.location.state.list).toEqual(origin.list)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('ignores late record loads across route A → B → A, including the first response for A', async () => {
    const oldA = deferred<Breeding>()
    const oldB = deferred<Breeding>()
    const newestA = deferred<Breeding>()
    vi.mocked(getBreeding).mockReturnValueOnce(oldA.promise).mockReturnValueOnce(oldB.promise).mockReturnValueOnce(newestA.promise)
    const router = start()
    await act(async () => { await router.navigate('/cruzamentos/B/editar') })
    await act(async () => { await router.navigate('/cruzamentos/A/editar') })
    await act(async () => newestA.resolve(record('A', candidateMale, candidateFemale, 'A mais recente')))
    await screen.findByDisplayValue('A mais recente')
    await act(async () => oldB.resolve(record('B', historicalMale, historicalFemale, 'B atrasado')))
    await act(async () => oldA.resolve(record('A', historicalMale, historicalFemale, 'A atrasado')))
    expect(screen.getByLabelText('Objetivo')).toHaveValue('A mais recente')
    expect(screen.getByLabelText('Macho')).toHaveValue(candidateMale.id)
    expect(screen.getByLabelText('Fêmea')).toHaveValue(candidateFemale.id)
    expect(getBreeding).toHaveBeenCalledTimes(3)
  })

  it('retries a failed form load for the same record before exposing a save action', async () => {
    vi.mocked(getBreeding).mockRejectedValueOnce(new Error('Load unavailable')).mockResolvedValueOnce(record('A'))
    start()
    await screen.findByText('Não foi possível carregar o cruzamento.')
    expect(screen.queryByRole('button', { name: 'Salvar' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    await loaded()
    expect(getBreeding).toHaveBeenNthCalledWith(1, 'A')
    expect(getBreeding).toHaveBeenNthCalledWith(2, 'A')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Lista de cruzamentos' })
    expect(updateBreeding).toHaveBeenCalledWith('A', expect.objectContaining({ machoId: historicalMale.id, femeaId: historicalFemale.id }))
  })

  it('lets only the current form navigate after save when an earlier route save finishes late', async () => {
    const previousSave = deferred<Breeding>()
    vi.mocked(updateBreeding).mockReturnValueOnce(previousSave.promise).mockResolvedValueOnce(record('B', candidateMale, candidateFemale))
    vi.mocked(getBreeding).mockImplementation(async id => id === 'B' ? record('B', candidateMale, candidateFemale) : record(id))
    const router = start('A', undefined)
    await loaded()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(updateBreeding).toHaveBeenCalledTimes(1)
    await act(async () => { await router.navigate('/cruzamentos/B/editar') })
    await screen.findByDisplayValue('Objetivo B')
    await act(async () => previousSave.resolve(record('A')))
    expect(router.state.location.pathname).toBe('/cruzamentos/B/editar')
    expect(screen.getByLabelText('Macho')).toHaveValue(candidateMale.id)
    fireEvent.change(screen.getByLabelText('Objetivo'), { target: { value: 'B editado' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Destino do cruzamento B' })
    expect(updateBreeding).toHaveBeenLastCalledWith('B', expect.objectContaining({ machoId: candidateMale.id, femeaId: candidateFemale.id, objetivo: 'B editado' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/cruzamentos/B')
  })
})
