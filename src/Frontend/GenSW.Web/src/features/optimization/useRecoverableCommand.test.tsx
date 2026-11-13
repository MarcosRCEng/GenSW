import { act, renderHook } from '@testing-library/react'
import { vi } from 'vitest'
import { HttpError, SessionExpiredError } from '../../shared/http/httpErrors'
import { useRecoverableCommand } from './useRecoverableCommand'

const scope = 'operador:execucao'
const storageKey = `gensw.optimization.request.v1:${scope}`
const body = { referenciaDescricao: 'Formulação sintética', massaKg: '100' }
const result = { id: 'resultado' }

describe('Comandos recuperáveis da F05', () => {
  beforeEach(() => sessionStorage.clear())

  it('mantém a chave em 429, exige consulta e permite reenviar apenas o pedido original', async () => {
    const send = vi.fn<(value: typeof body, key: string) => Promise<typeof result>>()
      .mockRejectedValueOnce(new HttpError(429)).mockResolvedValueOnce(result)
    const lookup = vi.fn<(key: string) => Promise<typeof result>>().mockRejectedValueOnce(new HttpError(404))
    const hook = renderHook(() => useRecoverableCommand(scope, send, lookup))
    await act(async () => { await hook.result.current.submit(body) })
    const key = hook.result.current.key
    expect(key).toBeTruthy()
    expect(sessionStorage.getItem(storageKey)).toBe(key)
    expect(sessionStorage.getItem(storageKey)).not.toContain(body.referenciaDescricao)
    expect(hook.result.current.canRetry).toBe(false)
    await act(async () => { await hook.result.current.consult() })
    expect(lookup).toHaveBeenCalledExactlyOnceWith(key)
    expect(send).toHaveBeenCalledTimes(1)
    expect(hook.result.current.canRetry).toBe(true)
    await act(async () => { await expect(hook.result.current.retry()).resolves.toEqual(result) })
    expect(send.mock.calls).toEqual([[body, key], [body, key]])
    expect(sessionStorage.getItem(storageKey)).toBeNull()
  })

  it('distingue conflito de uma resposta desconhecida por sessão expirada', async () => {
    const send = vi.fn<(value: typeof body, key: string) => Promise<typeof result>>()
      .mockRejectedValueOnce(new HttpError(409, 'Conflict', 'A referência mudou.'))
      .mockRejectedValueOnce(new SessionExpiredError())
    const lookup = vi.fn<(key: string) => Promise<typeof result>>().mockRejectedValueOnce(new HttpError(403))
    const hook = renderHook(() => useRecoverableCommand(scope, send, lookup))
    await act(async () => { await hook.result.current.submit(body) })
    expect(hook.result.current.errorStatus).toBe(409)
    expect(hook.result.current.key).toBeNull()
    await act(async () => { await hook.result.current.submit(body) })
    expect(hook.result.current.key).toBeTruthy()
    expect(hook.result.current.error).toContain('sessão')
    await act(async () => { await hook.result.current.consult() })
    expect(hook.result.current.errorStatus).toBe(403)
    expect(hook.result.current.key).toBeTruthy()
    expect(hook.result.current.canRetry).toBe(false)
    expect(send).toHaveBeenCalledTimes(2)
  })
})
