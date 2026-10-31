import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { useAuth } from '../auth/hooks/useAuth'
import { InventoryOperationPage } from '../inventory/InventoryOperationPage'
import { event, local, lot, page, preview, responsible, stock } from '../inventory/inventoryFixtures'
import { eggGroup, eggPage, eggScope } from './eggBridgeFixtures'
import type { MovementCommand } from '../inventory/types'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: vi.fn() }))
const managed = { ...lot, geridoPelaPonteOvos: true, grupoPonteOvosOrigemId: null, unidade: 'un' }
const stockPreview = { ...preview, unidade: 'un', quantidade: { ...preview.quantidade, unidade: 'un', unidadeDeclarada: 'un' } }
function mockApi(group: string | null = null) {
  vi.mocked(httpRequest).mockImplementation(async (path, options) => {
    if (path === '/estoque/previas') return stockPreview
    if (options?.method) return { evento: event, previa: stockPreview }
    if (path === `/estoque/lotes/${lot.id}`) return { ...managed, grupoPonteOvosOrigemId: group }
    if (path.startsWith('/estoque/lotes?')) return page([{ ...managed, grupoPonteOvosOrigemId: group }])
    if (path.startsWith('/estoque/saldos/')) return { ...stock, unidade: 'un' }
    if (path.startsWith('/estoque/locais?')) return page([local])
    if (path === `/estoque/locais/${local.id}`) return local
    if (path.startsWith('/estoque/responsaveis?')) return page([responsible])
    if (path === `/estoque/responsaveis/${responsible.id}`) return responsible
    if (path.startsWith('/estoque/movimentos?')) return page([event])
    if (path.startsWith('/producao/ponte-ovos/recortes?')) return eggPage([eggScope])
    if (path === `/producao/ponte-ovos/recortes/${eggScope.id}`) return eggScope
    if (path.startsWith('/ordens-producao?')) return eggPage([])
    throw new Error(`Requisição inesperada: ${path}`)
  })
}
function view(operation: 'entrada' | 'ajuste') {
  return render(<MemoryRouter initialEntries={[`/estoque/operacoes/${operation}?lote=${lot.id}`]}><Routes><Route path="/estoque/operacoes/:type" element={<InventoryOperationPage />} /></Routes></MemoryRouter>)
}
async function fill(operation: 'entrada' | 'ajuste') {
  await screen.findByText(/Lote MIL-01/)
  const select = screen.getByLabelText('Selecionar Local')
  await within(select).findByRole('option', { name: /DEP/ })
  fireEvent.change(select, { target: { value: local.id } })
  fireEvent.change(screen.getByLabelText(operation === 'ajuste' ? 'Quantidade contada (alvo)' : 'Quantidade declarada'), { target: { value: '1' } })
  fireEvent.change(screen.getByLabelText('Motivo'), { target: { value: 'Conferência física e documental' } })
}
describe('Recebimento F01 de ovos geridos pela F03', () => {
  beforeEach(() => {
    vi.mocked(httpRequest).mockReset()
    vi.mocked(useAuth).mockReturnValue({ user: { userId: responsible.id, pessoaId: 'p', nome: responsible.nome, userName: 'marina', roles: ['Admin'] }, isAuthenticated: true, isInitializing: false, bootstrap: vi.fn(), login: vi.fn(), logout: vi.fn() })
    mockApi()
  })
  it('exige origem distinta explícita e conferência antes da prévia sem presumir declaração', async () => {
    view('entrada'); await fill('entrada')
    expect(screen.getByLabelText('Origem de ovos comprovada')).toHaveValue('')
    fireEvent.change(screen.getByLabelText('Evidência da origem distinta dos ovos'), { target: { value: 'Documento ainda sem declaração de origem distinta' } })
    fireEvent.click(screen.getByRole('button', { name: 'Calcular prévia' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('origem coletiva distinta')
    expect(vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method)).toHaveLength(0)
  })
  it('envia a referência de conferência e a prova de origem distinta no pedido revisado', async () => {
    view('entrada'); await fill('entrada')
    fireEvent.change(screen.getByLabelText('Origem de ovos comprovada'), { target: { value: 'ColetivaDistinta' } })
    const scope = screen.getByLabelText('Selecionar Conferência vigente dos ovos')
    await within(scope).findByRole('option', { name: /Origem física/ })
    fireEvent.change(scope, { target: { value: eggScope.id } })
    fireEvent.change(screen.getByLabelText('Evidência da origem distinta dos ovos'), { target: { value: 'Contagem coletiva e origem física distintas comprovadas' } })
    fireEvent.click(screen.getByRole('button', { name: 'Calcular prévia' }))
    await screen.findByRole('heading', { name: 'Revise antes de confirmar' })
    const request = vi.mocked(httpRequest).mock.calls.find(([path]) => path === '/estoque/previas')
    expect((request?.[1]?.body as { comando: MovementCommand }).comando).toMatchObject({ conciliacaoOvosId: eggScope.id, origemOvosComprovada: 'ColetivaDistinta', evidencia: 'Contagem coletiva e origem física distintas comprovadas' })
    expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path === '/estoque/entradas')).toBe(false)
  })
  it('ajuste no lote F03 referencia automaticamente seu grupo estruturado sem liberar reentrada', async () => {
    mockApi(eggGroup.id)
    view('ajuste'); await fill('ajuste')
    fireEvent.change(screen.getByLabelText('Evidência e declaração da conferência física'), { target: { value: 'Contagem presente, sem recriar origem' } })
    expect(screen.getByRole('link', { name: 'Consultar grupo de origem deste lote' })).toHaveAttribute('href', `/producao/ponte-ovos/grupos/${eggGroup.id}`)
    fireEvent.click(screen.getByRole('button', { name: 'Calcular prévia' }))
    await screen.findByRole('heading', { name: 'Revise antes de confirmar' })
    const request = vi.mocked(httpRequest).mock.calls.find(([path]) => path === '/estoque/previas')
    expect((request?.[1]?.body as { comando: MovementCommand }).comando).toMatchObject({ grupoPonteOvosReferenciaId: eggGroup.id, quantidadeContada: '1' })
  })
})
