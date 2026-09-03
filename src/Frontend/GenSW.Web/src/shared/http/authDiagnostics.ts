const AUTH_DIAGNOSTIC_PREFIX = '[GenSW auth]'

const SAFE_ERROR_TYPES = new Set([
  'AbortError',
  'HttpError',
  'InvalidApiResponseError',
  'NetworkError',
  'SessionExpiredError',
  'TypeError',
])

export interface AuthDiagnosticDetails {
  errorType?: string
  event: string
  stage?: string
  status?: number
  tokenPresent?: boolean
}

export function getAuthErrorType(error: unknown): string {
  if (!(error instanceof Error)) {
    return typeof error
  }

  return SAFE_ERROR_TYPES.has(error.name) ? error.name : 'Error'
}

export function logAuthDiagnostic(details: AuthDiagnosticDetails): void {
  const safeDetails: AuthDiagnosticDetails = { event: details.event }

  if (details.errorType !== undefined) {
    safeDetails.errorType = details.errorType
  }

  if (details.stage !== undefined) {
    safeDetails.stage = details.stage
  }

  if (details.status !== undefined) {
    safeDetails.status = details.status
  }

  if (details.tokenPresent !== undefined) {
    safeDetails.tokenPresent = details.tokenPresent
  }

  try {
    console.info(`${AUTH_DIAGNOSTIC_PREFIX} ${JSON.stringify(safeDetails)}`)
  } catch {
    // Diagnósticos são best-effort e nunca podem interromper a autenticação.
  }
}
