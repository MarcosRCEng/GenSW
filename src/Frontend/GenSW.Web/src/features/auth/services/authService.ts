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

let logoutInFlight: Promise<void> | null = null

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

async function requestRefreshedAccessToken(): Promise<AccessTokenResponse | null> {
  try {
    const response = await httpRequest<unknown>(AUTH_ENDPOINTS.refresh, {
      method: 'POST',
      authenticated: false,
      retryOnUnauthorized: false,
    })

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
  const refreshedToken = await refreshSession()

  if (refreshedToken === null) {
    return null
  }

  return loadCurrentUserAfterToken()
}

export async function login(request: LoginRequest): Promise<CurrentUser> {
  logAuthDiagnostic({ event: 'login.attempt.started' })
  let stage = 'wait_for_pending_logout'

  try {
    await waitForPendingLogout()
    stage = 'wait_for_session_refresh'
    await waitForSessionRefresh()

    stage = 'request_access_token'
    const accessTokenResponse = await requestAccessToken(request)

    stage = 'set_access_token'
    logAuthDiagnostic({
      event: 'login.token_store.started',
      tokenPresent: accessTokenResponse.accessToken.length > 0,
    })
    setAccessToken(accessTokenResponse.accessToken)
    logAuthDiagnostic({
      event: 'login.token_store.completed',
      tokenPresent: getSessionSnapshot().accessToken !== null,
    })

    stage = 'load_current_user'
    const currentUser = await loadCurrentUserAfterToken()
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

  invalidateSession()

  const operation = (async () => {
    await waitForSessionRefresh()

    await httpRequest<void>(AUTH_ENDPOINTS.logout, {
      method: 'POST',
      authenticated: false,
      retryOnUnauthorized: false,
    })
  })().finally(() => {
    if (logoutInFlight === operation) {
      logoutInFlight = null
    }
  })

  logoutInFlight = operation
  return operation
}

export { subscribeToSessionInvalidation }
