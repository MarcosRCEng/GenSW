import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../../shared/http/httpClient'
import { HttpError } from '../../../shared/http/httpErrors'
import { parentReturnState } from '../../../shared/details/listNavigation'
import { AnimalDetailsPage } from './AnimalDetailsPage'
import { AnimalRelatedDetailsPage } from './AnimalRelatedDetailsPage'
import { BreedingDetailsPage } from '../../breedings/BreedingDetailsPage'
import { ReproductiveCycleDetailsPage } from '../../reproductive-cycles/ReproductiveCycleDetailsPage'
import { OffspringDetailsPage } from '../../offspring/OffspringDetailsPage'
import { OffspringFormPage } from '../../offspring/OffspringFormPage'

vi.mock('../../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))
const animal = { id: 'animal-1', codigoInterno: 'AN-001', nome: 'Animal histórico', especieId: 'species-1', racaId: null, variedadeId: null, sexo: 1, dataNascimento: '2026-01-01', escopo: 1, ativo: false, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', especie: { id: 'species-1', nomeComum: 'Espécie histórica', ativo: false }, raca: null, variedade: null }
const page = (items: unknown[] = [], number = 1, totalPages = 1) => ({ items, page: number, pageSize: 100, totalItems: items.length, totalPages })
const weight = { id: 'weight-1', dataMedicao: '2026-09-01', pesoGramas: 125, tipoMarco: 1, descricaoMarco: null, idadeReferenciaDias: null, idadeDiasNaMedicao: 243, observacao: 'Peso consultado' }
const photo = { id: 'photo-1', legenda: 'Imagem histórica', dataCaptura: null, ordem: 0, ativa: false, representativa: false, thumbnailPath: '/conteudo-miniatura', conteudoPath: '/conteudo-privado' }
const species = { ...animal.especie, nomeCientifico: null, createdAtUtc: animal.createdAtUtc, updatedAtUtc: animal.updatedAtUtc }
const eggEntry = { id: 'egg-1', dataPostura: '2026-09-02', pesoGramas: 12.5, observacao: 'Registro da segunda página' }
function app(path: string, state?: unknown) { return render(<MemoryRouter initialEntries={[{ pathname: path, state }]}><Routes>
  <Route path="/animais/:id" element={<AnimalDetailsPage />} />
  <Route path="/animais/:animalId/pesagens/:recordId" element={<AnimalRelatedDetailsPage section="pesagens" />} />
  <Route path="/animais/:animalId/producoes-ovos/:recordId" element={<AnimalRelatedDetailsPage section="producoes-ovos" />} />
  <Route path="/animais/:animalId/registros/:recordId" element={<AnimalRelatedDetailsPage section="registros" />} />
  <Route path="/variedades/:varietyId/imagens/:recordId" element={<AnimalRelatedDetailsPage section="imagens" kind="variedades" />} />
  <Route path="/cruzamentos/:id" element={<BreedingDetailsPage />} />
  <Route path="/ciclos-reprodutivos/:id" element={<ReproductiveCycleDetailsPage />} />
  <Route path="/proles/:id" element={<OffspringDetailsPage />} />
  <Route path="/proles/:id/editar" element={<OffspringFormPage />} />
</Routes></MemoryRouter>) }
function expectReadOnly() {
  expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => (!options?.method || options.method === 'GET') && options?.authenticated === true && !options.body)).toBe(true)
  expect(document.querySelector('form')).toBeNull()
}

describe('Detalhes animais e reprodução', () => {
  beforeEach(() => { vi.mocked(httpRequest).mockReset() })
  it('carrega os dados gerais e só consulta cada histórico na primeira abertura da aba', async () => {
    vi.mocked(httpRequest).mockImplementation(async (path) => {
      if (path === '/animais/animal-1') return animal
      if (path.endsWith('/filiacoes') || path.endsWith('/registros')) return []
      if (path.endsWith('/preferencial')) return { origem: 'nenhuma', imagem: null }
      return page()
    })
    app('/animais/animal-1')
    const ficha = await screen.findByRole('tab', { name: 'Dados gerais' })
    expect(ficha).toHaveAttribute('aria-selected', 'true')
    expect(vi.mocked(httpRequest).mock.calls.map(([path]) => path)).toEqual(['/animais/animal-1'])
    expect(screen.queryByRole('tab', { name: 'Postura' })).not.toBeInTheDocument()
    expect(within(screen.getByRole('tabpanel')).getByText('Inativo')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Espécie histórica' })).toHaveAttribute('href', '/especies/species-1')
    fireEvent.click(screen.getByRole('tab', { name: 'Identificações' }))
    await screen.findByText('Nenhuma identificação física cadastrada.')
    const weightsTab = screen.getByRole('tab', { name: 'Pesagens' })
    fireEvent.click(weightsTab)
    await screen.findByText('Nenhuma pesagem registrada.')
    fireEvent.click(ficha)
    expect(screen.queryByRole('region', { name: 'Pesagens' })).not.toBeInTheDocument()
    fireEvent.click(weightsTab)
    expect(screen.getByRole('region', { name: 'Pesagens' })).toBeVisible()
    expect(vi.mocked(httpRequest).mock.calls.filter(([path]) => path.includes('/pesagens?'))).toHaveLength(1)
    expect(screen.queryByRole('button', { name: /Inativar|Adicionar|Salvar|Corrigir|Definir pai/ })).not.toBeInTheDocument()
    expectReadOnly()
  })
  it('consulta a pesagem por GET individual vinculado ao animal', async () => {
    vi.mocked(httpRequest).mockResolvedValue(weight)
    app('/animais/animal-1/pesagens/weight-1')
    expect(await screen.findByText('Peso consultado')).toBeInTheDocument()
    expect(screen.getByText('125 g')).toBeInTheDocument()
    expect(httpRequest).toHaveBeenCalledWith('/animais/animal-1/pesagens/weight-1', expect.objectContaining({ authenticated: true }))
    expectReadOnly()
  })
  it('abre Evolução pelo teclado e vincula a ficha à comparação', async () => {
    vi.mocked(httpRequest).mockImplementation(async path => {
      if (path === '/animais/animal-1') return animal
      if (path.endsWith('/filiacoes') || path.endsWith('/registros')) return []
      return page(path.includes('/pesagens?') ? [weight] : [])
    })
    app('/animais/animal-1')
    const ficha = await screen.findByRole('tab', { name: 'Dados gerais' })
    const evolution = screen.getByRole('tab', { name: 'Evolução' })
    ficha.focus()
    fireEvent.keyDown(ficha, { key: 'ArrowLeft' })
    expect(evolution).toHaveFocus()
    expect(evolution).toHaveAttribute('aria-selected', 'true')
    expect(await screen.findByRole('heading', { name: 'Evolução do peso corporal' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Comparar aves' })).toHaveAttribute('href', '/animais/comparar?animalId=animal-1')
    fireEvent.keyDown(evolution, { key: 'Home' })
    expect(ficha).toHaveFocus()
    expect(ficha).toHaveAttribute('aria-selected', 'true')
    expectReadOnly()
  })

  it('mostra a aba Postura para fêmea de espécie ovípara e consulta 10 registros ao abrir', async () => {
    vi.mocked(httpRequest).mockImplementation(async path => {
      if (path === '/animais/animal-1') return { ...animal, sexo: 2 }
      if (path === '/especies/species-1') return { ...species, ovipara: true }
      return page()
    })
    app('/animais/animal-1')
    const postureTab = await screen.findByRole('tab', { name: 'Postura' })
    expect(vi.mocked(httpRequest).mock.calls.map(([path]) => path)).toEqual(['/animais/animal-1', '/especies/species-1'])
    fireEvent.click(postureTab)
    expect(await screen.findByText('Nenhuma produção de ovos registrada.')).toBeVisible()
    expect(httpRequest).toHaveBeenCalledWith('/animais/animal-1/producoes-ovos?page=1&pageSize=10&includeHistory=false', expect.objectContaining({ authenticated: true }))
    expectReadOnly()
  })

  it('volta do detalhe de pesagem para a aba, página e filtros anteriores', async () => {
    vi.mocked(httpRequest).mockImplementation(async path => {
      if (path === '/animais/animal-1') return animal
      if (path.endsWith('/weight-1')) return weight
      return page([weight], 2, 2)
    })
    const path = '/animais/animal-1'
    app(path, parentReturnState(undefined, path, { section: 'pesagens', page: 2, filters: { from: '2026-09-01', filter: '1' } }))
    expect(await screen.findByRole('tab', { name: 'Pesagens' })).toHaveAttribute('aria-selected', 'true')
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: 'Visualizar pesagem' })
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Animal' }))
    const region = await screen.findByRole('region', { name: 'Pesagens' })
    expect(region).toHaveFocus()
    expect(within(region).getByLabelText('De')).toHaveValue('2026-09-01')
    expect(within(region).getByLabelText('Filtrar marco')).toHaveValue('1')
    expect(await within(region).findByText('Página 2 de 2')).toBeVisible()
  })

  it('restaura Postura e sua página depois de verificar a elegibilidade da espécie', async () => {
    vi.mocked(httpRequest).mockImplementation(async path => {
      if (path === '/animais/animal-1') return { ...animal, sexo: 2 }
      if (path === '/especies/species-1') return { ...species, ovipara: true }
      return {
        ...page([eggEntry], 2, 2),
        pageSize: 10,
        totalItems: 11,
        metricas: { totalLancamentos: 11, pesoMedioGramas: 12.5, pesoMinimoGramas: 12, pesoMaximoGramas: 13, pesoPadraoGramas: null, diasAtePesoPadrao: null },
      }
    })
    const path = '/animais/animal-1'
    app(path, parentReturnState(undefined, path, { section: 'producoes-ovos', page: 2 }))
    expect(await screen.findByRole('tab', { name: 'Postura' })).toHaveAttribute('aria-selected', 'true')
    expect(document.getElementById('record-producoes-ovos')).toHaveFocus()
    expect(await screen.findByText(/Página 2 de 2/)).toBeVisible()
    expect(httpRequest).toHaveBeenCalledWith('/animais/animal-1/producoes-ovos?page=2&pageSize=10&includeHistory=false', expect.objectContaining({ authenticated: true }))
  })
  it('encontra produção antiga além da primeira página ao acessar URL direta', async () => {
    vi.mocked(httpRequest).mockImplementation(async path => path.includes('page=1&') ? page([], 1, 2) : page([eggEntry], 2, 2))
    app('/animais/animal-1/producoes-ovos/egg-1')
    expect(await screen.findByText('Registro da segunda página')).toBeInTheDocument()
    expect(httpRequest).toHaveBeenCalledWith('/animais/animal-1/producoes-ovos?page=2&pageSize=100', expect.objectContaining({ authenticated: true }))
    expectReadOnly()
  })
  it('consulta imagem inativa de variedade sem requisitar conteúdo indisponível', async () => {
    vi.mocked(httpRequest).mockImplementation(async path => path.includes('ativo=false') ? page([photo]) : page())
    app('/variedades/variety-1/imagens/photo-1')
    expect(await screen.findByText('Imagem histórica')).toBeInTheDocument()
    expect(screen.getByText('Inativo')).toBeInTheDocument()
    expect(screen.getByText(/conteúdo de imagens inativas não está disponível/)).toBeInTheDocument()
    expect(vi.mocked(httpRequest).mock.calls.some(([path]) => path.includes('conteudo'))).toBe(false)
    expectReadOnly()
  })
  it('não encontra registro pertencente a outro animal', async () => {
    vi.mocked(httpRequest).mockResolvedValue([])
    app('/animais/animal-1/registros/foreign-record')
    expect(await screen.findByRole('alert')).toHaveTextContent('Registro não encontrado.')
    expectReadOnly()
  })
  it.each([403, 404, 500])('trata HTTP %s e não apresenta edição ou dados', async status => {
    vi.mocked(httpRequest).mockRejectedValue(new HttpError(status))
    app('/animais/animal-1')
    expect(await screen.findByRole('alert')).toHaveTextContent(status === 403 ? 'Você não tem permissão' : status === 404 ? 'Registro não encontrado.' : 'Não foi possível carregar')
    expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument()
    expectReadOnly()
  })
  it('consulta cruzamento e relações sem alterar status', async () => {
    vi.mocked(httpRequest).mockResolvedValue({ id: 'b1', machoId: 'male', femeaId: 'female', status: 4, dataInicio: null, dataFim: null, objetivo: 'Objetivo registrado', observacao: null, macho: { id: 'male', codigoInterno: 'M-001', nome: null }, femea: { id: 'female', codigoInterno: 'F-001', nome: null } })
    app('/cruzamentos/b1')
    expect(await screen.findByText('Objetivo registrado')).toBeInTheDocument()
    expect(screen.getByText('Cancelado')).toBeInTheDocument()
    expectReadOnly()
  })
  it('consulta ciclo com estatísticas sem operações de conclusão', async () => {
    vi.mocked(httpRequest).mockResolvedValue({ id: 'c1', cruzamentoId: 'b1', tipo: 1, status: 2, ovosPostos: 10, ovosFerteis: 8, ovosIncubados: 8, ovosEclodidos: 7, taxaFertilidade: 80, taxaEclosao: 87.5, observacao: 'Ciclo finalizado' })
    app('/ciclos-reprodutivos/c1')
    expect(await screen.findByText('Ciclo finalizado')).toBeInTheDocument()
    expect(screen.getByText('87.5')).toBeInTheDocument()
    expectReadOnly()
  })
  it('consulta prole sem conversão/desdobramento; mantém acesso à edição', async () => {
    vi.mocked(httpRequest).mockResolvedValue({ id: 'o1', cicloReprodutivoId: 'c1', loteOrigemId: null, tipoRegistro: 2, quantidade: 5, quantidadeDesdobrada: 1, origem: 2, data: '2026-09-02', pesoGramas: null, sexo: 3, condicao: 'Prole histórica', observacao: null, animalId: null })
    app('/proles/o1')
    expect(await screen.findByText('Prole histórica')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Editar' })).toHaveAttribute('href', '/proles/o1/editar')
    expect(screen.queryByRole('button', { name: /Desdobrar|Criar Animal/ })).not.toBeInTheDocument()
    expectReadOnly()
  })
  it('faz retry da consulta individual preservando somente leitura', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new HttpError(500)).mockResolvedValue(weight)
    app('/animais/animal-1/pesagens/weight-1')
    fireEvent.click(await screen.findByRole('button', { name: 'Tentar novamente' }))
    await waitFor(() => expect(screen.getByText('Peso consultado')).toBeInTheDocument())
    expectReadOnly()
  })
  it('preserva desdobramento no fluxo de edição sem dispará-lo ao abrir', async () => {
    const offspring = { id: 'o1', cicloReprodutivoId: 'c1', tipoRegistro: 2, quantidade: 5, quantidadeDesdobrada: 1, origem: 2, data: '2026-09-02', pesoGramas: null, sexo: 3, condicao: 'Lote existente', observacao: null, animalId: null }
    vi.mocked(httpRequest).mockResolvedValue(offspring)
    app('/proles/o1/editar')
    const split = await screen.findByRole('button', { name: 'Desdobrar uma unidade em registro individual' })
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => !options?.method)).toBe(true)
    fireEvent.click(split)
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith('/proles/o1/desdobramentos', expect.objectContaining({ method: 'POST', body: expect.objectContaining({ condicao: 'Lote existente' }) })))
    expect(await screen.findByRole('heading', { name: 'Visualizar prole' })).toBeInTheDocument()
  })
  it('preserva formulário de conversão na edição e deixa o detalhe sem ele', async () => {
    vi.mocked(httpRequest).mockResolvedValue({ id: 'o1', cicloReprodutivoId: 'c1', tipoRegistro: 1, quantidade: 1, quantidadeDesdobrada: 0, origem: 2, data: '2026-09-02', pesoGramas: null, sexo: 3, condicao: 'Individual existente', observacao: null, animalId: null })
    app('/proles/o1/editar')
    expect(await screen.findByRole('button', { name: 'Criar Animal e continuar para confirmação de Filiação' })).toBeInTheDocument()
    expect(screen.getByLabelText('ID da espécie')).toBeRequired()
    expect(vi.mocked(httpRequest).mock.calls.every(([, options]) => !options?.method)).toBe(true)
  })
})
