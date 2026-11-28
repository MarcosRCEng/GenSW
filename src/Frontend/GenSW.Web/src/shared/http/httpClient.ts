import { buildApiUrl } from '../config/api'
import { logAuthDiagnostic } from './authDiagnostics'
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
  responseType?: 'json' | 'blob'
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
  headers.set('Accept', options.responseType === 'blob' ? 'image/*' : 'application/json')

  if (options.body !== undefined && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
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
      body: options.body === undefined ? undefined : options.body instanceof FormData ? options.body : JSON.stringify(options.body),
      credentials: 'include',
      cache: 'no-store',
      signal: options.signal,
    })
  } catch (error) {
    if (options.signal?.aborted || (error instanceof Error && error.name === 'AbortError')) {
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
  expectedGeneration?: number,
): Promise<T> {
  const requestSnapshot = getSessionSnapshot()
  if (expectedGeneration !== undefined && requestSnapshot.generation !== expectedGeneration) {
    throw new SessionExpiredError()
  }
  options.signal?.throwIfAborted()
  const response = await sendRequest(path, options, requestSnapshot)
  options.signal?.throwIfAborted()
  const normalizedEndpoint = endpointPath(path)

  if (normalizedEndpoint === '/auth/login') {
    logAuthDiagnostic({ event: 'login.post.response_received', status: response.status })
  } else if (normalizedEndpoint === '/auth/me') {
    logAuthDiagnostic({ event: 'auth.me.response_received', status: response.status })
  }

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

      return executeRequest<T>(path, options, true, expectedGeneration)
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

    return executeRequest<T>(path, options, true, expectedGeneration)
  }

  if (!response.ok) {
    let detail: string | undefined
    let code: string | undefined
    try {
      const problem = await response.json() as { title?: unknown; code?: unknown }
      if (typeof problem.title === 'string') detail = problem.title
      if (typeof problem.code === 'string') code = problem.code
    } catch { /* Non-JSON errors retain HTTP status. */ }
    throw new HttpError(response.status, response.statusText, detail, code)
  }

  const result = options.responseType === 'blob' ? await response.blob() as T : await readSuccessfulResponse<T>(response)

  options.signal?.throwIfAborted()

  return result
}

export async function httpRequest<T>(path: string, options: HttpRequestOptions = {}): Promise<T> {
  if (options.authenticated !== true) return executeRequest<T>(path, options, false)

  const session = getSessionSnapshot()
  if (session.accessToken === null) throw new SessionExpiredError()

  const callerSignal = options.signal
  const combined = callerSignal && new AbortController()
  const cancel = () => combined?.abort(callerSignal?.aborted ? callerSignal.reason : session.signal.reason)
  if (callerSignal && combined) {
    callerSignal.addEventListener('abort', cancel, { once: true })
    session.signal.addEventListener('abort', cancel, { once: true })
    if (callerSignal.aborted || session.signal.aborted) cancel()
  }

  try {
    const signal = combined?.signal ?? session.signal
    const result = await executeRequest<T>(path, { ...options, signal }, false, session.generation)
    signal.throwIfAborted()
    return result
  } catch (error) {
    if (session.signal.aborted) throw new SessionExpiredError()
    throw error
  } finally {
    callerSignal?.removeEventListener('abort', cancel)
    session.signal.removeEventListener('abort', cancel)
  }
}
