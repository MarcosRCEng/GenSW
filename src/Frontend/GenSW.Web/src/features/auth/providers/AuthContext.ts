import { createContext } from 'react'
import type { AuthState, LoginRequest } from '../types/auth'

export interface AuthContextValue extends AuthState {
  bootstrap: () => Promise<void>
  login: (request: LoginRequest) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
