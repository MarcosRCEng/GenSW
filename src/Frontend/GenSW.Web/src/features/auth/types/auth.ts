export interface LoginRequest {
  userName: string
  password: string
}

export interface AccessTokenResponse {
  accessToken: string
  expiresAtUtc: string
  sessionId: string
}

export interface CurrentUser {
  userId: string
  subjectId: string
  erpActorId: string
  pessoaId: string
  nome: string
  userName: string
  roles: string[]
}

export interface AuthState {
  user: CurrentUser | null
  isAuthenticated: boolean
  isInitializing: boolean
}
