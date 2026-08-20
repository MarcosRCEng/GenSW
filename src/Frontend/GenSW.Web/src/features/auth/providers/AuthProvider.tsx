import { useCallback, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react'
import {
  bootstrapSession,
  login as loginSession,
  logout as logoutSession,
  subscribeToSessionInvalidation,
} from '../services/authService'
import type { CurrentUser, LoginRequest } from '../types/auth'
import { AuthContext, type AuthContextValue } from './AuthContext'

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)
  const bootstrapInFlight = useRef<Promise<void> | null>(null)

  const bootstrap = useCallback(() => {
    if (bootstrapInFlight.current) {
      return bootstrapInFlight.current
    }

    setIsInitializing(true)

    const operation = bootstrapSession()
      .then(setUser)
      .catch(() => setUser(null))
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
    const currentUser = await loginSession(request)
    setUser(currentUser)
  }, [])

  const logout = useCallback(async () => {
    try {
      await logoutSession()
    } finally {
      setUser(null)
    }
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

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
