import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { listOffspring } from './offspringService'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
beforeEach(() => vi.mocked(httpRequest).mockReset())

describe('offspring pagination request', () => {
  it('sends the first page and size explicitly on direct access', async () => {
    await listOffspring()
    expect(httpRequest).toHaveBeenCalledWith('/proles?page=1&pageSize=25', { authenticated: true })
  })

  it('authenticates the requested page while preserving parent and existing filters', async () => {
    await listOffspring({ page: 2, pageSize: 25, cicloReprodutivoId: 'cycle & test', tipoRegistro: 2, origem: 1, convertida: false })
    expect(httpRequest).toHaveBeenCalledWith('/proles?page=2&pageSize=25&cicloReprodutivoId=cycle+%26+test&tipoRegistro=2&origem=1&convertida=false', { authenticated: true })
  })
})
