import { afterEach, describe, expect, it, vi } from 'vitest'
import { NetworkError, SessionExpiredError } from '../../../shared/http/httpErrors'
import { httpRequest } from '../../../shared/http/httpClient'
import {
  getSessionSnapshot,
  setAccessToken,
  subscribeToSessionInvalidation,
} from '../../../shared/http/sessionManager'
import { bootstrapSession, login, logout } from './authService'

const accessTokenResponse = {
  accessToken: 'restored-access-token',
  expiresAtUtc: '2026-08-20T15:00:00Z',
}

const currentUser = {
  userId: 'b7f14f7b-a8ff-499e-885f-a62c693de76c',
  pessoaId: '8a11a958-982d-49f4-8ba9-cbe5db840cd4',
  nome: 'Marina Silva',
  userName: 'marina',
  roles: [],
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve
    reject = promiseReject
  })

  return { promise, reject, resolve }
}

function jsonResponse(value: unknown): Response {
  return new Response(JSON.stringify(value), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('authService', () => {
  it('restaura a sessão com refresh e consulta o usuário usando o token em memória', async () => {
    const requests: Array<{
      path: string
      method: string | undefined
      credentials: RequestCredentials | undefined
      authorization: string | null
    }> = []

    const fetchMock = vi.fn(
      (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
        const path = new URL(String(input)).pathname
        requests.push({
          path,
          method: init?.method,
          credentials: init?.credentials,
          authorization: new Headers(init?.headers).get('Authorization'),
        })

        return Promise.resolve(
          path.endsWith('/auth/refresh')
            ? jsonResponse(accessTokenResponse)
            : jsonResponse(currentUser),
        )
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(bootstrapSession()).resolves.toEqual(currentUser)
    expect(requests).toEqual([
      {
        path: '/api/v1/auth/refresh',
        method: 'POST',
        credentials: 'include',
        authorization: null,
      },
      {
        path: '/api/v1/auth/me',
        method: 'GET',
        credentials: 'include',
        authorization: 'Bearer restored-access-token',
      },
    ])
  })

  it('trata refresh 401 no bootstrap como sessão anônima normal', async () => {
    const fetchMock = vi.fn((): Promise<Response> =>
      Promise.resolve(new Response(null, { status: 401 })),
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(bootstrapSession()).resolves.toBeNull()
    expect(fetchMock).toHaveBeenCalledOnce()
    expect(getSessionSnapshot().accessToken).toBeNull()
  })

  it('limpa o estado local depois do logout remoto', async () => {
    setAccessToken('active-access-token')
    let invalidations = 0
    const unsubscribe = subscribeToSessionInvalidation(() => {
      invalidations += 1
    })
    const fetchMock = vi.fn(
      (_input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
        expect(init?.method).toBe('POST')
        expect(init?.credentials).toBe('include')
        expect(new Headers(init?.headers).has('Authorization')).toBe(false)
        return Promise.resolve(new Response(null, { status: 204 }))
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(logout()).resolves.toBeUndefined()

    unsubscribe()
    expect(getSessionSnapshot().accessToken).toBeNull()
    expect(invalidations).toBe(1)
  })

  it('limpa o estado local mesmo quando o logout remoto falha', async () => {
    setAccessToken('active-access-token')
    let invalidations = 0
    const unsubscribe = subscribeToSessionInvalidation(() => {
      invalidations += 1
    })
    const fetchMock = vi.fn((): Promise<Response> =>
      Promise.reject(new TypeError('network unavailable')),
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(logout()).rejects.toBeInstanceOf(NetworkError)

    unsubscribe()
    expect(getSessionSnapshot().accessToken).toBeNull()
    expect(invalidations).toBe(1)
  })

  it('aguarda um refresh pendente antes de encerrar a sessão remota', async () => {
    setAccessToken('active-access-token')

    const refreshResponse = deferred<Response>()
    const refreshStarted = deferred<void>()
    const callOrder: string[] = []
    let resourceCalls = 0
    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        const path = new URL(String(input)).pathname
        callOrder.push(path)

        if (path.endsWith('/auth/refresh')) {
          refreshStarted.resolve(undefined)
          return refreshResponse.promise
        }

        if (path.endsWith('/auth/logout')) {
          return Promise.resolve(new Response(null, { status: 204 }))
        }

        resourceCalls += 1
        return Promise.resolve(
          resourceCalls === 1
            ? new Response(null, { status: 401 })
            : jsonResponse({ completed: true }),
        )
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const protectedRequest = httpRequest('/resource/in-flight', { authenticated: true })

    await refreshStarted.promise
    const logoutRequest = logout()
    await Promise.resolve()

    expect(getSessionSnapshot().accessToken).toBeNull()
    expect(callOrder).not.toContain('/api/v1/auth/logout')

    refreshResponse.resolve(jsonResponse(accessTokenResponse))

    await expect(logoutRequest).resolves.toBeUndefined()
    await expect(protectedRequest).rejects.toBeInstanceOf(SessionExpiredError)

    expect(callOrder.indexOf('/api/v1/auth/refresh')).toBeLessThan(
      callOrder.indexOf('/api/v1/auth/logout'),
    )
    expect(resourceCalls).toBe(1)
    expect(getSessionSnapshot().accessToken).toBeNull()
  })

  it('não invalida uma nova sessão quando /me antigo falha por rede', async () => {
    const currentUserRequest = deferred<Response>()
    const currentUserStarted = deferred<void>()
    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        const path = new URL(String(input)).pathname

        if (path.endsWith('/auth/login')) {
          return Promise.resolve(jsonResponse(accessTokenResponse))
        }

        currentUserStarted.resolve(undefined)
        return currentUserRequest.promise
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const oldLogin = login({ userName: 'marina', password: 'secret' })

    await currentUserStarted.promise
    setAccessToken('new-session-token')
    currentUserRequest.reject(new TypeError('network unavailable'))

    await expect(oldLogin).rejects.toBeInstanceOf(NetworkError)
    expect(getSessionSnapshot().accessToken).toBe('new-session-token')
  })

  it('registra os estágios do login sem expor senha, token, Authorization ou cookies', async () => {
    const password = 'SENSITIVE_PASSWORD_AUTH_309'
    const accessToken = 'SENSITIVE_ACCESS_TOKEN_AUTH_309'
    const refreshToken = 'SENSITIVE_REFRESH_TOKEN_AUTH_309'
    const authorization = `Bearer ${accessToken}`
    const cookie = `GenSW.RefreshSession=${refreshToken}`
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => undefined)
    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        const path = new URL(String(input)).pathname

        return Promise.resolve(
          path.endsWith('/auth/login')
            ? jsonResponse({ ...accessTokenResponse, accessToken })
            : jsonResponse(currentUser),
        )
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(login({ userName: 'marina', password })).resolves.toEqual(currentUser)

    const diagnostics = infoSpy.mock.calls.map(([message]) =>
      JSON.parse(String(message).replace('[GenSW auth] ', '')),
    )
    expect(diagnostics).toEqual(
      expect.arrayContaining([
        { event: 'login.attempt.started' },
        { event: 'login.post.started' },
        { event: 'login.post.response_received', status: 200 },
        { event: 'login.parser.started' },
        { event: 'login.parser.pass' },
        { event: 'login.token_store.started', tokenPresent: true },
        { event: 'login.token_store.completed', tokenPresent: true },
        { event: 'login.current_user_load.started' },
        { event: 'auth.me.started' },
        { event: 'auth.me.response_received', status: 200 },
        { event: 'login.completed' },
      ]),
    )

    const serializedDiagnostics = JSON.stringify(infoSpy.mock.calls)
    expect(serializedDiagnostics).not.toContain(password)
    expect(serializedDiagnostics).not.toContain(accessToken)
    expect(serializedDiagnostics).not.toContain(refreshToken)
    expect(serializedDiagnostics).not.toContain(authorization)
    expect(serializedDiagnostics).not.toContain(cookie)
  })

  it('registra falha segura do parser e o estágio onde o login aborta', async () => {
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => undefined)
    const fetchMock = vi.fn((): Promise<Response> => Promise.resolve(jsonResponse({ accepted: true })))

    vi.stubGlobal('fetch', fetchMock)

    await expect(login({ userName: 'marina', password: 'SENSITIVE_PASSWORD_AUTH_309' })).rejects.toThrow(
      'A API retornou um token de acesso inválido.',
    )

    const diagnostics = infoSpy.mock.calls.map(([message]) =>
      JSON.parse(String(message).replace('[GenSW auth] ', '')),
    )
    expect(diagnostics).toEqual(
      expect.arrayContaining([
        { event: 'login.parser.fail', errorType: 'InvalidApiResponseError' },
        {
          event: 'login.flow_aborted',
          errorType: 'InvalidApiResponseError',
          stage: 'request_access_token',
        },
      ]),
    )
  })

  it('mantém o fluxo funcional quando o console rejeita o diagnóstico', async () => {
    vi.spyOn(console, 'info').mockImplementation(() => {
      throw new Error('diagnostic sink unavailable')
    })
    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> =>
        Promise.resolve(
          new URL(String(input)).pathname.endsWith('/auth/login')
            ? jsonResponse(accessTokenResponse)
            : jsonResponse(currentUser),
        ),
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(login({ userName: 'marina', password: 'secret' })).resolves.toEqual(currentUser)
  })
})
