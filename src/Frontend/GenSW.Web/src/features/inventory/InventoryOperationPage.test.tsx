import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { useAuth } from '../auth/hooks/useAuth'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, NetworkError } from '../../shared/http/httpErrors'
import { InventoryOperationPage } from './InventoryOperationPage'
import { destination, event, local, lot, page, preview, responsible, stock } from './inventoryFixtures'
import { operationInfo, type MovementCommand, type Operation } from './types'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: vi.fn() }))
const auth = (roles = ['Admin']) => ({ user: { userId: responsible.id, pessoaId: 'p', nome: responsible.nome, userName: 'marina', roles }, isAuthenticated: true, isInitializing: false, bootstrap: vi.fn(), login: vi.fn(), logout: vi.fn() })
const writes = () => vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method && options.method !== 'GET')
function renderOperation(operation: Operation) {
  return render(<MemoryRouter initialEntries={[`/estoque/operacoes/${operation}?lote=${lot.id}`]}><Routes><Route path="/estoque/operacoes/:type" element={<InventoryOperationPage />} /><Route path="/estoque/movimentos/:id" element={<h1>Movimento registrado</h1>} /></Routes></MemoryRouter>)
}
async function fill(operation: Operation) {
  await screen.findByText(/Lote MIL-01/)
  const info = operationInfo[operation]
  await screen.findAllByRole('option', { name: /DEP — Depósito/ })
  if (info.transfer) {
    fireEvent.change(screen.getByLabelText('Selecionar Local de origem'), { target: { value: local.id } })
    fireEvent.change(screen.getByLabelText('Selecionar Local de destino'), { target: { value: destination.id } })
  } else fireEvent.change(screen.getByLabelText('Selecionar Local'), { target: { value: local.id } })
  fireEvent.change(screen.getByLabelText(operation === 'ajuste' ? 'Quantidade contada (alvo)' : 'Quantidade declarada'), { target: { value: '1,000001' } })
  fireEvent.change(screen.getByLabelText('Motivo'), { target: { value: 'Conferência física' } })
  if (info.admin) fireEvent.change(screen.getByLabelText('Evidência e declaração da conferência física'), { target: { value: 'Contagem verificada' } })
  if (operation === 'saida-manual') fireEvent.change(screen.getByLabelText('Destino e finalidade da saída'), { target: { value: 'Uso documentado' } })
  fireEvent.click(screen.getByRole('button', { name: 'Calcular prévia' }))
  await screen.findByRole('heading', { name: 'Revise antes de confirmar' })
}

describe('Apontamentos físicos F01', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue(auth())
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (path === '/estoque/previas') {
        const body = options?.body as { comando: MovementCommand; operacao: string }
        return { ...preview, operacao: body.operacao, versoesEsperadas: body.comando.versoesEsperadas }
      }
      if (options?.method) return { evento: event, previa: preview }
      if (path === `/estoque/lotes/${lot.id}`) return lot
      if (path.startsWith('/estoque/saldos/')) return path.endsWith(destination.id) ? { ...stock, localId: destination.id, localRevisao: 4, revisao: 0 } : stock
      if (path.startsWith('/estoque/lotes?')) return page([lot])
      if (path.startsWith('/estoque/locais?')) return page([local, destination])
      if (path.startsWith('/estoque/responsaveis?')) return page([responsible])
      if (path.startsWith('/estoque/movimentos?')) return page([event])
      if (path === `/estoque/responsaveis/${responsible.id}`) return responsible
      throw new Error(`Requisição inesperada: ${path}`)
    })
  })
  it.each(Object.keys(operationInfo) as Operation[])('confirma %s só depois de prévia server e envia versões e strings exatas', async operation => {
    renderOperation(operation)
    await fill(operation)
    expect(writes().map(([path]) => path)).toEqual(['/estoque/previas'])
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar movimento' }))
    await screen.findByRole('heading', { name: 'Movimento registrado' })
    const [path, options] = writes()[1]
    const command = options?.body as MovementCommand
    expect(path).toBe(`/estoque/${operationInfo[operation].endpoint}`)
    expect(new Headers(options?.headers).get('Idempotency-Key')).toBeTruthy()
    expect(command).toMatchObject({ quantidade: '1.000001', responsavelId: responsible.id, motivo: 'Conferência física', aceiteQuantizacao: null, versoesEsperadas: { item: 7, lote: 5 } })
    if (operationInfo[operation].transfer) expect(command).toMatchObject({ localId: null, origemLocalId: local.id, destinoLocalId: destination.id, conversaoItemId: null, versoesEsperadas: { origemLocal: 3, destinoLocal: 4, origemPosicao: 2, destinoPosicao: 0 } })
    else expect(command).toMatchObject({ localId: local.id, versoesEsperadas: { local: 3, posicao: 2 } })
    if (operation === 'ajuste') expect(command.quantidadeContada).toBe('1.000001')
  })
  it('exige aceite explícito do canônico e resíduo e envia os valores da prévia', async () => {
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path === '/estoque/previas' ? { ...preview, quantidade: { ...preview.quantidade, calculada: '0.333333333333', normalizada: '0.333333', residuo: '0.000000333333', exigeAceite: true } } : base(path, options))
    renderOperation('entrada'); await fill('entrada')
    expect(screen.getByRole('button', { name: 'Confirmar movimento' })).toBeDisabled()
    fireEvent.click(screen.getByRole('checkbox', { name: /Aceito registrar/ }))
    fireEvent.change(screen.getByLabelText('Motivo do aceite do resíduo'), { target: { value: 'Escala conferida' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar movimento' }))
    await screen.findByRole('heading', { name: 'Movimento registrado' })
    expect((writes()[1][1]?.body as MovementCommand).aceiteQuantizacao).toEqual({ calculado: '0.333333333333', normalizado: '0.333333', residuo: '0.000000333333', motivo: 'Escala conferida' })
  })
  it('invalida prévia e aceite ao editar e preserva o preenchimento em conflito', async () => {
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path === '/estoque/entradas' ? Promise.reject(new HttpError(409, '', 'Revisão mudou')) : base(path, options))
    renderOperation('entrada'); await fill('entrada')
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar movimento' }))
    await screen.findByText('Revisão mudou')
    expect(screen.getByLabelText('Quantidade declarada')).toHaveValue('1,000001')
    expect(screen.getByRole('button', { name: 'Confirmar movimento' })).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Quantidade declarada'), { target: { value: '2' } })
    expect(screen.queryByRole('button', { name: 'Confirmar movimento' })).not.toBeInTheDocument()
  })
  it('retenta resposta perdida com a mesma chave e corpo, sem duplicar o movimento', async () => {
    const base = vi.mocked(httpRequest).getMockImplementation()!; let failed = false
    vi.mocked(httpRequest).mockImplementation(async (path, options) => { if (path === '/estoque/entradas' && !failed) { failed = true; throw new NetworkError() }; return base(path, options) })
    renderOperation('entrada'); await fill('entrada')
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar movimento' }))
    await screen.findByText('Não foi possível conectar à API.')
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar movimento' }))
    await screen.findByRole('heading', { name: 'Movimento registrado' })
    expect(writes()[1][1]).toEqual(writes()[2][1])
  })
  it.each(['abertura', 'ajuste', 'segregacao', 'retorno-segregacao', 'descarte'] as Operation[])('bloqueia acesso direto sem Admin a %s antes de buscar referências', async operation => {
    vi.mocked(useAuth).mockReturnValue(auth([]))
    renderOperation(operation)
    expect(await screen.findByRole('alert')).toHaveTextContent('papel Admin')
    expect(httpRequest).not.toHaveBeenCalled()
  })
  it('impede confirmação dupla enquanto o comando está em voo', async () => {
    const base = vi.mocked(httpRequest).getMockImplementation()!; let complete!: (value: unknown) => void
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path === '/estoque/entradas' ? new Promise(resolve => { complete = resolve }) : base(path, options))
    renderOperation('entrada'); await fill('entrada')
    const button = screen.getByRole('button', { name: 'Confirmar movimento' })
    fireEvent.click(button); fireEvent.click(button)
    await waitFor(() => expect(writes()).toHaveLength(2))
    expect(screen.getByRole('button', { name: 'Confirmando…' })).toBeDisabled()
    complete({ evento: event, previa: preview }); await screen.findByRole('heading', { name: 'Movimento registrado' })
  })
})
