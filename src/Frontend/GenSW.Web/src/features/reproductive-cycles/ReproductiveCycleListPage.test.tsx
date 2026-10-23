import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Link, MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createReproductiveCycle, getReproductiveCycle, listReproductiveCycles, setReproductiveCycleStatus, updateReproductiveCycle } from './reproductiveCyclesService'
import { ReproductiveCycleListPage } from './ReproductiveCycleListPage'
import { ReproductiveCycleDetailsPage } from './ReproductiveCycleDetailsPage'
import { ReproductiveCycleFormPage } from './ReproductiveCycleFormPage'
import type { ReproductiveCycle, ReproductiveCyclesPage } from './types'

vi.mock('./reproductiveCyclesService', () => ({ listReproductiveCycles: vi.fn(), setReproductiveCycleStatus: vi.fn(), getReproductiveCycle: vi.fn(), createReproductiveCycle: vi.fn(), updateReproductiveCycle: vi.fn() }))

const cycles: ReproductiveCycle[] = Array.from({ length: 26 }, (_, index) => ({
  id: `cycle-${index + 1}`, cruzamentoId: 'crossing-a', tipo: 1, status: 1,
  dataPostura: null, dataInicioIncubacao: null, dataEclosao: null,
  ovosPostos: null, ovosFerteis: null, ovosIncubados: null, ovosEclodidos: index + 1,
  ovosInviaveis: null, pesoMedioOvoGramas: null, dataInicioGestacao: null,
  dataPrevistaParto: null, dataParto: null, nascidos: null, nascidosVivos: null,
  nascidosMortos: null, pesoAoNascerGramas: null, observacao: null,
  duracaoIncubacaoDias: null, duracaoGestacaoDias: null, taxaFertilidade: null, taxaEclosao: null,
}))

function paged(page = 1, records = cycles): ReproductiveCyclesPage {
  return { items: records.slice((page - 1) * 25, page * 25), page, pageSize: 25, totalItems: records.length, totalPages: Math.ceil(records.length / 25) }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (error: Error) => void
  const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail })
  return { promise, resolve, reject }
}

function LocationState() {
  const location = useLocation()
  return <output data-testid="location">{JSON.stringify({ path: `${location.pathname}${location.search}`, state: location.state })}</output>
}

function ContextLinks() {
  const location = useLocation()
  return <><Link to="/ciclos-reprodutivos?cruzamentoId=crossing-b" state={location.state}>Outro cruzamento</Link><Link to="/ciclos-reprodutivos" state={location.state}>Todos os ciclos</Link></>
}

function renderRoutes(path = '/ciclos-reprodutivos', state?: unknown) {
  return render(<MemoryRouter initialEntries={[{ pathname: path.split('?')[0], search: path.includes('?') ? `?${path.split('?')[1]}` : '', state }]}>
    <Routes>
      <Route path="/ciclos-reprodutivos" element={<><ContextLinks /><ReproductiveCycleListPage /></>} />
      <Route path="/ciclos-reprodutivos/novo" element={<ReproductiveCycleFormPage />} />
      <Route path="/ciclos-reprodutivos/:id" element={<ReproductiveCycleDetailsPage />} />
      <Route path="/ciclos-reprodutivos/:id/editar" element={<ReproductiveCycleFormPage />} />
      <Route path="/cruzamentos/:id" element={<h1>Cruzamento de origem</h1>} />
      <Route path="/proles" element={<h1>Proles do ciclo</h1>} />
    </Routes><LocationState />
  </MemoryRouter>)
}

const currentLocation = () => JSON.parse(screen.getByTestId('location').textContent ?? '{}')

beforeEach(() => {
  vi.mocked(listReproductiveCycles).mockImplementation((params = {}) => Promise.resolve(paged(params.page ?? 1)))
  vi.mocked(setReproductiveCycleStatus).mockResolvedValue({ ...cycles[0], status: 2 })
  vi.mocked(getReproductiveCycle).mockResolvedValue(cycles[25])
  vi.mocked(createReproductiveCycle).mockResolvedValue(cycles[25])
  vi.mocked(updateReproductiveCycle).mockResolvedValue(cycles[25])
})

describe('ReproductiveCycleListPage pagination', () => {
  it('makes the 26th cycle reachable with server metadata and navigation 1 → 2 → 1', async () => {
    render(<MemoryRouter><ReproductiveCycleListPage /></MemoryRouter>)
    expect(await screen.findByText('25 eclodidos')).toBeInTheDocument()
    expect(screen.queryByText('26 eclodidos')).not.toBeInTheDocument()
    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument()
    expect(screen.getByText('26 registros')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('26 eclodidos')).toBeInTheDocument()
    expect(screen.queryByText('25 eclodidos')).not.toBeInTheDocument()
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Anterior' }))
    expect(await screen.findByText('25 eclodidos')).toBeInTheDocument()
    await waitFor(() => expect(listReproductiveCycles).toHaveBeenLastCalledWith({ cruzamentoId: undefined, tipo: undefined, status: undefined, page: 1, pageSize: 25 }))
  })

  it('resets either filter to page one and does not display the previous query while loading', async () => {
    renderRoutes()
    await screen.findByText('25 eclodidos')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('26 eclodidos')
    const pending = deferred<ReproductiveCyclesPage>()
    vi.mocked(listReproductiveCycles).mockReturnValueOnce(pending.promise)
    fireEvent.change(screen.getByLabelText('Fluxo'), { target: { value: '2' } })
    expect(screen.queryByText('26 eclodidos')).not.toBeInTheDocument()
    expect(screen.getByText('Carregando ciclos…')).toHaveAttribute('role', 'status')
    expect(listReproductiveCycles).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, tipo: 2 }))
    await act(async () => pending.resolve(paged(1)))
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('26 eclodidos')
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '2' } })
    await waitFor(() => expect(listReproductiveCycles).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, tipo: 2, status: 2 })))
    await screen.findByText('Página 1 de 2')
  })

  it('ignores late page and filter responses after a newer query succeeds', async () => {
    renderRoutes()
    await screen.findByText('25 eclodidos')
    const oldPage = deferred<ReproductiveCyclesPage>()
    const oldFilter = deferred<ReproductiveCyclesPage>()
    const latest = deferred<ReproductiveCyclesPage>()
    vi.mocked(listReproductiveCycles).mockReturnValueOnce(oldPage.promise).mockReturnValueOnce(oldFilter.promise).mockReturnValueOnce(latest.promise)
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    fireEvent.change(screen.getByLabelText('Fluxo'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '2' } })
    await act(async () => latest.resolve(paged(1, [{ ...cycles[0], tipo: 2, nascidosVivos: 99, status: 2 }])))
    expect(screen.getByText('99 vivos')).toBeInTheDocument()
    await act(async () => { oldPage.resolve(paged(2)); oldFilter.resolve(paged(1)) })
    expect(screen.getByText('99 vivos')).toBeInTheDocument()
    expect(screen.queryByText('26 eclodidos')).not.toBeInTheDocument()
    expect(screen.queryByText('25 eclodidos')).not.toBeInTheDocument()
    expect(screen.getByText('1 registros')).toBeInTheDocument()
  })

  it('keeps page and filters on a retry and distinguishes a failure from a genuinely empty list', async () => {
    renderRoutes()
    await screen.findByText('25 eclodidos')
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '1' } })
    await screen.findByText('Página 1 de 2')
    const failed = deferred<ReproductiveCyclesPage>()
    vi.mocked(listReproductiveCycles).mockReturnValueOnce(failed.promise)
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(screen.queryByText('25 eclodidos')).not.toBeInTheDocument()
    await act(async () => failed.reject(new Error('offline')))
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível carregar os ciclos.')
    expect(screen.queryByText('Nenhum ciclo encontrado.')).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    await screen.findByText('26 eclodidos')
    expect(listReproductiveCycles).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, pageSize: 25, status: 1 }))
    vi.mocked(listReproductiveCycles).mockResolvedValueOnce(paged(1, []))
    fireEvent.change(screen.getByLabelText('Fluxo'), { target: { value: '2' } })
    expect(await screen.findByText('Nenhum ciclo encontrado.')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('0 registros')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('resets the page on a parent context change even if the same route component is reused', async () => {
    renderRoutes('/ciclos-reprodutivos?cruzamentoId=crossing-a')
    await screen.findByText('25 eclodidos')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('link', { name: 'Outro cruzamento' }))
    await waitFor(() => expect(listReproductiveCycles).toHaveBeenLastCalledWith(expect.objectContaining({ cruzamentoId: 'crossing-b', page: 1 })))
    expect(await screen.findByText('Página 1 de 2')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar para Cruzamento' })).toHaveAttribute('href', '/cruzamentos/crossing-b')
    expect(currentLocation().state.list.filters.page).toBe(1)
  })

  it('resets to page one when removing a parent despite carrying its history state', async () => {
    renderRoutes('/ciclos-reprodutivos?cruzamentoId=crossing-a')
    await screen.findByText('25 eclodidos')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('link', { name: 'Todos os ciclos' }))
    await screen.findByText('Página 1 de 2')
    expect(listReproductiveCycles).toHaveBeenLastCalledWith(expect.objectContaining({ cruzamentoId: undefined, page: 1 }))
    expect(screen.queryByRole('link', { name: 'Voltar para Cruzamento' })).not.toBeInTheDocument()
  })

  it('moves focus to the persistent summary before disabling the pagination button', async () => {
    renderRoutes()
    await screen.findByText('25 eclodidos')
    const pending = deferred<ReproductiveCyclesPage>()
    vi.mocked(listReproductiveCycles).mockReturnValueOnce(pending.promise)
    const next = screen.getByRole('button', { name: 'Próxima' })
    const summary = screen.getByRole('group', { name: 'Resumo da paginação de ciclos reprodutivos' })
    next.focus()
    fireEvent.click(next)
    expect(summary).toHaveFocus()
    expect(next).toBeDisabled()
    await act(async () => pending.resolve(paged(2)))
    expect(summary).toHaveFocus()
    expect(screen.getByRole('navigation', { name: 'Paginação de ciclos reprodutivos' })).toBeInTheDocument()
  })

  it('preserves summary focus after a page failure and retry without stealing a later filter focus', async () => {
    renderRoutes()
    await screen.findByText('25 eclodidos')
    const pending = deferred<ReproductiveCyclesPage>()
    const retry = deferred<ReproductiveCyclesPage>()
    vi.mocked(listReproductiveCycles).mockReturnValueOnce(pending.promise).mockReturnValueOnce(retry.promise)
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    const summary = screen.getByRole('group', { name: 'Resumo da paginação de ciclos reprodutivos' })
    await act(async () => pending.reject(new Error('offline')))
    expect(summary).toHaveFocus()
    const retryButton = screen.getByRole('button', { name: 'Tentar novamente' })
    retryButton.focus()
    fireEvent.click(retryButton)
    expect(summary).toHaveFocus()
    const filter = screen.getByLabelText('Status do ciclo')
    filter.focus()
    await act(async () => retry.resolve(paged(2)))
    expect(filter).toHaveFocus()
    expect(summary).toHaveTextContent('Página 2 de 2')
  })

  it.each(['Visualizar', 'Editar'])('restores parent, filters and page when returning from %s', async action => {
    const origin = { list: { path: '/cruzamentos', filters: { page: 3, status: 1 } } }
    renderRoutes('/ciclos-reprodutivos?cruzamentoId=crossing-a', { ...origin, parentReturn: { path: '/cruzamentos/crossing-a', section: 'ciclos', origin } })
    await screen.findByText('25 eclodidos')
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '1' } })
    await screen.findByText('Página 1 de 2')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('link', { name: action }))
    await screen.findByRole('heading', { name: action === 'Visualizar' ? 'Visualizar ciclo reprodutivo' : 'Editar ciclo' })
    if (action === 'Visualizar') {
      expect(await screen.findByText('26')).toBeInTheDocument()
      fireEvent.click(screen.getByRole('link', { name: 'Editar' }))
      await screen.findByRole('heading', { name: 'Editar ciclo' })
      fireEvent.click(screen.getByRole('link', { name: 'Voltar para ciclo reprodutivo' }))
      await screen.findByRole('heading', { name: 'Visualizar ciclo reprodutivo' })
    }
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ciclos reprodutivos' }))
    await screen.findByText('Página 2 de 2')
    expect(screen.getByLabelText('Status do ciclo')).toHaveValue('1')
    expect(currentLocation().path).toBe('/ciclos-reprodutivos?cruzamentoId=crossing-a')
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Cruzamento' }))
    await screen.findByRole('heading', { name: 'Cruzamento de origem' })
    expect(currentLocation().state).toEqual(origin)
  })

  it('keeps creation origin and the nested parent chain when opening the cycle offspring', async () => {
    const origin = { list: { path: '/cruzamentos', filters: { page: 3 } } }
    renderRoutes('/ciclos-reprodutivos?cruzamentoId=crossing-a', { parentReturn: { path: '/cruzamentos/crossing-a', section: 'ciclos', origin } })
    await screen.findByText('25 eclodidos')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('link', { name: 'Novo ciclo' }))
    await screen.findByRole('heading', { name: 'Novo ciclo reprodutivo' })
    expect(currentLocation().state.list.filters.page).toBe(2)
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('link', { name: 'Visualizar' }))
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar proles' }))
    await screen.findByRole('heading', { name: 'Proles do ciclo' })
    expect(currentLocation().state.parentReturn.origin.list.filters.page).toBe(2)
    expect(currentLocation().state.parentReturn.origin.parentReturn.origin).toEqual(origin)
  })

  it.each(['Editar', 'Visualizar'])('keeps the list page, filters and parent after saving an edit from %s', async action => {
    const origin = { list: { path: '/cruzamentos', filters: { page: 3 } } }
    const listPath = '/ciclos-reprodutivos?cruzamentoId=crossing-a'
    renderRoutes(listPath, { list: { path: listPath, filters: { page: 2, st: 1 } }, parentReturn: { path: '/cruzamentos/crossing-a', section: 'ciclos', origin } })
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('link', { name: action }))
    if (action === 'Visualizar') fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
    await screen.findByDisplayValue('26')
    fireEvent.change(screen.getByLabelText('Observação'), { target: { value: 'TEST-PAGINATION' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    if (action === 'Visualizar') {
      await screen.findByRole('heading', { name: 'Visualizar ciclo reprodutivo' })
      fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ciclos reprodutivos' }))
    }
    await screen.findByText('Página 2 de 2')
    expect(screen.getByLabelText('Status do ciclo')).toHaveValue('1')
    expect(currentLocation().path).toBe(listPath)
    expect(currentLocation().state.parentReturn.origin).toEqual(origin)
    expect(updateReproductiveCycle).toHaveBeenCalledWith('cycle-26', expect.objectContaining({ observacao: 'TEST-PAGINATION' }))
  })

  it('keeps the creation origin after saving, viewing the new record and returning to the list', async () => {
    const origin = { list: { path: '/cruzamentos', filters: { page: 3 } } }
    const listPath = '/ciclos-reprodutivos?cruzamentoId=crossing-a'
    renderRoutes(listPath, { list: { path: listPath, filters: { page: 2, st: 1 } }, parentReturn: { path: '/cruzamentos/crossing-a', section: 'ciclos', origin } })
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('link', { name: 'Novo ciclo' }))
    await screen.findByRole('heading', { name: 'Novo ciclo reprodutivo' })
    fireEvent.change(screen.getByLabelText('Observação'), { target: { value: 'TEST-PAGINATION' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Visualizar ciclo reprodutivo' })
    expect(createReproductiveCycle).toHaveBeenCalledWith('crossing-a', expect.objectContaining({ observacao: 'TEST-PAGINATION' }))
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ciclos reprodutivos' }))
    await screen.findByText('Página 2 de 2')
    expect(screen.getByLabelText('Status do ciclo')).toHaveValue('1')
    expect(currentLocation().state.parentReturn.origin).toEqual(origin)
  })

  it.each([25, 0])('reloads and reaches a valid page after completing the last filtered item (%i remaining)', async remaining => {
    let completed = false
    vi.mocked(listReproductiveCycles).mockImplementation((params = {}) => Promise.resolve(paged(params.page ?? 1, completed ? cycles.slice(0, remaining) : cycles)))
    vi.mocked(setReproductiveCycleStatus).mockImplementation(async () => { completed = true; return { ...cycles[25], status: 2 } })
    renderRoutes()
    await screen.findByText('25 eclodidos')
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '1' } })
    await screen.findByText('Página 1 de 2')
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('26 eclodidos')
    fireEvent.click(screen.getByRole('button', { name: 'Concluir' }))
    expect(await screen.findByText(remaining ? '25 eclodidos' : 'Nenhum ciclo encontrado.')).toBeInTheDocument()
    if (remaining) expect(screen.getByText('Página 1 de 1')).toBeInTheDocument()
    else expect(screen.queryByText(/Página \d de \d/)).not.toBeInTheDocument()
    expect(screen.getByText(`${remaining} registros`)).toBeInTheDocument()
    expect(listReproductiveCycles).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, pageSize: 25, status: 1 }))
    expect(setReproductiveCycleStatus).toHaveBeenCalledWith('cycle-26', 2)
  })

  it('ignores an obsolete failure after a newer filter succeeds', async () => {
    const obsolete = deferred<ReproductiveCyclesPage>()
    vi.mocked(listReproductiveCycles).mockReturnValueOnce(obsolete.promise)
    renderRoutes()
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '2' } })
    await screen.findByText('25 eclodidos')
    await act(async () => obsolete.reject(new Error('obsolete error')))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('25 eclodidos')).toBeInTheDocument()
  })

  it('starts a new loading state when a rapid filter round trip returns to the previous query', async () => {
    renderRoutes()
    await screen.findByText('25 eclodidos')
    const intermediate = deferred<ReproductiveCyclesPage>()
    const returning = deferred<ReproductiveCyclesPage>()
    vi.mocked(listReproductiveCycles).mockReturnValueOnce(intermediate.promise).mockReturnValueOnce(returning.promise)
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '' } })
    expect(screen.queryByText('25 eclodidos')).not.toBeInTheDocument()
    expect(screen.getByText('Carregando ciclos…')).toBeInTheDocument()
    await act(async () => returning.resolve(paged(1, [cycles[0]])))
    expect(screen.getByText('1 eclodidos')).toBeInTheDocument()
    expect(screen.queryByText('25 eclodidos')).not.toBeInTheDocument()
    await act(async () => intermediate.resolve(paged(1)))
    expect(screen.queryByText('25 eclodidos')).not.toBeInTheDocument()
  })

  it('keeps a failed status change separate and prevents duplicate completion requests', async () => {
    const mutation = deferred<ReproductiveCycle>()
    vi.mocked(setReproductiveCycleStatus).mockReturnValueOnce(mutation.promise)
    renderRoutes()
    await screen.findByText('25 eclodidos')
    fireEvent.click(screen.getAllByRole('button', { name: 'Concluir' })[0])
    const action = screen.getByRole('button', { name: 'Concluindo…' })
    expect(action).toBeDisabled()
    fireEvent.click(action)
    expect(setReproductiveCycleStatus).toHaveBeenCalledOnce()
    await act(async () => mutation.reject(new Error('status failed')))
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível atualizar o ciclo.')
    expect(screen.getByText('25 eclodidos')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument()
  })
})
