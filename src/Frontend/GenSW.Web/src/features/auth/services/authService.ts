import { HttpError, SessionExpiredError } from '../../../shared/http/httpErrors'
import { getAuthErrorType, logAuthDiagnostic } from '../../../shared/http/authDiagnostics'
import { httpRequest } from '../../../shared/http/httpClient'
import {
  getSessionSnapshot,
  invalidateSession,
  refreshSessionToken,
  registerSessionRefreshHandler,
  setAccessToken,
  subscribeToSessionInvalidation,
  waitForSessionRefresh,
} from '../../../shared/http/sessionManager'
import type { AccessTokenResponse, CurrentUser, LoginRequest } from '../types/auth'
import { parseAccessTokenResponse, parseCurrentUser } from './authContractParsers'

const AUTH_ENDPOINTS = {
  login: '/auth/login',
  refresh: '/auth/refresh',
  logout: '/auth/logout',
  me: '/auth/me',
} as const
const signedOutKey = 'gensw.auth.signed-out'

let logoutInFlight: Promise<void> | null = null
let cookieOperations = Promise.resolve()
let authIntent = 0
let signedOutLocally = false

function rememberSignOut(signedOut: boolean): void {
  signedOutLocally = signedOut
  try {
    if (signedOut) sessionStorage.setItem(signedOutKey, '1')
    else sessionStorage.removeItem(signedOutKey)
  } catch { /* The current document still honors the local sign-out. */ }
}

function allowsSessionRestore(): boolean {
  if (signedOutLocally) return false
  try { return sessionStorage.getItem(signedOutKey) === null }
  catch { return true }
}

function withAuthCookie<T>(operation: () => Promise<T>): Promise<T> {
  const pending = cookieOperations.then(() => navigator.locks
    ? navigator.locks.request('gensw.auth-cookie', operation)
    : operation())
  cookieOperations = pending.then(() => undefined, () => undefined)
  return pending
}

function endRemoteSession(accessToken: string | null, sessionId: string | null): Promise<void> {
  const headers = new Headers()
  if (accessToken !== null) headers.set('Authorization', `Bearer ${accessToken}`)
  if (sessionId !== null) headers.set('X-GenSW-Session', sessionId)
  return httpRequest<void>(AUTH_ENDPOINTS.logout, {
    method: 'POST',
    headers,
    authenticated: false,
    retryOnUnauthorized: false,
  })
}

async function waitForPendingLogout(): Promise<void> {
  if (logoutInFlight === null) {
    return
  }

  try {
    await logoutInFlight
  } catch {
    // Um novo login pode prosseguir depois que a tentativa remota de logout terminar.
  }
}

async function requestAccessToken(request: LoginRequest): Promise<AccessTokenResponse> {
  logAuthDiagnostic({ event: 'login.post.started' })
  const response = await httpRequest<unknown>(AUTH_ENDPOINTS.login, {
    method: 'POST',
    body: request,
    authenticated: false,
    retryOnUnauthorized: false,
  })

  logAuthDiagnostic({ event: 'login.parser.started' })

  try {
    const parsedResponse = parseAccessTokenResponse(response)
    logAuthDiagnostic({ event: 'login.parser.pass' })
    return parsedResponse
  } catch (error) {
    logAuthDiagnostic({ event: 'login.parser.fail', errorType: getAuthErrorType(error) })
    throw error
  }
}

async function requestRefreshedAccessToken(expectedSessionId: string | null): Promise<AccessTokenResponse | null> {
  try {
    const response = await withAuthCookie(() => httpRequest<unknown>(AUTH_ENDPOINTS.refresh, {
      method: 'POST',
      headers: expectedSessionId !== null ? { 'X-GenSW-Session': expectedSessionId } : undefined,
      authenticated: false,
      retryOnUnauthorized: false,
    }))

    return parseAccessTokenResponse(response)
  } catch (error) {
    if (error instanceof HttpError && error.status === 401) {
      return null
    }

    throw error
  }
}

registerSessionRefreshHandler(requestRefreshedAccessToken)

export function refreshSession(): Promise<AccessTokenResponse | null> {
  return refreshSessionToken()
}

export async function getCurrentUser(): Promise<CurrentUser> {
  logAuthDiagnostic({ event: 'auth.me.started' })
  const response = await httpRequest<unknown>(AUTH_ENDPOINTS.me, {
    authenticated: true,
    retryOnUnauthorized: false,
  })

  return parseCurrentUser(response)
}

async function loadCurrentUserAfterToken(): Promise<CurrentUser> {
  logAuthDiagnostic({ event: 'login.current_user_load.started' })
  const expectedGeneration = getSessionSnapshot().generation

  try {
    return await getCurrentUser()
  } catch (error) {
    if (!(error instanceof SessionExpiredError)) {
      invalidateSession(expectedGeneration)
    }

    throw error
  }
}

export async function bootstrapSession(): Promise<CurrentUser | null> {
  if (!allowsSessionRestore()) return null
  const refreshedToken = await refreshSession()

  if (refreshedToken === null) {
    return null
  }

  return loadCurrentUserAfterToken()
}

export async function login(request: LoginRequest): Promise<CurrentUser> {
  const intent = ++authIntent
  if (getSessionSnapshot().accessToken !== null) invalidateSession()
  logAuthDiagnostic({ event: 'login.attempt.started' })
  let stage = 'wait_for_pending_logout'

  try {
    await waitForPendingLogout()
    stage = 'wait_for_session_refresh'
    await waitForSessionRefresh()

    stage = 'request_access_token'
    const accessTokenResponse = await withAuthCookie(async () => {
      if (intent !== authIntent) throw new SessionExpiredError()
      const access = await requestAccessToken(request)
      if (intent !== authIntent) {
        await endRemoteSession(access.accessToken, access.sessionId)
        throw new SessionExpiredError()
      }
      return access
    })

    if (intent !== authIntent) throw new SessionExpiredError()

    stage = 'set_access_token'
    logAuthDiagnostic({
      event: 'login.token_store.started',
      tokenPresent: accessTokenResponse.accessToken.length > 0,
    })
    setAccessToken(accessTokenResponse.accessToken, accessTokenResponse.sessionId)
    logAuthDiagnostic({
      event: 'login.token_store.completed',
      tokenPresent: getSessionSnapshot().accessToken !== null,
    })

    stage = 'load_current_user'
    const currentUser = await loadCurrentUserAfterToken()
    if (intent !== authIntent) throw new SessionExpiredError()
    rememberSignOut(false)
    logAuthDiagnostic({ event: 'login.completed' })
    return currentUser
  } catch (error) {
    logAuthDiagnostic({
      event: 'login.flow_aborted',
      errorType: getAuthErrorType(error),
      stage,
    })
    throw error
  }
}

export function logout(): Promise<void> {
  if (logoutInFlight !== null) {
    return logoutInFlight
  }

  authIntent += 1
  const { accessToken, sessionId } = getSessionSnapshot()
  rememberSignOut(true)
  invalidateSession()

  const operation = (async () => {
    await waitForSessionRefresh()

    await withAuthCookie(() => endRemoteSession(accessToken, sessionId))
  })().finally(() => {
    if (logoutInFlight === operation) {
      logoutInFlight = null
    }
  })

  logoutInFlight = operation
  return operation
}

export { subscribeToSessionInvalidation }
