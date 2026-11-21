import { fireEvent, render, screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../../shared/http/httpClient'
import type { Weight } from '../evolution/api'
import type { Animal } from '../types/animals'
import { AnimalAnalyticsPanel } from './AnimalAnalyticsPanel'

vi.mock('../../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))

function bird(id: string, dataNascimento: string | null = '2026-01-01'): Animal {
  return {
    id, nome: `Ave ${id}`, codigoInterno: id, dataNascimento, sexo: 2, escopo: 1, ativo: true,
    especieId: 'species', racaId: null, variedadeId: null,
    especie: { id: 'species', nomeComum: 'Ave de teste', ativo: true },
    raca: null, variedade: null, createdAtUtc: '', updatedAtUtc: '',
  }
}

function weight(id: string, date: string, grams: number): Weight {
  return {
    id, dataMedicao: date, pesoGramas: grams, tipoMarco: 3,
    idadeReferenciaDias: 10, idadeDiasNaMedicao: null, descricaoMarco: null, observacao: null,
  }
}

const page = (items: unknown[], number = 1, totalPages = 1, totalItems = items.length) => ({
  items, page: number, totalPages, totalItems, pageSize: 100,
})

describe('Evolução e comparação de aves', () => {
  beforeEach(() => { vi.mocked(httpRequest).mockReset() })

  it('usa as idades observadas de todas as páginas e compara pelos extremos exatos', async () => {
    vi.mocked(httpRequest).mockImplementation(async path => {
      if (path.includes('/A/')) {
        return path.includes('page=1&')
          ? page([weight('a13', '2026-01-14', 19), weight('a8', '2026-01-09', 11.1)], 1, 2, 3)
          : page([weight('a2', '2026-01-03', 5.7)], 2, 2, 3)
      }
      return page([weight('b2', '2026-01-03', 4.6), weight('b8', '2026-01-09', 9.7), weight('b13', '2026-01-14', 16.2)])
    })
    render(<AnimalAnalyticsPanel animals={[bird('A'), bird('B')]} comparison />)
    const table = await screen.findByRole('table', { name: 'Resultados da comparação' })
    const a = within(table).getByRole('row', { name: /Ave A/ })
    const b = within(table).getByRole('row', { name: /Ave B/ })
    expect(a).toHaveTextContent('1,21 g/dia')
    expect(b).toHaveTextContent('1,05 g/dia')
    expect(a).toHaveTextContent('3 de 12 dias com registro')
    expect(within(a).getByRole('cell', { name: /1,21 g\/dia Liderança/ })).toBeInTheDocument()

    fireEvent.change(screen.getByRole('spinbutton', { name: 'Idade para consultar (dias)' }), { target: { value: '10' } })
    expect(within(a).getByText('Sem medição')).toBeInTheDocument()
    expect(within(b).getByText('Sem medição')).toBeInTheDocument()
    expect(httpRequest).toHaveBeenCalledWith('/animais/A/pesagens?page=2&pageSize=100', expect.objectContaining({ authenticated: true }))
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => options?.authenticated && !options.body && !options.method)).toBe(true)
  })

  it('exibe o histórico por calendário quando o nascimento é desconhecido', async () => {
    vi.mocked(httpRequest).mockResolvedValue(page([weight('only', '2026-01-02', 3.8)]))
    render(<AnimalAnalyticsPanel animals={[bird('A', null)]} />)
    expect(screen.getByRole('combobox', { name: 'Eixo do tempo' })).toHaveValue('calendar')
    expect(await screen.findByRole('button', { name: /Ave A.*02\/01\/2026.*3,8/ })).toBeInTheDocument()
  })

  it('conta posturas da mesma data e mantém a primeira postura histórica como referência', async () => {
    const first = { id: 'first', dataPostura: '2026-01-01', pesoGramas: 4, observacao: null }
    const sameDate = [
      { id: 'one', dataPostura: '2026-01-08', pesoGramas: 4.5, observacao: null },
      { id: 'two', dataPostura: '2026-01-08', pesoGramas: 4.8, observacao: null },
    ]
    vi.mocked(httpRequest).mockImplementation(async path => path.includes('producoes-ovos')
      ? { ...page([sameDate[1]], 1, 3, 3), metricas: { evolucao: [first, ...sameDate] } }
      : page([]))
    render(<AnimalAnalyticsPanel animals={[bird('A')]} />)
    fireEvent.click(screen.getByRole('button', { name: 'Postura' }))
    await screen.findByRole('heading', { name: 'Pesos registrados na postura' })
    fireEvent.change(screen.getByRole('combobox', { name: 'Alinhar postura por' }), { target: { value: 'first-laying' } })
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Dia inicial desde a primeira postura' }), { target: { value: '7' } })
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Dia final desde a primeira postura' }), { target: { value: '20' } })
    expect(screen.getByText('Primeira postura em 01/01/2026')).toBeInTheDocument()
    expect(screen.getByText('4,65 g')).toBeInTheDocument()
    expect(screen.getByText('9,3 g')).toBeInTheDocument()
    const weekly = screen.getByRole('region', { name: 'Lançamentos de postura por semana' })
    expect(within(weekly).getByRole('button', { name: /, 7, 2$/ })).toBeInTheDocument()
    expect(within(weekly).getByRole('button', { name: /, 14, 0$/ })).toBeInTheDocument()
    const weights = screen.getByRole('region', { name: 'Pesos registrados na postura' })
    expect(within(weights).getByRole('button', { name: /, 7, 4,5$/ })).toBeInTheDocument()
    expect(within(weights).getByRole('button', { name: /, 7, 4,8$/ })).toBeInTheDocument()
    expect(vi.mocked(httpRequest).mock.calls.filter(([path]) => path.includes('producoes-ovos'))).toHaveLength(1)
  })
})
