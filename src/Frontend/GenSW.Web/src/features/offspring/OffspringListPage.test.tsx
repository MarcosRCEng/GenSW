import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { transferableAbortController } from 'node:util'
import { createMemoryRouter, MemoryRouter, Outlet, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { parentReturnState, readParentReturnState } from '../../shared/details/listNavigation'
import { UnsavedChangesProvider } from '../../shared/navigation/UnsavedChangesProvider'
import { OffspringDetailsPage } from './OffspringDetailsPage'
import { OffspringFormPage } from './OffspringFormPage'
import { OffspringListPage } from './OffspringListPage'
import { createOffspring, getOffspring, listOffspring, updateOffspring } from './offspringService'
import type { Offspring, OffspringPage } from './types'

vi.mock('./offspringService', () => ({ listOffspring: vi.fn(), getOffspring: vi.fn(), createOffspring: vi.fn(), updateOffspring: vi.fn(), splitOffspring: vi.fn(), convertOffspring: vi.fn() }))

// Synthetic fixture for pagination only; no records are added to a running base.
const records: Offspring[] = Array.from({ length: 26 }, (_, index) => ({
  id: `offspring-${index + 1}`, cicloReprodutivoId: 'cycle-1', loteOrigemId: null,
  tipoRegistro: 1, quantidade: index + 1, quantidadeDesdobrada: 0, origem: 1,
  data: '2026-10-22', pesoGramas: null, sexo: 3, condicao: 'TEST-PAGINATION',
  observacao: null, animalId: null, createdAtUtc: '', updatedAtUtc: '',
}))
const pageOf = (page: number): OffspringPage => ({ items: records.slice((page - 1) * 25, page * 25), page, pageSize: 25, totalItems: 26, totalPages: 2 })
const cyclePath = '/proles?cicloReprodutivoId=cycle-1'
const cycleOrigin = { list: { path: '/ciclos-reprodutivos?cruzamentoId=cross-1', filters: { tipo: 1, st: 2, page: 2 } } }
const parentState = parentReturnState(cycleOrigin, '/ciclos-reprodutivos/cycle-1', { section: 'proles' })
const restoredState = { ...parentState, list: { path: cyclePath, filters: { page: 2, pageSize: 25 } } }

function app(path = cyclePath, state?: unknown) {
  const [pathname, search = ''] = path.split('?')
  const router = createMemoryRouter([{ element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [
    { path: '/proles', element: <OffspringListPage /> },
    { path: '/proles/nova', element: <OffspringFormPage /> },
    { path: '/proles/:id', element: <OffspringDetailsPage /> },
    { path: '/proles/:id/editar', element: <OffspringFormPage /> },
    { path: '/ciclos-reprodutivos/:id', element: <h1>Ciclo de origem</h1> },
  ] }], { initialEntries: [{ pathname, search: search ? `?${search}` : '', state }] })
  render(<RouterProvider router={router} />)
  return router
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: Error) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  vi.mocked(listOffspring).mockImplementation(async (query = {}) => pageOf(query.page ?? 1))
  vi.mocked(getOffspring).mockResolvedValue(records[25])
  vi.mocked(updateOffspring).mockResolvedValue(records[25])
  vi.mocked(createOffspring).mockResolvedValue(records[25])
})
afterEach(() => vi.unstubAllGlobals())

describe('OffspringListPage pagination', () => {
  it('reaches the 26th record through server pages 1 → 2 → 1', async () => {
    render(<MemoryRouter><OffspringListPage /></MemoryRouter>)
    await screen.findByText('25')
    expect(screen.queryByText('26')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('26')).toBeInTheDocument()
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    expect(screen.getByText('26 registros')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
    expect(listOffspring).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, pageSize: 25 }))
    fireEvent.click(screen.getByRole('button', { name: 'Anterior' }))
    await waitFor(() => expect(screen.getByText('Página 1 de 2')).toBeInTheDocument())
    expect(screen.getByText('25')).toBeInTheDocument()
    expect(listOffspring).toHaveBeenCalledTimes(3)
  })

  it('clears old rows during a new page, distinguishes error from empty and retries the same query', async () => {
    const pending = deferred<OffspringPage>()
    const retry = deferred<OffspringPage>()
    vi.mocked(listOffspring).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(pending.promise).mockReturnValueOnce(retry.promise)
    app()
    await screen.findByText('25')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(screen.getByRole('status')).toHaveTextContent('Carregando proles')
    expect(screen.queryByText('25')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
    await act(async () => pending.reject(new Error('offline')))
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível carregar as proles.')
    expect(screen.queryByText('Nenhuma prole encontrada.')).not.toBeInTheDocument()
    expect(screen.queryByText('25')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(listOffspring).toHaveBeenLastCalledWith({ cicloReprodutivoId: 'cycle-1', page: 2, pageSize: 25 })
    await act(async () => retry.resolve(pageOf(2)))
    expect(screen.getByText('26')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('renders a genuinely empty server page with disabled limits', async () => {
    vi.mocked(listOffspring).mockResolvedValue({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 })
    app('/proles')
    await screen.findByText('Nenhuma prole encontrada.')
    expect(screen.getByText('0 registros')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Registrar prole' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('uses server totals to recover a saved page that is no longer valid', async () => {
    vi.mocked(listOffspring).mockImplementation(async (query = {}) => ({ items: query.page === 1 ? [records[0]] : [], page: query.page ?? 1, pageSize: 25, totalItems: 1, totalPages: 1 }))
    app(cyclePath, { ...restoredState, list: { path: cyclePath, filters: { page: 3 } } })
    await screen.findByText('Página 1 de 1')
    expect(listOffspring).toHaveBeenNthCalledWith(1, { cicloReprodutivoId: 'cycle-1', page: 3, pageSize: 25 })
    expect(listOffspring).toHaveBeenLastCalledWith({ cicloReprodutivoId: 'cycle-1', page: 1, pageSize: 25 })
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('resets to page one when a reused route changes parent and ignores late responses', async () => {
    const oldPage = deferred<OffspringPage>()
    const newParent = deferred<OffspringPage>()
    vi.mocked(listOffspring).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(oldPage.promise).mockReturnValueOnce(newParent.promise)
    const router = app()
    await screen.findByText('25')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await act(async () => { await router.navigate('/proles?cicloReprodutivoId=cycle-2', { state: restoredState }) })
    expect(listOffspring).toHaveBeenLastCalledWith({ cicloReprodutivoId: 'cycle-2', page: 1, pageSize: 25 })
    expect(screen.queryByText('25')).not.toBeInTheDocument()
    await act(async () => newParent.resolve({ items: [{ ...records[0], quantidade: 77 }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 }))
    expect(screen.getByText('77')).toBeInTheDocument()
    await act(async () => oldPage.resolve(pageOf(2)))
    expect(screen.queryByText('26')).not.toBeInTheDocument()
    expect(screen.getByText('77')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 1')).toBeInTheDocument()
  })

  it('ignores an error from a previous parent query after the new parent succeeds', async () => {
    const oldParent = deferred<OffspringPage>()
    vi.mocked(listOffspring).mockReturnValueOnce(oldParent.promise).mockResolvedValue(pageOf(1))
    const router = app()
    await act(async () => { await router.navigate('/proles?cicloReprodutivoId=cycle-2') })
    await screen.findByText('25')
    await act(async () => oldParent.reject(new Error('late error')))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('25')).toBeInTheDocument()
  })

  it('keeps A → B → A loading until the newest A query finishes', async () => {
    const parentB = deferred<OffspringPage>()
    const newestA = deferred<OffspringPage>()
    vi.mocked(listOffspring).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(parentB.promise).mockReturnValueOnce(newestA.promise)
    const router = app()
    await screen.findByText('25')
    await act(async () => { await router.navigate('/proles?cicloReprodutivoId=cycle-2') })
    await act(async () => { await router.navigate(cyclePath) })
    expect(screen.getByRole('status')).toHaveTextContent('Carregando proles')
    expect(screen.queryByText('25')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
    await act(async () => parentB.resolve(pageOf(2)))
    expect(screen.queryByText('26')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Carregando proles')
    await act(async () => newestA.resolve({ ...pageOf(1), items: [{ ...records[0], quantidade: 88 }] }))
    expect(screen.getByText('88')).toBeInTheDocument()
    expect(screen.queryByText('25')).not.toBeInTheDocument()
  })

  it('resets to page one when the parent context is removed despite carried history state', async () => {
    const router = app(cyclePath, restoredState)
    await screen.findByText('Página 2 de 2')
    await act(async () => { await router.navigate('/proles', { state: restoredState }) })
    await screen.findByText('Página 1 de 2')
    expect(listOffspring).toHaveBeenLastCalledWith({ cicloReprodutivoId: undefined, page: 1, pageSize: 25 })
    expect(screen.queryByRole('link', { name: 'Voltar para Ciclo reprodutivo' })).not.toBeInTheDocument()
  })

  it('moves focus to the persistent summary before disabling the pagination button', async () => {
    const pending = deferred<OffspringPage>()
    vi.mocked(listOffspring).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(pending.promise)
    app()
    await screen.findByText('Página 1 de 2')
    const next = screen.getByRole('button', { name: 'Próxima' })
    const summary = screen.getByRole('group', { name: 'Resumo da paginação de proles' })
    next.focus()
    fireEvent.click(next)
    expect(summary).toHaveFocus()
    expect(next).toBeDisabled()
    await act(async () => pending.resolve(pageOf(2)))
    expect(summary).toHaveFocus()
    expect(screen.getByRole('navigation', { name: 'Paginação de proles' })).toBeInTheDocument()
  })

  it('keeps summary focus after a page failure and retry without stealing a later parent link focus', async () => {
    const pending = deferred<OffspringPage>()
    const retry = deferred<OffspringPage>()
    vi.mocked(listOffspring).mockResolvedValueOnce(pageOf(1)).mockReturnValueOnce(pending.promise).mockReturnValueOnce(retry.promise)
    app()
    await screen.findByText('Página 1 de 2')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    const summary = screen.getByRole('group', { name: 'Resumo da paginação de proles' })
    await act(async () => pending.reject(new Error('offline')))
    expect(summary).toHaveFocus()
    const retryButton = screen.getByRole('button', { name: 'Tentar novamente' })
    retryButton.focus()
    fireEvent.click(retryButton)
    expect(summary).toHaveFocus()
    const parent = screen.getByRole('link', { name: 'Voltar para Ciclo reprodutivo' })
    parent.focus()
    await act(async () => retry.resolve(pageOf(2)))
    expect(parent).toHaveFocus()
    expect(summary).toHaveTextContent('Página 2 de 2')
  })

  it('keeps page and parent origin through detail, detail edit cancellation and parent return', async () => {
    const router = app(cyclePath, restoredState)
    await screen.findByText('Página 2 de 2')
    fireEvent.click(screen.getByRole('link', { name: 'Visualizar' }))
    fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
    await screen.findByDisplayValue('TEST-PAGINATION')
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    await screen.findByRole('heading', { name: 'Visualizar prole' })
    expect(router.state.location.pathname).toBe('/proles/offspring-26')
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Proles' }))
    await screen.findByText('Página 2 de 2')
    expect(router.state.location.search).toBe('?cicloReprodutivoId=cycle-1')
    expect(readParentReturnState(router.state.location.state, '/ciclos-reprodutivos/cycle-1')?.parentReturn.origin).toEqual(cycleOrigin)
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ciclo reprodutivo' }))
    await screen.findByRole('heading', { name: 'Ciclo de origem' })
    expect(router.state.location.state).toEqual(cycleOrigin)
  })

  it('saves list editing back to the same page and cycle', async () => {
    const router = app(cyclePath, restoredState)
    await screen.findByText('Página 2 de 2')
    fireEvent.click(screen.getByRole('link', { name: 'Editar' }))
    await screen.findByDisplayValue('TEST-PAGINATION')
    fireEvent.change(screen.getByLabelText('Condição'), { target: { value: 'TEST-UPDATED' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByText('Página 2 de 2')
    expect(router.state.location.search).toBe('?cicloReprodutivoId=cycle-1')
    expect(updateOffspring).toHaveBeenCalledWith('offspring-26', expect.objectContaining({ condicao: 'TEST-UPDATED' }))
    expect(readParentReturnState(router.state.location.state, '/ciclos-reprodutivos/cycle-1')?.parentReturn.origin).toEqual(cycleOrigin)
  })

  it('keeps creation origin through cancel and a saved detail return', async () => {
    const router = app(cyclePath, restoredState)
    await screen.findByText('Página 2 de 2')
    fireEvent.click(screen.getByRole('link', { name: 'Registrar prole' }))
    await screen.findByRole('heading', { name: 'Registrar prole' })
    expect(router.state.location.search).toBe('?cicloReprodutivoId=cycle-1')
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    await screen.findByText('Página 2 de 2')
    fireEvent.click(screen.getByRole('link', { name: 'Registrar prole' }))
    await screen.findByRole('heading', { name: 'Registrar prole' })
    fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2026-10-22' } })
    fireEvent.change(screen.getByLabelText('Condição'), { target: { value: 'TEST-PAGINATION' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Visualizar prole' })
    expect(createOffspring).toHaveBeenCalledWith('cycle-1', expect.objectContaining({ condicao: 'TEST-PAGINATION' }))
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Proles' }))
    await screen.findByText('Página 2 de 2')
    expect(readParentReturnState(router.state.location.state, '/ciclos-reprodutivos/cycle-1')?.parentReturn.origin).toEqual(cycleOrigin)
  })
})
