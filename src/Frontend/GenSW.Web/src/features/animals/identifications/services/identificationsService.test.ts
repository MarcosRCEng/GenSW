import { afterEach, describe, expect, it, vi } from 'vitest'
import { invalidateSession, setAccessToken } from '../../../../shared/http/sessionManager'
import {
  createAnimalIdentification,
  getAnimalIdentification,
  listAnimalIdentifications,
  listGlobalAnimalIdentifications,
  setAnimalIdentificationAtivo,
  setAnimalIdentificationPrincipal,
  updateAnimalIdentificationMetadata,
} from './identificationsService'

const identification = { id: 'identification-1', animalId: 'animal-1', tipo: 3, descricaoTipo: null, valor: '985141000000001', principal: true, dataAplicacao: null, observacao: null, ativo: true, createdAtUtc: '2026-09-12T12:00:00Z', updatedAtUtc: '2026-09-12T12:00:00Z' }
function authenticatedFetch(responseValue: unknown, status = 200) {
  setAccessToken('identifications-access-token')
  const mock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>(() => Promise.resolve(new Response(JSON.stringify(responseValue), { status, headers: { 'Content-Type': 'application/json' } })))
  vi.stubGlobal('fetch', mock)
  return mock
}
function request(mock: ReturnType<typeof authenticatedFetch>) {
  const [input, init] = mock.mock.calls[0]
  expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer identifications-access-token')
  return { url: new URL(String(input)), init }
}
afterEach(() => { invalidateSession(); vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('identificationsService', () => {
  it('uses authenticated nested GET routes and only serializes supplied query values', async () => {
    const list = authenticatedFetch({ items: [identification], page: 2, pageSize: 50, totalItems: 51, totalPages: 2 })
    await expect(listAnimalIdentifications('animal-1', { page: 2, pageSize: 50, tipo: 3, valor: 'M 1', ativo: false, principal: true })).resolves.toEqual({ items: [identification], page: 2, pageSize: 50, totalItems: 51, totalPages: 2 })
    const listRequest = request(list)
    expect(listRequest.url.pathname).toBe('/api/v1/animais/animal-1/identificacoes')
    expect(listRequest.init?.method).toBe('GET')
    expect(Object.fromEntries(listRequest.url.searchParams)).toEqual({ page: '2', pageSize: '50', tipo: '3', valor: 'M 1', ativo: 'false', principal: 'true' })

    const get = authenticatedFetch(identification)
    await expect(getAnimalIdentification('animal-1', 'identification-1')).resolves.toEqual(identification)
    expect(request(get).url.pathname).toBe('/api/v1/animais/animal-1/identificacoes/identification-1')
    expect(request(get).init?.method).toBe('GET')
  })

  it('uses authenticated nested mutation routes', async () => {
    const create = authenticatedFetch(identification, 201)
    const createBody = { tipo: 3 as const, descricaoTipo: null, valor: '  keep  ', principal: true, dataAplicacao: null, observacao: null }
    await expect(createAnimalIdentification('animal-1', createBody)).resolves.toEqual(identification)
    expect(request(create).url.pathname).toBe('/api/v1/animais/animal-1/identificacoes')
    expect(request(create).init?.method).toBe('POST')
    expect(JSON.parse(String(request(create).init?.body))).toEqual(createBody)

    const metadata = authenticatedFetch(identification)
    const metadataBody = { dataAplicacao: null, observacao: undefined }
    await expect(updateAnimalIdentificationMetadata('animal-1', 'identification-1', metadataBody)).resolves.toEqual(identification)
    expect(request(metadata).url.pathname).toBe('/api/v1/animais/animal-1/identificacoes/identification-1')
    expect(request(metadata).init?.method).toBe('PATCH')
    const metadataRequestBody = JSON.parse(String(request(metadata).init?.body))
    expect(metadataRequestBody).toEqual({ dataAplicacao: null })
    expect(metadataRequestBody).not.toHaveProperty('tipo')
    expect(metadataRequestBody).not.toHaveProperty('descricaoTipo')
    expect(metadataRequestBody).not.toHaveProperty('valor')

    const ativo = authenticatedFetch({ ...identification, ativo: false })
    await expect(setAnimalIdentificationAtivo('animal-1', 'identification-1', false)).resolves.toEqual({ ...identification, ativo: false })
    expect(request(ativo).url.pathname).toBe('/api/v1/animais/animal-1/identificacoes/identification-1/ativo')
    expect(JSON.parse(String(request(ativo).init?.body))).toEqual({ ativo: false })

    const principal = authenticatedFetch({ ...identification, principal: false })
    await expect(setAnimalIdentificationPrincipal('animal-1', 'identification-1', false)).resolves.toEqual({ ...identification, principal: false })
    expect(request(principal).url.pathname).toBe('/api/v1/animais/animal-1/identificacoes/identification-1/principal')
    expect(JSON.parse(String(request(principal).init?.body))).toEqual({ principal: false })
  })

  it('uses only the authenticated global GET route', async () => {
    const global = authenticatedFetch({ items: [{ identificacao: identification, animal: { id: 'animal-1', codigoInterno: 'AN-000001', nome: null } }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 })
    await expect(listGlobalAnimalIdentifications({ valor: '985', principal: false })).resolves.toEqual({ items: [{ identificacao: identification, animal: { id: 'animal-1', codigoInterno: 'AN-000001', nome: null } }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 })
    const globalRequest = request(global)
    expect(globalRequest.url.pathname).toBe('/api/v1/identificacoes-animal')
    expect(globalRequest.init?.method).toBe('GET')
    expect(Object.fromEntries(globalRequest.url.searchParams)).toEqual({ valor: '985', principal: 'false' })
  })
})
