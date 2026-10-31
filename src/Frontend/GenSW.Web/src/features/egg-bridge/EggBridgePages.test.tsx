import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { useAuth } from '../auth/hooks/useAuth'
import { event, item, local, lot, page, responsible } from '../inventory/inventoryFixtures'
import { EggCandidatesPage } from './EggCandidatesPage'
import { EggAdministrationPage } from './EggAdministrationPage'
import { EggGroupDetailsPage, EggReconciliationPage } from './EggGroupPages'
import { eggAnchor, eggCandidate, eggDetail, eggGroup, eggPage, eggPreview, eggProof, eggScope } from './eggBridgeFixtures'
import type { EggPreview } from './types'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
vi.mock('../auth/hooks/useAuth', () => ({ useAuth: vi.fn() }))
const root = '/producao/ponte-ovos'
const first = eggCandidate()
const second = eggCandidate('10000000-0000-0000-0000-000000000002')
const third = eggCandidate('10000000-0000-0000-0000-000000000003')
const animal = { id: first.origem.animalId, codigoInterno: 'AVE-01', nome: 'Ave de teste', especieId: 'species', racaId: null, variedadeId: null, sexo: 2, dataNascimento: null, escopo: 1, ativo: true, createdAtUtc: first.origem.createdAtUtc, updatedAtUtc: first.origem.updatedAtUtc, especie: { id: 'species', nomeComum: 'Galinha', ativo: true }, raca: null, variedade: null }
const auth = (roles = ['Admin']) => ({ user: { userId: responsible.id, pessoaId: 'p', nome: responsible.nome, userName: 'marina', roles }, isAuthenticated: true, isInitializing: false, bootstrap: vi.fn(), login: vi.fn(), logout: vi.fn() })
function api(preview: EggPreview = eggPreview) {
  vi.mocked(httpRequest).mockImplementation(async (path, options) => {
    if (path === `${root}/previas`) return preview
    if (path.endsWith('/previas-tratamento')) return { token: 'treatment-token', grupoId: eggGroup.id, tipo: 'Compensacao', deltaEstoque: '-1', impedimentos: [], podeConfirmar: true, expiraEmUtc: eggPreview.expiraEmUtc }
    if (options?.method) return eggDetail
    if (path.startsWith(`${root}/candidatos?`)) return new URLSearchParams(path.split('?')[1]).get('page') === '2' ? eggPage([third], 2, 2) : eggPage([first, second], 1, 2)
    if (path.startsWith(`${root}/recortes?`)) return eggPage([eggScope])
    if (path === `${root}/recortes/${eggScope.id}`) return eggScope
    if (path.startsWith(`${root}/provas?`)) return eggPage([eggProof])
    if (path === `${root}/provas/${eggProof.id}`) return eggProof
    if (path.startsWith(`${root}/itens/`)) return eggAnchor
    if (path === `${root}/grupos/${eggGroup.id}`) return eggDetail
    if (path.startsWith('/itens?')) return page([{ ...item, unidade: 'un' }])
    if (path === `/itens/${item.id}`) return { ...item, unidade: 'un' }
    if (path.startsWith('/estoque/lotes?')) return page([{ ...lot, unidade: 'un' }])
    if (path === `/estoque/lotes/${lot.id}`) return { ...lot, unidade: 'un' }
    if (path.startsWith('/estoque/locais?')) return page([local])
    if (path === `/estoque/locais/${local.id}`) return local
    if (path.startsWith('/estoque/responsaveis?')) return page([responsible])
    if (path === `/estoque/responsaveis/${responsible.id}`) return responsible
    if (path.startsWith('/estoque/movimentos?')) return page([event])
    if (path.startsWith('/animais?')) return page([animal])
    if (path === `/animais/${first.origem.animalId}`) return { ...animal, id: first.origem.animalId }
    throw new Error(`Requisição inesperada: ${path}`)
  })
}
const writes = () => vi.mocked(httpRequest).mock.calls.filter(([path, options]) => options?.method && !path.endsWith('/previas') && !path.endsWith('/previas-tratamento'))
const candidateView = () => render(<MemoryRouter initialEntries={[root]}><EggCandidatesPage /></MemoryRouter>)
async function selectReference(label: string, value: string, option: RegExp) {
  const select = await screen.findByLabelText(`Selecionar ${label}`)
  await within(select).findByRole('option', { name: option })
  fireEvent.change(select, { target: { value } })
}
async function fillEntry() {
  fireEvent.click(await screen.findByLabelText(`Selecionar lançamento ${first.origem.id}`))
  await selectReference('Item de ovos', item.id, /un/)
  await selectReference('Conferência aprovada', eggScope.id, /Origem física/)
  await selectReference('Lote dedicado vazio', lot.id, /MIL/)
  await selectReference('Local da entrada', local.id, /DEP/)
  fireEvent.change(screen.getByLabelText('Motivo da ponte'), { target: { value: 'Seleção física conferida' } })
  fireEvent.change(screen.getByLabelText('Evidência da seleção e da origem física'), { target: { value: 'Contagem individual e não recebimento comprovados' } })
}
describe('Ponte explícita de ovos F03', () => {
  beforeEach(() => { sessionStorage.clear(); vi.mocked(httpRequest).mockReset(); vi.mocked(useAuth).mockReturnValue(auth()); api() })
  it.each([<EggAdministrationPage />, <EggReconciliationPage />])('nega tela Admin antes de consultar recursos', async element => {
    vi.mocked(useAuth).mockReturnValue(auth([]))
    render(<MemoryRouter>{element}</MemoryRouter>)
    expect(await screen.findByRole('alert')).toHaveTextContent('Admin atual')
    expect(httpRequest).not.toHaveBeenCalled()
  })
  it('mantém seleção parcial entre páginas e nunca seleciona pelo filtro', async () => {
    candidateView()
    const checkbox = await screen.findByLabelText(`Selecionar lançamento ${first.origem.id}`)
    expect(checkbox).not.toBeChecked()
    fireEvent.click(checkbox)
    fireEvent.click(within(screen.getByRole('table', { name: 'Lançamentos disponíveis para seleção' }).closest('section')!).getAllByRole('button', { name: 'Próxima' }).find(button => !button.hasAttribute('disabled'))!)
    fireEvent.click(await screen.findByLabelText(`Selecionar lançamento ${third.origem.id}`))
    expect(screen.getByRole('heading', { name: 'Selecionados entre páginas: 2' })).toBeInTheDocument()
    expect(screen.getByLabelText(`Remover lançamento ${first.origem.id}`)).toBeInTheDocument()
    expect(screen.queryByLabelText(`Selecionar lançamento ${second.origem.id}`)).not.toBeInTheDocument()
    await waitFor(() => expect(screen.getAllByText(/AVE-01/)).toHaveLength(3))
    expect(writes()).toHaveLength(0)
  })
  it('Enter não confirma; prévia exige aceite e mantém o token verificável', async () => {
    candidateView(); await fillEntry()
    fireEvent.submit(screen.getByLabelText('Motivo da ponte').closest('form')!)
    expect(writes()).toHaveLength(0)
    expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.endsWith('/previas'))).toBe(false)
    fireEvent.click(screen.getByRole('button', { name: 'Preparar prévia da seleção' }))
    const preview = await screen.findByRole('region', { name: 'Prévia da ponte' })
    expect(within(preview).getByText(/Delta de estoque: 1 un/)).toBeInTheDocument()
    const confirm = within(preview).getByRole('button', { name: 'Confirmar entrada individual' })
    expect(confirm).toBeDisabled()
    fireEvent.click(within(preview).getByRole('checkbox'))
    fireEvent.click(confirm)
    await screen.findByRole('heading', { name: 'Resultado original do comando' })
    expect(writes()).toEqual([[`${root}/confirmacoes`, expect.objectContaining({ body: { token: eggPreview.token }, headers: { 'Idempotency-Key': expect.any(String) } })]])
    expect(screen.getByRole('link', { name: 'Consultar estado atual do grupo' })).toHaveAttribute('href', `${root}/grupos/${eggGroup.id}`)
  })
  it('alteração de motivo ou seleção invalida a prévia sem omitir linhas', async () => {
    candidateView(); await fillEntry()
    fireEvent.click(screen.getByRole('button', { name: 'Preparar prévia da seleção' }))
    await screen.findByRole('region', { name: 'Prévia da ponte' })
    fireEvent.change(screen.getByLabelText('Motivo da ponte'), { target: { value: 'Mudança consciente' } })
    expect(screen.queryByRole('region', { name: 'Prévia da ponte' })).not.toBeInTheDocument()
    expect(screen.getByLabelText(`Selecionar lançamento ${first.origem.id}`)).toBeChecked()
    expect(writes()).toHaveLength(0)
  })
  it('impedimento mantém toda a seleção e bloqueia confirmação', async () => {
    api({ ...eggPreview, podeConfirmar: false, origens: [{ ...eggPreview.origens[0], impedimento: 'Origem alterada; nova conferência necessária' }] })
    candidateView(); await fillEntry()
    fireEvent.click(screen.getByRole('button', { name: 'Preparar prévia da seleção' }))
    expect(await screen.findByText('Origem alterada; nova conferência necessária')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirmar entrada individual' })).toBeDisabled()
    expect(screen.getByLabelText(`Selecionar lançamento ${first.origem.id}`)).toBeChecked()
    expect(writes()).toHaveLength(0)
  })
  it('associação mostra delta zero e mantém ordinal de 14 dígitos como string', async () => {
    api({ ...eggPreview, operacao: 'Associacao', deltaEstoque: '0', saldoAnterior: null, saldoPosterior: null, capacidadeRestante: '99999999999998' })
    candidateView()
    fireEvent.click(await screen.findByLabelText(`Selecionar lançamento ${first.origem.id}`))
    fireEvent.change(screen.getByLabelText('Operação da ponte'), { target: { value: 'Associacao' } })
    await selectReference('Item de ovos', item.id, /un/)
    await selectReference('Conferência aprovada', eggScope.id, /Origem física/)
    await selectReference('Prova de recebimento manual', eggProof.id, /99999999999999 un/)
    fireEvent.change(screen.getByLabelText(`Ordinal na prova · ${first.origem.id}`), { target: { value: '99999999999999' } })
    fireEvent.change(screen.getByLabelText('Motivo da ponte'), { target: { value: 'Equivalência comprovada' } })
    fireEvent.change(screen.getByLabelText('Evidência da seleção e da origem física'), { target: { value: 'Contagem original e mapeamento documental' } })
    fireEvent.click(screen.getByRole('button', { name: 'Preparar prévia da seleção' }))
    const preview = await screen.findByRole('region', { name: 'Prévia da ponte' })
    expect(within(preview).getByText(/Nenhum incremento de estoque/)).toHaveTextContent('99999999999998')
    const request = vi.mocked(httpRequest).mock.calls.find(([path]) => path === `${root}/previas`)
    expect(request?.[1]?.body).toMatchObject({ operacao: 'Associacao', selecao: [{ producaoOvoId: first.origem.id, ordinal: '99999999999999' }], loteId: null, localId: null, provaId: eggProof.id })
  })
  it('nova conferência não presume não recebido nem unidade individual', async () => {
    render(<MemoryRouter initialEntries={[{ pathname: `${root}/administracao`, state: { selected: [first] } }]}><EggAdministrationPage /></MemoryRouter>)
    const decision = await screen.findByLabelText(`Decisão · ${first.origem.id}`)
    expect(decision).toHaveValue('')
    expect(screen.getByLabelText('Comprovei que este lançamento representa um ovo individual.')).not.toBeChecked()
    await selectReference('Item a conferir', item.id, /un/)
    await screen.findByText(/Item inscrito/)
    fireEvent.change(screen.getByLabelText('Fronteira física, período e controle documental'), { target: { value: 'Galpão e período verificados' } })
    fireEvent.change(screen.getByLabelText('Fontes e registros efetivamente consultados'), { target: { value: 'Físico e documentos externos' } })
    fireEvent.change(screen.getByLabelText('Motivo da aprovação do recorte'), { target: { value: 'Deliberação humana' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aprovar conferência e decisões explícitas' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('decisão explícita')
    expect(writes()).toHaveLength(0)
  })
  it('tratamento exige prévia e confirmação consciente e distingue resultado original do estado atual', async () => {
    render(<MemoryRouter initialEntries={[`${root}/grupos/${eggGroup.id}`]}><Routes><Route path={`${root}/grupos/:id`} element={<EggGroupDetailsPage />} /></Routes></MemoryRouter>)
    fireEvent.change(await screen.findByLabelText('Tratamento'), { target: { value: 'Compensacao' } })
    fireEvent.change(screen.getByLabelText('Motivo do tratamento'), { target: { value: 'Erro de registro integral' } })
    fireEvent.change(screen.getByLabelText('Evidência do tratamento'), { target: { value: 'Grupo intacto e contagem conferidos' } })
    fireEvent.click(screen.getByLabelText(/Conferi a coerência física/))
    fireEvent.submit(screen.getByLabelText('Motivo do tratamento').closest('form')!)
    expect(writes()).toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: 'Preparar prévia do tratamento' }))
    const preview = await screen.findByRole('region', { name: 'Prévia do tratamento' })
    expect(within(preview).getByRole('button', { name: 'Confirmar tratamento' })).toBeDisabled()
    fireEvent.click(within(preview).getByRole('checkbox'))
    fireEvent.click(within(preview).getByRole('button', { name: 'Confirmar tratamento' }))
    await screen.findByRole('heading', { name: 'Resultado original do comando' })
    expect(writes()[0]).toEqual([`${root}/grupos/${eggGroup.id}/tratamentos`, expect.objectContaining({ body: { token: 'treatment-token' } })])
    expect(screen.getByRole('button', { name: 'Reconsultar estado atual do grupo' })).toBeInTheDocument()
  })
  it('distingue divergência posterior do snapshot sem oferecer reentrada', async () => {
    const previous = vi.mocked(httpRequest).getMockImplementation()!
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path === `${root}/grupos/${eggGroup.id}` ? { ...eggDetail, origensAtuais: [{ ...eggDetail.origensAtuais[0], hashOrigem: 'hash-alterado' }] } : previous(path, options))
    render(<MemoryRouter initialEntries={[`${root}/grupos/${eggGroup.id}`]}><Routes><Route path={`${root}/grupos/:id`} element={<EggGroupDetailsPage />} /></Routes></MemoryRouter>)
    expect(await screen.findByText(/origem atual divergente/)).toBeInTheDocument()
    expect(screen.getByText(/Vínculos e ordinais permanecem vitalícios/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /reentrada/i })).not.toBeInTheDocument()
    await waitFor(() => expect(writes()).toHaveLength(0))
  })
})
