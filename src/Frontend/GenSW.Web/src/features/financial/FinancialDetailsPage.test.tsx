import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError } from '../../shared/http/httpErrors'
import { CategoryDetailsPage, EntryDetailsPage } from './FinancialDetailsPage'
import { FinancialPage } from './FinancialPage'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: () => ({ user: { roles: [] } }) }))
const category = { id: 'category-1', nome: 'Categoria histórica', natureza: 2, codigo: null, ativa: false, versao: 4 }
const entry = { lancamento: { id: 'entry-1', tipo: 2, dataMovimento: '2026-09-15', valor: '123.45', descricao: 'Movimento de consulta', categoriaId: category.id, formaPagamento: 2, pessoaId: 'person-1', animalId: 'animal-1', observacao: 'Observação pública', cancelado: true, versao: 2, origem: 1, lancamentoOriginalId: null, motivoAjuste: null }, categoriaNome: category.nome, pessoaNome: 'Pessoa vinculada', animalNome: 'Animal vinculado', revertido: false }

function renderDetail(path: string) {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/financeiro/categorias/:id" element={<CategoryDetailsPage />} />
    <Route path="/financeiro/lancamentos/:id" element={<EntryDetailsPage />} />
  </Routes></MemoryRouter>)
}

describe('Visualização financeira', () => {
  beforeEach(() => { vi.mocked(httpRequest).mockReset() })

  it('consulta categoria inativa por URL, sem formulário ou mutação', async () => {
    vi.mocked(httpRequest).mockResolvedValue(category)
    renderDetail('/financeiro/categorias/category-1')
    expect(await screen.findByText(category.nome)).toBeInTheDocument()
    expect(screen.getByText('Inativo')).toBeInTheDocument()
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(httpRequest).toHaveBeenCalledExactlyOnceWith('/financeiro/categorias/category-1', expect.objectContaining({ method: 'GET', authenticated: true }))
  })

  it('consulta lançamento cancelado com valores formatados e relacionamentos', async () => {
    vi.mocked(httpRequest).mockResolvedValue(entry)
    renderDetail('/financeiro/lancamentos/entry-1')
    expect(await screen.findByText('Movimento de consulta')).toBeInTheDocument()
    expect(screen.getByText('Cancelado')).toBeInTheDocument()
    expect(screen.getByText('R$ 123,45')).toBeInTheDocument()
    expect(screen.getByText('15/09/2026')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: category.nome })).toHaveAttribute('href', '/financeiro/categorias/category-1')
    expect(screen.getByRole('link', { name: 'Pessoa vinculada' })).toHaveAttribute('href', '/pessoas/person-1')
    expect(screen.getByRole('link', { name: 'Animal vinculado' })).toHaveAttribute('href', '/animais/animal-1')
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(httpRequest).toHaveBeenCalledExactlyOnceWith('/financeiro/lancamentos/entry-1', expect.objectContaining({ method: 'GET' }))
  })

  it.each([[404, 'Registro não encontrado.'], [403, 'Você não tem permissão para visualizar este registro.'], [500, 'Não foi possível carregar os detalhes.']])('trata status %s sem expor dados', async (status, message) => {
    vi.mocked(httpRequest).mockRejectedValue(new HttpError(Number(status)))
    renderDetail('/financeiro/lancamentos/entry-1')
    expect(await screen.findByRole('alert')).toHaveTextContent(String(message))
    expect(screen.queryByText(entry.lancamento.descricao)).not.toBeInTheDocument()
  })

  it('permite retry da consulta sem gravação', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new HttpError(500)).mockResolvedValue(category)
    renderDetail('/financeiro/categorias/category-1')
    fireEvent.click(await screen.findByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText(category.nome)).toBeInTheDocument()
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => options?.method === 'GET')).toBe(true)
  })

  it('abre o registro selecionado da lista e restaura mês, filtros e página no retorno', async () => {
    vi.mocked(httpRequest).mockImplementation(async (path) => {
      if (path.endsWith('/configuracao')) return { dataInicio: '2026-01-01', saldoInicial: '0.00', versao: 1 }
      if (path.endsWith('/categorias')) return [category]
      if (path.endsWith('/fechamentos')) return []
      if (path === '/financeiro/lancamentos/entry-1') return entry
      if (path.includes('/lancamentos?')) return { items: [entry], page: 2, totalItems: 26, totalPages: 2 }
      return { mes: '2026-09', fechado: false, versao: 1, saldoAbertura: '0.00', receitas: '0.00', despesas: '0.00', resultado: '0.00', saldoFinal: '0.00', quantidadeReceitas: 0, quantidadeDespesas: 0 }
    })
    render(<MemoryRouter initialEntries={[{ pathname: '/financeiro', state: { list: { path: '/financeiro', filters: { month: '2026-09', page: 2, filters: { search: 'consulta', tipo: '', categoriaId: '', cancelado: '', de: '', ate: '' } } } } }]}><Routes>
      <Route path="/financeiro" element={<FinancialPage />} />
      <Route path="/financeiro/lancamentos/:id" element={<EntryDetailsPage />} />
    </Routes></MemoryRouter>)
    await screen.findByText('Movimento de consulta')
    fireEvent.click(screen.getAllByRole('link', { name: 'Visualizar' }).find((link) => link.getAttribute('href') === '/financeiro/lancamentos/entry-1')!)
    await screen.findByRole('heading', { name: 'Visualizar lançamento financeiro' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Financeiro' }))
    expect(await screen.findByText('Página 2 de 2')).toBeInTheDocument()
    expect(screen.getByLabelText('Pesquisar descrição')).toHaveValue('consulta')
    expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.includes('ano=2026&mes=9&page=2&search=consulta'))).toBe(true)
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => options?.method === 'GET')).toBe(true)
  })
})
