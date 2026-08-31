import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import { TipoPessoa, type Pessoa, type PessoasPage } from '../types/people'

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

function isIsoDateString(value: unknown): value is string {
  return isNonEmptyString(value) && !Number.isNaN(Date.parse(value))
}

function isTipoPessoa(value: unknown): value is TipoPessoa {
  return value === TipoPessoa.Fisica || value === TipoPessoa.Juridica
}

function isIntegerAtLeast(value: unknown, minimum: number): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= minimum
}

export function parsePessoa(value: unknown): Pessoa {
  if (
    !isRecord(value) ||
    !isNonEmptyString(value.id) ||
    !isTipoPessoa(value.tipoPessoa) ||
    typeof value.nome !== 'string' ||
    !(typeof value.nomeFantasia === 'string' || value.nomeFantasia === null) ||
    typeof value.ativo !== 'boolean' ||
    !isIsoDateString(value.createdAtUtc) ||
    !isIsoDateString(value.updatedAtUtc)
  ) {
    throw new InvalidApiResponseError('A API retornou dados inválidos para a pessoa.')
  }

  return {
    id: value.id,
    tipoPessoa: value.tipoPessoa,
    nome: value.nome,
    nomeFantasia: value.nomeFantasia,
    ativo: value.ativo,
    createdAtUtc: value.createdAtUtc,
    updatedAtUtc: value.updatedAtUtc,
  }
}

export function parsePessoasPage(value: unknown): PessoasPage {
  if (
    !isRecord(value) ||
    !Array.isArray(value.items) ||
    !isIntegerAtLeast(value.page, 1) ||
    !isIntegerAtLeast(value.pageSize, 1) ||
    !isIntegerAtLeast(value.totalItems, 0) ||
    !isIntegerAtLeast(value.totalPages, 0)
  ) {
    throw new InvalidApiResponseError('A API retornou uma página de pessoas inválida.')
  }

  return {
    items: value.items.map(parsePessoa),
    page: value.page,
    pageSize: value.pageSize,
    totalItems: value.totalItems,
    totalPages: value.totalPages,
  }
}
