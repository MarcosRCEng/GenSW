import { afterEach, describe, expect, it, vi } from 'vitest'
import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import { invalidateSession, setAccessToken } from '../../../shared/http/sessionManager'
import { TipoPessoa } from '../types/people'
import {
  createPessoa,
  getPessoaById,
  listPessoas,
  setPessoaAtivo,
  updatePessoa,
} from './peopleService'

const person = {
  id: 'person-1',
  tipoPessoa: TipoPessoa.Juridica,
  nome: 'Silva & Filhos Ltda.',
  nomeFantasia: 'Silva & Filhos',
  ativo: true,
  createdAtUtc: '2026-08-20T12:00:00Z',
  updatedAtUtc: '2026-08-21T13:30:00Z',
}

function jsonResponse(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function authenticatedFetch(responseValue: unknown, status = 200) {
  setAccessToken('people-access-token')
  const fetchMock = vi.fn(
    (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
      void input
      void init
      return Promise.resolve(jsonResponse(responseValue, status))
    },
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => {
  invalidateSession()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('peopleService', () => {
  it('cria pessoa pela API autenticada e valida a resposta', async () => {
    const fetchMock = authenticatedFetch(person, 201)
    const request = {
      tipoPessoa: TipoPessoa.Juridica,
      nome: 'Silva & Filhos Ltda.',
      nomeFantasia: 'Silva & Filhos',
    }

    await expect(createPessoa(request)).resolves.toEqual(person)

    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/pessoas')
    expect(init?.method).toBe('POST')
    expect(init?.credentials).toBe('include')
    expect(new Headers(init?.headers).get('Authorization')).toBe(
      'Bearer people-access-token',
    )
    expect(new Headers(init?.headers).get('Content-Type')).toBe('application/json')
    expect(JSON.parse(String(init?.body))).toEqual(request)
  })

  it('rejeita resposta estruturalmente inválida ao criar pessoa', async () => {
    authenticatedFetch({ ...person, tipoPessoa: 3 }, 201)

    await expect(
      createPessoa({ tipoPessoa: TipoPessoa.Fisica, nome: 'Marina Silva' }),
    ).rejects.toBeInstanceOf(InvalidApiResponseError)
  })

  it('consulta pessoa por id com GET autenticado', async () => {
    const fetchMock = authenticatedFetch(person)

    await expect(getPessoaById('person-1')).resolves.toEqual(person)

    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/pessoas/person-1')
    expect(init?.method).toBe('GET')
    expect(new Headers(init?.headers).get('Authorization')).toBe(
      'Bearer people-access-token',
    )
  })

  it('atualiza somente os campos aceitos pela API', async () => {
    const fetchMock = authenticatedFetch(person)
    const request = { nome: 'Novo nome', nomeFantasia: null }

    await expect(updatePessoa('person-1', request)).resolves.toEqual(person)

    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/pessoas/person-1')
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual(request)
    expect(JSON.parse(String(init?.body))).not.toHaveProperty('tipoPessoa')
    expect(JSON.parse(String(init?.body))).not.toHaveProperty('ativo')
  })

  it.each([false, true])('altera ativo para %s com PATCH', async (ativo) => {
    const fetchMock = authenticatedFetch({ ...person, ativo })

    await expect(setPessoaAtivo('person-1', ativo)).resolves.toEqual({
      ...person,
      ativo,
    })

    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/pessoas/person-1/ativo')
    expect(init?.method).toBe('PATCH')
    expect(JSON.parse(String(init?.body))).toEqual({ ativo })
  })

  it('lista pessoas sem adicionar query vazia', async () => {
    const page = { items: [person], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 }
    const fetchMock = authenticatedFetch(page)

    await expect(listPessoas()).resolves.toEqual(page)

    const [input, init] = fetchMock.mock.calls[0]
    expect(String(input)).not.toContain('?')
    expect(new URL(String(input)).pathname).toBe('/api/v1/pessoas')
    expect(init?.method).toBe('GET')
  })

  it('serializa todos os filtros e preserva ativo=false', async () => {
    const page = { items: [], page: 2, pageSize: 25, totalItems: 0, totalPages: 0 }
    const fetchMock = authenticatedFetch(page)

    await listPessoas({
      page: 2,
      pageSize: 25,
      search: 'Silva & Filhos',
      tipoPessoa: TipoPessoa.Juridica,
      ativo: false,
      sortBy: 'nome',
      sortDirection: 'desc',
    })

    const url = new URL(String(fetchMock.mock.calls[0][0]))
    expect(url.pathname).toBe('/api/v1/pessoas')
    expect(Object.fromEntries(url.searchParams)).toEqual({
      page: '2',
      pageSize: '25',
      search: 'Silva & Filhos',
      tipoPessoa: 'Juridica',
      ativo: 'false',
      sortBy: 'nome',
      sortDirection: 'desc',
    })
  })

  it('serializa pessoa física pelo nome aceito na query da API', async () => {
    const page = { items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 }
    const fetchMock = authenticatedFetch(page)

    await listPessoas({ tipoPessoa: TipoPessoa.Fisica })

    const url = new URL(String(fetchMock.mock.calls[0][0]))
    expect(url.searchParams.get('tipoPessoa')).toBe('Fisica')
  })
})
