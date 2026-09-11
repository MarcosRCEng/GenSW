import { describe, expect, it } from 'vitest'
import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import type { Animal } from '../types/animals'
import { parseAnimal, parseAnimalsPage } from './animalsContractParsers'

const animal: Animal = {
  id: 'animal-1',
  codigoInterno: 'AN-000001',
  nome: 'Bela',
  especieId: 'species-1',
  racaId: 'breed-1',
  variedadeId: 'variety-1',
  sexo: 2,
  dataNascimento: '2020-02-29',
  escopo: 1,
  ativo: true,
  createdAtUtc: '2026-09-08T12:00:00Z',
  updatedAtUtc: '2026-09-08T12:00:00Z',
  especie: { id: 'species-1', nomeComum: 'Canina', ativo: true },
  raca: { id: 'breed-1', nome: 'Pastor', ativo: true },
  variedade: { id: 'variety-1', nome: 'Curto', ativo: true },
}

describe('parseAnimal', () => {
  it('accepts a complete animal with nullable optional fields', () => {
    expect(parseAnimal(animal)).toEqual(animal)
    expect(parseAnimal({ ...animal, nome: null, dataNascimento: null, racaId: null, variedadeId: null, raca: null, variedade: null })).toEqual({ ...animal, nome: null, dataNascimento: null, racaId: null, variedadeId: null, raca: null, variedade: null })
  })

  it.each([
    null,
    [],
    { ...animal, id: '' },
    { ...animal, codigoInterno: 1 },
    { ...animal, nome: false },
    { ...animal, especieId: null },
    { ...animal, racaId: 1 },
    { ...animal, variedadeId: 1 },
    { ...animal, sexo: 0 },
    { ...animal, sexo: 4 },
    { ...animal, dataNascimento: '2026-02-29' },
    { ...animal, dataNascimento: '2026-2-09' },
    { ...animal, escopo: 3 },
    { ...animal, ativo: 'true' },
    { ...animal, createdAtUtc: 'not-a-timestamp' },
    { ...animal, updatedAtUtc: null },
    { ...animal, especie: { id: 'species-1', nomeComum: 'Canina' } },
    { ...animal, raca: { id: 'breed-1', nome: 'Pastor' } },
    { ...animal, variedade: { id: 'variety-1', ativo: true } },
  ])('rejects an incomplete or invalid contract %#', (value) => {
    expect(() => parseAnimal(value)).toThrow(InvalidApiResponseError)
  })
})

describe('parseAnimalsPage', () => {
  it('accepts complete pagination metadata', () => {
    const page = { items: [animal], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 }
    expect(parseAnimalsPage(page)).toEqual(page)
  })

  it.each([
    { items: null, page: 1, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [{ ...animal, sexo: 9 }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 },
    { items: [], page: 0, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 0, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: -1, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: -1 },
    { items: [], page: 1.5, pageSize: 25, totalItems: 0, totalPages: 0 },
  ])('rejects invalid pagination %#', (value) => {
    expect(() => parseAnimalsPage(value)).toThrow(InvalidApiResponseError)
  })
})
