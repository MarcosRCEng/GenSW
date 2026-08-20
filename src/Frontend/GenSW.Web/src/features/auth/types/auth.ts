export interface LoginRequest {
  userName: string
  password: string
}

export interface AccessTokenResponse {
  accessToken: string
  expiresAtUtc: string
}

export interface CurrentUser {
  userId: string
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
