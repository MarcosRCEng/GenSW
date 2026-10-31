import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { vi } from 'vitest'
import { AppShell } from './AppShell'
import { useAuth } from '../../features/auth/hooks/useAuth'

vi.mock('../../features/auth/hooks/useAuth', () => ({ useAuth: vi.fn() }))

function WorkingPage() {
  const { pathname } = useLocation()
  return <main><h1>{pathname}</h1></main>
}

function showShell(pathname = '/') {
  return render(<MemoryRouter initialEntries={[pathname]}><Routes>
    <Route element={<AppShell />}><Route path="*" element={<WorkingPage />} /></Route>
    <Route path="/login" element={<h1>Login</h1>} />
  </Routes></MemoryRouter>)
}

describe('AppShell A', () => {
  const logout = vi.fn().mockResolvedValue(undefined)
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ user: { userId: 'user', pessoaId: 'person', nome: 'Marina', userName: 'marina', roles: [] }, logout, isAuthenticated: true, isInitializing: false, bootstrap: vi.fn(), login: vi.fn() })
  })

  it('oferece somente módulos disponíveis em ordem alfabética e nenhuma seção Ações', () => {
    showShell()
    const menu = screen.getByRole('complementary', { name: 'Menu principal' })
    expect(within(menu).getAllByRole('heading').map(heading => heading.textContent)).toEqual(['Cadastros básicos', 'Processos gerenciais', 'Produção e operações'])
    expect(within(menu).getAllByRole('button').map(button => button.textContent?.replace('›', ''))).toEqual([
      'Animais', 'Insumos e produtos', 'Pessoas', 'Propriedades', 'Taxonomia', 'Financeiro', 'Estoque', 'Produção — transformações', 'Produção animal', 'Reprodução',
    ])
    expect(menu).not.toHaveTextContent(/Ações|Planejado|Compras|Vendas/)
  })

  it('abre páginas alfabeticamente, indica o catálogo para um perfil e fecha ao navegar', async () => {
    showShell('/producao/perfis/profile')
    const opener = screen.getByRole('button', { name: 'Insumos e produtos' })
    expect(opener).toHaveClass('is-active')
    opener.focus()
    fireEvent.click(opener)
    const dialog = screen.getByRole('dialog', { name: 'Insumos e produtos' })
    expect(within(dialog).getAllByRole('link').map(link => link.textContent?.replace('→', ''))).toEqual(['Catálogo', 'Categorias'])
    expect(within(dialog).getByRole('link', { name: 'Catálogo' })).toHaveAttribute('aria-current', 'page')
    fireEvent.click(within(dialog).getByRole('link', { name: 'Categorias' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: '/itens/categorias' })).toBeInTheDocument()
  })

  it('permite voltar de páginas a módulos no mesmo drawer e restaura o foco ao cancelar', async () => {
    showShell('/animais/id')
    const opener = screen.getByRole('button', { name: 'Explorar módulos' })
    opener.focus()
    fireEvent.click(opener)
    let dialog = screen.getByRole('dialog', { name: 'Módulos' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Estoque' }))
    dialog = screen.getByRole('dialog', { name: 'Estoque' })
    expect(within(dialog).getAllByRole('link').map(link => link.textContent?.replace('→', ''))).toEqual(['Locais', 'Lotes', 'Movimentos', 'Reconciliação', 'Saldos'])
    expect(dialog).not.toHaveTextContent('Ações')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Voltar aos módulos' }))
    dialog = screen.getByRole('dialog', { name: 'Módulos' })
    fireEvent(dialog, new Event('cancel', { bubbles: false, cancelable: true }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(opener).toHaveFocus())
  })

  it('mantém o comando de logout e direciona ao login', async () => {
    showShell('/animais')
    fireEvent.click(screen.getByRole('button', { name: 'Sair' }))
    await waitFor(() => expect(logout).toHaveBeenCalledOnce())
    expect(await screen.findByRole('heading', { name: 'Login' })).toBeInTheDocument()
  })
})
