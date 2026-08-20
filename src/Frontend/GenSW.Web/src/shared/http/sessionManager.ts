export interface SessionAccessToken {
  accessToken: string
  expiresAtUtc: string
}

export interface SessionSnapshot {
  accessToken: string | null
  generation: number
  revision: number
}

type SessionRefreshHandler = () => Promise<SessionAccessToken | null>
type SessionInvalidationListener = () => void

let accessToken: string | null = null
let sessionGeneration = 0
let sessionRevision = 0
let refreshHandler: SessionRefreshHandler | null = null
let refreshInFlight: Promise<SessionAccessToken | null> | null = null

const invalidationListeners = new Set<SessionInvalidationListener>()

export function getSessionSnapshot(): SessionSnapshot {
  return {
    accessToken,
    generation: sessionGeneration,
    revision: sessionRevision,
  }
}

export function setAccessToken(token: string): void {
  accessToken = token
  sessionGeneration += 1
  sessionRevision += 1
}

export function invalidateSession(expectedGeneration?: number): boolean {
  if (expectedGeneration !== undefined && expectedGeneration !== sessionGeneration) {
    return false
  }

  accessToken = null
  sessionGeneration += 1
  sessionRevision += 1

  for (const listener of invalidationListeners) {
    try {
      listener()
    } catch {
      // A falha de um observador não pode impedir a invalidação dos demais.
    }
  }

  return true
}

export function subscribeToSessionInvalidation(listener: SessionInvalidationListener): () => void {
  invalidationListeners.add(listener)

  return () => {
    invalidationListeners.delete(listener)
  }
}

export function registerSessionRefreshHandler(handler: SessionRefreshHandler): void {
  refreshHandler = handler
}

async function performRefresh(): Promise<SessionAccessToken | null> {
  const refreshGeneration = sessionGeneration

  if (refreshHandler === null) {
    invalidateSession(refreshGeneration)
    throw new Error('O mecanismo de renovação da sessão não foi configurado.')
  }

  try {
    const refreshedToken = await refreshHandler()

    if (sessionGeneration !== refreshGeneration) {
      return null
    }

    if (refreshedToken === null) {
      invalidateSession(refreshGeneration)
      return null
    }

    accessToken = refreshedToken.accessToken
    sessionRevision += 1
    return refreshedToken
  } catch (error) {
    invalidateSession(refreshGeneration)
    throw error
  }
}

export function refreshSessionToken(): Promise<SessionAccessToken | null> {
  if (refreshInFlight !== null) {
    return refreshInFlight
  }

  const currentRefresh = performRefresh().finally(() => {
    if (refreshInFlight === currentRefresh) {
      refreshInFlight = null
    }
  })

  refreshInFlight = currentRefresh
  return currentRefresh
}

export async function waitForSessionRefresh(): Promise<void> {
  const currentRefresh = refreshInFlight

  if (currentRefresh === null) {
    return
  }

  try {
    await currentRefresh
  } catch {
    // Login e logout devem prosseguir mesmo quando o refresh pendente falha.
  }
}
