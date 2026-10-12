import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { transferableAbortController } from 'node:util'
import { httpRequest } from '../../shared/http/httpClient'
import { UnsavedChangesProvider } from '../../shared/navigation/UnsavedChangesProvider'
import { ItemDetailsPage, ItemFormPage, ItemsPage } from './CatalogPages'
import { ProfileDetailsPage, ProfileFormPage } from './ProfilePages'
import { RecipeDetailsPage, RecipesPage, RecipeVersionDetailsPage, RecipeVersionFormPage } from './RecipePages'
import type { Item, Profile, Recipe, RecipeVersion } from './types'
import { SimulationFormPage } from './SimulationPages'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
const item: Item = { id: 'item-1', codigo: 'MIL', nome: 'Milho', descricao: null, categoriaId: null, classe: 'Alimentar', unidade: 'kg', podeEntrar: true, podeProduzir: false, usoInterno: true, venda: false, ativo: true, revisao: 1, unidadeFixada: false, createdAtUtc: '2026-10-01T00:00:00Z', updatedAtUtc: '2026-10-01T00:00:00Z' }
const profile: Profile = { id: 'profile-1', itemId: item.id, numero: 2, revisao: 1, estado: 'Rascunho', createdAtUtc: item.createdAtUtc, publishedAtUtc: null, conteudo: { nome: 'Perfil de teste', fonte: 'Fixture', metodo: 'Teste', preparacao: 'Amostra', referenciaAmostra: 'S1', contexto: 'Teste', especieId: null, fase: null, dataFonte: null, dataColeta: null, valores: [] } }
const recipe: Recipe = { id: 'recipe-1', codigo: 'R1', nome: 'Receita de teste', finalidade: 'Teste', ativo: true, revisao: 1 }
const version: RecipeVersion = { id: 'version-1', receitaId: recipe.id, numero: 2, revisao: 1, estado: 'Rascunho', conteudo: { tipo: 'MisturaSimples', modo: 'Quantidade', tamanhoReferencia: '100', unidadeReferencia: 'kg', entradas: [], saidas: [], perdas: [], etapas: [], retencoes: [], observacao: 'Fixture' } }
const page = <T,>(items: T[], current = 1) => ({ items, page: current, pageSize: 25, totalItems: 60, totalPages: 3 })
let currentProfile = profile
let currentVersion = version

beforeEach(() => {
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  currentProfile = structuredClone(profile)
  currentVersion = structuredClone(version)
  vi.mocked(httpRequest).mockReset()
  vi.mocked(httpRequest).mockImplementation(async (path, options) => {
    if (options?.method === 'PUT' && path === `/perfis-nutricionais/${profile.id}`) { currentProfile = { ...currentProfile, conteudo: (options.body as { conteudo: Profile['conteudo'] }).conteudo }; return currentProfile }
    if (options?.method === 'PUT' && path === `/receitas/versoes/${version.id}`) { currentVersion = { ...currentVersion, conteudo: (options.body as { conteudo: RecipeVersion['conteudo'] }).conteudo }; return currentVersion }
    if (path === `/itens/${item.id}`) return item
    if (path === '/itens/item-2') return { ...item, id: 'item-2', codigo: 'SEG', nome: 'Segundo item' }
    if (path === `/perfis-nutricionais/${profile.id}`) return currentProfile
    if (path === `/receitas/${recipe.id}`) return recipe
    if (path === `/receitas/versoes/${version.id}`) return currentVersion
    if (path === '/componentes-nutricionais') return []
    const current = Number(new URLSearchParams(path.split('?')[1]).get('page') ?? 1)
    if (path.startsWith(`/itens/${item.id}/perfis-nutricionais?`)) return page([currentProfile], current)
    if (path.startsWith(`/receitas/${recipe.id}/versoes?`)) return page([currentVersion], current)
    if (path.startsWith('/itens?')) return page([item], current)
    if (path.startsWith('/receitas?')) return page([recipe], current)
    if (path.includes('?')) return page([], current)
    throw new Error(`Requisição inesperada: ${path}`)
  })
})
afterEach(() => vi.unstubAllGlobals())

function mount(kind: 'perfil' | 'versao') {
  const path = kind === 'perfil' ? '/itens' : '/producao/receitas'
  const router = createMemoryRouter([{ element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [
    { path: '/itens', element: <ItemsPage /> }, { path: '/itens/:id', element: <ItemDetailsPage /> },
    { path: '/producao/perfis/:profileId', element: <ProfileDetailsPage /> }, { path: '/producao/perfis/:profileId/editar', element: <ProfileFormPage /> },
    { path: '/producao/receitas', element: <RecipesPage /> }, { path: '/producao/receitas/:id', element: <RecipeDetailsPage /> },
    { path: '/producao/receitas/versoes/:versionId', element: <RecipeVersionDetailsPage /> }, { path: '/producao/receitas/versoes/:versionId/editar', element: <RecipeVersionFormPage /> },
  ] }], { initialEntries: [{ pathname: path, state: { list: { path, filters: { search: 'teste', ativo: 'true', page: 2, sortBy: 'codigo', sortDirection: 'desc' } } } }] })
  render(<RouterProvider router={router} />)
  return router
}

describe('Origem de lista e seção do pai em perfis e versões', () => {
  it('trocar o ID da edição cria um novo baseline após descartar o primeiro rascunho', async () => {
    const router = createMemoryRouter([{ element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [{ path: '/itens/:id/editar', element: <ItemFormPage /> }, { path: '/itens/:id', element: <ItemDetailsPage /> }] }], { initialEntries: ['/itens/item-1/editar'] })
    render(<RouterProvider router={router} />)
    await screen.findByDisplayValue('Milho')
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Primeiro rascunho' } })
    await act(async () => { await router.navigate('/itens/item-2/editar') })
    fireEvent.click(within(await screen.findByRole('dialog', { name: 'Descartar alterações?' })).getByRole('button', { name: 'Descartar e sair' }))
    await screen.findByDisplayValue('Segundo item')
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar edição' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/itens/item-2'))
    expect(screen.queryByRole('dialog', { name: 'Descartar alterações?' })).not.toBeInTheDocument()
  })
  it('protege uma meta adicionada por botão à simulação antes de editar qualquer campo', async () => {
    const router = createMemoryRouter([{ element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [{ path: '/producao/formulacao/nova', element: <SimulationFormPage /> }, { path: '/producao/formulacao', element: <h1>Formulação</h1> }] }], { initialEntries: ['/producao/formulacao/nova'] })
    render(<RouterProvider router={router} />)
    fireEvent.click(screen.getByRole('button', { name: 'Adicionar meta' }))
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar simulação' }))
    await screen.findByRole('dialog', { name: 'Descartar alterações?' })
    expect(router.state.location.pathname).toBe('/producao/formulacao/nova')
    expect(screen.getByRole('group', { name: 'Meta 1' })).toBeInTheDocument()
  })
  it.each(['perfil', 'versao'] as const)('conserva origem e página do pai após cancelar edição de %s', async kind => {
    const router = mount(kind)
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar' }))
    const title = kind === 'perfil' ? 'Perfis nutricionais' : 'Versões'
    await screen.findByRole('heading', { name: title })
    const section = document.getElementById(kind === 'perfil' ? 'record-perfis' : 'record-versoes')!
    await within(section).findByRole('link')
    fireEvent.click(within(section).getByRole('button', { name: 'Próxima' }))
    await within(section).findByText('Página 2 de 3')
    fireEvent.click(within(section).getByRole('link'))
    fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
    await screen.findByRole('heading', { name: kind === 'perfil' ? 'Editar perfil nutricional' : 'Editar rascunho da receita' })
    fireEvent.click(screen.getByRole('link', { name: /Cancelar edição e voltar para/ }))
    await screen.findByRole('heading', { name: kind === 'perfil' ? 'Visualizar perfil nutricional' : 'Visualizar versão da receita' })
    fireEvent.click(screen.getByRole('link', { name: kind === 'perfil' ? 'Voltar para Item' : 'Voltar para Receita' }))
    await screen.findByRole('heading', { name: title })
    const restored = document.getElementById(kind === 'perfil' ? 'record-perfis' : 'record-versoes')!
    await within(restored).findByText('Página 2 de 3')
    expect(restored).toHaveFocus()
    fireEvent.click(screen.getByRole('link', { name: kind === 'perfil' ? 'Voltar para Insumos e produtos' : 'Voltar para Receitas' }))
    await screen.findByText('Página 2 de 3')
    expect(screen.getByLabelText(kind === 'perfil' ? 'Buscar código ou nome' : 'Buscar receita')).toHaveValue('teste')
    expect(screen.getByLabelText(kind === 'perfil' ? 'Ordenar' : 'Ordenar por')).toHaveValue('codigo')
    expect(router.state.location.state.list.filters.sortDirection).toBe('desc')
    expect(vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method && options.method !== 'GET')).toHaveLength(0)
  })

  it.each(['perfil', 'versao'] as const)('salva %s e conserva o encadeamento do detalhe, pai e lista', async kind => {
    const router = mount(kind)
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: kind === 'perfil' ? 'Perfis nutricionais' : 'Versões' })
    const section = document.getElementById(kind === 'perfil' ? 'record-perfis' : 'record-versoes')!
    fireEvent.click(await within(section).findByRole('link'))
    fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
    if (kind === 'perfil') {
      fireEvent.change(await screen.findByLabelText('Nome do perfil'), { target: { value: 'Perfil atualizado' } })
      fireEvent.click(screen.getByRole('button', { name: 'Salvar rascunho' }))
    } else {
      fireEvent.change(await screen.findByLabelText('Observações e hipóteses gerais'), { target: { value: 'Versão atualizada' } })
      fireEvent.click(screen.getByRole('button', { name: 'Salvar rascunho da receita' }))
    }
    await waitFor(() => expect(router.state.location.pathname).toBe(kind === 'perfil' ? '/producao/perfis/profile-1' : '/producao/receitas/versoes/version-1'))
    expect(await screen.findByText('Registro salvo.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog', { name: 'Descartar alterações?' })).not.toBeInTheDocument()
    fireEvent.click(await screen.findByRole('link', { name: kind === 'perfil' ? 'Voltar para Item' : 'Voltar para Receita' }))
    fireEvent.click(await screen.findByRole('link', { name: kind === 'perfil' ? 'Voltar para Insumos e produtos' : 'Voltar para Receitas' }))
    await screen.findByText('Página 2 de 3')
    expect(screen.getByLabelText(kind === 'perfil' ? 'Buscar código ou nome' : 'Buscar receita')).toHaveValue('teste')
    expect(vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method === 'PUT')).toHaveLength(1)
  })
})
