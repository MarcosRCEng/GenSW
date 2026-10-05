import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError } from '../../shared/http/httpErrors'
import { createPropriedade, desvincularAnimalPropriedade, getAnimalPropriedades, getPropriedadeById, listPropriedades, parseAnimalPropriedades, parsePropriedade, setPropriedadeAtivo, transferirAnimalPropriedade, updatePropriedade } from './propertiesService'

vi.mock('../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
const property = { id: 'property-1', nome: 'Unidade Norte', localizacao: null, observacao: null, ativo: true, createdAtUtc: '2026-09-01T00:00:00Z', updatedAtUtc: '2026-09-01T00:00:00Z' }
const link = { id: 'link-1', animalId: 'animal-1', propriedadeId: property.id, propriedadeNome: property.nome, propriedadeAtiva: true, dataInicio: '2026-09-01', dataFim: null, observacao: null, createdAtUtc: property.createdAtUtc, updatedAtUtc: property.updatedAtUtc }
beforeEach(() => vi.mocked(httpRequest).mockReset())

describe('contratos de propriedades', () => {
  it('valida datas, campos obrigatórios e consistência dos vínculos atuais', () => {
    expect(parsePropriedade(property)).toEqual(property)
    expect(() => parsePropriedade({ ...property, ativo: 'true' })).toThrow(InvalidApiResponseError)
    expect(parseAnimalPropriedades({ atual: null, historico: [] })).toEqual({ atual: null, historico: [] })
    expect(parseAnimalPropriedades({ atual: link, historico: [link] }).atual).toEqual(link)
    expect(() => parseAnimalPropriedades({ atual: link, historico: [link, { ...link, id: 'link-2' }] })).toThrow(InvalidApiResponseError)
    expect(() => parseAnimalPropriedades({ atual: null, historico: [link] })).toThrow(InvalidApiResponseError)
    expect(() => parseAnimalPropriedades({ atual: null, historico: [{ ...link, dataInicio: '2026-02-30', dataFim: '2026-03-01' }] })).toThrow(InvalidApiResponseError)
    expect(() => parseAnimalPropriedades({ atual: null, historico: [{ ...link, dataFim: '2026-08-31' }] })).toThrow(InvalidApiResponseError)
  })

  it('autentica consulta, gravação e status e mantém todos os filtros na URL', async () => {
    vi.mocked(httpRequest).mockResolvedValue({ items: [property], page: 2, pageSize: 25, totalItems: 26, totalPages: 2 })
    await listPropriedades({ page: 2, pageSize: 25, search: 'Norte & Sul', ativo: false, sortBy: 'nome', sortDirection: 'desc' })
    expect(httpRequest).toHaveBeenLastCalledWith('/propriedades?page=2&pageSize=25&search=Norte+%26+Sul&ativo=false&sortBy=nome&sortDirection=desc', { authenticated: true })
    vi.mocked(httpRequest).mockResolvedValue(property)
    const body = { nome: property.nome, localizacao: null, observacao: null }
    await createPropriedade(body)
    expect(httpRequest).toHaveBeenLastCalledWith('/propriedades', { method: 'POST', authenticated: true, body })
    await updatePropriedade(property.id, body)
    expect(httpRequest).toHaveBeenLastCalledWith('/propriedades/property-1', { method: 'PUT', authenticated: true, body })
    await getPropriedadeById(property.id)
    expect(httpRequest).toHaveBeenLastCalledWith('/propriedades/property-1', { authenticated: true })
    await setPropriedadeAtivo(property.id, false)
    expect(httpRequest).toHaveBeenLastCalledWith('/propriedades/property-1/ativo', { method: 'PATCH', authenticated: true, body: { ativo: false } })
  })

  it('envia o vínculo esperado nas operações e aceita ausência de propriedade', async () => {
    vi.mocked(httpRequest).mockResolvedValue({ atual: null, historico: [] })
    await getAnimalPropriedades('animal-1')
    expect(httpRequest).toHaveBeenLastCalledWith('/animais/animal-1/propriedades', { authenticated: true })
    const body = { propriedadeId: property.id, dataInicio: '2026-09-01', vinculoAtualIdEsperado: null, observacao: null }
    await transferirAnimalPropriedade('animal-1', body)
    expect(httpRequest).toHaveBeenLastCalledWith('/animais/animal-1/propriedade/transferir', { method: 'POST', authenticated: true, body })
    await desvincularAnimalPropriedade('animal-1', { dataFim: '2026-09-02', vinculoAtualIdEsperado: 'link-1' })
    expect(httpRequest).toHaveBeenLastCalledWith('/animais/animal-1/propriedade/desvincular', { method: 'POST', authenticated: true, body: { dataFim: '2026-09-02', vinculoAtualIdEsperado: 'link-1' } })
  })
})
