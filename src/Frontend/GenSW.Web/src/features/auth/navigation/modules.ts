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
type AvailableFeature = { id: string; name: string; route: string }
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
    id: 'produtos', name: 'Produtos', area: 'cadastros', state: 'planned',
    description: 'Catálogo de produtos e insumos.',
  },
  {
    id: 'propriedades', name: 'Propriedades', area: 'cadastros', state: 'planned',
    description: 'Organização das propriedades e unidades rurais.',
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
    id: 'producao', name: 'Produção', area: 'operacoes', state: 'planned',
    description: 'Planejamento e acompanhamento da produção agrícola.',
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
    id: 'estoque', name: 'Estoque', area: 'operacoes', state: 'planned',
    description: 'Controle de materiais, insumos e produtos armazenados.',
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
