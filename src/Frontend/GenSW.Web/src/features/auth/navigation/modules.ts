export const functionalAreas = [
  {
    id: 'cadastros',
    name: 'Cadastros básicos',
    description: 'Pessoas, animais e referências compartilhadas pela gestão.',
  },
  {
    id: 'operacoes',
    name: 'Produção e operações',
    description: 'Acompanhamento da produção e dos processos do campo.',
  },
  {
    id: 'gerenciais',
    name: 'Processos gerenciais',
    description: 'Controle financeiro e visão de gestão do negócio.',
  },
] as const

type FunctionalArea = (typeof functionalAreas)[number]['id']
export type AvailableFeature = { id: string; name: string; route: string }
type ModuleInfo = {
  id: string
  name: string
  description: string
  area: FunctionalArea
}

export type NavigationModule = ModuleInfo & (
  | { state: 'planned'; route?: never; features?: never }
  | { state: 'available'; route: string; features?: never }
  | {
      state: 'available'
      route?: never
      features: readonly [AvailableFeature, ...AvailableFeature[]]
    }
)

export const navigationModules: readonly NavigationModule[] = [
  {
    id: 'pessoas', name: 'Pessoas', area: 'cadastros', state: 'available',
    description: 'Cadastro e consulta de pessoas físicas e jurídicas.', route: '/pessoas',
  },
  {
    id: 'taxonomia', name: 'Taxonomia', area: 'cadastros', state: 'available',
    description: 'Espécies, raças e variedades para classificar os animais.',
    features: [
      { id: 'especies', name: 'Espécies', route: '/especies' },
      { id: 'racas', name: 'Raças', route: '/racas' },
      { id: 'variedades', name: 'Variedades', route: '/variedades' },
    ],
  },
  {
    id: 'animais', name: 'Animais', area: 'cadastros', state: 'available',
    description: 'Cadastro individual, pesos, imagens, genealogia e produção de ovos.', route: '/animais',
  },
  {
    id: 'produtos', name: 'Insumos e produtos', area: 'cadastros', state: 'available',
    description: 'Catálogo único de materiais, conversões e perfis nutricionais.',
    features: [
      { id: 'catalogo', name: 'Catálogo', route: '/itens' },
      { id: 'categorias-itens', name: 'Categorias', route: '/itens/categorias' },
    ],
  },
  {
    id: 'propriedades', name: 'Propriedades', area: 'cadastros', state: 'available',
    description: 'Unidades operacionais físicas e histórico de localização dos animais.', route: '/propriedades',
  },
  {
    id: 'reproducao', name: 'Reprodução', area: 'operacoes', state: 'available',
    description: 'Do cruzamento ao ciclo reprodutivo e ao registro das proles.',
    features: [
      { id: 'cruzamentos', name: 'Cruzamentos', route: '/cruzamentos' },
      { id: 'ciclos-reprodutivos', name: 'Ciclos reprodutivos', route: '/ciclos-reprodutivos' },
      { id: 'proles', name: 'Proles', route: '/proles' },
    ],
  },
  {
    id: 'producao', name: 'Produção agrícola', area: 'operacoes', state: 'planned',
    description: 'Planejamento e acompanhamento da produção agrícola.',
  },
  {
    id: 'transformacoes', name: 'Produção — transformações', area: 'operacoes', state: 'available',
    description: 'Receitas versionadas, ordens, execução física e genealogia dos materiais.',
    features: [
      { id: 'ordens-producao', name: 'Ordens', route: '/producao/ordens' },
      { id: 'receitas', name: 'Receitas', route: '/producao/receitas' },
      { id: 'formulacao', name: 'Formulação e comparação', route: '/producao/formulacao' },
    ],
  },
  {
    id: 'producao-animal', name: 'Produção animal', area: 'operacoes', state: 'planned',
    description: 'Gestão integrada da produção animal; registros de ovos já disponíveis em Animais.',
  },
  {
    id: 'genetica', name: 'Genética', area: 'operacoes', state: 'planned',
    description: 'Melhoramento genético; a genealogia atual permanece em Animais.',
  },
  {
    id: 'estoque', name: 'Estoque', area: 'operacoes', state: 'available',
    description: 'Locais, lotes, entradas manuais e livro de movimentos físicos.',
    features: [
      { id: 'saldos', name: 'Saldos', route: '/estoque/saldos' },
      { id: 'locais-estoque', name: 'Locais', route: '/estoque/locais' },
      { id: 'lotes-materiais', name: 'Lotes', route: '/estoque/lotes' },
      { id: 'movimentos-estoque', name: 'Movimentos', route: '/estoque/movimentos' },
      { id: 'reconciliacao-estoque', name: 'Reconciliação', route: '/estoque/reconciliacao' },
    ],
  },
  {
    id: 'financeiro', name: 'Financeiro', area: 'gerenciais', state: 'available',
    description: 'Recebimentos, pagamentos e fechamento mensal do caixa.',
    features: [{ id: 'fluxo-de-caixa', name: 'Fluxo de caixa', route: '/financeiro' }],
  },
  {
    id: 'compras', name: 'Compras', area: 'gerenciais', state: 'planned',
    description: 'Gestão das compras e do abastecimento.',
  },
  {
    id: 'vendas', name: 'Vendas', area: 'gerenciais', state: 'planned',
    description: 'Gestão comercial de produtos e animais.',
  },
  {
    id: 'fiscal', name: 'Fiscal', area: 'gerenciais', state: 'planned',
    description: 'Documentos e obrigações fiscais.',
  },
  {
    id: 'contabil', name: 'Contábil', area: 'gerenciais', state: 'planned',
    description: 'Organização dos registros contábeis.',
  },
  {
    id: 'relatorios', name: 'Relatórios', area: 'gerenciais', state: 'planned',
    description: 'Consultas consolidadas para acompanhar a gestão.',
  },
  {
    id: 'bi', name: 'BI / Indicadores', area: 'gerenciais', state: 'planned',
    description: 'Análise e indicadores gerenciais do negócio.',
  },
]

const alphabetic = new Intl.Collator('pt-BR', { sensitivity: 'base', numeric: true })

export const sortedFunctionalAreas = [...functionalAreas].sort((a, b) => alphabetic.compare(a.name, b.name))

export function modulesForArea(area: FunctionalArea, availableOnly = false) {
  return navigationModules
    .filter((module) => module.area === area && (!availableOnly || module.state === 'available'))
    .sort((a, b) => alphabetic.compare(a.name, b.name))
}

export function modulePages(module: NavigationModule): AvailableFeature[] {
  if (module.state === 'planned') return []
  return (module.route !== undefined
    ? [{ id: module.id, name: module.name, route: module.route }]
    : [...module.features]).sort((a, b) => alphabetic.compare(a.name, b.name))
}

// Match whole route segments; a profile belongs to its Item, not the production area.
export function activeNavigation(pathname: string) {
  if (pathname === '/producao/genealogia' || pathname.startsWith('/producao/genealogia/')) {
    const module = navigationModules.find((entry) => entry.id === 'transformacoes')!
    return { module, page: modulePages(module).find((entry) => entry.id === 'ordens-producao')! }
  }
  if (pathname === '/producao/perfis' || pathname.startsWith('/producao/perfis/')) {
    const module = navigationModules.find((entry) => entry.id === 'produtos')!
    return { module, page: modulePages(module).find((entry) => entry.id === 'catalogo')! }
  }
  const matches = navigationModules.flatMap((module) => modulePages(module)
    .filter((page) => pathname === page.route || pathname.startsWith(`${page.route}/`))
    .map((page) => ({ module, page })))
    .sort((a, b) => b.page.route.length - a.page.route.length)
  if (matches[0]) return matches[0]
  if (pathname === '/estoque' || pathname.startsWith('/estoque/')) {
    return { module: navigationModules.find((entry) => entry.id === 'estoque')!, page: undefined }
  }
  return undefined
}
