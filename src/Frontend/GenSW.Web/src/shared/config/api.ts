const DEVELOPMENT_API_BASE_URL = 'https://localhost:7443/api/v1'

function normalizeBaseUrl(value: string): string {
  return value.trim().replace(/\/+$/, '')
}

function resolveApiBaseUrl(): string {
  const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL

  if (configuredBaseUrl?.trim()) {
    return normalizeBaseUrl(configuredBaseUrl)
  }

  if (import.meta.env.DEV) {
    return DEVELOPMENT_API_BASE_URL
  }

  throw new Error('VITE_API_BASE_URL deve ser configurada fora do ambiente de desenvolvimento.')
}

export const API_BASE_URL = resolveApiBaseUrl()

export function buildApiUrl(path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`

  return `${API_BASE_URL}${normalizedPath}`
}
