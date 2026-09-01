import { afterEach, describe, expect, it, vi } from 'vitest'
import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import { invalidateSession, setAccessToken } from '../../../shared/http/sessionManager'
import {
  createEspecie,
  getEspecieById,
  listEspecies,
  setEspecieAtivo,
  updateEspecie,
} from './speciesService'

const especie = {
  id: 'species-1',
  nomeComum: 'Cão doméstico',
  nomeCientifico: 'Canis familiaris',
  ativo: true,
  createdAtUtc: '2026-08-31T12:00:00Z',
  updatedAtUtc: '2026-08-31T12:00:00Z',
}

function jsonResponse(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function authenticatedFetch(responseValue: unknown, status = 200) {
  setAccessToken('species-access-token')
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

describe('speciesService', () => {
  it('cria espécie pela API autenticada e valida a resposta', async () => {
    const fetchMock = authenticatedFetch(especie, 201)
    const request = { nomeComum: 'Cão doméstico', nomeCientifico: 'Canis familiaris' }

    await expect(createEspecie(request)).resolves.toEqual(especie)

    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/especies')
    expect(init?.method).toBe('POST')
    expect(new Headers(init?.headers).get('Authorization')).toBe(
      'Bearer species-access-token',
    )
    expect(JSON.parse(String(init?.body))).toEqual(request)
  })

  it('rejeita resposta estruturalmente inválida ao criar espécie', async () => {
    authenticatedFetch({ ...especie, nomeComum: 123 }, 201)

    await expect(createEspecie({ nomeComum: 'Cão doméstico' })).rejects.toBeInstanceOf(
      InvalidApiResponseError,
    )
  })

  it('consulta espécie por id com GET autenticado', async () => {
    const fetchMock = authenticatedFetch(especie)

    await expect(getEspecieById('species-1')).resolves.toEqual(especie)

    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/especies/species-1')
    expect(init?.method).toBe('GET')
  })

  it('atualiza somente os campos aceitos pela API', async () => {
    const fetchMock = authenticatedFetch(especie)
    const request = { nomeComum: 'Novo nome', nomeCientifico: null }

    await expect(updateEspecie('species-1', request)).resolves.toEqual(especie)

    const [, init] = fetchMock.mock.calls[0]
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual(request)
    expect(JSON.parse(String(init?.body))).not.toHaveProperty('ativo')
  })

  it.each([false, true])('altera ativo para %s com PATCH', async (ativo) => {
    const fetchMock = authenticatedFetch({ ...especie, ativo })

    await expect(setEspecieAtivo('species-1', ativo)).resolves.toEqual({
      ...especie,
      ativo,
    })

    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/especies/species-1/ativo')
    expect(init?.method).toBe('PATCH')
    expect(JSON.parse(String(init?.body))).toEqual({ ativo })
  })

  it('lista espécies sem adicionar query vazia', async () => {
    const page = { items: [especie], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 }
    const fetchMock = authenticatedFetch(page)

    await expect(listEspecies()).resolves.toEqual(page)

    const [input, init] = fetchMock.mock.calls[0]
    expect(String(input)).not.toContain('?')
    expect(new URL(String(input)).pathname).toBe('/api/v1/especies')
    expect(init?.method).toBe('GET')
  })

  it('serializa todos os filtros e preserva ativo=false', async () => {
    const page = { items: [], page: 2, pageSize: 50, totalItems: 0, totalPages: 0 }
    const fetchMock = authenticatedFetch(page)

    await listEspecies({
      page: 2,
      pageSize: 50,
      search: 'Canis',
      ativo: false,
      sortBy: 'nomeCientifico',
      sortDirection: 'desc',
    })

    const requestedUrl = new URL(String(fetchMock.mock.calls[0][0]))
    expect(requestedUrl.pathname).toBe('/api/v1/especies')
    expect(requestedUrl.searchParams.get('ativo')).toBe('false')
    expect(requestedUrl.searchParams.get('sortBy')).toBe('nomeCientifico')
    expect(Object.fromEntries(requestedUrl.searchParams)).toEqual({
      page: '2',
      pageSize: '50',
      search: 'Canis',
      ativo: 'false',
      sortBy: 'nomeCientifico',
      sortDirection: 'desc',
    })
  })
})
