import { bindSessionRecovery, getRecoverySessionId } from './sessionRecovery'

export interface SessionAccessToken {
  accessToken: string
  expiresAtUtc: string
  sessionId: string
}

export interface SessionSnapshot {
  accessToken: string | null
  sessionId: string | null
  generation: number
  revision: number
  signal: AbortSignal
}

type SessionRefreshHandler = (expectedSessionId: string | null) => Promise<SessionAccessToken | null>
type SessionInvalidationListener = () => void

let accessToken: string | null = null
let sessionId: string | null = null
let sessionGeneration = 0
let sessionRevision = 0
let sessionAbort = new AbortController()
let refreshHandler: SessionRefreshHandler | null = null
let refreshInFlight: Promise<SessionAccessToken | null> | null = null

const invalidationListeners = new Set<SessionInvalidationListener>()
const generationListeners = new Set<SessionInvalidationListener>()

function notifyGenerationChange(): void {
  for (const listener of generationListeners) {
    try { listener() } catch { /* Observers cannot block a session change. */ }
  }
}

export function subscribeToSessionGenerationChange(listener: SessionInvalidationListener): () => void {
  generationListeners.add(listener)
  return () => { generationListeners.delete(listener) }
}

export function getSessionSnapshot(): SessionSnapshot {
  return {
    accessToken,
    sessionId,
    generation: sessionGeneration,
    revision: sessionRevision,
    signal: sessionAbort.signal,
  }
}

function replaceSession(token: string | null, id: string | null): void {
  const previousAbort = sessionAbort
  sessionAbort = new AbortController()
  accessToken = token
  sessionId = id
  sessionGeneration += 1
  sessionRevision += 1
  bindSessionRecovery(id)
  previousAbort.abort()
  notifyGenerationChange()
}

export function setAccessToken(token: string, id: string | null = null): void {
  replaceSession(token, id)
}

export function invalidateSession(expectedGeneration?: number): boolean {
  if (expectedGeneration !== undefined && expectedGeneration !== sessionGeneration) {
    return false
  }

  replaceSession(null, null)

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
  const expectedSessionId = sessionId ?? getRecoverySessionId()

  if (refreshHandler === null) {
    invalidateSession(refreshGeneration)
    throw new Error('O mecanismo de renovação da sessão não foi configurado.')
  }

  try {
    const refreshedToken = await refreshHandler(expectedSessionId)

    if (sessionGeneration !== refreshGeneration) {
      return null
    }

    if (refreshedToken === null || (expectedSessionId !== null && refreshedToken.sessionId !== expectedSessionId)) {
      invalidateSession(refreshGeneration)
      return null
    }

    if (accessToken === null) {
      replaceSession(refreshedToken.accessToken, refreshedToken.sessionId)
    } else {
      accessToken = refreshedToken.accessToken
      sessionRevision += 1
    }
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
