import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { transferableAbortController } from 'node:util'
import { createMemoryRouter, MemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getAnimalById, listAnimals } from '../animals/services/animalsService'
import type { Animal } from '../animals/types/animals'
import { BreedingDetailsPage } from './BreedingDetailsPage'
import { BreedingFormPage } from './BreedingFormPage'
import { BreedingListPage } from './BreedingListPage'
import { createBreeding, getBreeding, listBreedings, setBreedingStatus, updateBreeding } from './breedingsService'
import type { Breeding, BreedingsPage } from './types'

vi.mock('../animals/services/animalsService', () => ({ listAnimals: vi.fn(), getAnimalById: vi.fn() }))
vi.mock('./breedingsService', () => ({ listBreedings: vi.fn(), setBreedingStatus: vi.fn(), getBreeding: vi.fn(), createBreeding: vi.fn(), updateBreeding: vi.fn() }))

// Synthetic records belong exclusively to these regressions, never to the pilot.
const records: Breeding[] = Array.from({ length: 26 }, (_, index) => ({
  id: `cross-${index + 1}`, machoId: 'male', femeaId: 'female', status: 1,
  dataInicio: null, dataFim: null, objetivo: null, observacao: null,
  createdAtUtc: '', updatedAtUtc: '',
  macho: { id: 'male', codigoInterno: `TEST-M-${String(index + 1).padStart(2, '0')}`, nome: null },
  femea: { id: 'female', codigoInterno: `TEST-F-${String(index + 1).padStart(2, '0')}`, nome: null },
}))
const pageOf = (page: number): BreedingsPage => ({ items: records.slice((page - 1) * 25, page * 25), page, pageSize: 25, totalItems: 26, totalPages: 2 })
const emptyPage = (page = 1): BreedingsPage => ({ items: [], page, pageSize: 25, totalItems: 0, totalPages: 0 })
const animal = (id: string, sexo: 1 | 2): Animal => ({ id, sexo, codigoInterno: `FILTER-${id}`, nome: null, especieId: 'species', racaId: null, variedadeId: null, dataNascimento: null, escopo: 1, ativo: true, createdAtUtc: '', updatedAtUtc: '', especie: { id: 'species', nomeComum: 'Teste', ativo: true }, raca: null, variedade: null })
const origin = { list: { path: '/cruzamentos', filters: { page: 2, status: 1, machoId: 'male', femeaId: 'female' } } }
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no })
  return { promise, resolve, reject }
}
function renderPage(state?: unknown) { render(<MemoryRouter initialEntries={[{ pathname: '/cruzamentos', state }]}><BreedingListPage /></MemoryRouter>) }
function app(state: unknown = origin) {
  const router = createMemoryRouter([
    { path: '/cruzamentos', element: <BreedingListPage /> },
    { path: '/cruzamentos/novo', element: <BreedingFormPage /> },
    { path: '/cruzamentos/:id', element: <BreedingDetailsPage /> },
    { path: '/cruzamentos/:id/editar', element: <BreedingFormPage /> },
    { path: '/ciclos-reprodutivos', element: <h1>Ciclos filtrados</h1> },
  ], { initialEntries: [{ pathname: '/cruzamentos', state }] })
  render(<RouterProvider router={router} />)
  return router
}
async function expectRestoredPage() {
  await screen.findByText('Página 2 de 2')
  expect(screen.getByLabelText('Status')).toHaveValue('1')
  expect(screen.getByLabelText('Macho')).toHaveValue('male')
  expect(screen.getByLabelText('Fêmea')).toHaveValue('female')
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  vi.mocked(listAnimals).mockImplementation(async ({ sexo, page = 1, pageSize = 10 } = {}) => ({ items: [animal('male', 1), animal('female', 2)].filter(item => !sexo || item.sexo === sexo), page, pageSize, totalItems: 1, totalPages: 1 }))
  vi.mocked(getAnimalById).mockImplementation(async id => animal(id, id === 'female' ? 2 : 1))
  vi.mocked(listBreedings).mockImplementation(async ({ page = 1 } = {}) => pageOf(page))
  vi.mocked(setBreedingStatus).mockResolvedValue({ ...records[25], status: 2 })
  vi.mocked(getBreeding).mockResolvedValue(records[25])
  vi.mocked(updateBreeding).mockResolvedValue(records[25])
  vi.mocked(createBreeding).mockResolvedValue(records[25])
})
afterEach(() => vi.unstubAllGlobals())

describe('BreedingListPage pagination', () => {
  it('reaches the 26th record through server pages 1 → 2 → 1', async () => {
    renderPage()
    await screen.findByText('TEST-M-01')
    expect(screen.queryByText('TEST-M-26')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('TEST-M-26')).toBeInTheDocument()
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    expect(screen.getByText('26 registros')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
    expect(listBreedings).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, pageSize: 25 }))
    expect(screen.getByRole('navigation', { name: 'Paginação de cruzamentos' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Anterior' }))
    await waitFor(() => expect(screen.getByText('Página 1 de 2')).toBeInTheDocument())
    expect(screen.getByText('TEST-M-01')).toBeInTheDocument()
    expect(listBreedings).toHaveBeenCalledTimes(3)
  })

  it('moves focus to the persistent summary before disabling the pagination button', async () => {
    const pending = deferred<BreedingsPage>()
    vi.mocked(listBreedings).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(pending.promise)
    renderPage()
    await screen.findByText('Página 1 de 2')
    const next = screen.getByRole('button', { name: 'Próxima' })
    const summary = screen.getByRole('group', { name: 'Resumo da paginação de cruzamentos' })
    next.focus()
    fireEvent.click(next)
    expect(summary).toHaveFocus()
    expect(next).toBeDisabled()
    await act(async () => pending.resolve(pageOf(2)))
    expect(summary).toHaveFocus()
    expect(summary).toHaveTextContent('Página 2 de 2')
  })

  it('keeps summary focus after a page failure and retry without stealing a later filter focus', async () => {
    const pending = deferred<BreedingsPage>()
    const retry = deferred<BreedingsPage>()
    vi.mocked(listBreedings).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(pending.promise).mockReturnValueOnce(retry.promise)
    renderPage()
    await screen.findByText('Página 1 de 2')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    const summary = screen.getByRole('group', { name: 'Resumo da paginação de cruzamentos' })
    await act(async () => pending.reject(new Error('offline')))
    expect(summary).toHaveFocus()
    const retryButton = screen.getByRole('button', { name: 'Tentar novamente' })
    retryButton.focus()
    fireEvent.click(retryButton)
    expect(summary).toHaveFocus()
    const filter = screen.getByLabelText('Status')
    filter.focus()
    await act(async () => retry.resolve(pageOf(2)))
    expect(filter).toHaveFocus()
    expect(summary).toHaveTextContent('Página 2 de 2')
  })

  it.each([
    ['Status', '2', { status: 2 }],
    ['Macho', '', { machoId: undefined }],
    ['Fêmea', '', { femeaId: undefined }],
  ])('resets page 2 on changing %s while preserving other filters', async (filter, value, expected) => {
    renderPage(origin)
    await expectRestoredPage()
    fireEvent.change(screen.getByLabelText(filter), { target: { value } })
    await screen.findByText('Página 1 de 2')
    expect(listBreedings).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, status: 1, machoId: 'male', femeaId: 'female', ...expected })
  })

  it('hides old rows during page loading and failure, retrying the same page and filters', async () => {
    const pending = deferred<BreedingsPage>()
    vi.mocked(listBreedings).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(pending.promise).mockResolvedValueOnce(pageOf(2))
    renderPage({ list: { path: '/cruzamentos', filters: { status: 2, machoId: 'male' } } })
    await screen.findByText('TEST-M-01')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(screen.getByText('Carregando cruzamentos…')).toHaveAttribute('role', 'status')
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
    expect(screen.queryByText('TEST-M-01')).not.toBeInTheDocument()
    await act(async () => pending.reject(new Error('unavailable')))
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível carregar os cruzamentos.')
    expect(screen.queryByText('Nenhum cruzamento encontrado.')).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    await screen.findByText('TEST-M-26')
    expect(listBreedings).toHaveBeenLastCalledWith({ page: 2, pageSize: 25, status: 2, machoId: 'male', femeaId: undefined })
  })

  it('ignores an older page rejection after a newer filter succeeds', async () => {
    const pendingPage = deferred<BreedingsPage>()
    vi.mocked(listBreedings).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(pendingPage.promise).mockResolvedValueOnce({ ...pageOf(1), items: [records[25]] })
    renderPage()
    await screen.findByText('TEST-M-01')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: '2' } })
    await screen.findByText('TEST-M-26')
    await act(async () => pendingPage.reject(new Error('old request')))
    expect(screen.getByText('TEST-M-26')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument()
  })

  it('ignores older filter results and their finalization while the newest filter loads', async () => {
    const older = deferred<BreedingsPage>()
    const newest = deferred<BreedingsPage>()
    vi.mocked(listBreedings).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(older.promise).mockReturnValueOnce(newest.promise)
    renderPage()
    await screen.findByText('TEST-M-01')
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: '1' } })
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: '2' } })
    await act(async () => older.resolve(pageOf(1)))
    expect(screen.getByText('Carregando cruzamentos…')).toHaveAttribute('role', 'status')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    await act(async () => newest.resolve({ ...pageOf(1), items: [records[25]] }))
    expect(screen.getByText('TEST-M-26')).toBeInTheDocument()
    expect(screen.queryByText('TEST-M-01')).not.toBeInTheDocument()
  })

  it('shows genuine empty results with no invalid page navigation', async () => {
    vi.mocked(listBreedings).mockResolvedValue(emptyPage())
    renderPage()
    await screen.findByText('Nenhum cruzamento encontrado.')
    expect(screen.getByText('0 registros')).toBeInTheDocument()
    expect(within(screen.getByRole('navigation', { name: 'Paginação de cruzamentos' })).queryByText(/Página/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
    expect(listBreedings).toHaveBeenCalledOnce()
  })

  it('starts a fresh generation when a filter returns A → B → A with B still pending', async () => {
    const older = deferred<BreedingsPage>()
    const newest = deferred<BreedingsPage>()
    vi.mocked(listBreedings).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(older.promise).mockReturnValueOnce(newest.promise)
    renderPage()
    await screen.findByText('TEST-M-01')
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: '' } })
    expect(screen.queryByText('TEST-M-01')).not.toBeInTheDocument()
    expect(screen.getByText('Carregando cruzamentos…')).toHaveAttribute('role', 'status')
    await act(async () => newest.resolve({ ...pageOf(1), items: [records[25]] }))
    await act(async () => older.resolve(pageOf(1)))
    expect(screen.getByText('TEST-M-26')).toBeInTheDocument()
    expect(screen.queryByText('TEST-M-01')).not.toBeInTheDocument()
    expect(listBreedings).toHaveBeenCalledTimes(3)
  })

  it.each([1, 0])('recovers the last valid page after status removes its only row (totalPages=%s)', async totalPages => {
    vi.mocked(listBreedings).mockResolvedValueOnce(pageOf(2))
      .mockResolvedValueOnce({ items: [], page: 2, pageSize: 25, totalItems: totalPages ? 25 : 0, totalPages })
      .mockResolvedValueOnce(totalPages ? { ...pageOf(1), totalItems: 25, totalPages: 1 } : emptyPage())
    renderPage(origin)
    await screen.findByText('TEST-M-26')
    fireEvent.click(screen.getByRole('button', { name: 'Iniciar' }))
    await waitFor(() => expect(listBreedings).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, status: 1, machoId: 'male', femeaId: 'female' }))
    await screen.findByText(totalPages ? 'TEST-M-01' : 'Nenhum cruzamento encontrado.')
    expect(screen.queryByText('Página 2 de 1')).not.toBeInTheDocument()
    expect(listBreedings).toHaveBeenCalledTimes(3)
  })

  it('prevents duplicate status mutation and keeps mutation failure distinct from query failure', async () => {
    const mutation = deferred<Breeding>()
    vi.mocked(setBreedingStatus).mockReturnValue(mutation.promise)
    renderPage(origin)
    await screen.findByText('TEST-M-26')
    const button = screen.getByRole('button', { name: 'Iniciar' })
    fireEvent.click(button)
    fireEvent.click(button)
    expect(setBreedingStatus).toHaveBeenCalledOnce()
    expect(button).toBeDisabled()
    await act(async () => mutation.reject(new Error('cannot update')))
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível alterar o status do cruzamento.')
    expect(screen.getByText('TEST-M-26')).toBeInTheDocument()
    expect(button).toBeEnabled()
    expect(listBreedings).toHaveBeenCalledOnce()
  })

  it('keeps catalog failure separate from successful paginated results', async () => {
    vi.mocked(listAnimals).mockRejectedValue(new Error('catalog failure'))
    renderPage()
    await screen.findByText('TEST-M-01')
    expect(screen.getAllByRole('alert')).toHaveLength(2)
    expect(screen.getByRole('button', { name: 'Tentar novamente: Macho' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeEnabled()
  })
})

describe('BreedingListPage navigation', () => {
  it('restores page and all filters through details, detail editing and cancel', async () => {
    app()
    await expectRestoredPage()
    fireEvent.click(screen.getByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: 'Visualizar cruzamento' })
    fireEvent.click(screen.getByRole('link', { name: 'Editar' }))
    await screen.findByRole('button', { name: 'Salvar' })
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    await screen.findByRole('heading', { name: 'Visualizar cruzamento' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Cruzamentos' }))
    await expectRestoredPage()
  })

  it('restores page and filters after saving list editing', async () => {
    const router = app()
    await expectRestoredPage()
    fireEvent.click(screen.getByRole('link', { name: 'Editar' }))
    await screen.findByDisplayValue('Planejado')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await expectRestoredPage()
    expect(router.state.location.pathname).toBe('/cruzamentos')
    expect(updateBreeding).toHaveBeenCalledWith('cross-26', expect.objectContaining({ machoId: 'male', femeaId: 'female' }))
  })

  it('preserves creation origin through cancel and successful creation detail', async () => {
    app()
    await expectRestoredPage()
    fireEvent.click(screen.getByRole('link', { name: 'Novo cruzamento' }))
    await screen.findByRole('heading', { name: 'Novo cruzamento' })
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    await expectRestoredPage()
    fireEvent.click(screen.getByRole('link', { name: 'Novo cruzamento' }))
    await screen.findByRole('option', { name: 'FILTER-male' })
    fireEvent.change(screen.getByLabelText('Macho'), { target: { value: 'male' } })
    fireEvent.change(screen.getByLabelText('Fêmea'), { target: { value: 'female' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Visualizar cruzamento' })
    expect(createBreeding).toHaveBeenCalledOnce()
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Cruzamentos' }))
    await expectRestoredPage()
  })

  it('passes the paginated origin when opening cycles from details', async () => {
    const router = app()
    await expectRestoredPage()
    fireEvent.click(screen.getByRole('link', { name: 'Visualizar' }))
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar ciclos reprodutivos' }))
    await screen.findByRole('heading', { name: 'Ciclos filtrados' })
    expect(router.state.location.search).toBe('?cruzamentoId=cross-26')
    expect(router.state.location.state.parentReturn.origin).toEqual(origin)
  })

  it('passes the paginated origin when opening cycles from editing', async () => {
    const router = app()
    await expectRestoredPage()
    fireEvent.click(screen.getByRole('link', { name: 'Editar' }))
    fireEvent.click(await screen.findByRole('link', { name: 'Ciclos e Proles' }))
    await screen.findByRole('heading', { name: 'Ciclos filtrados' })
    expect(router.state.location.search).toBe('?cruzamentoId=cross-26')
    expect(router.state.location.state.parentReturn.origin).toEqual(origin)
  })
})
