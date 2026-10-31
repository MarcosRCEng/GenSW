import { act, renderHook } from '@testing-library/react'
import { vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { HttpError, InvalidApiResponseError, NetworkError, SessionExpiredError } from '../../shared/http/httpErrors'
import { eggDetail } from './eggBridgeFixtures'
import { useEggBridgeCommand } from './useEggBridgeCommand'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
describe('Recuperação do comando global F03', () => {
  beforeEach(() => { sessionStorage.clear(); vi.mocked(httpRequest).mockReset() })
  it.each([new NetworkError(), new SessionExpiredError(), new InvalidApiResponseError(), new HttpError(403), new HttpError(503)])('mantém chave e token exatos após %s, recarga e reautenticação', async error => {
    vi.mocked(httpRequest).mockRejectedValueOnce(error).mockResolvedValueOnce(eggDetail)
    const first = renderHook(useEggBridgeCommand)
    const body = { token: 'preview-original' }
    await act(async () => { await expect(first.result.current.submit('Entrada', '/confirmacoes', body)).rejects.toBe(error) })
    const original = vi.mocked(httpRequest).mock.calls[0]
    first.unmount()
    const second = renderHook(useEggBridgeCommand)
    await act(async () => { await expect(second.result.current.submit('Entrada', '/confirmacoes', { token: 'outro' })).rejects.toThrow('pendente') })
    expect(httpRequest).toHaveBeenCalledTimes(1)
    await act(async () => { await second.result.current.retry() })
    expect(vi.mocked(httpRequest).mock.calls[1]).toEqual(original)
    expect(second.result.current.pending).toBeNull()
    expect(sessionStorage.getItem('gensw.egg-bridge.retry.v1')).toBeNull()
  })
  it.each([400, 409])('libera edição apenas após recusa %s e consulta sem resultado', async status => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new HttpError(status)).mockRejectedValueOnce(new HttpError(404))
    const { result } = renderHook(useEggBridgeCommand)
    await act(async () => { await expect(result.current.submit('Associacao', '/confirmacoes', { token: 'original' })).rejects.toThrow() })
    act(() => result.current.release())
    expect(result.current.pending).not.toBeNull()
    await act(async () => { await result.current.consult() })
    expect(result.current.releaseAllowed).toBe(true)
    act(() => result.current.release())
    expect(result.current.pending).toBeNull()
  })
  it('404 após erro ambíguo não libera contexto nem cria chave nova', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new NetworkError()).mockRejectedValueOnce(new HttpError(404)).mockResolvedValueOnce(eggDetail)
    const { result } = renderHook(useEggBridgeCommand)
    await act(async () => { await expect(result.current.submit('Entrada', '/confirmacoes', { token: 'original' })).rejects.toThrow() })
    await act(async () => { await result.current.consult() })
    expect(result.current.releaseAllowed).toBe(false)
    act(() => result.current.release())
    expect(result.current.pending?.body).toEqual({ token: 'original' })
    await act(async () => { await result.current.retry() })
    const calls = vi.mocked(httpRequest).mock.calls
    expect(calls[0]).toEqual(calls[2])
  })
  it('retorno inválido mantém a chave para consultar o resultado original sem POST novo', async () => {
    vi.mocked(httpRequest).mockResolvedValueOnce({ grupo: null }).mockResolvedValueOnce(eggDetail)
    const { result } = renderHook(useEggBridgeCommand)
    await act(async () => { await expect(result.current.submit('Compensacao', `/grupos/${eggDetail.grupo.id}/tratamentos`, { token: 'original' })).rejects.toThrow() })
    expect(result.current.pending?.operation).toBe('Compensacao')
    await act(async () => { await result.current.consult() })
    expect(vi.mocked(httpRequest).mock.calls[1][0]).toMatch(/\/comandos\/Compensacao\//)
    expect(vi.mocked(httpRequest).mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1)
  })
  it('repetição ambígua remove a rejeição anterior como base para liberação', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new HttpError(400)).mockRejectedValueOnce(new NetworkError()).mockRejectedValueOnce(new HttpError(404))
    const { result } = renderHook(useEggBridgeCommand)
    await act(async () => { await expect(result.current.submit('Entrada', '/confirmacoes', { token: 'original' })).rejects.toThrow() })
    await act(async () => { await expect(result.current.retry()).rejects.toThrow() })
    await act(async () => { await result.current.consult() })
    expect(result.current.releaseAllowed).toBe(false)
  })
  it('bloqueia envio se não puder persistir o contexto antes do POST', async () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementationOnce(() => { throw new DOMException('unavailable') })
    const { result } = renderHook(useEggBridgeCommand)
    await act(async () => { await expect(result.current.submit('Entrada', '/confirmacoes', { token: 'original' })).rejects.toThrow() })
    expect(httpRequest).not.toHaveBeenCalled()
  })
  it('preserva contexto corrompido e bloqueia envio para diagnóstico', async () => {
    const invalid = JSON.stringify({ operation: 'Compensacao', path: '/confirmacoes', key: 'original', body: { token: 'original' } })
    sessionStorage.setItem('gensw.egg-bridge.retry.v1', invalid)
    const { result } = renderHook(useEggBridgeCommand)
    expect(result.current.recoveryError).toContain('diagnóstico')
    await act(async () => { await expect(result.current.submit('Entrada', '/confirmacoes', { token: 'novo' })).rejects.toThrow() })
    expect(httpRequest).not.toHaveBeenCalled()
    expect(sessionStorage.getItem('gensw.egg-bridge.retry.v1')).toBe(invalid)
  })
})
