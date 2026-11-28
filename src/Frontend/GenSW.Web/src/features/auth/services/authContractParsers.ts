import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import type { AccessTokenResponse, CurrentUser } from '../types/auth'

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

export function parseAccessTokenResponse(value: unknown): AccessTokenResponse {
  if (
    !isRecord(value) ||
    !isNonEmptyString(value.accessToken) ||
    !isNonEmptyString(value.sessionId) ||
    !isNonEmptyString(value.expiresAtUtc) ||
    Number.isNaN(Date.parse(value.expiresAtUtc))
  ) {
    throw new InvalidApiResponseError('A API retornou um token de acesso inválido.')
  }

  return {
    accessToken: value.accessToken,
    expiresAtUtc: value.expiresAtUtc,
    sessionId: value.sessionId,
  }
}

export function parseCurrentUser(value: unknown): CurrentUser {
  if (
    !isRecord(value) ||
    !isNonEmptyString(value.subjectId) ||
    value.userId !== value.subjectId ||
    !isNonEmptyString(value.erpActorId) ||
    !isNonEmptyString(value.pessoaId) ||
    typeof value.nome !== 'string' ||
    !isNonEmptyString(value.userName) ||
    !Array.isArray(value.roles) ||
    !value.roles.every((role) => typeof role === 'string')
  ) {
    throw new InvalidApiResponseError('A API retornou dados inválidos para o usuário atual.')
  }

  return {
    userId: value.userId,
    subjectId: value.subjectId,
    erpActorId: value.erpActorId,
    pessoaId: value.pessoaId,
    nome: value.nome,
    userName: value.userName,
    roles: [...value.roles],
  }
}
