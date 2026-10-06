import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, NetworkError } from '../../shared/http/httpErrors'
import { ItemDetailsPage, ItemsPage } from './CatalogPages'
import { ServerSelect } from './FormControls'
import { ProfileDetailsPage, ProfileFormPage } from './ProfilePages'
import { SimulationDetailsPage, SimulationFormPage } from './SimulationPages'
import { RecipeVersionDetailsPage } from './RecipePages'
import type { Item, Profile, RecipeVersion, Simulation } from './types'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
const item: Item = {
  id: 'item-1',
  codigo: 'A',
  nome: 'Ingrediente sintético',
  descricao: null,
  categoriaId: null,
  classe: 'Alimentar',
  unidade: 'kg',
  podeEntrar: true,
  podeProduzir: true,
  usoInterno: true,
  venda: false,
  ativo: false,
  revisao: 4,
  unidadeFixada: true,
  createdAtUtc: '2026-10-06T00:00:00Z',
  updatedAtUtc: '2026-10-06T00:00:00Z',
}
const profile: Profile = {
  id: 'profile-1',
  itemId: item.id,
  numero: 1,
  revisao: 2,
  estado: 'Publicado',
  createdAtUtc: item.createdAtUtc,
  publishedAtUtc: item.createdAtUtc,
  conteudo: {
    nome: 'Fonte sintética',
    fonte: 'Fixture',
    metodo: 'Sintético',
    preparacao: 'Amostra',
    referenciaAmostra: 'A',
    contexto: 'Sintético',
    especieId: null,
    fase: null,
    dataFonte: null,
    dataColeta: null,
    valores: [
      {
        componente: 'PB',
        estado: 'Conhecido',
        valor: '0',
        origem: 'Declarado',
        base: 'BN',
        unidade: 'g/kg',
        metodo: 'Sintético',
        contexto: '',
        qualificador: 'Pontual',
        motivo: null,
        fonte: null,
        hipotese: null,
      },
      {
        componente: 'FB',
        estado: 'Desconhecido',
        valor: null,
        origem: null,
        base: 'BN',
        unidade: 'g/kg',
        metodo: 'Sintético',
        contexto: '',
        qualificador: 'Pontual',
        motivo: 'Não analisado',
        fonte: null,
        hipotese: null,
      },
    ],
  },
}
const version: RecipeVersion = {
  id: 'version-1',
  receitaId: 'recipe-1',
  numero: 1,
  revisao: 2,
  estado: 'Publicado',
  conteudo: {
    tipo: 'MisturaSimples',
    modo: 'Quantidade',
    tamanhoReferencia: '100',
    unidadeReferencia: 'kg',
    entradas: [],
    saidas: [],
    perdas: [],
    etapas: [],
    retencoes: [],
    observacao: 'Fixture',
  },
}
const simulation: Simulation = {
  id: 'simulation-1',
  createdAtUtc: item.createdAtUtc,
  conteudo: {
    pedido: {
      receitaVersaoId: version.id,
      tamanho: '100',
      unidade: 'kg',
      especieId: null,
      fase: null,
      metas: [],
      variacao: null,
      anteriorId: null,
    },
    receita: version,
    cabecalho: { codigo: 'F1', nome: 'F1 sintética', finalidade: 'Teste' },
    escalonada: version.conteudo,
    resultado: {
      motor: '1',
      massaKg: '100',
      materiaSecaKg: '86',
      umidadePercentual: '14',
      avisos: ['Simulação manual'],
      componentes: [
        {
          componente: 'PB',
          metodo: 'Sintético',
          contexto: '',
          unidade: 'g/kg',
          estado: 'Parcial',
          valorBN: null,
          valorMS: null,
          contribuicaoConhecidaBN: '60',
          coberturaPercentual: '60',
          estimado: false,
          kcalBN: null,
          contribuicoes: [
            {
              linhaId: 'line-1',
              item: 'A',
              massaKg: '60',
              quantidadeComponente: '6000',
              origem: 'Declarado',
              fonte: 'Fixture A',
              motivo: null,
            },
            {
              linhaId: 'line-2',
              item: 'B',
              massaKg: '40',
              quantidadeComponente: null,
              origem: null,
              fonte: 'Fixture B',
              motivo: 'PB ausente',
            },
          ],
        },
      ],
      metas: [
        {
          meta: {
            componente: 'PB',
            metodo: 'Sintético',
            contexto: '',
            base: 'BN',
            unidade: 'g/kg',
            minimo: '250',
            maximo: null,
            origem: 'InformadaUsuario',
            fonte: null,
            aceitarEstimados: true,
          },
          estado: 'Indeterminada',
          motivo: 'Dados incompletos',
          impossibilidadeLocal: false,
        },
      ],
    },
    rendimentoPercentual: '100',
    balancoKg: '0',
    problemas: [],
    linhas: [],
  },
}
const paged = <T,>(items: T[], page = 1, totalPages = 1) => ({
  items,
  page,
  pageSize: 25,
  totalItems: items.length,
  totalPages,
})
function mount(path: string, component: React.ReactNode, route: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path={route} element={component} />
        <Route
          path="/producao/formulacao/:simulationId"
          element={<p>Snapshot salvo</p>}
        />
        <Route path="/itens" element={<p>Lista de itens</p>} />
      </Routes>
    </MemoryRouter>,
  )
}
beforeEach(() => {
  vi.mocked(httpRequest).mockReset()
  vi.mocked(httpRequest).mockImplementation(async (path) => {
    if (path === '/itens/item-1') return item
    if (path === '/perfis-nutricionais/profile-1') return profile
    if (path === '/receitas/versoes/version-1') return version
    if (path === '/simulacoes-formulacao/simulation-1') return simulation
    if (path === '/componentes-nutricionais')
      return [
        {
          codigo: 'PB',
          nome: 'Proteína bruta',
          grandeza: 'massa',
          semantica: 'Não proteína digestível',
          versao: 1,
        },
      ]
    if (path.includes('?')) return paged([])
    throw new Error(`Requisição inesperada: ${path}`)
  })
})
it('consulta item inativo em leitura e preserva ações e fontes históricas', async () => {
  mount('/itens/item-1', <ItemDetailsPage />, '/itens/:id')
  expect(await screen.findByText(item.nome)).toBeInTheDocument()
  expect(screen.getByText('Inativo')).toBeInTheDocument()
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Editar' })).toHaveAttribute(
    'href',
    '/itens/item-1/editar',
  )
  expect(
    screen.queryByRole('link', { name: 'Novo perfil' }),
  ).not.toBeInTheDocument()
  expect(httpRequest).toHaveBeenCalledWith('/itens/item-1', {
    authenticated: true,
  })
})
it.each([404, 403, 500])(
  'exibe erro HTTP %s sem esconder retorno',
  async (status) => {
    vi.mocked(httpRequest).mockRejectedValue(new HttpError(status))
    mount('/itens/item-1', <ItemDetailsPage />, '/itens/:id')
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Voltar para Insumos e produtos' }),
    ).toBeInTheDocument()
  },
)
it('perfil publicado distingue zero e desconhecido e oferece nova revisão sem formulário', async () => {
  mount(
    '/producao/perfis/profile-1',
    <ProfileDetailsPage />,
    '/producao/perfis/:profileId',
  )
  expect(await screen.findByText('0 g/kg BN · Declarado')).toBeInTheDocument()
  expect(screen.getByText('Motivo: Não analisado')).toBeInTheDocument()
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument()
  expect(
    screen.getByRole('link', { name: 'Criar nova revisão' }),
  ).toHaveAttribute('href', '/itens/item-1/perfis/novo?origem=profile-1')
})
it('publicação de receita envia revisão esperada e mostra conflito junto ao comando', async () => {
  const draft = { ...version, estado: 'Rascunho', revisao: 1 }
  vi.mocked(httpRequest).mockImplementation(async (path) => {
    if (path.endsWith('/publicacao'))
      throw new HttpError(409, '', 'Registro mudou. Recarregue.')
    if (path === '/receitas/versoes/version-1') return draft
    if (path.includes('?')) return paged([])
    throw new Error(path)
  })
  mount(
    '/producao/receitas/versoes/version-1',
    <RecipeVersionDetailsPage />,
    '/producao/receitas/versoes/:versionId',
  )
  fireEvent.click(
    await screen.findByRole('button', { name: 'Publicar receita' }),
  )
  expect(await screen.findByRole('alert')).toHaveTextContent('Registro mudou')
  expect(httpRequest).toHaveBeenCalledWith(
    '/receitas/versoes/version-1/publicacao',
    expect.objectContaining({
      body: { versaoEsperada: 1 },
      authenticated: true,
    }),
  )
})
it('resultado parcial exibe cobertura, contribuição conhecida, fonte e meta indeterminada', async () => {
  mount(
    '/producao/formulacao/simulation-1',
    <SimulationDetailsPage />,
    '/producao/formulacao/:simulationId',
  )
  expect(
    await screen.findByText('Parcial · Cobertura: 60%'),
  ).toBeInTheDocument()
  expect(
    screen.getByText(
      'Contribuição conhecida: 60 g/kg BN; total indeterminado.',
    ),
  ).toBeInTheDocument()
  fireEvent.click(screen.getByText('Contribuições e fontes de PB'))
  expect(screen.getByText('PB ausente')).toBeInTheDocument()
  expect(screen.getByText('Fonte: Fixture B')).toBeInTheDocument()
  expect(screen.getByText('PB · BN · Indeterminada')).toBeInTheDocument()
  expect(
    screen.getByRole('link', { name: 'Nova variação explícita' }),
  ).toHaveAttribute('href', '/producao/formulacao/nova?anterior=simulation-1')
})
it('retry da simulação preserva a chave e os decimais como strings', async () => {
  let saves = 0
  vi.mocked(httpRequest).mockImplementation(async (path) => {
    if (path === '/simulacoes-formulacao') {
      if (++saves === 1) throw new NetworkError()
      return simulation
    }
    if (path === '/receitas/versoes/version-1') return version
    if (path === '/componentes-nutricionais') return []
    if (path.includes('?')) return paged([])
    throw new Error(path)
  })
  mount(
    '/producao/formulacao/nova?receita=version-1',
    <SimulationFormPage />,
    '/producao/formulacao/nova',
  )
  await waitFor(() =>
    expect(screen.getByLabelText('Tamanho da simulação')).toHaveValue('100'),
  )
  fireEvent.change(screen.getByLabelText('Tamanho da simulação'), {
    target: { value: '12,5' },
  })
  fireEvent.click(
    screen.getByRole('button', { name: 'Calcular e salvar simulação' }),
  )
  await screen.findByRole('alert')
  fireEvent.click(
    screen.getByRole('button', { name: 'Calcular e salvar simulação' }),
  )
  await screen.findByText('Snapshot salvo')
  const requests = vi
    .mocked(httpRequest)
    .mock.calls.filter(([path]) => path === '/simulacoes-formulacao')
  expect(requests).toHaveLength(2)
  expect(requests[0][1]?.headers).toEqual(requests[1][1]?.headers)
  expect(requests[0][1]?.body).toMatchObject({
    tamanho: '12.5',
    variacao: null,
  })
})
it('seletor busca no servidor, pagina completamente e mantém referência histórica', async () => {
  vi.mocked(httpRequest).mockImplementation(async (path) =>
    path.includes('page=2')
      ? paged([{ id: 'later', nome: 'Página 2' }], 2, 2)
      : paged([{ id: 'first', nome: 'Página 1' }], 1, 2),
  )
  const onChange = vi.fn()
  render(
    <ServerSelect<{ id: string; nome: string }>
      label="Material"
      path="/itens"
      value="historical"
      onChange={onChange}
      caption={(i) => i.nome}
    />,
  )
  expect(
    await screen.findByRole('option', { name: 'Página 1' }),
  ).toBeInTheDocument()
  expect(
    screen.getByRole('option', { name: /Referência selecionada: historical/ }),
  ).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
  await screen.findByRole('option', { name: 'Página 2' })
  fireEvent.change(screen.getByLabelText('Selecionar Material'), {
    target: { value: 'later' },
  })
  expect(onChange).toHaveBeenCalledWith('later', {
    id: 'later',
    nome: 'Página 2',
  })
  expect(httpRequest).toHaveBeenCalledWith(expect.stringContaining('page=2'), {
    authenticated: true,
  })
})
it('lista mantém paginação e filtros no estado do link Visualizar', async () => {
  vi.mocked(httpRequest).mockImplementation(async (path) =>
    path.startsWith('/itens?') ? paged([item], 1, 2) : paged([]),
  )
  render(
    <MemoryRouter>
      <ItemsPage />
    </MemoryRouter>,
  )
  fireEvent.change(await screen.findByLabelText('Buscar código ou nome'), {
    target: { value: 'A%_' },
  })
  await screen.findByText('A — Ingrediente sintético')
  const detail = screen.getByRole('link', { name: 'Visualizar' })
  expect(detail).toHaveAttribute('href', '/itens/item-1')
  expect(httpRequest).toHaveBeenCalledWith(
    expect.stringContaining('search=A%25_'),
    { authenticated: true },
  )
})
it('formulário desconhecido não oferece número enquanto o estado não for conhecido', async () => {
  mount(
    '/itens/item-1/perfis/novo',
    <ProfileFormPage />,
    '/itens/:id/perfis/novo',
  )
  fireEvent.click(
    await screen.findByRole('button', { name: 'Adicionar observação' }),
  )
  expect(
    screen.queryByLabelText('Valor (zero explícito permitido)'),
  ).not.toBeInTheDocument()
  const observation = screen.getByRole('group', { name: 'Observação 1' })
  fireEvent.change(within(observation).getByLabelText('Estado do valor'), {
    target: { value: 'Conhecido' },
  })
  expect(screen.getByLabelText('Valor (zero explícito permitido)')).toHaveValue(
    '',
  )
})
