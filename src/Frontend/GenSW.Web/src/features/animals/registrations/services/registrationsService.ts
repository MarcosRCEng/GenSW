import { httpRequest } from '../../../../shared/http/httpClient'
import type { AnimalRegistration } from '../types'

function endpoint(animalId: string) { return `/animais/${animalId}/registros` }
export function listAnimalRegistrations(animalId: string) { return httpRequest<AnimalRegistration[]>(endpoint(animalId), { authenticated: true }) }
export function createAnimalRegistration(animalId: string, request: { tipoRegistro: number; numeroRegistro: string; dataInicio: string }) { return httpRequest<AnimalRegistration>(endpoint(animalId), { method: 'POST', authenticated: true, body: request }) }
export function inactivateAnimalRegistration(animalId: string, registrationId: string, dataFim: string) { return httpRequest<AnimalRegistration>(`${endpoint(animalId)}/${registrationId}/inativar`, { method: 'PATCH', authenticated: true, body: { dataFim } }) }
