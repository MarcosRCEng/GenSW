import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { listReproductiveCycles } from './reproductiveCyclesService'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))

beforeEach(() => { vi.mocked(httpRequest).mockResolvedValue({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 }) })

describe('listReproductiveCycles', () => {
  it.each([undefined, { page: undefined, pageSize: undefined }])('sends explicit pagination defaults (%s)', async params => {
    await listReproductiveCycles(params)
    expect(httpRequest).toHaveBeenCalledWith('/ciclos-reprodutivos?page=1&pageSize=25', { authenticated: true })
  })

  it('sends only the requested page and keeps the parent, type and status filters', async () => {
    await listReproductiveCycles({ page: 2, pageSize: 25, cruzamentoId: 'crossing-a', tipo: 1, status: 2 })
    expect(httpRequest).toHaveBeenCalledOnce()
    expect(httpRequest).toHaveBeenCalledWith('/ciclos-reprodutivos?page=2&pageSize=25&cruzamentoId=crossing-a&tipo=1&status=2', { authenticated: true })
  })
})
