import { afterEach, describe, expect, it, vi } from 'vitest'
import { invalidateSession, setAccessToken } from '../../../shared/http/sessionManager'
import {
  createAnimal,
  getAnimalById,
  listAnimals,
  setAnimalAtivo,
  updateAnimal,
} from './animalsService'
import type { CreateAnimalRequest } from '../types/animals'

const animal = {
  id: 'animal-1', codigoInterno: 'AN-000001', nome: 'Bela', especieId: 'species-1', racaId: 'breed-1', variedadeId: 'variety-1', sexo: 2, dataNascimento: '2020-02-29', escopo: 1, ativo: true,
  createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z',
  especie: { id: 'species-1', nomeComum: 'Canina', ativo: true }, raca: { id: 'breed-1', nome: 'Pastor', ativo: true }, variedade: { id: 'variety-1', nome: 'Curto', ativo: true },
}

function authenticatedFetch(responseValue: unknown, status = 200) {
  setAccessToken('animals-access-token')
  const fetchMock = vi.fn(
    (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
      void input
      void init
      return Promise.resolve(new Response(JSON.stringify(responseValue), { status, headers: { 'Content-Type': 'application/json' } }))
    },
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => { invalidateSession(); vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('animalsService', () => {
  const createCases: Array<[string, CreateAnimalRequest, boolean, string | null | undefined]> = [
    ['omits undefined codigoInterno', { especieId: 'species-1', sexo: 2, escopo: 1 }, false, undefined],
    ['preserves null codigoInterno', { codigoInterno: null, especieId: 'species-1', sexo: 2, escopo: 1 }, true, null],
    ['preserves empty codigoInterno', { codigoInterno: '', especieId: 'species-1', sexo: 2, escopo: 1 }, true, ''],
    ['preserves whitespace codigoInterno', { codigoInterno: '   ', especieId: 'species-1', sexo: 2, escopo: 1 }, true, '   '],
  ]

  it.each(createCases)('%s during create serialization', async (_, request, hasCode, code) => {
    const fetchMock = authenticatedFetch(animal, 201)
    await expect(createAnimal(request)).resolves.toEqual(animal)
    const [input, init] = fetchMock.mock.calls[0]
    expect(new URL(String(input)).pathname).toBe('/api/v1/animais')
    expect(init?.method).toBe('POST')
    expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer animals-access-token')
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>
    expect(Object.prototype.hasOwnProperty.call(body, 'codigoInterno')).toBe(hasCode)
    if (hasCode) expect(body.codigoInterno).toBe(code)
  })

  it('gets and updates an animal through authenticated endpoints', async () => {
    const getFetch = authenticatedFetch(animal)
    await expect(getAnimalById('animal-1')).resolves.toEqual(animal)
    expect(new URL(String(getFetch.mock.calls[0][0])).pathname).toBe('/api/v1/animais/animal-1')
    expect(getFetch.mock.calls[0][1]?.method).toBe('GET')

    const updateFetch = authenticatedFetch(animal)
    const request = { codigoInterno: 'MANUAL 01', nome: null, especieId: 'species-1', racaId: null, variedadeId: null, sexo: 1 as const, dataNascimento: null, escopo: 2 as const }
    await expect(updateAnimal('animal-1', request)).resolves.toEqual(animal)
    expect(new URL(String(updateFetch.mock.calls[0][0])).pathname).toBe('/api/v1/animais/animal-1')
    expect(updateFetch.mock.calls[0][1]?.method).toBe('PUT')
    expect(JSON.parse(String(updateFetch.mock.calls[0][1]?.body))).toEqual(request)
  })

  it('serializes every supplied list filter and status mutation', async () => {
    const page = { items: [animal], page: 2, pageSize: 50, totalItems: 51, totalPages: 2 }
    const listFetch = authenticatedFetch(page)
    await expect(listAnimals({ page: 2, pageSize: 50, search: 'Bela', especieId: 'species-1', racaId: 'breed-1', variedadeId: 'variety-1', sexo: 3, escopo: 2, ativo: false, sortBy: 'createdAtUtc', sortDirection: 'desc' })).resolves.toEqual(page)
    const url = new URL(String(listFetch.mock.calls[0][0]))
    expect(url.pathname).toBe('/api/v1/animais')
    expect(Object.fromEntries(url.searchParams)).toEqual({ page: '2', pageSize: '50', search: 'Bela', especieId: 'species-1', racaId: 'breed-1', variedadeId: 'variety-1', sexo: '3', escopo: '2', ativo: 'false', sortBy: 'createdAtUtc', sortDirection: 'desc' })

    const statusFetch = authenticatedFetch({ ...animal, ativo: false })
    await expect(setAnimalAtivo('animal-1', false)).resolves.toEqual({ ...animal, ativo: false })
    expect(new URL(String(statusFetch.mock.calls[0][0])).pathname).toBe('/api/v1/animais/animal-1/ativo')
    expect(statusFetch.mock.calls[0][1]?.method).toBe('PATCH')
    expect(JSON.parse(String(statusFetch.mock.calls[0][1]?.body))).toEqual({ ativo: false })
  })
})
