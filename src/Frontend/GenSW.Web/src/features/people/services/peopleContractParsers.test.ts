import { describe, expect, it } from 'vitest'
import { InvalidApiResponseError } from '../../../shared/http/httpErrors'
import { TipoPessoa } from '../types/people'
import { parsePessoa, parsePessoasPage } from './peopleContractParsers'

const physicalPerson = {
  id: 'person-1',
  tipoPessoa: TipoPessoa.Fisica,
  nome: 'Marina Silva',
  nomeFantasia: null,
  ativo: true,
  createdAtUtc: '2026-08-20T12:00:00Z',
  updatedAtUtc: '2026-08-21T13:30:00+00:00',
}

const legalPerson = {
  ...physicalPerson,
  id: 'person-2',
  tipoPessoa: TipoPessoa.Juridica,
  nome: 'Silva & Filhos Ltda.',
  nomeFantasia: 'Silva & Filhos',
  ativo: false,
}

describe('parsePessoa', () => {
  it.each([physicalPerson, legalPerson])('aceita uma pessoa válida %#', (value) => {
    expect(parsePessoa(value)).toEqual(value)
  })

  it.each([
    null,
    [],
    { ...physicalPerson, id: undefined },
    { ...physicalPerson, tipoPessoa: 0 },
    { ...physicalPerson, tipoPessoa: 3 },
    { ...physicalPerson, tipoPessoa: 'Fisica' },
    { ...physicalPerson, nome: 123 },
    { ...physicalPerson, nomeFantasia: 123 },
    { ...physicalPerson, ativo: 'true' },
    { ...physicalPerson, createdAtUtc: 'not-a-date' },
    { ...physicalPerson, updatedAtUtc: 'not-a-date' },
  ])('rejeita contrato inválido %#', (value) => {
    expect(() => parsePessoa(value)).toThrow(InvalidApiResponseError)
  })
})

describe('parsePessoasPage', () => {
  it.each([
    {
      items: [physicalPerson, legalPerson],
      page: 1,
      pageSize: 25,
      totalItems: 2,
      totalPages: 1,
    },
    { items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [], page: 999, pageSize: 25, totalItems: 0, totalPages: 0 },
  ])('aceita uma página válida %#', (value) => {
    expect(parsePessoasPage(value)).toEqual(value)
  })

  it.each([
    { items: null, page: 1, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [{ ...physicalPerson, tipoPessoa: 3 }], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 },
    { items: [], page: 0, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 0, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: -1, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: -1 },
    { items: [], page: 1.5, pageSize: 25, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 25.5, totalItems: 0, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: 0.5, totalPages: 0 },
    { items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0.5 },
  ])('rejeita uma página inválida %#', (value) => {
    expect(() => parsePessoasPage(value)).toThrow(InvalidApiResponseError)
  })
})
