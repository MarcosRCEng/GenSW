import { afterEach, describe, expect, it, vi } from 'vitest'
import { invalidateSession, setAccessToken } from '../../../shared/http/sessionManager'
import { createRaca, getRacaById, listRacas, setRacaAtivo, updateRaca } from './breedsService'

const raca = { id: 'breed-1', especieId: 'species-1', nome: 'Cão de trabalho', ativo: true, createdAtUtc: '2026-09-01T12:00:00Z', updatedAtUtc: '2026-09-01T12:00:00Z', especie: { id: 'species-1', nomeComum: 'Cão', ativo: true } }
function mockFetch(value: unknown) { setAccessToken('breeds-token'); const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit): Promise<Response> => { void input; void init; return Promise.resolve(new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } })) }); vi.stubGlobal('fetch', fetchMock); return fetchMock }
afterEach(() => { invalidateSession(); vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('breedsService', () => {
  it('envia operações autenticadas e contratos próprios', async () => { const fetchMock = mockFetch(raca); await createRaca({ especieId: 'species-1', nome: 'Cão' }); await getRacaById('breed-1'); await updateRaca('breed-1', { especieId: 'species-1', nome: 'Novo' }); await setRacaAtivo('breed-1', false); expect(new URL(String(fetchMock.mock.calls[0][0])).pathname).toBe('/api/v1/racas'); expect(fetchMock.mock.calls[0][1]?.method).toBe('POST'); expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get('Authorization')).toBe('Bearer breeds-token'); expect(new URL(String(fetchMock.mock.calls[1][0])).pathname).toBe('/api/v1/racas/breed-1'); expect(fetchMock.mock.calls[2][1]?.method).toBe('PUT'); expect(fetchMock.mock.calls[3][1]?.method).toBe('PATCH') })
  it('serializa todos os filtros de listagem', async () => { const fetchMock = mockFetch({ items: [], page: 2, pageSize: 50, totalItems: 0, totalPages: 0 }); await listRacas({ page: 2, pageSize: 50, search: 'Cão', especieId: 'species-1', ativo: false, sortBy: 'createdAtUtc', sortDirection: 'desc' }); expect(Object.fromEntries(new URL(String(fetchMock.mock.calls[0][0])).searchParams)).toEqual({ page: '2', pageSize: '50', search: 'Cão', especieId: 'species-1', ativo: 'false', sortBy: 'createdAtUtc', sortDirection: 'desc' }) })
})
