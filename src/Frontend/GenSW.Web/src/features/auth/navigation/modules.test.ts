import { activeNavigation, modulePages, modulesForArea, sortedFunctionalAreas } from './modules'

describe('mapa de navegação', () => {
  it('ordena áreas, módulos e páginas pelo nome em português, incluindo destinos já existentes', () => {
    expect(sortedFunctionalAreas.map(area => area.name)).toEqual(['Cadastros básicos', 'Processos gerenciais', 'Produção e operações'])
    expect(modulesForArea('cadastros', true).map(module => module.name)).toEqual(['Animais', 'Insumos e produtos', 'Pessoas', 'Propriedades', 'Taxonomia'])
    const stock = modulesForArea('operacoes', true).find(module => module.id === 'estoque')!
    expect(modulePages(stock).map(page => [page.name, page.route])).toEqual([
      ['Locais', '/estoque/locais'], ['Lotes', '/estoque/lotes'], ['Movimentos', '/estoque/movimentos'],
      ['Reconciliação', '/estoque/reconciliacao'], ['Saldos', '/estoque/saldos'],
    ])
    const catalog = modulesForArea('cadastros', true).find(module => module.id === 'produtos')!
    expect(modulePages(catalog).map(page => page.route)).toEqual(['/itens', '/itens/categorias'])
    expect(modulesForArea('gerenciais', true).map(module => module.name)).toEqual(['Financeiro'])
  })

  it.each([
    ['/animais/id/pesagens/record', 'animais', '/animais'],
    ['/producao/perfis/profile/editar', 'produtos', '/itens'],
    ['/itens/categorias', 'produtos', '/itens/categorias'],
    ['/producao/receitas/versoes/version/editar', 'transformacoes', '/producao/receitas'],
    ['/producao/formulacao/comparacoes/id', 'transformacoes', '/producao/formulacao'],
    ['/financeiro/lancamentos/id', 'financeiro', '/financeiro'],
    ['/estoque/operacoes/entrada', 'estoque', undefined],
  ])('identifica o destino proprietário de %s', (pathname, moduleId, route) => {
    const active = activeNavigation(pathname)
    expect(active?.module.id).toBe(moduleId)
    expect(active?.page?.route).toBe(route)
  })

  it('não aceita prefixos semelhantes como rota do módulo', () => {
    expect(activeNavigation('/itens-outros')).toBeUndefined()
    expect(activeNavigation('/producao/perfis-outros')).toBeUndefined()
    expect(activeNavigation('/')).toBeUndefined()
  })
})
