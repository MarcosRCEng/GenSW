import { act, fireEvent, render, renderHook, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, NetworkError } from '../../shared/http/httpErrors'
import { CostPreviewPanel, CostSummaryPanel, ReceiptCostEditor } from './MaterialCostControls'
import { allocationBody, evidenceForm, receiptBody, receiptForm } from './costForms'
import { parseCostInventoryPreview, parseCostSummary } from './materialCostService'
import { useCostCommand, validCostPending } from './useCostCommand'
import type { CostSummary } from './types'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
const id = '10000000-0000-0000-0000-000000000001'
const other = '10000000-0000-0000-0000-000000000002'
const component = { id, camadaId: id, itemId: id, unidade: 'kg', quantidade: '2', valorConhecido: '4.00', lacunas: [], evidenciaId: id, fracaoNumerador: '1', fracaoDenominador: '1' }
const complete: CostSummary = { estado: 'Completo', valorConhecido: '4.00', valorTotal: '4.00', custoUnitarioCompleto: '2.000000', componentes: [component], lacunas: [], sequenciaAte: '123', revisao: 1 }
const body = { id, loteId: id, localId: other, quantidade: '2', revisaoEsperada: 1, motivo: 'Documento conferido', evidencia: { fonte: 'Documento teste', referenciaDocumento: 'opaco-123', dataFonte: '2026-11-03', moeda: 'BRL', totalDocumentado: '4.00', componentes: ['Material'], lacunas: [] } }
const response = { id, tipo: 'AvaliacaoPresente', sequencia: '123', motivo: 'Documento conferido', createdAtUtc: '2026-11-03T12:00:00Z', snapshotJson: '{}', eventoOriginalId: null }
const path = '/custos-materiais/avaliacoes'
describe('Contrato monetário e recuperação F04', () => {
  beforeEach(() => { sessionStorage.clear(); vi.mocked(httpRequest).mockReset() })
  it('mantém precisão máxima em string e rejeita número JS, escala, soma ou cobertura falsas', () => {
    expect(parseCostSummary(complete)).toEqual(complete)
    for (const invalid of [{ ...complete, valorConhecido: 4 }, { ...complete, valorTotal: '4.001' }, { ...complete, valorConhecido: '3.00' }, { ...complete, estado: 'Parcial' }, { ...complete, lacunas: ['Frete ausente'] }, { ...complete, componentes: [{ ...component, valorConhecido: null }] }]) expect(() => parseCostSummary(invalid)).toThrow()
    expect(() => parseCostInventoryPreview({ custo: { ...complete, valorConhecido: 0 }, sequenciaEsperada: '123', avisos: [] })).toThrow()
  })
  it('renderiza desconhecido, parcial e zero comprovado sem converter falha ou ausência em zero', () => {
    const unknown = { ...complete, estado: 'Desconhecido' as const, valorConhecido: '0.00', valorTotal: null, custoUnitarioCompleto: null, componentes: [{ ...component, valorConhecido: null, lacunas: ['Documento ausente'] }], lacunas: ['Documento ausente'] }
    const view = render(<MemoryRouter><CostSummaryPanel value={parseCostSummary(unknown)} components={false} /></MemoryRouter>)
    expect(screen.getByText('Sem parcela comprovada')).toBeInTheDocument(); expect(screen.queryByText('R$ 0,00')).not.toBeInTheDocument()
    view.rerender(<MemoryRouter><CostSummaryPanel value={{ ...complete, valorConhecido: '0.00', valorTotal: '0.00', custoUnitarioCompleto: '0.000000', componentes: [{ ...component, valorConhecido: '0.00' }] }} components={false} /></MemoryRouter>)
    expect(screen.getAllByText('R$ 0,00')).toHaveLength(2)
  })
  it('exibe o resíduo unitário exato e preserva o total em centavos na saída positiva', () => {
    const cost = { ...complete, valorConhecido: '0.01', valorTotal: '0.01', custoUnitarioCompleto: '0.003333', componentes: [] }
    render(<MemoryRouter><CostPreviewPanel value={{ estado: 'Apurada', custo: cost, sequenciaEsperada: '123', destinos: [{
      id, destinoId: other, tipo: 'Saida', saidaProducaoId: other, perdaId: null, quantidade: '3', unidade: 'un', percentual: '100',
      custo: cost, unitarioConhecido: '0.003333', unitarioArredondado: true, residuoNumerador: '1000000', residuoDenominador: '3000000', motivo: 'Rateio conferido',
    }] }} /></MemoryRouter>)
    expect(screen.getByText('Resíduo exato de (1000000/3000000) × 0,000001 BRL/un.')).toBeInTheDocument()
    expect(screen.getAllByRole('cell', { name: 'R$ 0,01' })).toHaveLength(2)
  })
  it('exige decisão consciente no ingresso e conserva zero documental explícito', () => {
    expect(() => receiptBody(receiptForm())).toThrow('Escolha')
    const value = { ...receiptForm(), choice: 'Conhecido' as const, motivo: 'Custo conferido', fonte: 'Documento', referenciaDocumento: 'opaco', componentes: 'Materiais', totalDocumentado: '0,00' }
    expect(receiptBody(value).evidencia?.totalDocumentado).toBe('0.00')
    expect(() => receiptBody({ ...value, totalDocumentado: '' })).toThrow()
    const onChange = vi.fn(); render(<ReceiptCostEditor value={receiptForm()} onChange={onChange} />)
    expect(screen.getByLabelText('Conhecimento do custo')).toHaveValue('')
    fireEvent.change(screen.getByLabelText('Conhecimento do custo'), { target: { value: 'Desconhecido' } }); expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ choice: 'Desconhecido' }))
    expect(evidenceForm().totalDocumentado).toBe('')
  })
  it('não cria percentuais por peso, exige soma exata e mantém identidades das parcelas e perdas', () => {
    const destinations = [{ id, tipo: 'Saida' as const, label: 'A', zero: false }, { id: other, tipo: 'Perda' as const, label: 'B', zero: false }]
    expect(() => allocationBody({ choice: 'apurar', motivo: 'Confirmado', percentages: { [id]: '99,999999', [other]: '0' } }, destinations)).toThrow('100%')
    expect(allocationBody({ choice: 'apurar', motivo: 'Confirmado', percentages: { [id]: '50', [other]: '50' } }, destinations).percentuais).toEqual([{ destinoId: id, tipo: 'Saida', percentual: '50' }, { destinoId: other, tipo: 'Perda', percentual: '50' }])
    expect(allocationBody({ choice: 'pendente', motivo: 'Sem política aprovada', percentages: {} }, destinations)).toMatchObject({ pendenteRateio: true, percentuais: [] })
  })
  it.each([new NetworkError(), new HttpError(503), new HttpError(403)])('recupera corpo e chave após erro ambíguo e recarga: %s', async error => {
    vi.mocked(httpRequest).mockRejectedValueOnce(error).mockResolvedValueOnce(response)
    const first = renderHook(() => useCostCommand('test'))
    await act(async () => { await expect(first.result.current.submit(id, 'avaliacao', path, body)).rejects.toBe(error) })
    const call = vi.mocked(httpRequest).mock.calls[0]; first.unmount()
    const next = renderHook(() => useCostCommand('test')); expect(next.result.current.pending?.body).toEqual(body)
    expect(() => next.result.current.submit(id, 'avaliacao', path, { ...body, motivo: 'Alterado' })).toThrow('original')
    await act(async () => { expect(await next.result.current.retry()).toEqual(response) }); expect(vi.mocked(httpRequest).mock.calls[1]).toEqual(call); expect(next.result.current.pending).toBeNull()
    expect(sessionStorage.getItem('gensw.material-cost.retry.v1:test')).toBeNull()
  })
  it('404 isolado não libera edição; rejeição definitiva com consulta ausente permite revisar', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new NetworkError()).mockRejectedValueOnce(new HttpError(404)).mockRejectedValueOnce(new HttpError(409)).mockRejectedValueOnce(new HttpError(404))
    const { result } = renderHook(() => useCostCommand('test'))
    await act(async () => { await expect(result.current.submit(id, 'avaliacao', path, body)).rejects.toThrow() }); await act(async () => { await result.current.consult() }); expect(result.current.releaseAllowed).toBe(false)
    act(() => result.current.release()); expect(result.current.pending).not.toBeNull()
    await act(async () => { await expect(result.current.retry()).rejects.toThrow() }); await act(async () => { await result.current.consult() }); expect(result.current.releaseAllowed).toBe(true); act(() => result.current.release()); expect(result.current.pending).toBeNull()
  })
  it('resposta inválida ou de outro pedido conserva o contexto para diagnóstico', async () => {
    vi.mocked(httpRequest).mockResolvedValueOnce({ ...response, id: other })
    const { result } = renderHook(() => useCostCommand('test')); await act(async () => { await expect(result.current.submit(id, 'avaliacao', path, body)).rejects.toThrow('identidade') }); expect(result.current.pending?.body).toEqual(body)
  })
  it('contexto corrompido bloqueia envio e não inventa nova chave', async () => {
    const saved = { resource: id, operation: 'avaliacao', path, key: 'same-key', body: { ...body, evidencia: { ...body.evidencia, totalDocumentado: 4 } } }
    expect(validCostPending(saved)).toBe(false); sessionStorage.setItem('gensw.material-cost.retry.v1:test', JSON.stringify(saved)); const { result } = renderHook(() => useCostCommand('test'))
    expect(result.current.recoveryError).toBeTruthy(); expect(() => result.current.submit(id, 'avaliacao', path, body)).toThrow('inválido'); expect(httpRequest).not.toHaveBeenCalled()
  })
  it('persiste antes do envio e impede ação dupla enquanto o resultado está em voo', async () => {
    let finish!: (v: unknown) => void; vi.mocked(httpRequest).mockImplementation(() => new Promise(resolve => { finish = resolve })); const { result } = renderHook(() => useCostCommand('test')); let request!: Promise<unknown>
    act(() => { request = result.current.submit(id, 'avaliacao', path, body) }); expect(sessionStorage.getItem('gensw.material-cost.retry.v1:test')).toContain('Documento conferido'); expect(result.current.busy).toBe(true)
    expect(() => result.current.submit(id, 'avaliacao', path, body)).toThrow(); expect(httpRequest).toHaveBeenCalledTimes(1); await act(async () => { finish(response); await request }); expect(result.current.pending).toBeNull()
  })
})
