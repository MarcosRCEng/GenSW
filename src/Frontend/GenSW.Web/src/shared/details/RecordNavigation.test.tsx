import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { transferableAbortController } from 'node:util'
import { createMemoryRouter, Link, Outlet, RouterProvider, useLocation } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PropertiesListPage } from '../../features/properties/PropertiesListPage'
import { PropertyDetailsPage } from '../../features/properties/PropertyDetailsPage'
import { PropertyFormPage } from '../../features/properties/PropertyFormPage'
import { createPropriedade, getPropriedadeById, listPropriedades, updatePropriedade } from '../../features/properties/propertiesService'
import { WeightsPanel } from '../../features/animals/evolution/WeightsPanel'
import { api } from '../../features/animals/evolution/api'
import { AnimalRelatedDetailsPage } from '../../features/animals/pages/AnimalRelatedDetailsPage'
import { BreedingDetailsPage } from '../../features/breedings/BreedingDetailsPage'
import { getBreeding } from '../../features/breedings/breedingsService'
import { ReproductiveCycleDetailsPage } from '../../features/reproductive-cycles/ReproductiveCycleDetailsPage'
import { ReproductiveCycleListPage } from '../../features/reproductive-cycles/ReproductiveCycleListPage'
import { getReproductiveCycle, listReproductiveCycles } from '../../features/reproductive-cycles/reproductiveCyclesService'
import { OffspringListPage } from '../../features/offspring/OffspringListPage'
import { listOffspring } from '../../features/offspring/offspringService'
import { UnsavedChangesProvider } from '../navigation/UnsavedChangesProvider'
import { formReturnTarget, listReturnState, parentReturnState, readParentReturnState, recordEditState, useRestoreParentSection } from './listNavigation'

vi.mock('../../features/properties/propertiesService', () => ({ createPropriedade: vi.fn(), getPropriedadeById: vi.fn(), listPropriedades: vi.fn(), updatePropriedade: vi.fn(), setPropriedadeAtivo: vi.fn() }))
vi.mock('../../features/animals/evolution/api', () => ({ api: vi.fn(), save: vi.fn(), message: () => 'Falha' }))
vi.mock('../../features/breedings/breedingsService', () => ({ getBreeding: vi.fn() }))
vi.mock('../../features/reproductive-cycles/reproductiveCyclesService', () => ({ getReproductiveCycle: vi.fn(), listReproductiveCycles: vi.fn(), setReproductiveCycleStatus: vi.fn() }))
vi.mock('../../features/offspring/offspringService', () => ({ listOffspring: vi.fn() }))
const property = { id: 'property-1', nome: 'Unidade Norte', localizacao: null, observacao: null, ativo: true, createdAtUtc: '2026-10-11T00:00:00Z', updatedAtUtc: '2026-10-11T00:00:00Z' }
const weight = { id: 'weight-1', dataMedicao: '2026-10-10', pesoGramas: 500, tipoMarco: 1, descricaoMarco: null, idadeReferenciaDias: null, idadeDiasNaMedicao: null, observacao: null }
const origin = { list: { path: '/propriedades', filters: { search: 'Norte', searchDraft: 'Norte', page: 2, pageSize: 25, ativo: 'true', sortBy: 'nome', sortDirection: 'asc' } } }
afterEach(() => vi.unstubAllGlobals())

function WeightParent() {
  const location = useLocation()
  useRestoreParentSection('/animais/animal-1', true)
  const state = listReturnState(location.state, '/animais')
  return <><Link to="/animais" state={state}>Voltar para Animais</Link><WeightsPanel animalId="animal-1" birth={null} readOnly /></>
}

function app(path = '/propriedades', state?: unknown) {
  const router = createMemoryRouter([{ element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [
    { path: '/propriedades', element: <PropertiesListPage /> },
    { path: '/propriedades/nova', element: <PropertyFormPage /> },
    { path: '/propriedades/:id', element: <PropertyDetailsPage /> },
    { path: '/propriedades/:id/editar', element: <PropertyFormPage /> },
    { path: '/animais/animal-1', element: <WeightParent /> },
    { path: '/animais/:animalId/pesagens/:recordId', element: <AnimalRelatedDetailsPage section="pesagens" /> },
    { path: '/animais', element: <p>Lista de animais</p> },
    { path: '/cruzamentos', element: <h1>Cruzamentos listados</h1> },
    { path: '/cruzamentos/:id', element: <BreedingDetailsPage /> },
    { path: '/ciclos-reprodutivos', element: <ReproductiveCycleListPage /> },
    { path: '/ciclos-reprodutivos/:id', element: <ReproductiveCycleDetailsPage /> },
    { path: '/proles', element: <OffspringListPage /> },
  ] }], { initialEntries: [{ pathname: path, state }] })
  render(<RouterProvider router={router} />)
  return router
}

beforeEach(() => {
  vi.resetAllMocks()
  // Router Request and AbortController must share Node's realm in JSDOM.
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  vi.mocked(getPropriedadeById).mockResolvedValue(property)
  vi.mocked(createPropriedade).mockResolvedValue(property)
  vi.mocked(updatePropriedade).mockResolvedValue(property)
  vi.mocked(listPropriedades).mockImplementation(async (query = {}) => ({ items: [property], page: query.page ?? 1, pageSize: 25, totalItems: 26, totalPages: 2 }))
  vi.mocked(api).mockImplementation(async (url) => url.endsWith('/weight-1') ? weight : { items: [weight], page: 2, pageSize: 10, totalItems: 11, totalPages: 2 })
  vi.mocked(getBreeding).mockResolvedValue({ id: 'cross-1', machoId: 'male', femeaId: 'female', status: 1, dataInicio: null, dataFim: null, objetivo: null, observacao: null, createdAtUtc: '', updatedAtUtc: '', macho: { id: 'male', codigoInterno: 'PAI-1', nome: null }, femea: { id: 'female', codigoInterno: 'MAE-1', nome: null } })
  const cycle = { id: 'cycle-1', cruzamentoId: 'cross-1', tipo: 1 as const, status: 1 as const, dataPostura: null, dataInicioIncubacao: null, dataEclosao: null, ovosPostos: null, ovosFerteis: null, ovosIncubados: null, ovosEclodidos: null, ovosInviaveis: null, pesoMedioOvoGramas: null, dataInicioGestacao: null, dataPrevistaParto: null, dataParto: null, nascidos: null, nascidosVivos: null, nascidosMortos: null, pesoAoNascerGramas: null, observacao: null, duracaoIncubacaoDias: null, duracaoGestacaoDias: null, taxaFertilidade: null, taxaEclosao: null }
  vi.mocked(getReproductiveCycle).mockResolvedValue(cycle)
  vi.mocked(listReproductiveCycles).mockResolvedValue({ items: [cycle], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 })
  vi.mocked(listOffspring).mockResolvedValue({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 })
})

describe('record navigation contract', () => {
  it('cancels detail editing with protection and returns through the original filtered list', async () => {
    const router = app('/propriedades/property-1', origin)
    fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
    await screen.findByDisplayValue(property.nome)
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Rascunho' } })
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    const stay = await screen.findByRole('button', { name: 'Continuar editando' })
    expect(stay).toHaveFocus()
    fireEvent.click(stay)
    expect(screen.getByLabelText('Nome')).toHaveValue('Rascunho')
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar e sair' }))
    expect(await screen.findByRole('heading', { name: 'Visualizar propriedade' })).toBeInTheDocument()
    expect(router.state.location.state).toEqual(origin)
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Propriedades' }))
    await screen.findByText('Página 2 de 2')
    expect(screen.getByLabelText('Buscar por nome')).toHaveValue('Norte')
    expect(updatePropriedade).not.toHaveBeenCalled()
  })

  it('saves detail editing to the detail and list editing to the list, preserving context', async () => {
    const router = app('/propriedades/property-1/editar', recordEditState(origin, '/propriedades', '/propriedades/property-1'))
    await screen.findByDisplayValue(property.nome)
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Norte revisada' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Visualizar propriedade' })
    expect(router.state.location.state).toEqual({ ...origin, recordSavedPath: '/propriedades/property-1' })
    expect(screen.getByRole('status')).toHaveTextContent('Registro salvo.')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Propriedades' }))
    await screen.findByText('Página 2 de 2')
    fireEvent.click(screen.getByRole('link', { name: 'Editar' }))
    await screen.findByDisplayValue(property.nome)
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Mais uma revisão' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByText('Página 2 de 2')
    expect(router.state.location.pathname).toBe('/propriedades')
    expect(screen.getByLabelText('Buscar por nome')).toHaveValue('Norte')
    expect(screen.getByRole('status')).toHaveTextContent('Registro salvo.')
  })

  it('keeps creation feedback when the destination list initializes its remembered filters', async () => {
    const router = app('/propriedades/nova')
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Nova unidade' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('link', { name: 'Visualizar' })
    expect(screen.getByRole('status')).toHaveTextContent('Registro salvo.')
    expect(router.state.location.state.recordSavedPath).toBe('/propriedades')
  })

  it('keeps unsaved values and protection after an unsuccessful save', async () => {
    vi.mocked(updatePropriedade).mockRejectedValue(new Error('offline'))
    app('/propriedades/property-1/editar')
    await screen.findByDisplayValue(property.nome)
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Conservar rascunho' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('alert')
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Continuar editando' }))
    expect(screen.getByLabelText('Nome')).toHaveValue('Conservar rascunho')
  })

  it('returns a weight to its parent section and page without dropping the animal list origin', async () => {
    const animalOrigin = { list: { path: '/animais', filters: { search: 'Matriz', page: 3 } } }
    const router = app('/animais/animal-1', parentReturnState(animalOrigin, '/animais/animal-1', { section: 'pesagens', page: 2, filters: { from: '2026-10-01', filter: '1' } }))
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: 'Visualizar pesagem' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Animal' }))
    await screen.findByRole('link', { name: 'Visualizar' })
    expect(screen.getByLabelText('De')).toHaveValue('2026-10-01')
    expect(screen.getByLabelText('Filtrar marco')).toHaveValue('1')
    expect(document.getElementById('record-pesagens')).toHaveFocus()
    await waitFor(() => expect(api).toHaveBeenLastCalledWith(expect.stringContaining('page=2'), expect.objectContaining({ aborted: false })))
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Animais' }))
    expect(router.state.location.state).toEqual(animalOrigin)
  })

  it('keeps both parent origins through Cruzamento → Ciclos → Proles and the deterministic returns', async () => {
    const crossingOrigin = { list: { path: '/cruzamentos', filters: { status: 2, machoId: 'male' } } }
    const router = app('/cruzamentos/cross-1', crossingOrigin)
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar ciclos reprodutivos' }))
    await screen.findByRole('link', { name: 'Visualizar' })
    fireEvent.change(screen.getByLabelText('Fluxo'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Status do ciclo'), { target: { value: '3' } })
    await waitFor(() => expect(listReproductiveCycles).toHaveBeenLastCalledWith({ cruzamentoId: 'cross-1', tipo: 2, status: 3, page: 1, pageSize: 25 }))
    expect(readParentReturnState(router.state.location.state, '/cruzamentos/cross-1')?.parentReturn.origin).toEqual(crossingOrigin)
    fireEvent.click(screen.getByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: 'Visualizar ciclo reprodutivo' })
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar proles' }))
    await screen.findByRole('heading', { name: 'Proles' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ciclo reprodutivo' }))
    await screen.findByRole('heading', { name: 'Visualizar ciclo reprodutivo' })
    expect(readParentReturnState(router.state.location.state, '/cruzamentos/cross-1')?.parentReturn.origin).toEqual(crossingOrigin)
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ciclos reprodutivos' }))
    await screen.findByRole('link', { name: 'Visualizar' })
    expect(screen.getByLabelText('Fluxo')).toHaveValue('2')
    expect(screen.getByLabelText('Status do ciclo')).toHaveValue('3')
    expect(router.state.location.search).toBe('?cruzamentoId=cross-1')
    expect(readParentReturnState(router.state.location.state, '/cruzamentos/cross-1')?.parentReturn.origin).toEqual(crossingOrigin)
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Cruzamento' }))
    await screen.findByRole('heading', { name: 'Visualizar cruzamento' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Cruzamentos' }))
    await screen.findByRole('heading', { name: 'Cruzamentos listados' })
    expect(router.state.location.state).toEqual(crossingOrigin)
  })

  it('rejects foreign and mismatched return destinations while retaining deterministic direct access', () => {
    expect(formReturnTarget({ list: { path: 'https://outside.invalid', filters: {} }, recordReturn: { path: '/propriedades/other' } }, '/propriedades', '/propriedades/property-1')).toEqual({ to: '/propriedades/property-1', state: undefined })
    expect(readParentReturnState({ parentReturn: { path: '/animais/other', section: 'pesagens', page: 3 } }, '/animais/animal-1')).toBeUndefined()
    expect(parentReturnState(undefined, '//outside.invalid/a', { section: 'pesagens' })).toBeUndefined()
    expect(formReturnTarget(undefined, '/propriedades')).toEqual({ to: '/propriedades', state: undefined })
    expect(listReturnState({ list: { path: '/propriedades-invasoras', filters: {} } }, '/propriedades')).toBeUndefined()
  })

  it('retains only the parent matching the relationship query when restoring a list', () => {
    for (const [root, parameter, parentRoot] of [
      ['/ciclos-reprodutivos', 'cruzamentoId', '/cruzamentos'],
      ['/proles', 'cicloReprodutivoId', '/ciclos-reprodutivos'],
    ]) {
      const list = { path: `${root}?${parameter}=parent-2`, filters: { page: 2 } }
      const stale = { list, parentReturn: { path: `${parentRoot}/parent-1`, section: 'children' } }
      expect(listReturnState(stale, root)).toEqual({ list })
      const matched = { list, parentReturn: { path: `${parentRoot}/parent-2`, section: 'children' } }
      expect(listReturnState(matched, root)?.parentReturn?.path).toBe(`${parentRoot}/parent-2`)
      expect(listReturnState({ ...matched, list: { path: root, filters: {} } }, root)?.parentReturn).toBeUndefined()
    }
  })
})
