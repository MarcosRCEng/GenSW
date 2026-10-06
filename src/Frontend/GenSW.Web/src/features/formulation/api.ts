import { httpRequest } from '../../shared/http/httpClient'
import {
  isHttpError,
  InvalidApiResponseError,
} from '../../shared/http/httpErrors'
import type { Page } from './types'

export async function read<T>(path: string): Promise<T> {
  return httpRequest<T>(path, { authenticated: true })
}
export async function send<T>(
  path: string,
  body: unknown,
  method: 'POST' | 'PUT' | 'PATCH' = 'POST',
  key?: string,
): Promise<T> {
  return httpRequest<T>(path, {
    authenticated: true,
    method,
    body,
    headers: key ? { 'Idempotency-Key': key } : undefined,
  })
}
export async function page<T>(
  path: string,
  params: Record<string, string | number | boolean | undefined> = {},
): Promise<Page<T>> {
  const query = new URLSearchParams()
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== '') query.set(k, String(v))
  })
  const result = await read<Page<T>>(`${path}?${query}`)
  if (
    !result ||
    !Array.isArray(result.items) ||
    !Number.isInteger(result.page) ||
    !Number.isInteger(result.totalPages)
  )
    throw new InvalidApiResponseError()
  return result
}
export function message(error: unknown) {
  if (isHttpError(error))
    return (
      error.detail ??
      (error.status === 404
        ? 'Registro não encontrado.'
        : error.status === 403
          ? 'Acesso não permitido.'
          : error.status === 409
            ? 'Registro alterado; recarregue antes de tentar novamente.'
            : 'Não foi possível executar a operação.')
    )
  return error instanceof Error
    ? error.message
    : 'Não foi possível conectar à API.'
}
// Decimal strings remain strings in forms, requests and results. The browser never calculates nutrition.
export const decimal = (value: string) => value.trim().replace(',', '.')
export function display(value: string | null | undefined): string {
  return value == null ? 'Indeterminado' : value.replace('.', ',')
}
