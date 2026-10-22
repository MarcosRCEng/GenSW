import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, NetworkError } from '../../shared/http/httpErrors'
import { FinancialPage } from './FinancialPage'
import type { Summary } from './financial'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: () => ({ user: { roles: ['Admin'] } }) }))

const config = { dataInicio: '2026-08-01', saldoInicial: '100.00', versao: 1 }
const summary: Summary = { mes: '2026-09-01', fechado: false, versao: 1, saldoAbertura: '100', receitas: '2300', despesas: '54.33', resultado: '2245.67', saldoFinal: '2345.67', quantidadeReceitas: 1, quantidadeDespesas: 1 }
let finishNextMonth: (value: unknown) => void
let failNextMonth: (reason: unknown) => void
let nextMonthResponse: Promise<unknown>
let currentMonthResponse: Promise<unknown>

beforeEach(() => {
  nextMonthResponse = new Promise((resolve, reject) => { finishNextMonth = resolve; failNextMonth = reject })
  currentMonthResponse = Promise.resolve(summary)
  vi.mocked(httpRequest).mockReset()
  vi.mocked(httpRequest).mockImplementation(async path => {
    if (path.endsWith('/configuracao')) return config
    if (path.endsWith('/categorias') || path.endsWith('/fechamentos')) return []
    if (path.includes('/lancamentos?')) return { items: [], page: 1, totalItems: 0, totalPages: 0 }
    if (path === '/financeiro/meses/2026/8') return nextMonthResponse
    return currentMonthResponse
  })
})

function mount() {
  render(<MemoryRouter initialEntries={[{ pathname: '/financeiro', state: { list: { path: '/financeiro', filters: { month: '2026-09', page: 1, filters: { search: '', tipo: '', categoriaId: '', cancelado: '', de: '', ate: '' } } } } }]}><FinancialPage /></MemoryRouter>)
}

async function changeMonth() {
  mount()
  await screen.findByText('R$ 2.345,67')
  fireEvent.change(screen.getByLabelText('Mês do caixa'), { target: { value: '2026-08' } })
  await waitFor(() => expect(httpRequest).toHaveBeenCalledWith('/financeiro/meses/2026/8', expect.anything()))
}

describe('Caixa durante a consulta de outro mês', () => {
  it('retira os indicadores do mês anterior enquanto a nova consulta está pendente', async () => {
    await changeMonth()
    expect(screen.getByLabelText('Mês do caixa')).toHaveValue('2026-08')
    expect(screen.queryByText('R$ 2.345,67')).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Resumo mensal' })).not.toBeInTheDocument()
    await act(async () => finishNextMonth({ ...summary, mes: '2026-08-01', saldoFinal: '100' }))
    expect(screen.getByRole('region', { name: 'Resumo mensal' })).toBeInTheDocument()
  })

  it('impede lançamento e fechamento baseados no resumo anterior enquanto consulta outro mês', async () => {
    await changeMonth()
    expect(screen.getByRole('button', { name: 'Novo lançamento' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Fechar mês' })).toBeDisabled()
  })

  it('conserva a falha da consulta sem apresentar resumo anterior ou carregamento infinito', async () => {
    await changeMonth()
    await act(async () => failNextMonth(new NetworkError()))
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByText('R$ 2.345,67')).not.toBeInTheDocument()
    expect(screen.queryByText('Carregando lançamentos…')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Novo lançamento' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Fechar mês' })).toBeDisabled()
  })

  it('recupera a consulta mantendo o mês e os filtros escolhidos', async () => {
    await changeMonth()
    fireEvent.change(screen.getByLabelText('Pesquisar descrição'), { target: { value: 'documento conferido' } })
    await act(async () => failNextMonth(new NetworkError()))
    await screen.findByRole('alert')
    nextMonthResponse = Promise.resolve({ ...summary, mes: '2026-08-01', saldoFinal: '1350' })
    fireEvent.click(screen.getByRole('button', { name: 'Recarregar' }))
    await screen.findByText('R$ 1.350,00')
    expect(screen.getByLabelText('Mês do caixa')).toHaveValue('2026-08')
    expect(screen.getByLabelText('Pesquisar descrição')).toHaveValue('documento conferido')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Fechar mês' })).toBeEnabled()
    expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.includes('mes=8') && path.includes('search=documento+conferido'))).toBe(true)
  })

  it('descarta uma resposta atrasada depois de voltar para o mês anterior', async () => {
    await changeMonth()
    fireEvent.change(screen.getByLabelText('Mês do caixa'), { target: { value: '2026-09' } })
    await screen.findByText('R$ 2.345,67')
    await act(async () => finishNextMonth({ ...summary, mes: '2026-08-01', saldoFinal: '1350', fechado: true }))
    expect(screen.getByLabelText('Mês do caixa')).toHaveValue('2026-09')
    expect(screen.getByText('R$ 2.345,67')).toBeInTheDocument()
    expect(screen.queryByText('R$ 1.350,00')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Fechar mês' })).toBeEnabled()
  })

  it.each([401, 403])('não oferece ações nem vazio para consulta recusada com HTTP %i', async status => {
    await changeMonth()
    await act(async () => failNextMonth(new HttpError(status)))
    await screen.findByRole('alert')
    expect(screen.queryByRole('region', { name: 'Resumo mensal' })).not.toBeInTheDocument()
    expect(screen.queryByText(/Nenhum lançamento encontrado/)).not.toBeInTheDocument()
    expect(screen.queryByText('Carregando lançamentos…')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Novo lançamento' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Fechar mês' })).toBeDisabled()
  })

  it('preserva o fechamento em atualização e exige nova conferência quando o resumo muda', async () => {
    mount()
    await screen.findByText('R$ 2.345,67')
    fireEvent.click(screen.getByRole('button', { name: 'Fechar mês' }))
    const dialog = await screen.findByRole('dialog', { name: 'Fechamento do mês' })
    const confirm = within(dialog).getByRole('button', { name: 'Confirmar fechamento definitivo' })
    fireEvent.change(within(dialog).getByLabelText('Observação obrigatória'), { target: { value: 'Conferência documental' } })
    fireEvent.change(within(dialog).getByLabelText('Saldo conferido (R$, opcional)'), { target: { value: '2345,67' } })
    fireEvent.click(within(dialog).getByRole('checkbox'))
    expect(confirm).toBeEnabled()
    let finishReload!: (value: Summary) => void
    currentMonthResponse = new Promise(resolve => { finishReload = resolve })
    fireEvent.click(screen.getByRole('button', { name: 'Recarregar caixa' }))
    expect(confirm).toBeDisabled()
    expect(within(dialog).getByRole('checkbox')).toBeDisabled()
    expect(within(dialog).queryByText('R$ 2.345,67')).not.toBeInTheDocument()
    // The submit handler also guards a submission dispatched without a button click.
    fireEvent.submit(confirm.closest('form')!)
    expect(vi.mocked(httpRequest).mock.calls.some(([, options]) => options?.method === 'POST')).toBe(false)
    await act(async () => finishReload({ ...summary, versao: 2, saldoFinal: '3456.78' }))
    expect(within(dialog).getByLabelText('Observação obrigatória')).toHaveValue('Conferência documental')
    expect(within(dialog).getByLabelText('Saldo conferido (R$, opcional)')).toHaveValue('2345,67')
    expect(within(dialog).getByText('R$ 3.456,78')).toBeInTheDocument()
    expect(within(dialog).getByRole('checkbox')).not.toBeChecked()
    expect(confirm).toBeDisabled()
    fireEvent.click(within(dialog).getByRole('checkbox'))
    expect(confirm).toBeEnabled()
  })

  it('preserva os campos e impede fechamento se a atualização falhar, permitindo repetir a consulta', async () => {
    mount()
    await screen.findByText('R$ 2.345,67')
    fireEvent.click(screen.getByRole('button', { name: 'Fechar mês' }))
    const dialog = await screen.findByRole('dialog', { name: 'Fechamento do mês' })
    fireEvent.change(within(dialog).getByLabelText('Observação obrigatória'), { target: { value: 'Motivo conservado' } })
    fireEvent.click(within(dialog).getByRole('checkbox'))
    let failReload!: (reason: unknown) => void
    currentMonthResponse = new Promise((_, reject) => { failReload = reject })
    fireEvent.click(screen.getByRole('button', { name: 'Recarregar caixa' }))
    await act(async () => failReload(new NetworkError()))
    expect(within(dialog).getByRole('alert')).toBeInTheDocument()
    expect(within(dialog).queryByText('Atualizando o resumo para conferência…')).not.toBeInTheDocument()
    expect(within(dialog).getByLabelText('Observação obrigatória')).toHaveValue('Motivo conservado')
    expect(within(dialog).getByRole('button', { name: 'Confirmar fechamento definitivo' })).toBeDisabled()
    currentMonthResponse = Promise.resolve(summary)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Recarregar resumo' }))
    await within(dialog).findByText('R$ 2.345,67')
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument()
    expect(within(dialog).getByLabelText('Observação obrigatória')).toHaveValue('Motivo conservado')
    expect(within(dialog).getByRole('checkbox')).not.toBeChecked()
    expect(within(dialog).getByRole('button', { name: 'Confirmar fechamento definitivo' })).toBeDisabled()
    fireEvent.click(within(dialog).getByRole('checkbox'))
    expect(within(dialog).getByRole('button', { name: 'Confirmar fechamento definitivo' })).toBeEnabled()
  })

  it.each([403, 409])('conserva a conferência recusada com HTTP %i e só envia a versão atual após recarregar e reconferir', async status => {
    let rejectClosing = true
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (path.endsWith('/fechamento') && options?.method === 'POST') {
        if (rejectClosing) throw new HttpError(status)
        return {}
      }
      return base(path, options)
    })
    mount()
    await screen.findByText('R$ 2.345,67')
    fireEvent.click(screen.getByRole('button', { name: 'Fechar mês' }))
    const dialog = await screen.findByRole('dialog', { name: 'Fechamento do mês' })
    fireEvent.change(within(dialog).getByLabelText('Observação obrigatória'), { target: { value: 'Conferência preservada após recusa' } })
    fireEvent.change(within(dialog).getByLabelText('Saldo conferido (R$, opcional)'), { target: { value: '2345,67' } })
    fireEvent.click(within(dialog).getByRole('checkbox'))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Confirmar fechamento definitivo' }))
    await within(dialog).findByRole('alert')
    expect(within(dialog).getByLabelText('Observação obrigatória')).toHaveValue('Conferência preservada após recusa')
    expect(within(dialog).getByLabelText('Saldo conferido (R$, opcional)')).toHaveValue('2345,67')
    const posts = () => vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method === 'POST')
    expect(posts()).toHaveLength(1)
    rejectClosing = false
    currentMonthResponse = Promise.resolve({ ...summary, versao: 2 })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Recarregar resumo' }))
    await waitFor(() => expect(within(dialog).getByRole('checkbox')).toBeEnabled())
    expect(within(dialog).getByRole('checkbox')).not.toBeChecked()
    expect(within(dialog).getByRole('button', { name: 'Confirmar fechamento definitivo' })).toBeDisabled()
    expect(posts()).toHaveLength(1)
    fireEvent.click(within(dialog).getByRole('checkbox'))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Confirmar fechamento definitivo' }))
    await screen.findByText('Operação registrada com sucesso.')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(posts()).toHaveLength(2)
    expect(posts()[1]).toEqual(['/financeiro/meses/2026/9/fechamento', expect.objectContaining({ body: { versaoEsperada: 2, observacao: 'Conferência preservada após recusa', saldoConferido: '2345.67' } })])
  })
})
