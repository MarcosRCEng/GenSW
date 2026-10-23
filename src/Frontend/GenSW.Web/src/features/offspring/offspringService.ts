import { httpRequest } from '../../shared/http/httpClient'
import type { ListOffspringParams, Offspring, OffspringConversion, OffspringInput, OffspringPage, OffspringRecordType } from './types'

const root = '/proles'
export const listOffspring = ({ page = 1, pageSize = 25, ...filters }: ListOffspringParams = {}) => {
  const query = new URLSearchParams()
  Object.entries({ page, pageSize, ...filters }).forEach(([key, value]) => { if (value !== undefined) query.set(key, String(value)) })
  return httpRequest<OffspringPage>(`${root}?${query}`, { authenticated: true })
}
export const getOffspring = (id: string) => httpRequest<Offspring>(`${root}/${id}`, { authenticated: true })
export const createOffspring = (cycle: string, body: OffspringInput & { tipoRegistro: OffspringRecordType; quantidade: number }) => httpRequest<Offspring>(`${root}?cicloReprodutivoId=${encodeURIComponent(cycle)}`, { method: 'POST', authenticated: true, body })
export const updateOffspring = (id: string, body: OffspringInput) => httpRequest<Offspring>(`${root}/${id}`, { method: 'PUT', authenticated: true, body })
export const splitOffspring = (id: string, body: OffspringInput) => httpRequest<Offspring>(`${root}/${id}/desdobramentos`, { method: 'POST', authenticated: true, body })
export const convertOffspring = (id: string, body: { codigoInterno?: string | null; nome?: string | null; especieId: string; racaId?: string | null; variedadeId?: string | null; escopo: 1 | 2 }) => httpRequest<OffspringConversion>(`${root}/${id}/conversoes`, { method: 'POST', authenticated: true, body })
