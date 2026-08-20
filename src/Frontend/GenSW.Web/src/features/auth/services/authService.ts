import { HttpError, SessionExpiredError } from '../../../shared/http/httpErrors'
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
  const response = await httpRequest<unknown>(AUTH_ENDPOINTS.login, {
    method: 'POST',
    body: request,
    authenticated: false,
    retryOnUnauthorized: false,
  })

  return parseAccessTokenResponse(response)
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
  const response = await httpRequest<unknown>(AUTH_ENDPOINTS.me, {
    authenticated: true,
    retryOnUnauthorized: false,
  })

  return parseCurrentUser(response)
}

async function loadCurrentUserAfterToken(): Promise<CurrentUser> {
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
  await waitForPendingLogout()
  await waitForSessionRefresh()

  const accessTokenResponse = await requestAccessToken(request)
  setAccessToken(accessTokenResponse.accessToken)

  return loadCurrentUserAfterToken()
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
