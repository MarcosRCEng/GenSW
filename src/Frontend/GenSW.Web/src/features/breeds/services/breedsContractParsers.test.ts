import { describe, expect, it } from 'vitest'
import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import { parseRaca, parseRacasPage } from './breedsContractParsers'

const raca = { id: 'breed-1', especieId: 'species-1', nome: 'Cão de trabalho', ativo: true, createdAtUtc: '2026-09-01T12:00:00Z', updatedAtUtc: '2026-09-01T12:00:00Z', especie: { id: 'species-1', nomeComum: 'Cão', ativo: true } }

describe('parseRaca', () => {
  it('aceita raça com resumo de espécie', () => expect(parseRaca(raca)).toEqual(raca))
  it.each([null, { ...raca, especieId: 1 }, { ...raca, nome: 1 }, { ...raca, ativo: 'true' }, { ...raca, createdAtUtc: 'invalid' }, { ...raca, especie: { ...raca.especie, ativo: 'true' } }])('rejeita contrato inválido %#', (value) => expect(() => parseRaca(value)).toThrow(InvalidApiResponseError))
})

describe('parseRacasPage', () => {
  it('aceita página válida', () => expect(parseRacasPage({ items: [raca], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 })).toEqual({ items: [raca], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 }))
  it('rejeita metadados inválidos', () => expect(() => parseRacasPage({ items: [], page: 0, pageSize: 25, totalItems: 0, totalPages: 0 })).toThrow(InvalidApiResponseError))
})
