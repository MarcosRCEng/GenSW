import { httpRequest } from '../../../shared/http/httpClient'
import {
  TipoPessoa,
  type CreatePessoaRequest,
  type ListPessoasParams,
  type Pessoa,
  type PessoasPage,
  type UpdatePessoaRequest,
  type UpdatePessoaStatusRequest,
} from '../types/people'
import { parsePessoa, parsePessoasPage } from './peopleContractParsers'

const PEOPLE_ENDPOINT = '/pessoas'

function tipoPessoaToQueryValue(tipoPessoa: TipoPessoa): 'Fisica' | 'Juridica' {
  switch (tipoPessoa) {
    case TipoPessoa.Fisica:
      return 'Fisica'
    case TipoPessoa.Juridica:
      return 'Juridica'
  }
}

function buildListPath(params?: ListPessoasParams): string {
  const query = new URLSearchParams()

  if (params?.page !== undefined) query.set('page', String(params.page))
  if (params?.pageSize !== undefined) query.set('pageSize', String(params.pageSize))
  if (params?.search !== undefined) query.set('search', params.search)
  if (params?.tipoPessoa !== undefined) {
    query.set('tipoPessoa', tipoPessoaToQueryValue(params.tipoPessoa))
  }
  if (params?.ativo !== undefined) query.set('ativo', String(params.ativo))
  if (params?.sortBy !== undefined) query.set('sortBy', params.sortBy)
  if (params?.sortDirection !== undefined) query.set('sortDirection', params.sortDirection)

  const queryString = query.toString()
  return queryString ? `${PEOPLE_ENDPOINT}?${queryString}` : PEOPLE_ENDPOINT
}

export async function createPessoa(request: CreatePessoaRequest): Promise<Pessoa> {
  const response = await httpRequest<unknown>(PEOPLE_ENDPOINT, {
    method: 'POST',
    authenticated: true,
    body: request,
  })

  return parsePessoa(response)
}

export async function getPessoaById(id: string): Promise<Pessoa> {
  const response = await httpRequest<unknown>(`${PEOPLE_ENDPOINT}/${id}`, {
    authenticated: true,
  })

  return parsePessoa(response)
}

export async function listPessoas(params?: ListPessoasParams): Promise<PessoasPage> {
  const response = await httpRequest<unknown>(buildListPath(params), {
    authenticated: true,
  })

  return parsePessoasPage(response)
}

export async function updatePessoa(
  id: string,
  request: UpdatePessoaRequest,
): Promise<Pessoa> {
  const response = await httpRequest<unknown>(`${PEOPLE_ENDPOINT}/${id}`, {
    method: 'PUT',
    authenticated: true,
    body: request,
  })

  return parsePessoa(response)
}

export async function setPessoaAtivo(id: string, ativo: boolean): Promise<Pessoa> {
  const request: UpdatePessoaStatusRequest = { ativo }
  const response = await httpRequest<unknown>(`${PEOPLE_ENDPOINT}/${id}/ativo`, {
    method: 'PATCH',
    authenticated: true,
    body: request,
  })

  return parsePessoa(response)
}
