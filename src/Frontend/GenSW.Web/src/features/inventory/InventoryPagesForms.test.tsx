import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { useAuth } from '../auth/hooks/useAuth'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, NetworkError } from '../../shared/http/httpErrors'
import { InventoryStatePage, LocalFormPage, LoteFormPage } from './InventoryForms'
import { EventDetailsPage, InventoryListPage, LocalDetailsPage, LoteDetailsPage } from './InventoryPages'
import { ReferenceSelect } from './InventoryControls'
import { parseLocal } from './inventoryService'
import { event, item, local, lot, page, responsible, stock } from './inventoryFixtures'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: vi.fn() }))
const auth = (roles = ['Admin']) => ({ user: { userId: responsible.id, pessoaId: 'p', nome: responsible.nome, userName: 'marina', roles }, isAuthenticated: true, isInitializing: false, bootstrap: vi.fn(), login: vi.fn(), logout: vi.fn() })
const writes = () => vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method)
function app(path: string, state?: unknown) {
  return render(<MemoryRouter initialEntries={[{ pathname: path, state }]}><Routes>
    <Route path="/estoque/locais" element={<InventoryListPage resource="locais" />} />
    <Route path="/estoque/saldos" element={<InventoryListPage resource="saldos" />} />
    <Route path="/estoque/movimentos" element={<InventoryListPage resource="movimentos" />} />
    <Route path="/estoque/reconciliacao" element={<InventoryListPage resource="reconciliacao" />} />
    <Route path="/estoque/locais/novo" element={<LocalFormPage />} />
    <Route path="/estoque/locais/:id/editar" element={<LocalFormPage />} />
    <Route path="/estoque/locais/:id" element={<LocalDetailsPage />} />
    <Route path="/estoque/locais/:id/comandos/:command" element={<InventoryStatePage kind="locais" />} />
    <Route path="/estoque/lotes/novo" element={<LoteFormPage />} />
    <Route path="/estoque/lotes/:id/editar" element={<LoteFormPage />} />
    <Route path="/estoque/lotes/:id" element={<LoteDetailsPage />} />
    <Route path="/estoque/lotes/:id/comandos/:command" element={<InventoryStatePage kind="lotes" />} />
    <Route path="/estoque/movimentos/:id" element={<EventDetailsPage />} />
  </Routes></MemoryRouter>)
}

describe('Cadastros e consultas de estoque', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue(auth())
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (options?.method) return path.includes('/lotes') ? lot : local
      if (path === '/estoque/locais/local-1') return local
      if (path === '/estoque/lotes/lot-1') return lot
      if (path === '/estoque/movimentos/event-1') return event
      if (path === '/itens/item-1') return item
      if (path === '/estoque/responsaveis/user-1') return responsible
      if (path.includes('/historico?')) return page([])
      if (path.startsWith('/estoque/locais?')) return page([local], Number(new URLSearchParams(path.split('?')[1]).get('page') ?? 1), 3)
      if (path.startsWith('/estoque/lotes?')) return page([lot])
      if (path.startsWith('/estoque/saldos?')) return page([stock])
      if (path.startsWith('/estoque/movimentos?')) return page([event])
      if (path.startsWith('/estoque/responsaveis?')) return page([responsible])
      if (path.startsWith('/itens?')) return page([item])
      if (path.startsWith('/estoque/reconciliacao?') || path.startsWith('/propriedades?') || path.includes('/perfis-nutricionais?') || path.includes('/conversoes?')) return page([])
      throw new Error(`Requisição inesperada: ${path}`)
    })
  })
  it('cria local sem saldo e envia metadados, versão zero e chave idempotente', async () => {
    app('/estoque/locais/novo')
    fireEvent.change(screen.getByLabelText('Código'), { target: { value: 'DEP' } })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Depósito' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar local' }))
    await screen.findByRole('heading', { name: 'Visualizar local de estoque' })
    expect(writes()[0]).toEqual(['/estoque/locais', expect.objectContaining({ method: 'POST', authenticated: true, headers: { 'Idempotency-Key': expect.any(String) }, body: expect.objectContaining({ codigo: 'DEP', nome: 'Depósito', versaoEsperada: 0, ativo: true, propriedadeId: null }) })])
  })
  it('conserva preenchimento e retenta local com mesma chave após perda da resposta; edição reverte cria nova chave', async () => {
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => options?.method ? Promise.reject(new NetworkError()) : base(path, options))
    app('/estoque/locais/local-1/editar')
    await screen.findByDisplayValue('Depósito')
    fireEvent.change(screen.getByLabelText('Motivo do cadastro ou alteração'), { target: { value: 'Revisão metadata' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar local' }))
    await screen.findByText('Não foi possível conectar à API.')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar local' }))
    await waitFor(() => expect(writes()).toHaveLength(2))
    expect(writes()[0][1]).toEqual(writes()[1][1])
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Temp' } })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Depósito' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar local' }))
    await waitFor(() => expect(writes()).toHaveLength(3))
    expect(writes()[2][1]?.headers).not.toEqual(writes()[1][1]?.headers)
    expect(writes()[2][1]?.body).toMatchObject({ versaoEsperada: 3, finalidade: 'Ordinario', ativo: true })
  })
  it('cria lote com revisão do Item e responsáveis sem sintetizar saldo ou validade', async () => {
    app('/estoque/lotes/novo')
    await screen.findByRole('option', { name: /MIL — Milho/ })
    fireEvent.change(screen.getByLabelText('Selecionar Item'), { target: { value: item.id } })
    fireEvent.change(screen.getByLabelText('Código interno único'), { target: { value: lot.codigo } })
    fireEvent.change(screen.getByLabelText('Origem'), { target: { value: lot.origem } })
    fireEvent.change(screen.getByLabelText('Fonte'), { target: { value: lot.fonte } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Salvar lote' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Salvar lote' }))
    await screen.findByRole('heading', { name: 'Visualizar lote de material' })
    expect(writes()[0][1]?.body).toMatchObject({ itemId: item.id, itemVersaoEsperada: 7, versaoEsperada: 0, responsavelId: responsible.id, validade: null, perfilNutricionalId: null, conversaoItemId: null, pendente: false })
    expect(writes()).toHaveLength(1)
  })
  it('edita somente metadados do lote, mantendo identidade e referências e exigindo revisão', async () => {
    app('/estoque/lotes/lot-1/editar')
    await screen.findByDisplayValue(lot.codigo)
    expect(screen.getByLabelText('Código interno único')).toBeDisabled()
    expect(screen.queryByLabelText('Validade declarada')).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Código externo'), { target: { value: 'DOC-EXT' } })
    fireEvent.change(screen.getByLabelText('Motivo do cadastro ou alteração'), { target: { value: 'Documento atualizado' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Salvar lote' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Salvar lote' }))
    await screen.findByRole('heading', { name: 'Visualizar lote de material' })
    expect(writes()[0]).toEqual(['/estoque/lotes/lot-1', expect.objectContaining({ method: 'PUT', body: expect.objectContaining({ codigoExterno: 'DOC-EXT', itemId: item.id, codigo: lot.codigo, versaoEsperada: 5 }) })])
  })
  it.each([['locais', 'ativo'], ['locais', 'finalidade'], ['lotes', 'ativo'], ['lotes', 'bloqueio'], ['lotes', 'liberacao'], ['lotes', 'encerramento'], ['lotes', 'validade'], ['lotes', 'referencias']])('envia comando Admin %s/%s com motivo, evidência, revisão e idempotência', async (kind, command) => {
    const id = kind === 'locais' ? local.id : lot.id
    app(`/estoque/${kind}/${id}/comandos/${command}`)
    await screen.findByText(/revisão [35]/)
    fireEvent.change(screen.getByLabelText('Motivo'), { target: { value: 'Conferência física' } })
    fireEvent.change(screen.getByLabelText('Evidência da conferência'), { target: { value: 'Documentação verificada' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar comando' }))
    await screen.findByRole('heading', { name: kind === 'locais' ? 'Visualizar local de estoque' : 'Visualizar lote de material' })
    expect(writes()[0]).toEqual([`/estoque/${kind}/${id}/${command}`, expect.objectContaining({ method: command === 'ativo' ? 'PATCH' : 'POST', headers: { 'Idempotency-Key': expect.any(String) }, body: expect.objectContaining({ versaoEsperada: kind === 'locais' ? 3 : 5, motivo: 'Conferência física', evidencia: 'Documentação verificada' }) })])
  })
  it('nega rota administrativa antes de carregar dados ao usuário comum', () => {
    vi.mocked(useAuth).mockReturnValue(auth([]))
    app('/estoque/lotes/lot-1/comandos/bloqueio')
    expect(screen.getByRole('alert')).toHaveTextContent('papel Admin')
    expect(httpRequest).not.toHaveBeenCalled()
  })
  it.each([['/estoque/locais/local-1', 'Visualizar local de estoque'], ['/estoque/lotes/lot-1', 'Visualizar lote de material'], ['/estoque/movimentos/event-1', 'Visualizar movimento de estoque']])('abre detalhe readonly por URL %s sem efeitos de escrita', async (path, title) => {
    vi.mocked(useAuth).mockReturnValue(auth([]))
    const view = app(path)
    const heading = await screen.findByRole('heading', { name: title })
    await waitFor(() => expect(view.container.querySelector('dl')).toBeInTheDocument())
    expect(heading).toHaveFocus()
    expect(view.container.querySelector('form, input, textarea, select')).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Inativar|Bloquear|Corrigir validade|Ajuste de inventário/ })).not.toBeInTheDocument()
    expect(writes()).toEqual([])
  })
  it('mostra aviso de validade separadamente da elegibilidade e da quantidade utilizável', async () => {
    app('/estoque/saldos')
    expect(await screen.findByText('Aviso: Validade não informada')).toBeInTheDocument()
    expect(screen.getByText('Elegível')).toBeInTheDocument()
    expect(screen.getAllByText('10,000000 kg')).toHaveLength(2)
  })
  it('mostra códigos e nomes congelados em cada linha e conserva o JSON original', async () => {
    const frozen = '{"lote":{"id":"lot-1","codigo":"LOTE-HIST"},"item":{"nome":"Milho histórico"},"locais":[{"id":"local-1","codigo":"DEP-HIST","nome":"Depósito histórico"}],"quantidade":99999999999999.123456}'
    const base = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path === '/estoque/movimentos/event-1' ? { ...event, snapshotJson: frozen, movimentos: [{ ...event.movimentos[0], snapshotJson: frozen }] } : base(path, options))
    app('/estoque/movimentos/event-1')
    expect(await screen.findByRole('link', { name: 'LOTE-HIST — Milho histórico' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'DEP-HIST — Depósito histórico' })).toBeInTheDocument()
    expect(screen.getAllByText(frozen)).toHaveLength(2)
    expect(writes()).toEqual([])
  })
  it('consulta reconciliação somente por leitura', async () => {
    app('/estoque/reconciliacao')
    await screen.findByText('Sem divergências no corte consultado.')
    expect(screen.queryByRole('navigation', { name: 'Operações de estoque' })).not.toBeInTheDocument()
    expect(writes()).toEqual([])
  })
  it('mantém busca e página da lista ao visualizar e voltar, sem teto fixo de registros', async () => {
    app('/estoque/locais', { list: { path: '/estoque/locais', filters: { page: 2, search: 'dep', pageSize: 25 } } })
    const detail = await screen.findByRole('link', { name: 'Visualizar' })
    expect(screen.getByLabelText('Buscar código ou nome')).toHaveValue('dep')
    expect(httpRequest).toHaveBeenCalledWith(expect.stringContaining('page=2'), expect.objectContaining({ authenticated: true }))
    fireEvent.click(detail)
    await screen.findByRole('heading', { name: 'Visualizar local de estoque' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Locais' }))
    await screen.findByText('Página 2 de 3')
    expect(screen.getByLabelText('Buscar código ou nome')).toHaveValue('dep')
    fireEvent.click(within(screen.getAllByRole('navigation', { name: 'Paginação' }).at(-1)!).getByRole('button', { name: 'Próxima' }))
    await screen.findByText('Página 3 de 3')
    expect(httpRequest).toHaveBeenCalledWith(expect.stringContaining('page=3'), expect.objectContaining({ authenticated: true }))
  })
  it.each(['detalhe', 'cancelar edição', 'salvar edição'])('retorna de Saldos ao filtro do Local após %s do lote', async flow => {
    app('/estoque/saldos')
    await screen.findByRole('option', { name: 'DEP — Depósito' })
    fireEvent.change(screen.getByLabelText('Selecionar Local'), { target: { value: local.id } })
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith(expect.stringMatching(/^\/estoque\/saldos\?.*localId=local-1/), expect.objectContaining({ authenticated: true })))
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar lote' }))
    await screen.findByRole('heading', { name: 'Visualizar lote de material' })
    expect(screen.getByRole('link', { name: 'Voltar para Saldos' })).toHaveAttribute('href', '/estoque/saldos')
    if (flow !== 'detalhe') {
      fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
      await screen.findByDisplayValue(lot.codigo)
      expect(screen.getByRole('link', { name: 'Voltar para Saldos' })).toHaveAttribute('href', '/estoque/saldos')
      if (flow === 'salvar edição') {
        fireEvent.change(screen.getByLabelText('Código externo'), { target: { value: 'DOC-FILTRO' } })
        fireEvent.change(screen.getByLabelText('Motivo do cadastro ou alteração'), { target: { value: 'Conferência documental' } })
        await waitFor(() => expect(screen.getByRole('button', { name: 'Salvar lote' })).toBeEnabled())
        fireEvent.click(screen.getByRole('button', { name: 'Salvar lote' }))
        await screen.findByRole('heading', { name: 'Visualizar lote de material' })
      }
    }
    vi.mocked(httpRequest).mockClear()
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Saldos' }))
    await screen.findByRole('heading', { name: 'Saldos de estoque' })
    await waitFor(() => expect(screen.getByLabelText('Selecionar Local')).toHaveValue(local.id))
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith(expect.stringMatching(/^\/estoque\/saldos\?.*localId=local-1/), expect.objectContaining({ authenticated: true })))
  })
  it.each(['/financeiro', 'https://example.invalid'])('ignora origem %s fora das listas conhecidas de estoque', async path => {
    app('/estoque/lotes/lot-1', { list: { path, filters: { localId: local.id } } })
    await screen.findByRole('heading', { name: 'Visualizar lote de material' })
    expect(screen.getByRole('link', { name: 'Voltar para Lotes' })).toHaveAttribute('href', '/estoque/lotes')
  })
  it.each([404, 403, 500])('usa erro de detalhe compartilhado no status %i', async status => {
    vi.mocked(httpRequest).mockRejectedValue(new HttpError(status))
    app('/estoque/lotes/lot-1')
    expect(await screen.findByRole('alert')).toHaveTextContent(status === 404 ? 'Registro não encontrado.' : status === 403 ? 'Você não tem permissão' : 'Não foi possível carregar')
    expect(writes()).toEqual([])
  })
  it('resolve seleção histórica individualmente e pagina/busca referências no servidor', async () => {
    const historical = { ...local, id: 'historical', ativo: false, nome: 'Local antigo' }
    vi.mocked(httpRequest).mockImplementation(async path => path === '/estoque/locais/historical' ? historical : page([local], Number(new URLSearchParams(path.split('?')[1]).get('page') ?? 1), 3))
    render(<ReferenceSelect label="Local" path="/estoque/locais" value="historical" params={{ ativo: true }} parse={parseLocal} caption={x => `${x.nome}${x.ativo ? '' : ' · Inativo'}`} onChange={vi.fn()} />)
    expect(await screen.findByRole('option', { name: 'Local antigo · Inativo' })).toBeInTheDocument()
    await screen.findByRole('option', { name: 'Depósito' })
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith(expect.stringContaining('page=2&pageSize=10'), expect.any(Object)))
    fireEvent.change(screen.getByLabelText('Buscar Local'), { target: { value: 'servidor' } })
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith('/estoque/locais?ativo=true&search=servidor&page=1&pageSize=10', expect.any(Object)))
    expect(screen.getByLabelText('Selecionar Local')).toHaveValue('historical')
  })
  it('preserva sequência bigint e filtra movimentos pelo tipo exato do contrato API', async () => {
    app('/estoque/movimentos')
    await screen.findByText(stock.sequenciaAte)
    expect(screen.getAllByText('Entrada manual').length).toBeGreaterThan(0)
    fireEvent.change(screen.getByLabelText('Tipo de movimento'), { target: { value: 'ConsumoInterno' } })
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith(expect.stringContaining('tipo=ConsumoInterno'), expect.any(Object)))
  })
})
