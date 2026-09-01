import { describe, expect, it } from 'vitest'
import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import type { Especie } from '../types/species'
import { parseEspecie, parseEspeciesPage } from './speciesContractParsers'

const especie: Especie = {
  id: 'species-1',
  nomeComum: 'Cão doméstico',
  nomeCientifico: 'Canis familiaris',
  ativo: true,
  createdAtUtc: '2026-08-31T12:00:00Z',
  updatedAtUtc: '2026-08-31T12:00:00Z',
}

describe('parseEspecie', () => {
  it.each([especie, { ...especie, nomeCientifico: null }])(
    'aceita uma espécie válida %#',
    (value) => {
      expect(parseEspecie(value)).toEqual(value)
    },
  )

  it.each([
    null,
    [],
    { ...especie, id: '' },
    { ...especie, nomeComum: 123 },
    { ...especie, nomeCientifico: 123 },
    { ...especie, ativo: 'true' },
    { ...especie, createdAtUtc: 'not-a-date' },
    { ...especie, updatedAtUtc: 'not-a-date' },
  ])('rejeita contrato inválido %#', (value) => {
    expect(() => parseEspecie(value)).toThrow(InvalidApiResponseError)
  })
})

describe('parseEspeciesPage', () => {
  it('aceita uma página válida', () => {
    const page = {
      items: [especie],
      page: 1,
      pageSize: 25,
      totalItems: 1,
      totalPages: 1,
    }

    expect(parseEspeciesPage(page)).toEqual(page)
  })

  it.each([
    { items: null, page: 1, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [{ ...especie, id: '' }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 },
    { items: [], page: 0, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 0, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: -1, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: -1 },
    { items: [], page: 1.5, pageSize: 25, totalItems: 0, totalPages: 0 },
  ])('rejeita metadados de página inválidos %#', (value) => {
    expect(() => parseEspeciesPage(value)).toThrow(InvalidApiResponseError)
  })
})
