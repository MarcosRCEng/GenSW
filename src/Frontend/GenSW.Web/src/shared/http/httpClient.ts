import { buildApiUrl } from '../config/api'
import { HttpError, InvalidApiResponseError, NetworkError, SessionExpiredError } from './httpErrors'
import {
  getSessionSnapshot,
  invalidateSession,
  refreshSessionToken,
  type SessionSnapshot,
} from './sessionManager'

const ENDPOINTS_WITHOUT_AUTOMATIC_REFRESH = new Set([
  '/auth/login',
  '/auth/refresh',
  '/auth/logout',
  '/auth/me',
])

export interface HttpRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  headers?: HeadersInit
  authenticated?: boolean
  retryOnUnauthorized?: boolean
  signal?: AbortSignal
}

function endpointPath(path: string): string {
  const pathWithoutQuery = path.split(/[?#]/, 1)[0]
  return pathWithoutQuery.startsWith('/') ? pathWithoutQuery : `/${pathWithoutQuery}`
}

function permitsAutomaticRefresh(path: string, options: HttpRequestOptions): boolean {
  return (
    options.authenticated === true &&
    options.retryOnUnauthorized !== false &&
    !ENDPOINTS_WITHOUT_AUTOMATIC_REFRESH.has(endpointPath(path))
  )
}

function createHeaders(options: HttpRequestOptions, snapshot: SessionSnapshot): Headers {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')

  if (options.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  if (options.authenticated === true && snapshot.accessToken !== null) {
    headers.set('Authorization', `Bearer ${snapshot.accessToken}`)
  }

  return headers
}

async function sendRequest(
  path: string,
  options: HttpRequestOptions,
  snapshot: SessionSnapshot,
): Promise<Response> {
  try {
    return await fetch(buildApiUrl(path), {
      method: options.method ?? 'GET',
      headers: createHeaders(options, snapshot),
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      credentials: 'include',
      signal: options.signal,
    })
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      throw error
    }

    throw new NetworkError(error)
  }
}

async function readSuccessfulResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }

  const responseText = await response.text()

  if (!responseText) {
    return undefined as T
  }

  try {
    return JSON.parse(responseText) as T
  } catch (error) {
    throw new InvalidApiResponseError(
      error instanceof Error ? `A API retornou JSON inválido: ${error.message}` : undefined,
    )
  }
}

async function executeRequest<T>(
  path: string,
  options: HttpRequestOptions,
  hasRetried: boolean,
): Promise<T> {
  const requestSnapshot = getSessionSnapshot()
  const response = await sendRequest(path, options, requestSnapshot)
  const currentSnapshot = getSessionSnapshot()

  if (
    options.authenticated === true &&
    currentSnapshot.generation !== requestSnapshot.generation
  ) {
    throw new SessionExpiredError()
  }

  if (response.status === 401 && options.authenticated === true) {
    if (currentSnapshot.revision !== requestSnapshot.revision) {
      if (currentSnapshot.accessToken === null) {
        throw new SessionExpiredError()
      }

      if (hasRetried) {
        throw new SessionExpiredError()
      }

      return executeRequest<T>(path, options, true)
    }

    if (hasRetried || !permitsAutomaticRefresh(path, options)) {
      invalidateSession(requestSnapshot.generation)
      throw new SessionExpiredError()
    }

    const refreshedToken = await refreshSessionToken()

    if (refreshedToken === null) {
      throw new SessionExpiredError()
    }

    const refreshedSnapshot = getSessionSnapshot()

    if (
      refreshedSnapshot.generation !== requestSnapshot.generation ||
      refreshedSnapshot.accessToken === null
    ) {
      throw new SessionExpiredError()
    }

    return executeRequest<T>(path, options, true)
  }

  if (!response.ok) {
    throw new HttpError(response.status, response.statusText)
  }

  const result = await readSuccessfulResponse<T>(response)

  if (
    options.authenticated === true &&
    getSessionSnapshot().generation !== requestSnapshot.generation
  ) {
    throw new SessionExpiredError()
  }

  return result
}

export function httpRequest<T>(path: string, options: HttpRequestOptions = {}): Promise<T> {
  return executeRequest<T>(path, options, false)
}
