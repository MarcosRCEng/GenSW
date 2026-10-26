import { act, renderHook } from '@testing-library/react'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, InvalidApiResponseError, NetworkError, SessionExpiredError } from '../../shared/http/httpErrors'
import { useProductionCommand } from './useProductionCommand'
import { order } from './productionFixtures'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
describe('Retry durável de produção', () => {
  beforeEach(() => { sessionStorage.clear(); vi.mocked(httpRequest).mockReset() })
  it.each([new NetworkError(), new SessionExpiredError(), new InvalidApiResponseError(), new HttpError(503)])('preserva chave e corpo após erro ambíguo %s, recarga e reautenticação', async error => {
    vi.mocked(httpRequest).mockRejectedValueOnce(error).mockResolvedValueOnce(order)
    const first = renderHook(() => useProductionCommand(order.id))
    const body = { versaoEsperada: 2, responsavelId: 'responsavel', entradas: [{ linhaId: 'linha', alocacoes: [{ id: 'allocation', loteId: 'lote', localId: 'local', quantidade: '100.000001', unidade: 'kg' }] }], motivo: 'Conferido' }
    await act(async () => { await expect(first.result.current.submit(order.id, 'inicio', body)).rejects.toBe(error) })
    const original = vi.mocked(httpRequest).mock.calls[0]
    expect(first.result.current.pending?.body).toEqual(body)
    expect(sessionStorage.getItem(`gensw.production.retry.v1:${order.id}`)).not.toContain('Authorization')
    first.unmount()
    const reauthenticated = renderHook(() => useProductionCommand(order.id))
    await act(async () => { await expect(reauthenticated.result.current.submit(order.id, 'inicio', { ...body, motivo: 'Alterado' })).rejects.toThrow('pendente') })
    expect(httpRequest).toHaveBeenCalledTimes(1)
    await act(async () => { expect(await reauthenticated.result.current.retry()).toEqual(order) })
    expect(vi.mocked(httpRequest).mock.calls[1]).toEqual(original)
    expect(reauthenticated.result.current.pending).toBeNull()
  })
  it.each([400, 409])('só permite liberar edição após rejeição definitiva %s e GET confirmar ausência da chave', async status => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new HttpError(status)).mockRejectedValueOnce(new HttpError(404))
    const { result } = renderHook(() => useProductionCommand(order.id))
    await act(async () => { await expect(result.current.submit(order.id, 'planejamento', { versaoEsperada: 1, motivo: 'Conferido' })).rejects.toThrow() })
    act(() => result.current.release())
    expect(result.current.pending).not.toBeNull()
    await act(async () => { await result.current.consult() })
    expect(result.current.absent).toBe(true)
    expect(result.current.releaseAllowed).toBe(true)
    expect(vi.mocked(httpRequest).mock.calls[1][0]).toMatch(/\/comandos\/planejamento\//)
    act(() => result.current.release())
    expect(result.current.pending).toBeNull()
  })
  it.each([new NetworkError(), new SessionExpiredError(), new HttpError(403), new HttpError(503)])('404 não libera edição após resultado ambíguo ou perda de permissão %s', async error => {
    vi.mocked(httpRequest).mockRejectedValueOnce(error).mockRejectedValueOnce(new HttpError(404)).mockResolvedValueOnce(order)
    const { result } = renderHook(() => useProductionCommand(order.id))
    const body = { versaoEsperada: 1, motivo: 'Pedido original' }
    await act(async () => { await expect(result.current.submit(order.id, 'planejamento', body)).rejects.toThrow() })
    const original = vi.mocked(httpRequest).mock.calls[0]
    await act(async () => { await result.current.consult() })
    expect(result.current.absent).toBe(true)
    expect(result.current.releaseAllowed).toBe(false)
    act(() => result.current.release())
    expect(result.current.pending?.body).toEqual(body)
    await act(async () => { await expect(result.current.submit(order.id, 'planejamento', { ...body, motivo: 'Alterado' })).rejects.toThrow('pendente') })
    await act(async () => { await result.current.retry() })
    expect(vi.mocked(httpRequest).mock.calls[2]).toEqual(original)
  })
  it('conserva rejeição definitiva na recarga, mas uma nova tentativa ambígua exige resolver a chave original', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new HttpError(400)).mockRejectedValueOnce(new NetworkError()).mockRejectedValueOnce(new HttpError(404))
    const first = renderHook(() => useProductionCommand(order.id))
    await act(async () => { await expect(first.result.current.submit(order.id, 'planejamento', { versaoEsperada: 1, motivo: 'Pedido original' })).rejects.toThrow() })
    expect(first.result.current.pending?.definitiveFailureStatus).toBe(400)
    first.unmount()
    const second = renderHook(() => useProductionCommand(order.id))
    expect(second.result.current.pending?.definitiveFailureStatus).toBe(400)
    await act(async () => { await expect(second.result.current.retry()).rejects.toThrow() })
    expect(second.result.current.pending?.definitiveFailureStatus).toBeUndefined()
    await act(async () => { await second.result.current.consult() })
    expect(second.result.current.releaseAllowed).toBe(false)
    act(() => second.result.current.release())
    expect(second.result.current.pending).not.toBeNull()
  })
  it('consulta recupera o resultado original sem novo POST; perda de Admin mantém contexto', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new NetworkError()).mockRejectedValueOnce(new HttpError(403)).mockResolvedValueOnce(order)
    const { result } = renderHook(() => useProductionCommand(order.id))
    await act(async () => { await expect(result.current.submit(order.id, 'reversao', { versaoEsperada: 2 })).rejects.toThrow() })
    await act(async () => { await expect(result.current.consult()).rejects.toThrow() })
    expect(result.current.pending?.operation).toBe('reversao')
    await act(async () => { await result.current.consult() })
    expect(result.current.pending).toBeNull()
    expect(vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1)
  })
  it('persiste a identidade da criação antes do request e bloqueia clique duplo', async () => {
    let finish!: (value: unknown) => void
    vi.mocked(httpRequest).mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const { result } = renderHook(() => useProductionCommand('nova'))
    let original!: Promise<unknown>
    act(() => { original = result.current.submit(order.id, 'criacao', { id: order.id, tamanho: '100' }) })
    expect(JSON.parse(sessionStorage.getItem('gensw.production.retry.v1:nova')!).body.id).toBe(order.id)
    await act(async () => { await expect(result.current.submit(order.id, 'criacao', { id: 'new-id' })).rejects.toThrow('pendente') })
    expect(httpRequest).toHaveBeenCalledTimes(1)
    await act(async () => { finish(order); await original })
    expect(result.current.busy).toBe(false)
  })
  it('bloqueia novo envio e preserva evidência quando o contexto salvo está inválido', async () => {
    sessionStorage.setItem(`gensw.production.retry.v1:${order.id}`, '{incompleto')
    const { result } = renderHook(() => useProductionCommand(order.id))
    expect(result.current.recoveryError).toContain('Preserve esta sessão')
    expect(result.current.busy).toBe(true)
    await act(async () => { await expect(result.current.submit(order.id, 'confirmacao', {})).rejects.toThrow('diagnóstico') })
    expect(httpRequest).not.toHaveBeenCalled()
    expect(sessionStorage.getItem(`gensw.production.retry.v1:${order.id}`)).toBe('{incompleto')
  })
  it('apresenta bloqueio recuperável quando o armazenamento da sessão está indisponível', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new DOMException('Unavailable', 'SecurityError') })
    const { result } = renderHook(() => useProductionCommand(order.id))
    expect(result.current.recoveryError).toContain('diagnóstico')
    await act(async () => { await expect(result.current.submit(order.id, 'inicio', {})).rejects.toThrow('diagnóstico') })
    expect(httpRequest).not.toHaveBeenCalled()
  })
  it.each([
    ['planejamento', null], ['planejamento', []], ['planejamento', { versaoEsperada: 1, motivo: 7 }],
    ['criacao', { versaoEsperada: 0, motivo: 'Cadastro', id: order.id }],
    ['inicio', { versaoEsperada: 2, motivo: 'Início', responsavelId: 'responsavel', entradas: [null] }],
    ['inicio', { versaoEsperada: 2, motivo: 'Início', responsavelId: 'responsavel', entradas: [{ linhaId: 'linha', alocacoes: [null] }] }],
    ['confirmacao', { versaoEsperada: 3, motivo: 'Confirmação', responsavelId: 'responsavel', entradas: [], saidas: [null], perdas: [] }],
    ['confirmacao-ocorrencia', { versaoEsperada: 3, motivo: 'Confirmação', responsavelId: 'responsavel', entradas: [], saidas: [], perdas: [null] }],
    ['reversao', { versaoEsperada: 4, motivo: 'Reversão', evidencia: 'Conferido' }],
  ])('bloqueia contexto JSON válido com corpo corrompido na operação %s', async (operation, body) => {
    const context = JSON.stringify({ orderId: order.id, operation, path: operation === 'criacao' ? '/ordens-producao' : `/ordens-producao/${order.id}/${operation}`, method: 'POST', key: 'original-key', body })
    sessionStorage.setItem(`gensw.production.retry.v1:${order.id}`, context)
    const { result } = renderHook(() => useProductionCommand(order.id))
    expect(result.current.recoveryError).toContain('diagnóstico')
    expect(result.current.busy).toBe(true)
    await act(async () => { await expect(result.current.submit(order.id, 'planejamento', {})).rejects.toThrow('diagnóstico') })
    expect(httpRequest).not.toHaveBeenCalled()
    expect(sessionStorage.getItem(`gensw.production.retry.v1:${order.id}`)).toBe(context)
  })
})
