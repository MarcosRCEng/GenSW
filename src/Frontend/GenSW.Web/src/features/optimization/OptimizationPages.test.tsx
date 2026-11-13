import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, NetworkError } from '../../shared/http/httpErrors'
import { useAuth } from '../auth/hooks/useAuth'
import type { Item, Profile, Recipe, RecipeData, RecipeVersion } from '../formulation/types'
import { OptimizationFormPage } from './OptimizationForm'
import { OptimizationDetailsPage, OptimizationsPage } from './OptimizationPages'
import { optimizationApi, optimizationPath, outcomeLabels } from './api'
import type { DraftPreview, DraftReceipt, OptimizationExecution, OptimizationGoal, OptimizationRequest, Outcome, Solution } from './types'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: vi.fn() }))

const guid = (index: number) => `10000000-0000-0000-0000-${String(index).padStart(12, '0')}`
const actorId = guid(90)
const runId = guid(91)
const now = '2026-11-12T10:00:00Z'
const auth: ReturnType<typeof useAuth> = {
  user: { userId: actorId, pessoaId: guid(89), nome: 'Operador sintético', userName: 'teste', roles: [] },
  isAuthenticated: true, isInitializing: false, bootstrap: vi.fn(), login: vi.fn(), logout: vi.fn(),
}

const item = (index: number, nome: string): Item => ({
  id: guid(index), nome, codigo: `I${index}`, descricao: null, categoriaId: null, classe: 'Alimentar', unidade: 'kg',
  podeEntrar: true, podeProduzir: true, usoInterno: true, venda: false, ativo: true, revisao: 2, unidadeFixada: true, createdAtUtc: now, updatedAtUtc: now,
})
const a = item(1, 'Ingrediente A')
const b = item(2, 'Ingrediente B')
const output = item(3, 'Mistura sintética')
const liquid = { ...item(4, 'Ingrediente em litros'), unidade: 'L' }
const profile = (index: number, ingredient: Item): Profile => ({
  id: guid(index), itemId: ingredient.id, numero: 1, revisao: 3, estado: 'Publicado', createdAtUtc: now, publishedAtUtc: now,
  conteudo: { nome: `Perfil ${ingredient.nome}`, fonte: 'Fixture', metodo: 'Sintético', preparacao: 'BN', referenciaAmostra: 'Amostra', contexto: '', especieId: null, fase: null, dataFonte: null, dataColeta: null, valores: [] },
})
const pa = profile(11, a)
const pb = profile(12, b)
const goal: OptimizationGoal = { id: guid(15), componente: 'PB', metodo: 'Sintético', contexto: '', base: 'BN', unidade: 'g/kg', minimo: '250', maximo: null, origem: 'InformadaUsuario', fonte: null, aceitarEstimados: false, justificativa: 'Meta da fixture' }
const request: OptimizationRequest = {
  versaoModelo: 'f05-n1/1', modo: 'N1', disponibilidade: false, massaKg: '100', referenciaDescricao: 'Mistura da fixture', receitaId: null, receitaRevisao: null,
  receitaVersaoId: null, receitaVersaoRevisao: null, anteriorId: null, especieId: null, fase: null, preparo: null,
  candidatos: [a, b].map((i, index) => ({ id: guid(20 + index), itemId: i.id, itemRevisao: i.revisao, perfilId: [pa, pb][index].id, perfilRevisao: 3, referenciaKg: index === 0 ? '60' : '40', inclusaoMinima: '0', inclusaoMaxima: '100', justificativa: null })), metas: [goal],
}
const recipe: Recipe = { id: guid(30), codigo: 'R1', nome: 'Receita sintética', finalidade: 'Fixture', ativo: true, revisao: 2 }
const data: RecipeData = {
  tipo: 'MisturaSimples', modo: 'Quantidade', tamanhoReferencia: '100', unidadeReferencia: 'kg', observacao: null, perdas: [], etapas: [], retencoes: [],
  entradas: request.candidatos.map((c) => ({ id: c.id, itemId: c.itemId, perfilId: c.perfilId, quantidade: c.referenciaKg, papel: 'Alimentar', unidade: 'kg', escala: 'Variavel', conversaoId: null, subReceitaVersaoId: null, subSaidaId: null, inclusaoMinima: c.id === guid(20) ? '25' : null, inclusaoMaxima: null })),
  saidas: [{ id: guid(32), itemId: output.id, nome: output.nome, quantidade: '100', unidade: 'kg', escala: 'Variavel', principal: true, perfilId: null, conversaoId: null, massaLiquidaKg: null, massaDrenadaKg: null, massaBrutaKg: null, metodoMedicao: null }],
}
const version: RecipeVersion = { id: guid(31), receitaId: recipe.id, numero: 1, revisao: 4, estado: 'Publicado', conteudo: data }
const solution: Solution = {
  rawMasses: { [guid(20)]: '50.0000001', [guid(21)]: '49.9999999' },
  quantizedMasses: { [guid(20)]: '50', [guid(21)]: '50' }, residualsKg: { [guid(20)]: '0.0000001', [guid(21)]: '-0.0000001' },
  rawDeviationKg: '20', quantizedDeviationKg: '20', redistributedKg: '10', objectiveQuality: 'ViavelAposQuantizacao',
  constraints: [{ id: 'PB/min', kind: 'Meta', referenceId: goal.id, value: '250', unit: 'g/kg BN', minimum: '250', lowerSlack: '0', active: true, nearBoundary: false, violated: false }],
  contributions: [{ goalId: goal.id, ingredientId: guid(20), value: '5000', unit: 'g' }, { goalId: goal.id, ingredientId: guid(21), value: '20000', unit: 'g' }],
}

function execution(outcome: Outcome, body = request): OptimizationExecution {
  const incomplete = outcome === 'DadosIncompletos'
  return {
    id: runId, autorId: actorId, createdAtUtc: now, estado: 'Finalizada', revisao: 2, pedido: body,
    entradas: {
      corteUtc: now, metas: body.metas,
      candidatos: body.candidatos.map((c) => ({ id: c.id, itemId: c.itemId, nome: c.itemId === a.id ? a.nome : b.nome, excluido: false, perfil: c.itemId === a.id ? pa : pb,
        coeficientes: body.metas.map((meta) => ({ metaId: meta.id, estado: incomplete ? 'Desconhecido' : 'Conhecido', valorNormalizado: incomplete ? null : '100', unidade: 'g/kg', fonte: 'Documento da fixture' })) })),
      diagnosticos: incomplete ? [{ codigo: 'CoeficienteAusente', mensagem: 'PB do ingrediente B ausente', desfecho: outcome, candidatoId: guid(21), metaId: goal.id }] : [],
    },
    resultado: { desfecho: outcome, codigo: outcome, motor: {
      outcome, code: outcome, candidate: outcome === 'SolucaoViavel' || outcome === 'LimiteTempo' ? solution : null,
      proof: outcome === 'InviabilidadeDemonstrada' ? { code: 'IntervalosDisjuntos', constraintIds: ['PB/min', 'EM/min'], details: ['PB exige B ≥ 50 kg. EM exige B ≤ 47,5 kg.'] } : null,
    } },
  }
}

const collection = <T,>(items: T[]) => ({ items, page: 1, pageSize: 25, totalItems: items.length, totalPages: 1 })
const components = [{ codigo: 'PB', nome: 'Proteína bruta', grandeza: 'massa', semantica: 'Fixture', versao: 1 }]
const receipt: DraftReceipt = { id: guid(92), otimizacaoId: runId, receitaId: recipe.id, versaoId: guid(33), createdAtUtc: now }
const preview: DraftPreview = { hash: 'hash-da-previa', conteudo: data, avisos: ['A conferência da fixture é necessária.'], exigeAceiteResiduos: true }

function mockApi(outcome: Outcome = 'SolucaoViavel') {
  const post = vi.mocked(httpRequest)
  post.mockImplementation(async (path, options) => {
    const route = path.split('?')[0]
    if (route === '/componentes-nutricionais') return components
    if (route === '/especies') return collection([])
    if (route === '/itens') return collection([a, b, output, liquid])
    if (route === `/itens/${a.id}`) return a
    if (route === `/itens/${b.id}`) return b
    if (route === `/itens/${a.id}/perfis-nutricionais`) return collection([pa])
    if (route === `/itens/${b.id}/perfis-nutricionais`) return collection([pb])
    if (route === `/perfis-nutricionais/${pa.id}`) return pa
    if (route === `/perfis-nutricionais/${pb.id}`) return pb
    if (route === '/receitas') return collection([recipe])
    if (route === `/receitas/${recipe.id}/versoes`) return collection([version])
    if (route === `/receitas/versoes/${version.id}/escalonamento`) return data
    if (route === `${optimizationApi}/previas`) return execution(outcome, options?.body as OptimizationRequest).entradas
    if (route === `${optimizationApi}/por-chave`) return execution(outcome)
    if (route === optimizationApi && options?.method === 'POST') return execution(outcome, options.body as OptimizationRequest)
    if (route === optimizationApi) return collection([{ id: runId, autorId: actorId, createdAtUtc: now, estado: 'Finalizada', desfecho: outcome }])
    if (route === `${optimizationApi}/${runId}`) return execution(outcome)
    if (route === `${optimizationApi}/${runId}/previas-rascunho`) return preview
    if (route === `${optimizationApi}/${runId}/rascunhos`) throw new NetworkError()
    if (route === `${optimizationApi}/${runId}/rascunhos/por-chave`) return receipt
    throw new HttpError(404)
  })
}

function open(path: string) {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route path={optimizationPath} element={<OptimizationsPage />} />
    <Route path={`${optimizationPath}/nova`} element={<OptimizationFormPage />} />
    <Route path={`${optimizationPath}/:optimizationId`} element={<OptimizationDetailsPage />} />
    <Route path="/producao/receitas/versoes/:versionId/editar" element={<h1>Editor da receita</h1>} />
  </Routes></MemoryRouter>)
}

const change = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } })
const click = (name: string) => fireEvent.click(screen.getByRole('button', { name }))

describe('Otimização assistida F05', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.mocked(useAuth).mockReturnValue(auth)
    vi.mocked(httpRequest).mockReset()
    mockApi()
  })

  it('constrói a formulação manual com revisões e perfis explícitos e calcula somente após conferir os coeficientes', async () => {
    open(`${optimizationPath}/nova`)
    change('Massa da mistura (base natural)', '100000')
    change('Unidade da massa', 'g')
    change('Identificação da formulação de referência', 'Mistura da fixture')
    click('Adicionar ingrediente')
    await screen.findByRole('option', { name: 'I1 · Ingrediente A' })
    expect(screen.queryByRole('option', { name: /Ingredientes? em litros/ })).not.toBeInTheDocument()
    change('Selecionar Item do ingrediente 1', a.id)
    await screen.findByRole('option', { name: 'Perfil Ingrediente A · v1 · Amostra' })
    expect(screen.getByLabelText('Selecionar Perfil publicado do ingrediente 1')).toHaveValue('')
    change('Selecionar Perfil publicado do ingrediente 1', pa.id)
    change('Massa de referência do ingrediente 1', '60,000000')
    click('Adicionar ingrediente')
    const second = screen.getByRole('group', { name: 'Ingrediente 2' })
    await within(second).findByRole('option', { name: 'I2 · Ingrediente B' })
    change('Selecionar Item do ingrediente 2', b.id)
    await screen.findByRole('option', { name: 'Perfil Ingrediente B · v1 · Amostra' })
    change('Selecionar Perfil publicado do ingrediente 2', pb.id)
    change('Massa de referência do ingrediente 2', '40')
    click('Adicionar meta')
    expect(screen.getByLabelText('Componente da meta 1')).toHaveValue('')
    expect(screen.getByLabelText('Aceitar estimativas na meta 1')).not.toBeChecked()
    change('Componente da meta 1', 'PB')
    change('Método da meta 1', 'Sintético')
    change('Mínimo da meta 1', '250')
    change('Justificativa da meta 1', 'Meta da fixture')
    click('Revisar coeficientes')
    expect(await screen.findByRole('heading', { name: 'Conferir pedido' })).toHaveFocus()
    expect(screen.getByRole('table', { name: 'Matriz de ingredientes e metas' })).toBeInTheDocument()
    expect(vi.mocked(httpRequest).mock.calls.filter(([path, options]) => path === optimizationApi && options?.method === 'POST')).toHaveLength(0)
    click('Executar otimização')
    expect(await screen.findByRole('heading', { name: 'Solução viável' })).toBeInTheDocument()
    const call = vi.mocked(httpRequest).mock.calls.find(([path]) => path === optimizationApi)
    expect(call?.[1]).toMatchObject({ method: 'POST', headers: { 'Idempotency-Key': expect.any(String) }, body: {
      massaKg: '100', disponibilidade: false, candidatos: [expect.objectContaining({ itemRevisao: 2, perfilId: pa.id, perfilRevisao: 3, referenciaKg: '60' }), expect.objectContaining({ perfilId: pb.id, referenciaKg: '40' })],
      metas: [expect.objectContaining({ aceitarEstimados: false, base: 'BN', minimo: '250' })],
    } })
  })

  it('importa a versão publicada e mantém as massas de referência e os limites registrados', async () => {
    open(`${optimizationPath}/nova`)
    change('Origem da mistura', 'receita')
    await screen.findByRole('option', { name: 'R1 · Receita sintética' })
    change('Selecionar Receita', recipe.id)
    await screen.findByRole('option', { name: 'Versão 1 · 100 kg' })
    change('Selecionar Versão publicada', version.id)
    click('Importar composição para a massa informada')
    expect(await screen.findByText(/Composição carregada para 100 kg BN/)).toBeInTheDocument()
    expect(screen.getByText('I1 · Ingrediente A · 60 kg BN na receita')).toBeInTheDocument()
    expect(screen.getByLabelText('Inclusão mínima (%) do ingrediente 1')).toHaveValue('25')
    expect(screen.getByLabelText('Selecionar Perfil publicado do ingrediente 1')).toHaveValue(pa.id)
    expect(screen.queryByLabelText('Massa de referência do ingrediente 1')).not.toBeInTheDocument()
    change('Inclusão mínima (%) do ingrediente 1', '20')
    expect(screen.getByLabelText('Justificativa de exclusão ou alteração de limites do ingrediente 1')).toBeRequired()
  })

  it.each(Object.keys(outcomeLabels) as Outcome[])('distingue %s e habilita rascunho só para solução viável', async (outcome) => {
    mockApi(outcome)
    open(`${optimizationPath}/${runId}`)
    expect(await screen.findByRole('heading', { name: outcomeLabels[outcome] })).toBeInTheDocument()
    expect(screen.getByText(/Ótimo matemático não certifica uma dieta/)).toBeInTheDocument()
    expect(Boolean(screen.queryByRole('button', { name: 'Preparar rascunho desta solução' }))).toBe(outcome === 'SolucaoViavel')
    if (outcome === 'LimiteTempo') expect(screen.getByRole('heading', { name: 'Candidato para diagnóstico' })).toBeInTheDocument()
    if (outcome === 'DadosIncompletos') expect(screen.getByText(/PB do ingrediente B ausente/)).toBeInTheDocument()
    if (outcome === 'InviabilidadeDemonstrada') expect(screen.getByText(/PB exige B ≥ 50 kg/)).toBeInTheDocument()
    expect(vi.mocked(httpRequest).mock.calls.some(([, options]) => options?.method === 'POST')).toBe(false)
  })

  it('cria rascunho somente após a prévia e os aceites e recupera uma resposta perdida por consulta', async () => {
    open(`${optimizationPath}/${runId}`)
    fireEvent.click(await screen.findByRole('button', { name: 'Preparar rascunho desta solução' }))
    change('Código da nova receita', 'R2')
    change('Nome da nova receita', 'Variação sintética')
    change('Finalidade da nova receita', 'Teste')
    await screen.findByRole('option', { name: 'I3 · Mistura sintética' })
    change('Selecionar Item de saída', output.id)
    click('Visualizar prévia do rascunho')
    const confirm = await screen.findByRole('button', { name: 'Confirmar criação do rascunho' })
    expect(confirm).toBeDisabled()
    expect(vi.mocked(httpRequest).mock.calls.filter(([path]) => path === `${optimizationApi}/${runId}/rascunhos`)).toHaveLength(0)
    fireEvent.click(screen.getByLabelText('Aceito as quantidades arredondadas e os resíduos apresentados'))
    expect(confirm).toBeDisabled()
    fireEvent.click(screen.getByLabelText('Conferi e aceito os avisos desta prévia'))
    fireEvent.click(confirm)
    expect(await screen.findByText('Não foi possível conectar à API.')).toBeInTheDocument()
    click('Consultar o rascunho')
    expect(await screen.findByRole('heading', { name: 'Editor da receita' })).toBeInTheDocument()
    const posts = vi.mocked(httpRequest).mock.calls.filter(([path]) => path === `${optimizationApi}/${runId}/rascunhos`)
    expect(posts).toHaveLength(1)
    expect(posts[0][1]?.body).toMatchObject({ hashPrevia: preview.hash, aceitaResiduos: true, aceitaAvisos: true })
    expect(vi.mocked(httpRequest).mock.calls).toContainEqual([
      `${optimizationApi}/${runId}/rascunhos/por-chave?chave=${new Headers(posts[0][1]?.headers).get('Idempotency-Key')}`, { authenticated: true },
    ])
  })

  it('recupera a execução por chave após voltar ao formulário e não repete o POST', async () => {
    sessionStorage.setItem(`gensw.optimization.request.v1:${actorId}:execucao`, guid(99))
    open(`${optimizationPath}/nova`)
    click('Consultar o pedido')
    expect(await screen.findByRole('heading', { name: 'Solução viável' })).toBeInTheDocument()
    expect(vi.mocked(httpRequest).mock.calls).toContainEqual([`${optimizationApi}/por-chave?chave=${guid(99)}`, { authenticated: true }])
    expect(vi.mocked(httpRequest).mock.calls.some(([, options]) => options?.method === 'POST')).toBe(false)
    await waitFor(() => expect(sessionStorage.getItem(`gensw.optimization.request.v1:${actorId}:execucao`)).toBeNull())
  })
})
