import { Fragment, useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore, type PropsWithChildren } from 'react'
import { getSessionSnapshot, subscribeToSessionGenerationChange } from '../../../shared/http/sessionManager'
import {
  bootstrapSession,
  login as loginSession,
  logout as logoutSession,
  subscribeToSessionInvalidation,
} from '../services/authService'
import type { CurrentUser, LoginRequest } from '../types/auth'
import { AuthContext, type AuthContextValue } from './AuthContext'

const getGeneration = () => getSessionSnapshot().generation

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)
  const bootstrapInFlight = useRef<Promise<void> | null>(null)
  const latestIntent = useRef(0)
  const generation = useSyncExternalStore(subscribeToSessionGenerationChange, getGeneration)

  const bootstrap = useCallback(() => {
    if (bootstrapInFlight.current) {
      return bootstrapInFlight.current
    }

    setIsInitializing(true)
    const intent = latestIntent.current

    const operation = bootstrapSession()
      .then(currentUser => { if (intent === latestIntent.current) setUser(currentUser) })
      .catch(() => { if (intent === latestIntent.current) setUser(null) })
      .finally(() => {
        setIsInitializing(false)
        bootstrapInFlight.current = null
      })

    bootstrapInFlight.current = operation
    return operation
  }, [])

  useEffect(() => {
    const unsubscribe = subscribeToSessionInvalidation(() => {
      setUser(null)
    })

    void bootstrap()

    return unsubscribe
  }, [bootstrap])

  const login = useCallback(async (request: LoginRequest) => {
    const intent = ++latestIntent.current
    setUser(null)
    const currentUser = await loginSession(request)
    if (intent === latestIntent.current) setUser(currentUser)
  }, [])

  const logout = useCallback(async () => {
    latestIntent.current += 1
    setUser(null)
    await logoutSession()
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isInitializing,
      bootstrap,
      login,
      logout,
    }),
    [bootstrap, isInitializing, login, logout, user],
  )

  return <AuthContext.Provider value={value}>
    <Fragment key={user === null ? 'anonymous' : generation}>{children}</Fragment>
  </AuthContext.Provider>
}
