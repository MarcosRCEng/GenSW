import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, NetworkError } from '../../shared/http/httpErrors'
import { useAuth } from '../auth/hooks/useAuth'
import { destination, event as inventoryEvent, local, lot, observed, page, preview as inventoryPreview, responsible, stock } from '../inventory/inventoryFixtures'
import { OrderActionPage } from './OrderActionPage'
import { ProductionGenealogyPage } from './GenealogyPage'
import { OrderDetailsPage, OrdersPage } from './OrderPages'
import { order, productionPage, recipe, started, version } from './productionFixtures'
import type { ProductionOrder } from './types'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: vi.fn() }))
const auth = (roles = ['Admin']) => ({ user: { userId: responsible.id, pessoaId: 'p', nome: responsible.nome, userName: 'marina', roles }, isAuthenticated: true, isInitializing: false, bootstrap: vi.fn(), login: vi.fn(), logout: vi.fn() })
const writeCalls = () => vi.mocked(httpRequest).mock.calls.filter(([path, options]) => options?.method && !path.endsWith('/previas'))
function renderAction(action: string) {
  return render(<MemoryRouter initialEntries={[`/producao/ordens/${order.id}/acoes/${action}`]}><Routes><Route path="/producao/ordens/:id/acoes/:action" element={<OrderActionPage />} /><Route path="/producao/ordens/:id" element={<h1>Ordem atualizada</h1>} /></Routes></MemoryRouter>)
}
function mockApi(current: ProductionOrder = order) {
  vi.mocked(httpRequest).mockImplementation(async (path, options) => {
    if (path.endsWith('/previas')) {
      const request = options?.body as { inicio?: { entradas: { linhaId: string; alocacoes: { id: string; quantidade: string; unidade: string }[] }[] }; confirmacao?: { entradas: { linhaId: string; alocacoes: { id: string; quantidade: string; unidade: string }[] }[]; saidas: { linhaId: string; id: string; quantidade: string; unidade: string }[] } }
      const body = request.inicio ?? request.confirmacao!
      return { quantidades: [...body.entradas.flatMap(line => line.alocacoes.map(a => ({ papel: 'Entrada', linhaId: line.linhaId, id: a.id, quantidade: { ...inventoryPreview.quantidade, declarada: a.quantidade, unidadeDeclarada: a.unidade, calculada: a.quantidade, normalizada: a.quantidade } }))), ...(request.confirmacao?.saidas ?? []).filter(o => o.quantidade !== '0').map(o => ({ papel: 'Saida', linhaId: o.linhaId, id: o.id, quantidade: { ...inventoryPreview.quantidade, declarada: o.quantidade, unidadeDeclarada: o.unidade, calculada: o.quantidade, normalizada: o.quantidade } }))], avisos: [] }
    }
    if (options?.method) return { ...current, estado: 'Confirmada', acoesPermitidas: [] }
    if (path === `/ordens-producao/${current.id}`) return current
    if (path.startsWith('/ordens-producao?')) return productionPage([current])
    if (path.includes('/historico?')) return productionPage([])
    if (path.startsWith('/estoque/responsaveis?')) return page([responsible])
    if (path === `/estoque/responsaveis/${responsible.id}`) return responsible
    if (path.startsWith('/estoque/lotes?')) return page([lot])
    if (path === `/estoque/lotes/${lot.id}`) return lot
    if (path.startsWith('/estoque/locais?')) return page([local, destination])
    if (path === `/estoque/locais/${local.id}`) return local
    if (path === `/estoque/locais/${destination.id}`) return destination
    if (path.startsWith('/estoque/saldos/')) return { ...stock, fisico: '100', reservado: '60', disponivel: '40', quantidadeElegivel: '40' }
    if (path.startsWith('/propriedades?') || path.includes('/conversoes?') || path.includes('/perfis-nutricionais?')) return page([])
    if (path.startsWith('/receitas?')) return productionPage([recipe])
    if (path.startsWith(`/receitas/${recipe.id}/versoes?`)) return productionPage([version])
    if (path === `/receitas/versoes/${version.id}`) return version
    throw new Error(`Requisição inesperada: ${path}`)
  })
}
describe('Ordens e execução física F02', () => {
  beforeEach(() => { sessionStorage.clear(); vi.mocked(httpRequest).mockReset(); vi.mocked(useAuth).mockReturnValue(auth()); mockApi() })
  it.each(['confirmacao-ocorrencia', 'aborto-sem-consumo', 'reversao'])('nega ação Admin %s antes de consultar referências', async action => {
    vi.mocked(useAuth).mockReturnValue(auth([]))
    renderAction(action)
    expect(await screen.findByRole('alert')).toHaveTextContent('Admin')
    expect(httpRequest).not.toHaveBeenCalled()
  })
  it('planeja só na ação explícita e conserva versão/revisão/Idempotency-Key', async () => {
    mockApi({ ...order, estado: 'Rascunho', planejamento: null, acoesPermitidas: ['planejamento'] })
    renderAction('planejamento')
    fireEvent.change(await screen.findByLabelText('Motivo'), { target: { value: 'Plano conferido' } })
    fireEvent.submit(screen.getByLabelText('Motivo').closest('form')!)
    expect(writeCalls()).toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: 'Planejar ordem' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()[0]).toEqual([`/ordens-producao/${order.id}/planejamento`, expect.objectContaining({ body: { versaoEsperada: 2, motivo: 'Plano conferido' }, headers: { 'Idempotency-Key': expect.any(String) } })])
  })
  it('inicia com alocações explícitas e mostra físico/reservado/disponível sem calcular no browser', async () => {
    renderAction('inicio')
    fireEvent.click(await screen.findByRole('button', { name: 'Adicionar alocação à entrada 1' }))
    await screen.findByRole('option', { name: /MIL-01/ })
    fireEvent.change(screen.getByLabelText('Selecionar Lote da entrada 1, alocação 1'), { target: { value: lot.id } })
    fireEvent.change(screen.getByLabelText('Selecionar Local da entrada 1, alocação 1'), { target: { value: local.id } })
    fireEvent.change(screen.getByLabelText('Quantidade a reservar · entrada 1, alocação 1'), { target: { value: '100,000001' } })
    fireEvent.change(screen.getByLabelText('Motivo da operação e dos desvios'), { target: { value: 'Lote e local conferidos' } })
    await screen.findByText('Utilizável: 40 kg')
    expect(screen.getByText('Físico')).toBeInTheDocument()
    expect(screen.getByText('Reservado')).toBeInTheDocument()
    expect(screen.getByText('Disponível')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    await screen.findByRole('heading', { name: 'Revise antes de reservar' })
    expect(writeCalls()).toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: 'Iniciar e reservar' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()[0][1]?.body).toMatchObject({ versaoEsperada: order.revisao, entradas: [{ linhaId: 'input-a', alocacoes: [{ loteId: lot.id, localId: local.id, quantidade: '100.000001', unidade: 'kg' }] }] })
  })
  it('não inicia linha sem reserva integral e não cria comando por Enter', async () => {
    renderAction('inicio')
    fireEvent.change(await screen.findByLabelText('Motivo da operação e dos desvios'), { target: { value: 'Conferido' } })
    fireEvent.submit(screen.getByLabelText('Motivo da operação e dos desvios').closest('form')!)
    expect(writeCalls()).toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('alocação integral')
    expect(writeCalls()).toHaveLength(0)
  })
  it('confirma reais, perda e novo lote com destino e strings decimais; preview muda ao editar', async () => {
    mockApi(started)
    renderAction('confirmacao')
    fireEvent.change(await screen.findByLabelText('Quantidade realmente retirada · entrada 1, alocação 1'), { target: { value: '99,000001' } })
    await within(await screen.findByLabelText('Selecionar Destino da saída 1')).findByRole('option', { name: /DEP — Depósito/ })
    fireEvent.change(screen.getByLabelText('Selecionar Destino da saída 1'), { target: { value: local.id } })
    fireEvent.change(screen.getByLabelText('Quantidade real da saída 1'), { target: { value: '96' } })
    fireEvent.change(screen.getByLabelText('Quantidade da perda 1'), { target: { value: '3' } })
    fireEvent.change(screen.getByLabelText('Motivo da operação e dos desvios'), { target: { value: 'Medição da execução' } })
    fireEvent.change(screen.getByLabelText('Observação e diferenças não explicadas'), { target: { value: 'Quantidades medidas no processo' } })
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    await screen.findByRole('heading', { name: 'Revise antes de confirmar' })
    fireEvent.change(screen.getByLabelText('Quantidade real da saída 1'), { target: { value: '95' } })
    expect(screen.queryByRole('button', { name: 'Confirmar execução' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Confirmar execução' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()[0][1]?.body).toMatchObject({ entradas: [{ alocacoes: [{ quantidade: '99.000001' }] }], saidas: [{ novoCodigoLote: 'OP-01-1', quantidade: '95', localId: local.id, perfilNutricionalId: null, conversaoItemId: null }], perdas: [{ quantidade: '3', motivo: 'Perda medida' }] })
  })
  it('exige aceite individual do resíduo retornado pelo servidor', async () => {
    mockApi(started)
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      const result = await base(path, options)
      if (path.endsWith('/previas')) { const p = result as { quantidades: { quantidade: object }[] }; p.quantidades[0].quantidade = { ...inventoryPreview.quantidade, declarada: '100', calculada: '0.3333333333', normalizada: '0.333333', residuo: '0.0000003333', exigeAceite: true } }
      return result
    })
    renderAction('confirmacao')
    await within(await screen.findByLabelText('Selecionar Destino da saída 1')).findByRole('option', { name: /DEP — Depósito/ })
    fireEvent.change(screen.getByLabelText('Selecionar Destino da saída 1'), { target: { value: local.id } })
    fireEvent.change(screen.getByLabelText('Motivo da operação e dos desvios'), { target: { value: 'Conferido' } })
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    const confirm = await screen.findByRole('button', { name: 'Confirmar execução' })
    expect(confirm).toBeDisabled()
    fireEvent.click(screen.getByRole('checkbox', { name: /Aceito registrar/ }))
    expect(confirm).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Motivo do aceite do resíduo 1'), { target: { value: 'Escala conferida' } })
    fireEvent.click(confirm)
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()[0][1]?.body).toMatchObject({ entradas: [{ alocacoes: [{ aceiteQuantizacao: { calculado: '0.3333333333', normalizado: '0.333333', residuo: '0.0000003333', motivo: 'Escala conferida' } }] }] })
  })
  it('restaura a declaração e a conversão aprovadas na reserva e exige nova prévia', async () => {
    const conversion = { id: 'conversion-a', itemId: lot.itemId, numero: 1, origem: 'L', destino: 'kg', fator: '2', fonte: 'Medição do lote', metodo: 'Medido', contexto: 'Lote reservado', referenciaAmostra: '', proveniencia: 'Medido', dataFonte: '2026-10-25' }
    mockApi({ ...started, reservas: started.reservas.map(r => ({ ...r, quantidadeDeclarada: '50', unidadeDeclarada: 'L', conversaoItemId: conversion.id })) })
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation((path, options) => path.includes('/conversoes?') ? Promise.resolve(page([conversion])) : path === `/estoque/conversoes/${conversion.id}` ? Promise.resolve(conversion) : base(path, options))
    renderAction('confirmacao')
    expect(await screen.findByLabelText('Quantidade realmente retirada · entrada 1, alocação 1')).toHaveValue('50')
    expect(screen.getByLabelText('Unidade · entrada 1, alocação 1')).toHaveValue('L')
    expect(screen.getByLabelText('Selecionar Conversão documental · entrada 1, alocação 1')).toHaveValue(conversion.id)
    expect(screen.queryByRole('button', { name: 'Confirmar execução' })).not.toBeInTheDocument()
    await within(await screen.findByLabelText('Selecionar Destino da saída 1')).findByRole('option', { name: /DEP — Depósito/ })
    fireEvent.change(screen.getByLabelText('Selecionar Destino da saída 1'), { target: { value: local.id } })
    fireEvent.change(screen.getByLabelText('Motivo da operação e dos desvios'), { target: { value: 'Declaração reservada conferida' } })
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Confirmar execução' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()[0][1]?.body).toMatchObject({ entradas: [{ alocacoes: [{ id: started.reservas[0].id, quantidade: '50', unidade: 'L', conversaoItemId: conversion.id }] }] })
  })
  it('gera outra identidade ao substituir a posição reservada na confirmação', async () => {
    const alternative = { ...local, id: 'local-substituto', codigo: 'OUTRO' }
    mockApi(started)
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation((path, options) => path.startsWith('/estoque/locais?') ? Promise.resolve(page([local, alternative])) : path === `/estoque/locais/${alternative.id}` ? Promise.resolve(alternative) : base(path, options))
    renderAction('confirmacao')
    await within(await screen.findByLabelText('Selecionar Local da entrada 1, alocação 1')).findByRole('option', { name: /OUTRO/ })
    fireEvent.change(screen.getByLabelText('Selecionar Local da entrada 1, alocação 1'), { target: { value: alternative.id } })
    await within(await screen.findByLabelText('Selecionar Destino da saída 1')).findByRole('option', { name: /DEP — Depósito/ })
    fireEvent.change(screen.getByLabelText('Selecionar Destino da saída 1'), { target: { value: local.id } })
    fireEvent.change(screen.getByLabelText('Motivo da operação e dos desvios'), { target: { value: 'Posição substituta conferida' } })
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Confirmar execução' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    const body = writeCalls()[0][1]?.body as { entradas: { alocacoes: { id: string; localId: string }[] }[] }
    expect(body.entradas[0].alocacoes[0].id).not.toBe(started.reservas[0].id)
    expect(body.entradas[0].alocacoes[0].localId).toBe(alternative.id)
  })
  it('E01 exige evidência e mantém todas as saídas bloqueadas em segregação mesmo com aptidão comprovada', async () => {
    mockApi(started)
    renderAction('confirmacao-ocorrencia')
    await within(await screen.findByLabelText('Selecionar Destino da saída 1')).findByRole('option', { name: /SEG — Segregação/ })
    expect(screen.getByRole('checkbox', { name: /Saída bloqueada/ })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: /Saída bloqueada/ })).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Selecionar Destino da saída 1'), { target: { value: destination.id } })
    fireEvent.change(screen.getByLabelText('Aptidão comprovada no fato'), { target: { value: 'Apto' } })
    fireEvent.change(screen.getByLabelText('Motivo da operação e dos desvios'), { target: { value: 'Registro do fato ocorrido' } })
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('evidência')
    expect(writeCalls()).toHaveLength(0)
    fireEvent.change(screen.getByLabelText('Evidência do fato e da conferência física'), { target: { value: 'Material apto na data observada; execução conferida' } })
    fireEvent.click(screen.getByRole('button', { name: 'Revisar quantidades e disponibilidade' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar fato passado (Admin)' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()[0][0]).toMatch(/\/confirmacao-ocorrencia$/)
    expect(writeCalls()[0][1]?.body).toMatchObject({ aptidaoNoFato: 'Apto', evidencia: 'Material apto na data observada; execução conferida', saidas: [{ localId: destination.id, bloqueado: true }] })
    expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.startsWith('/estoque/locais?') && path.includes('finalidade=Segregacao'))).toBe(true)
  })
  it('retém formulário congelado após timeout; reentrada consulta o comando sem novo fato', async () => {
    mockApi({ ...order, estado: 'Rascunho', acoesPermitidas: ['planejamento'] })
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path.endsWith('/planejamento') && options?.method ? Promise.reject(new NetworkError()) : path.includes('/comandos/') ? order : base(path, options))
    const first = renderAction('planejamento')
    fireEvent.change(await screen.findByLabelText('Motivo'), { target: { value: 'Plano conferido' } })
    fireEvent.click(screen.getByRole('button', { name: 'Planejar ordem' }))
    await screen.findByRole('heading', { name: 'Operação aguardando confirmação' })
    expect(screen.getByLabelText('Motivo')).toBeDisabled()
    first.unmount()
    renderAction('planejamento')
    fireEvent.click(await screen.findByRole('button', { name: 'Consultar resultado' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()).toHaveLength(1)
  })
  it('aborto exige declaração explícita e evidência; não cria saldo ou consumo no cliente', async () => {
    mockApi(started)
    renderAction('aborto-sem-consumo')
    fireEvent.change(await screen.findByLabelText('Motivo'), { target: { value: 'Sem consumo físico' } })
    fireEvent.click(screen.getByRole('button', { name: 'Abortar sem consumo (Admin)' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('declaração')
    expect(writeCalls()).toHaveLength(0)
    fireEvent.change(screen.getByLabelText('Evidência e conferência física'), { target: { value: 'Material integral conferido' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Declaro que não houve/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Abortar sem consumo (Admin)' }))
    await screen.findByRole('heading', { name: 'Ordem atualizada' })
    expect(writeCalls()[0][1]?.body).toEqual({ versaoEsperada: started.revisao, nenhumConsumoFisico: true, motivo: 'Sem consumo físico', evidencia: 'Material integral conferido' })
  })
  it('mantém impedimento de reversão visível em conflito e conserva o pedido', async () => {
    mockApi({ ...started, estado: 'Confirmada', acoesPermitidas: ['reversao'] })
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => options?.method ? Promise.reject(new HttpError(409, '', 'Lote MIL-01 teve reserva posterior já liberada.')) : base(path, options))
    renderAction('reversao')
    fireEvent.change(await screen.findByLabelText('Motivo'), { target: { value: 'Lançamento incorreto' } })
    fireEvent.change(screen.getByLabelText('Evidência e conferência física'), { target: { value: 'Contagem integral' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Confirmo que o lançamento/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Reverter lançamento (Admin)' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('reserva posterior já liberada')
    expect(screen.getByLabelText('Motivo')).toBeDisabled()
  })
  it('lista e visualização preservam estado e não oferecem ação privilegiada a comum', async () => {
    vi.mocked(useAuth).mockReturnValue(auth([]))
    mockApi(started)
    render(<MemoryRouter initialEntries={['/producao/ordens']}><Routes><Route path="/producao/ordens" element={<OrdersPage />} /><Route path="/producao/ordens/:id" element={<OrderDetailsPage />} /></Routes></MemoryRouter>)
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: 'Visualizar ordem de produção' })
    expect(screen.queryByRole('link', { name: 'Abortar sem consumo (Admin)' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Registrar fato passado (Admin)' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Confirmar execução' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ordens' }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Ordens de produção' })).toBeInTheDocument())
  })
  it('aplica a versão escolhida e restaura o filtro ao voltar da visualização', async () => {
    render(<MemoryRouter initialEntries={['/producao/ordens']}><Routes><Route path="/producao/ordens" element={<OrdersPage />} /><Route path="/producao/ordens/:id" element={<OrderDetailsPage />} /></Routes></MemoryRouter>)
    await within(await screen.findByLabelText('Selecionar Receita para filtrar')).findByRole('option', { name: /R01/ })
    fireEvent.change(screen.getByLabelText('Selecionar Receita para filtrar'), { target: { value: recipe.id } })
    await within(await screen.findByLabelText('Selecionar Versão da receita para filtrar')).findByRole('option', { name: /Versão 3/ })
    fireEvent.change(screen.getByLabelText('Selecionar Versão da receita para filtrar'), { target: { value: version.id } })
    await waitFor(() => expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.startsWith('/ordens-producao?') && path.includes(`receitaVersaoId=${version.id}`))).toBe(true))
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: 'Visualizar ordem de produção' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Ordens' }))
    expect(await screen.findByLabelText('Selecionar Receita para filtrar')).toHaveValue(recipe.id)
    expect(await screen.findByLabelText('Selecionar Versão da receita para filtrar')).toHaveValue(version.id)
    expect(vi.mocked(httpRequest).mock.calls.filter(([path]) => path.startsWith('/ordens-producao?')).every(([path]) => !/[?&]receitaId=/.test(path))).toBe(true)
  })
  it('separa ajustes presentes da produção e permite consultar o snapshot histórico anterior', async () => {
    const originalPlan = { ...order.planejamento!, responsavel: { id: responsible.id, nome: 'Marina histórica', ativo: true } }
    const current: ProductionOrder = { ...started, estado: 'Confirmada', acoesPermitidas: ['ocorrencias'], execucao: { id: 'execution-a', ordemId: order.id, resultado: 'ComDesvio', autorId: responsible.id, responsavelId: responsible.id, createdAtUtc: observed, dataOperacional: '2026-10-08', dataObservada: null, snapshotJson: JSON.stringify({ realizado: { entradas: [], saidas: [], perdas: [] } }), movimentos: [], lotesSaida: [{ id: 'output-a', linhaId: 'output-a', loteId: lot.id, localId: local.id, quantidade: '98', unidade: 'kg' }], ajustesPresentes: [{ ...inventoryEvent, tipo: 'Ajuste', motivo: 'Contagem atual conferida' }] } }
    mockApi(current)
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation((path, options) => path.includes('/historico?') ? Promise.resolve(productionPage([{ id: 'history-a', ordemId: order.id, operacao: 'planejamento', autorId: responsible.id, createdAtUtc: observed, motivo: 'Primeira revisão preservada', antesJson: 'null', depoisJson: JSON.stringify({ estado: 'Planejada', revisao: 2, snapshotPlanejadoJson: JSON.stringify(originalPlan) }) }])) : base(path, options))
    render(<MemoryRouter initialEntries={[`/producao/ordens/${order.id}`]}><Routes><Route path="/producao/ordens/:id" element={<OrderDetailsPage />} /></Routes></MemoryRouter>)
    expect(await screen.findByRole('heading', { name: 'Ajustes presentes vinculados' })).toBeInTheDocument()
    expect(screen.getByText('Correção positiva: 1 kg')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Ajuste · sequência/ })).toHaveAttribute('href', `/estoque/movimentos/${inventoryEvent.id}`)
    fireEvent.click(await screen.findByText('Consultar snapshot histórico desta revisão'))
    expect(screen.getByText('Responsável no planejamento: Marina histórica')).toBeInTheDocument()
  })
  it('consulta genealogia nos sentidos origens e destinos aceitos pela API', async () => {
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation((path, options) => path.startsWith(`/estoque/lotes/${lot.id}/genealogia?`) ? Promise.resolve(productionPage([])) : base(path, options))
    render(<MemoryRouter initialEntries={[`/producao/genealogia/${lot.id}`]}><Routes><Route path="/producao/genealogia/:loteId" element={<ProductionGenealogyPage />} /></Routes></MemoryRouter>)
    await screen.findByText('Nenhuma transformação encontrada neste corte.')
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'origens' } })
    await waitFor(() => expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.includes('/genealogia?direcao=origens&'))).toBe(true))
    fireEvent.change(screen.getByLabelText('Direção'), { target: { value: 'destinos' } })
    await waitFor(() => expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.includes('/genealogia?direcao=destinos&'))).toBe(true))
  })
})
