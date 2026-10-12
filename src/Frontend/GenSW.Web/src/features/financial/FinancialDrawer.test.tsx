import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { transferableAbortController } from 'node:util'
import { httpRequest } from '../../shared/http/httpClient'
import { UnsavedChangesProvider } from '../../shared/navigation/UnsavedChangesProvider'
import { FinancialPage } from './FinancialPage'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
const roles = vi.hoisted(() => ({ value: [] as string[] }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: () => ({ user: { roles: roles.value } }) }))
const category = { id: 'cat-1', nome: 'Insumos', natureza: 2, codigo: null, ativa: true, versao: 1 }
const entry = { lancamento: { id: 'entry-1', tipo: 2, dataMovimento: '2026-09-15', valor: '123.45', descricao: 'Insumo pago', categoriaId: category.id, formaPagamento: 2, pessoaId: null, animalId: null, observacao: null, cancelado: false, versao: 2, origem: 1, lancamentoOriginalId: null, motivoAjuste: null }, categoriaNome: category.nome, pessoaNome: null, animalNome: null, revertido: false }
let closed = false
let cancelled = false

function mount() {
  const router = createMemoryRouter([{ element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [{ path: '/financeiro', element: <FinancialPage /> }, { path: '/destino', element: <h1>Outra página</h1> }] }], { initialEntries: [{ pathname: '/financeiro', state: { list: { path: '/financeiro', filters: { month: '2026-09', page: 2, filters: { search: 'pago', tipo: '', categoriaId: category.id, cancelado: '', de: '', ate: '' } } } } }] })
  render(<RouterProvider router={router} />)
  return router
}

beforeEach(() => {
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  roles.value = []
  closed = false
  cancelled = false
  vi.mocked(httpRequest).mockReset()
  vi.mocked(httpRequest).mockImplementation(async path => {
    if (path.endsWith('/configuracao')) return { dataInicio: '2026-01-01', saldoInicial: '0.00', versao: 1 }
    if (path.endsWith('/categorias')) return [category]
    if (path.endsWith('/fechamentos')) return closed ? [{ id: 'closing-1', mes: '2026-09-01', saldoAbertura: '0', receitas: '0', despesas: '123.45', saldoFinal: '-123.45', saldoConferido: null, observacao: 'Conferido', createdAtUtc: '2026-10-01T00:00:00Z', autorId: 'user-1' }] : []
    if (path.endsWith('/historico')) return [{ id: 'audit-1', operacao: 'Criado', motivo: 'Registro', createdAtUtc: '2026-09-15T00:00:00Z', autorId: 'user-1', antesJson: null, depoisJson: null }]
    if (path.includes('/lancamentos?')) return { items: [{ ...entry, lancamento: { ...entry.lancamento, cancelado: cancelled } }], page: 2, totalItems: 26, totalPages: 2 }
    return { mes: '2026-09', fechado: closed, versao: 1, saldoAbertura: '0', receitas: '0', despesas: '123.45', resultado: '-123.45', saldoFinal: '-123.45' }
  })
})
afterEach(() => vi.unstubAllGlobals())

describe('Drawer financeiro e contexto do caixa', () => {
  it('mostra um único título no drawer e mantém foco entre campos e prévia', async () => {
    mount()
    await screen.findByText('Insumo pago')
    fireEvent.click(screen.getByRole('button', { name: 'Novo lançamento' }))
    const editor = await screen.findByRole('dialog', { name: 'Novo lançamento' })
    expect(within(editor).getAllByRole('heading', { name: 'Novo lançamento' })).toHaveLength(1)
    expect(within(editor).getByLabelText('Natureza')).toHaveFocus()
    fireEvent.change(within(editor).getByLabelText('Descrição'), { target: { value: 'Compra de insumos' } })
    fireEvent.change(within(editor).getByLabelText('Valor (R$)'), { target: { value: '100' } })
    fireEvent.click(within(editor).getByRole('button', { name: 'Revisar e confirmar' }))
    expect(within(editor).getByRole('heading', { name: 'Confira antes de confirmar' })).toHaveFocus()
    fireEvent.click(within(editor).getByRole('button', { name: 'Revisar campos' }))
    expect(within(editor).getByLabelText('Natureza')).toHaveFocus()
    expect(within(editor).getByLabelText('Descrição')).toHaveValue('Compra de insumos')
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => options?.method === 'GET')).toBe(true)
  })

  it('fecha histórico e devolve foco ao acionador sem mudar mês, filtros ou página', async () => {
    const router = mount()
    const trigger = await screen.findByRole('button', { name: 'Histórico' })
    trigger.focus()
    fireEvent.click(trigger)
    const dialog = await screen.findByRole('dialog', { name: 'Histórico do lançamento' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    await within(dialog).findByText('Registro')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Fechar painel financeiro' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(trigger).toHaveFocus()
    expect(screen.getByLabelText('Mês do caixa')).toHaveValue('2026-09')
    expect(screen.getByLabelText('Pesquisar descrição')).toHaveValue('pago')
    expect(screen.getByLabelText('Categoria')).toHaveValue(category.id)
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/financeiro')
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => options?.method === 'GET')).toBe(true)
  })

  it('protege a gravação pendente e permite fechar histórico com Escape após gravar', async () => {
    let finishSave!: (value: unknown) => void
    let finishHistory!: (value: unknown) => void
    const saving = new Promise(resolve => { finishSave = resolve })
    const history = new Promise(resolve => { finishHistory = resolve })
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation((path, options) => {
      if (options?.method === 'PUT') return saving
      if (path.endsWith('/historico')) return history
      return base(path, options)
    })
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Corrigir' }))
    const editor = await screen.findByRole('dialog', { name: 'Corrigir lançamento' })
    fireEvent.change(within(editor).getByLabelText('Motivo obrigatório'), { target: { value: 'Conferência' } })
    fireEvent.click(within(editor).getByRole('button', { name: 'Revisar e confirmar' }))
    fireEvent.click(within(editor).getByRole('button', { name: 'Confirmar gravação' }))
    await within(editor).findByRole('button', { name: 'Salvando…' })
    fireEvent(editor, new Event('cancel', { cancelable: true }))
    expect(editor).toBeInTheDocument()
    expect(screen.queryByRole('dialog', { name: 'Descartar alterações?' })).not.toBeInTheDocument()
    await act(async () => { finishSave({}) })
    await screen.findByText('Lançamento registrado.')
    const trigger = await screen.findByRole('button', { name: 'Histórico' })
    trigger.focus()
    fireEvent.click(trigger)
    const drawer = await screen.findByRole('dialog', { name: 'Histórico do lançamento' })
    expect(within(drawer).getAllByRole('heading', { name: 'Histórico do lançamento' })).toHaveLength(1)
    expect(within(drawer).getByText('Carregando histórico…')).toBeInTheDocument()
    fireEvent(drawer, new Event('cancel', { cancelable: true }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(trigger).toHaveFocus()
    await act(async () => { finishHistory([]) })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Mês do caixa')).toHaveValue('2026-09')
    expect(screen.getByLabelText('Pesquisar descrição')).toHaveValue('pago')
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
  })

  it('conserva o rascunho ao continuar editando e descarta somente após confirmação', async () => {
    mount()
    const trigger = await screen.findByRole('button', { name: 'Corrigir' })
    trigger.focus()
    fireEvent.click(trigger)
    const editor = await screen.findByRole('dialog', { name: 'Corrigir lançamento' })
    fireEvent.change(within(editor).getByLabelText('Descrição'), { target: { value: 'Rascunho conservado' } })
    fireEvent.click(within(editor).getByRole('button', { name: 'Fechar painel financeiro' }))
    const guard = await screen.findByRole('dialog', { name: 'Descartar alterações?' })
    expect(within(guard).getByRole('button', { name: 'Continuar editando' })).toHaveFocus()
    fireEvent.click(within(guard).getByRole('button', { name: 'Continuar editando' }))
    expect(within(editor).getByLabelText('Descrição')).toHaveValue('Rascunho conservado')
    fireEvent.click(within(editor).getByRole('button', { name: 'Cancelar edição' }))
    fireEvent.click(within(await screen.findByRole('dialog', { name: 'Descartar alterações?' })).getByRole('button', { name: 'Descartar e sair' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.getByLabelText('Pesquisar descrição')).toHaveValue('pago')
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => options?.method === 'GET')).toBe(true)
  })

  it.each(['cancelado', 'fechado'])('mantém ações restritas de lançamento %s', async mode => {
    cancelled = mode === 'cancelado'
    closed = mode === 'fechado'
    mount()
    await screen.findByText('Insumo pago')
    expect(screen.queryByRole('button', { name: 'Corrigir' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancelar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Fechar mês' })).not.toBeInTheDocument()
    if (closed) expect(screen.getByRole('button', { name: 'Ajustar' })).toBeInTheDocument()
    else expect(screen.queryByRole('button', { name: 'Ajustar' })).not.toBeInTheDocument()
  })

  it('protege a configuração inicial alterada mesmo antes de existir configuração', async () => {
    roles.value = ['Admin']
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path.endsWith('/configuracao') ? null : base(path, options))
    const router = mount()
    await screen.findByRole('button', { name: 'Configurar caixa' })
    fireEvent.change(screen.getByLabelText('Saldo inicial (R$; negativo permitido)'), { target: { value: '100' } })
    await act(async () => { await router.navigate('/destino') })
    await screen.findByRole('dialog', { name: 'Descartar alterações?' })
    expect(router.state.location.pathname).toBe('/financeiro')
    expect(screen.getByLabelText('Saldo inicial (R$; negativo permitido)')).toHaveValue('100')
  })

  it('salvar lançamento não remove a proteção de uma categoria ainda não salva', async () => {
    const router = mount()
    await screen.findByRole('button', { name: 'Corrigir' })
    fireEvent.click(screen.getByText('Categorias financeiras'))
    fireEvent.change(screen.getByRole('textbox', { name: /^Nome$/ }), { target: { value: 'Categoria não salva' } })
    fireEvent.click(screen.getByRole('button', { name: 'Corrigir' }))
    const editor = await screen.findByRole('dialog', { name: 'Corrigir lançamento' })
    fireEvent.change(within(editor).getByLabelText('Motivo obrigatório'), { target: { value: 'Conferência' } })
    fireEvent.click(within(editor).getByRole('button', { name: 'Revisar e confirmar' }))
    fireEvent.click(within(editor).getByRole('button', { name: 'Confirmar gravação' }))
    await screen.findByText('Lançamento registrado.')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await act(async () => { await router.navigate('/destino') })
    await screen.findByRole('dialog', { name: 'Descartar alterações?' })
    expect(router.state.location.pathname).toBe('/financeiro')
    expect(screen.getByRole('textbox', { name: /^Nome$/ })).toHaveValue('Categoria não salva')
  })

  it('protege uma mudança isolada de natureza na nova categoria', async () => {
    const router = mount()
    await screen.findByText('Insumo pago')
    fireEvent.click(screen.getByText('Categorias financeiras'))
    fireEvent.change(screen.getByLabelText('Natureza'), { target: { value: '1' } })
    await act(async () => { await router.navigate('/destino') })
    await screen.findByRole('dialog', { name: 'Descartar alterações?' })
    expect(router.state.location.pathname).toBe('/financeiro')
  })

  it('o reload após salvar lançamento conserva também a configuração alterada no fundo', async () => {
    roles.value = ['Admin']
    const router = mount()
    await screen.findByRole('button', { name: 'Corrigir' })
    fireEvent.click(screen.getByText('Configuração do início do controle'))
    fireEvent.change(screen.getByLabelText('Saldo inicial (R$)'), { target: { value: '500' } })
    fireEvent.click(screen.getByRole('button', { name: 'Corrigir' }))
    const editor = await screen.findByRole('dialog', { name: 'Corrigir lançamento' })
    fireEvent.change(within(editor).getByLabelText('Motivo obrigatório'), { target: { value: 'Conferência' } })
    fireEvent.click(within(editor).getByRole('button', { name: 'Revisar e confirmar' }))
    fireEvent.click(within(editor).getByRole('button', { name: 'Confirmar gravação' }))
    await screen.findByText('Lançamento registrado.')
    await screen.findByText('Insumo pago')
    expect(screen.getByLabelText('Saldo inicial (R$)')).toHaveValue('500')
    await act(async () => { await router.navigate('/destino') })
    await screen.findByRole('dialog', { name: 'Descartar alterações?' })
    expect(router.state.location.pathname).toBe('/financeiro')
  })
})
