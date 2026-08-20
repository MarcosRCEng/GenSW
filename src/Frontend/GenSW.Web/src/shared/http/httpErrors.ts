export class HttpError extends Error {
  readonly status: number

  constructor(status: number, statusText?: string) {
    const suffix = statusText?.trim() ? `: ${statusText.trim()}` : ''
    super(`A API respondeu com status ${status}${suffix}.`)
    this.name = 'HttpError'
    this.status = status
  }
}

export class NetworkError extends Error {
  readonly cause: unknown

  constructor(cause?: unknown) {
    super('Não foi possível conectar à API.')
    this.name = 'NetworkError'
    this.cause = cause
  }
}

export class SessionExpiredError extends Error {
  constructor(message = 'A sessão não é mais válida.') {
    super(message)
    this.name = 'SessionExpiredError'
  }
}

export class InvalidApiResponseError extends Error {
  constructor(message = 'A API retornou uma resposta inválida.') {
    super(message)
    this.name = 'InvalidApiResponseError'
  }
}

export function isHttpError(error: unknown): error is HttpError {
  return error instanceof HttpError
}
