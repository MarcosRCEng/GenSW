import { describe, expect, it } from 'vitest'
import { InvalidApiResponseError } from '../../../../shared/http/httpErrors'
import { parseAnimalIdentification, parseAnimalIdentificationsPage, parseGlobalAnimalIdentificationsPage } from './identificationsContractParsers'

const identification = {
  id: 'identification-1', animalId: 'animal-1', tipo: 3, descricaoTipo: null, valor: '985141000000001', principal: true,
  dataAplicacao: null, observacao: null, ativo: true, createdAtUtc: '2026-09-12T12:00:00Z', updatedAtUtc: '2026-09-12T12:00:00.123Z',
}

describe('identificationsContractParsers', () => {
  it.each([1, 2, 3, 4, 5, 6] as const)('accepts identification type %s and nullable fields', (tipo) => {
    expect(parseAnimalIdentification({ ...identification, tipo, descricaoTipo: 'custom', dataAplicacao: '2026-02-28', observacao: 'checked' })).toEqual({ ...identification, tipo, descricaoTipo: 'custom', dataAplicacao: '2026-02-28', observacao: 'checked' })
    expect(parseAnimalIdentification({ ...identification, tipo })).toEqual({ ...identification, tipo })
  })

  it.each([null, { ...identification, tipo: 0 }, { ...identification, descricaoTipo: undefined }, { ...identification, dataAplicacao: '2026-02-29' }, { ...identification, observacao: 2 }, { ...identification, createdAtUtc: 'invalid' }, { ...identification, updatedAtUtc: null }])('rejects malformed identification %#', (value) => {
    expect(() => parseAnimalIdentification(value)).toThrow(InvalidApiResponseError)
  })

  it.each(['2026-02-29T12:00:00Z', '2026-02-30T12:00:00Z', '0000-01-01T00:00:00Z', '2026-01-01T00:00:00+14:01', '2026-01-01T00:00:00-15:00'])('rejects timestamps that JavaScript would normalize or whose offset is outside DateTimeOffset bounds', (timestamp) => {
    expect(() => parseAnimalIdentification({ ...identification, createdAtUtc: timestamp })).toThrow(InvalidApiResponseError)
  })

  it.each(['0001-01-01T00:00:00+14:00', '9999-12-31T23:59:59-14:00'])('rejects offsets whose implied UTC instant is outside DateTimeOffset range', (timestamp) => {
    expect(() => parseAnimalIdentification({ ...identification, createdAtUtc: timestamp })).toThrow(InvalidApiResponseError)
  })

  it('parses local pages with complete pagination metadata', () => {
    const page = { items: [identification], page: 2, pageSize: 25, totalItems: 26, totalPages: 2 }
    expect(parseAnimalIdentificationsPage(page)).toEqual(page)
  })

  it('parses global pages including the animal summary', () => {
    const page = { items: [{ identificacao: identification, animal: { id: 'animal-1', codigoInterno: 'AN-000001', nome: null } }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 }
    expect(parseGlobalAnimalIdentificationsPage(page)).toEqual(page)
  })

  it.each([
    { items: [], page: 0, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 0, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: -1, totalPages: 0 },
    { items: [{ identificacao: identification, animal: { id: 'animal-1', codigoInterno: 2, nome: null } }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 },
  ])('rejects malformed page contracts %#', (value) => {
    expect(() => parseGlobalAnimalIdentificationsPage(value)).toThrow(InvalidApiResponseError)
  })
})
