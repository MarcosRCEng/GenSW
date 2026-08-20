import { afterEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, login } from '../../features/auth/services/authService'
import { HttpError, SessionExpiredError } from './httpErrors'
import { httpRequest } from './httpClient'
import {
  getSessionSnapshot,
  invalidateSession,
  setAccessToken,
  subscribeToSessionInvalidation,
} from './sessionManager'

const renewedAccessToken = {
  accessToken: 'renewed-access-token',
  expiresAtUtc: '2026-08-20T15:00:00Z',
}

const currentUser = {
  userId: 'b7f14f7b-a8ff-499e-885f-a62c693de76c',
  pessoaId: '8a11a958-982d-49f4-8ba9-cbe5db840cd4',
  nome: 'Marina Silva',
  userName: 'marina',
  roles: ['Administrator'],
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((promiseResolve) => {
    resolve = promiseResolve
  })

  return { promise, resolve }
}

function jsonResponse(value: unknown): Response {
  return new Response(JSON.stringify(value), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function unauthorizedResponse(): Response {
  return new Response(null, { status: 401 })
}

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('httpClient session refresh', () => {
  it('faz duas requisições concorrentes aguardarem o mesmo refresh', async () => {
    setAccessToken('expired-access-token')

    const refreshResponse = deferred<Response>()
    const refreshStarted = deferred<void>()
    const attempts = new Map<string, number>()
    let refreshCalls = 0

    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        const url = String(input)

        if (url.endsWith('/auth/refresh')) {
          refreshCalls += 1
          refreshStarted.resolve(undefined)
          return refreshResponse.promise
        }

        const resource = url.endsWith('/resource/first') ? 'first' : 'second'
        const attempt = (attempts.get(resource) ?? 0) + 1
        attempts.set(resource, attempt)

        return Promise.resolve(
          attempt === 1 ? unauthorizedResponse() : jsonResponse({ resource }),
        )
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const requests = [
      httpRequest<{ resource: string }>('/resource/first', { authenticated: true }),
      httpRequest<{ resource: string }>('/resource/second', { authenticated: true }),
    ]

    await refreshStarted.promise
    refreshResponse.resolve(jsonResponse(renewedAccessToken))

    await expect(Promise.all(requests)).resolves.toEqual([
      { resource: 'first' },
      { resource: 'second' },
    ])
    expect(refreshCalls).toBe(1)
    expect(attempts).toEqual(
      new Map([
        ['first', 2],
        ['second', 2],
      ]),
    )
  })

  it('usa um único refresh e não cria outro quando o segundo 401 chega atrasado', async () => {
    setAccessToken('expired-access-token')

    const delayedUnauthorized = deferred<Response>()
    const refreshResponse = deferred<Response>()
    const refreshStarted = deferred<void>()
    const authorizations: Array<{ resource: string; value: string | null }> = []
    const attempts = new Map<string, number>()
    let refreshCalls = 0

    const fetchMock = vi.fn(
      (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
        const url = String(input)

        if (url.endsWith('/auth/refresh')) {
          refreshCalls += 1
          refreshStarted.resolve(undefined)
          return refreshResponse.promise
        }

        const resource = url.endsWith('/resource/first') ? 'first' : 'second'
        const attempt = (attempts.get(resource) ?? 0) + 1
        attempts.set(resource, attempt)
        authorizations.push({
          resource,
          value: new Headers(init?.headers).get('Authorization'),
        })

        if (attempt === 1) {
          return resource === 'first'
            ? Promise.resolve(unauthorizedResponse())
            : delayedUnauthorized.promise
        }

        return Promise.resolve(jsonResponse({ resource }))
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const firstRequest = httpRequest<{ resource: string }>('/resource/first', {
      authenticated: true,
    })
    const secondRequest = httpRequest<{ resource: string }>('/resource/second', {
      authenticated: true,
    })

    await refreshStarted.promise
    expect(refreshCalls).toBe(1)

    refreshResponse.resolve(jsonResponse(renewedAccessToken))
    await expect(firstRequest).resolves.toEqual({ resource: 'first' })

    delayedUnauthorized.resolve(unauthorizedResponse())
    await expect(secondRequest).resolves.toEqual({ resource: 'second' })

    expect(refreshCalls).toBe(1)
    expect(attempts).toEqual(
      new Map([
        ['first', 2],
        ['second', 2],
      ]),
    )
    expect(authorizations).toEqual([
      { resource: 'first', value: 'Bearer expired-access-token' },
      { resource: 'second', value: 'Bearer expired-access-token' },
      { resource: 'first', value: 'Bearer renewed-access-token' },
      { resource: 'second', value: 'Bearer renewed-access-token' },
    ])
  })

  it('não repete uma requisição antiga depois da troca de sessão', async () => {
    setAccessToken('session-a-token')

    const delayedUnauthorized = deferred<Response>()
    const authorizations: Array<string | null> = []
    const fetchMock = vi.fn(
      (_input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
        authorizations.push(new Headers(init?.headers).get('Authorization'))
        return delayedUnauthorized.promise
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const oldSessionRequest = httpRequest('/resource/mutation', {
      method: 'POST',
      body: { value: 'session-a-operation' },
      authenticated: true,
    })

    invalidateSession()
    setAccessToken('session-b-token')
    delayedUnauthorized.resolve(unauthorizedResponse())

    await expect(oldSessionRequest).rejects.toBeInstanceOf(SessionExpiredError)
    expect(fetchMock).toHaveBeenCalledOnce()
    expect(authorizations).toEqual(['Bearer session-a-token'])
    expect(getSessionSnapshot().accessToken).toBe('session-b-token')
  })

  it('não entrega uma resposta autenticada depois da troca de sessão', async () => {
    setAccessToken('session-a-token')

    const delayedSuccess = deferred<Response>()
    const fetchMock = vi.fn((): Promise<Response> => delayedSuccess.promise)

    vi.stubGlobal('fetch', fetchMock)

    const oldSessionRequest = httpRequest('/resource/session-a-data', {
      authenticated: true,
    })

    invalidateSession()
    setAccessToken('session-b-token')
    delayedSuccess.resolve(jsonResponse({ owner: 'session-a' }))

    await expect(oldSessionRequest).rejects.toBeInstanceOf(SessionExpiredError)
    expect(fetchMock).toHaveBeenCalledOnce()
    expect(getSessionSnapshot().accessToken).toBe('session-b-token')
  })

  it('não entrega o corpo se a sessão mudar durante a leitura da resposta', async () => {
    setAccessToken('session-a-token')

    const responseBody = deferred<string>()
    const bodyReadStarted = deferred<void>()
    const delayedBodyResponse = {
      ok: true,
      status: 200,
      statusText: 'OK',
      text: () => {
        bodyReadStarted.resolve(undefined)
        return responseBody.promise
      },
    } as Response
    const fetchMock = vi.fn((): Promise<Response> => Promise.resolve(delayedBodyResponse))

    vi.stubGlobal('fetch', fetchMock)

    const oldSessionRequest = httpRequest('/resource/session-a-body', {
      authenticated: true,
    })

    await bodyReadStarted.promise
    invalidateSession()
    setAccessToken('session-b-token')
    responseBody.resolve(JSON.stringify({ owner: 'session-a' }))

    await expect(oldSessionRequest).rejects.toBeInstanceOf(SessionExpiredError)
    expect(getSessionSnapshot().accessToken).toBe('session-b-token')
  })

  it('descarta o resultado de refresh que ficou obsoleto após novo login', async () => {
    setAccessToken('session-a-token')

    const refreshResponse = deferred<Response>()
    const refreshStarted = deferred<void>()
    let protectedCalls = 0
    let refreshCalls = 0
    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        if (String(input).endsWith('/auth/refresh')) {
          refreshCalls += 1
          refreshStarted.resolve(undefined)
          return refreshResponse.promise
        }

        protectedCalls += 1
        return Promise.resolve(unauthorizedResponse())
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const oldSessionRequest = httpRequest('/resource/session-a', { authenticated: true })

    await refreshStarted.promise
    setAccessToken('session-b-token')
    refreshResponse.resolve(jsonResponse(renewedAccessToken))

    await expect(oldSessionRequest).rejects.toBeInstanceOf(SessionExpiredError)
    expect(protectedCalls).toBe(1)
    expect(refreshCalls).toBe(1)
    expect(getSessionSnapshot().accessToken).toBe('session-b-token')
  })

  it('preserva o token renovado quando o 401 do usuário atual chega atrasado', async () => {
    setAccessToken('expired-access-token')

    const delayedCurrentUserUnauthorized = deferred<Response>()
    const authorizations: Array<{ path: string; value: string | null }> = []
    let currentUserCalls = 0
    let protectedCalls = 0
    let refreshCalls = 0
    const fetchMock = vi.fn(
      (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
        const path = new URL(String(input)).pathname
        authorizations.push({
          path,
          value: new Headers(init?.headers).get('Authorization'),
        })

        if (path.endsWith('/auth/refresh')) {
          refreshCalls += 1
          return Promise.resolve(jsonResponse(renewedAccessToken))
        }

        if (path.endsWith('/auth/me')) {
          currentUserCalls += 1
          return currentUserCalls === 1
            ? delayedCurrentUserUnauthorized.promise
            : Promise.resolve(jsonResponse(currentUser))
        }

        protectedCalls += 1
        return Promise.resolve(
          protectedCalls === 1
            ? unauthorizedResponse()
            : jsonResponse({ completed: true }),
        )
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const currentUserRequest = getCurrentUser()
    await expect(
      httpRequest('/resource/refresh-trigger', { authenticated: true }),
    ).resolves.toEqual({ completed: true })

    delayedCurrentUserUnauthorized.resolve(unauthorizedResponse())

    await expect(currentUserRequest).resolves.toEqual(currentUser)
    expect(currentUserCalls).toBe(2)
    expect(refreshCalls).toBe(1)
    expect(getSessionSnapshot().accessToken).toBe('renewed-access-token')
    expect(
      authorizations.filter(({ path }) => path.endsWith('/auth/me')).map(({ value }) => value),
    ).toEqual(['Bearer expired-access-token', 'Bearer renewed-access-token'])
  })

  it('repete a requisição original no máximo uma vez', async () => {
    setAccessToken('expired-access-token')

    let protectedCalls = 0
    let refreshCalls = 0
    let invalidations = 0
    const unsubscribe = subscribeToSessionInvalidation(() => {
      invalidations += 1
    })

    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        const url = String(input)

        if (url.endsWith('/auth/refresh')) {
          refreshCalls += 1
          return Promise.resolve(jsonResponse(renewedAccessToken))
        }

        protectedCalls += 1
        return Promise.resolve(unauthorizedResponse())
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(
      httpRequest('/resource/rejected-after-retry', { authenticated: true }),
    ).rejects.toBeInstanceOf(SessionExpiredError)

    unsubscribe()
    expect(protectedCalls).toBe(2)
    expect(refreshCalls).toBe(1)
    expect(invalidations).toBe(1)
  })

  it('compartilha a falha do refresh, invalida uma vez e não entra em loop', async () => {
    setAccessToken('expired-access-token')

    const refreshResponse = deferred<Response>()
    const refreshStarted = deferred<void>()
    let protectedCalls = 0
    let refreshCalls = 0
    let invalidations = 0
    const unsubscribe = subscribeToSessionInvalidation(() => {
      invalidations += 1
    })

    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        const url = String(input)

        if (url.endsWith('/auth/refresh')) {
          refreshCalls += 1
          refreshStarted.resolve(undefined)
          return refreshResponse.promise
        }

        protectedCalls += 1
        return Promise.resolve(unauthorizedResponse())
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    const firstRequest = httpRequest('/resource/first', { authenticated: true })
    const secondRequest = httpRequest('/resource/second', { authenticated: true })

    await refreshStarted.promise
    refreshResponse.resolve(unauthorizedResponse())

    const results = await Promise.allSettled([firstRequest, secondRequest])

    unsubscribe()
    expect(results.every((result) => result.status === 'rejected')).toBe(true)
    expect(protectedCalls).toBe(2)
    expect(refreshCalls).toBe(1)
    expect(invalidations).toBe(1)
  })

  it('não tenta refresh automático para login ou usuário atual', async () => {
    setAccessToken('expired-access-token')

    let refreshCalls = 0
    const fetchMock = vi.fn(
      (input: RequestInfo | URL): Promise<Response> => {
        if (String(input).endsWith('/auth/refresh')) {
          refreshCalls += 1
        }

        return Promise.resolve(unauthorizedResponse())
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(login({ userName: 'marina', password: 'incorreta' })).rejects.toBeInstanceOf(
      HttpError,
    )
    await expect(getCurrentUser()).rejects.toBeInstanceOf(SessionExpiredError)
    expect(refreshCalls).toBe(0)
  })

  it('mantém o access token somente em memória durante login e leitura do usuário', async () => {
    const localStorageSetItem = vi.spyOn(window.localStorage, 'setItem')
    const sessionStorageSetItem = vi.spyOn(window.sessionStorage, 'setItem')
    let currentUserAuthorization: string | null = null

    const fetchMock = vi.fn(
      (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
        const url = String(input)

        if (url.endsWith('/auth/login')) {
          return Promise.resolve(jsonResponse(renewedAccessToken))
        }

        currentUserAuthorization = new Headers(init?.headers).get('Authorization')
        return Promise.resolve(jsonResponse(currentUser))
      },
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(login({ userName: 'marina', password: 'segredo' })).resolves.toEqual(currentUser)
    expect(currentUserAuthorization).toBe('Bearer renewed-access-token')
    expect(localStorageSetItem).not.toHaveBeenCalled()
    expect(sessionStorageSetItem).not.toHaveBeenCalled()
  })
})
