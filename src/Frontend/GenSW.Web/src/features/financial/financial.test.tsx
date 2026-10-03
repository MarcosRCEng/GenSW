import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi } from 'vitest'
import { NetworkError } from '../../shared/http/httpErrors'
import { brl, cents, decimal, fromCents, type Category } from './financial'
import { EntryEditor } from './EntryEditor'
import { FinancialPage } from './FinancialPage'

const mock = vi.hoisted(() => ({ request: vi.fn(), roles: ['Admin'] }))
vi.mock('../../shared/http/httpClient', () => ({ httpRequest: mock.request }))
vi.mock('../auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { roles: mock.roles } }),
}))
const categories: Category[] = [
  {
    id: 'expense',
    nome: 'Insumos',
    natureza: 2,
    codigo: null,
    ativa: true,
    versao: 1,
  },
]
describe('Financeiro', () => {
  it('identifica o módulo e a funcionalidade com retorno explícito à página inicial', async () => {
    mock.roles = []
    mock.request.mockImplementation((path: string) =>
      Promise.resolve(path.endsWith('/configuracao') ? null : []),
    )
    render(
      <MemoryRouter initialEntries={['/financeiro']}>
        <FinancialPage />
      </MemoryRouter>,
    )

    await screen.findByText('Um Admin precisa configurar o início do controle.')
    expect(screen.getByText('Financeiro')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Fluxo de caixa' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Voltar ao início/ })).toHaveAttribute('href', '/')
  })

  it('formats the decimal limit without float conversion and rejects excessive precision', () => {
    expect(brl('9999999999999999.99')).toBe('R$ 9.999.999.999.999.999,99')
    expect(decimal('12,30', true)).toBe('12.30')
    expect(() => decimal('1.001')).toThrow()
    expect(() => decimal('0', true)).toThrow()
    expect(fromCents(cents('250.00') - cents('80.00'))).toBe('170.00')
  })
  it('requires preview, blocks repeated submission, and reuses the key on network retry', async () => {
    mock.request.mockImplementation((path: string) =>
      path === '/financeiro/lancamentos'
        ? Promise.reject(new NetworkError())
        : Promise.resolve({ items: [] }),
    )
    render(
      <EntryEditor
        categories={categories}
        onDone={vi.fn()}
        onCancel={vi.fn()}
      />,
    )
    fireEvent.change(screen.getByLabelText('Valor (R$)'), {
      target: { value: '80,00' },
    })
    fireEvent.change(screen.getByLabelText('Descrição'), {
      target: { value: 'Ração paga' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Revisar e confirmar' }))
    expect(screen.getByText('Confira antes de confirmar')).toBeInTheDocument()
    const confirm = screen.getByRole('button', { name: 'Confirmar gravação' })
    fireEvent.click(confirm)
    fireEvent.click(confirm)
    await screen.findByRole('alert')
    const calls = () =>
      mock.request.mock.calls.filter(
        ([path]) => path === '/financeiro/lancamentos',
      )
    expect(calls()).toHaveLength(1)
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar gravação' }))
    await waitFor(() => expect(calls()).toHaveLength(2))
    expect(calls()[0][1].headers['Idempotency-Key']).toBe(
      calls()[1][1].headers['Idempotency-Key'],
    )
    expect(calls()[0][1].body.valor).toBe('80.00')
  })
  it('keeps monthly totals independent of list filters', async () => {
    mock.roles = ['Admin']
    mock.request.mockImplementation((path: string) => {
      if (path.endsWith('/configuracao'))
        return Promise.resolve({
          dataInicio: '2026-08-01',
          saldoInicial: '100.00',
          versao: 1,
        })
      if (path.endsWith('/categorias')) return Promise.resolve(categories)
      if (path.endsWith('/fechamentos')) return Promise.resolve([])
      if (path.includes('/lancamentos?'))
        return Promise.resolve({
          items: [],
          page: 1,
          totalItems: 0,
          totalPages: 0,
        })
      return Promise.resolve({
        fechado: false,
        versao: 1,
        saldoAbertura: '100',
        receitas: '250',
        despesas: '80',
        resultado: '170',
        saldoFinal: '270',
      })
    })
    render(
      <MemoryRouter>
        <FinancialPage />
      </MemoryRouter>,
    )
    await screen.findByText('R$ 270,00')
    fireEvent.change(screen.getByLabelText('Pesquisar descrição'), {
      target: { value: 'inexistente' },
    })
    await screen.findByText(
      'Nenhum lançamento encontrado neste mês com os filtros escolhidos.',
    )
    expect(screen.getByText('R$ 270,00')).toBeInTheDocument()
    expect(
      mock.request.mock.calls.some(([path]) =>
        path.includes('search=inexistente'),
      ),
    ).toBe(true)
  })
  it('shows the Admin setup requirement for ordinary users', async () => {
    mock.roles = []
    mock.request.mockImplementation((path: string) =>
      Promise.resolve(path.endsWith('/configuracao') ? null : []),
    )
    render(
      <MemoryRouter>
        <FinancialPage />
      </MemoryRouter>,
    )
    await screen.findByText('Um Admin precisa configurar o início do controle.')
    expect(
      screen.queryByRole('button', { name: 'Configurar caixa' }),
    ).not.toBeInTheDocument()
  })
})
