vi.mock('../evolution/WeightsPanel', () => ({ WeightsPanel: () => null }))
vi.mock('../evolution/ImageGallery', () => ({ ImageGallery: () => null }))
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useNavigate } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { parentReturnState, recordEditState } from '../../../shared/details/listNavigation'
import { HttpError } from '../../../shared/http/httpErrors'
import { listRacas } from '../../breeds/services/breedsService'
import { getEspecieById, listEspecies } from '../../species/services/speciesService'
import { listVariedades } from '../../varieties/services/varietiesService'
import { createAnimal, getAnimalById, updateAnimal } from '../services/animalsService'
import { listFiliations } from '../filiations/services/filiationsService'
import { listAnimalIdentifications } from '../identifications/services/identificationsService'
import { AnimalFormPage } from './AnimalFormPage'

vi.mock('../../species/services/speciesService', () => ({ getEspecieById: vi.fn(), listEspecies: vi.fn() }))
vi.mock('../../breeds/services/breedsService', () => ({ listRacas: vi.fn() }))
vi.mock('../../varieties/services/varietiesService', () => ({ listVariedades: vi.fn() }))
vi.mock('../services/animalsService', () => ({ createAnimal: vi.fn(), getAnimalById: vi.fn(), updateAnimal: vi.fn() }))
vi.mock('../filiations/services/filiationsService', () => ({ listFiliations: vi.fn() }))
vi.mock('../egg-production/AnimalEggProductionPanel', () => ({
  AnimalEggProductionPanel: () => <section><h2>Produção de ovos</h2><label>Observação da postura<input /></label></section>,
}))
vi.mock('../identifications/services/identificationsService', () => ({
  createAnimalIdentification: vi.fn(),
  listAnimalIdentifications: vi.fn(),
  setAnimalIdentificationAtivo: vi.fn(),
  setAnimalIdentificationPrincipal: vi.fn(),
  updateAnimalIdentificationMetadata: vi.fn(),
}))

const speciesA = { id: 'species-a', nomeComum: 'Canina', nomeCientifico: null, ovipara: false, ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z' }
const speciesB = { ...speciesA, id: 'species-b', nomeComum: 'Galinha', ovipara: true }
const breedA = { id: 'breed-a', especieId: speciesA.id, nome: 'Pastor', ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: speciesA.id, nomeComum: speciesA.nomeComum, ativo: true } }
const breedB = { ...breedA, id: 'breed-b', especieId: speciesB.id, nome: 'Siamês', especie: { id: speciesB.id, nomeComum: speciesB.nomeComum, ativo: true } }
const varietyA = { id: 'variety-a', especieId: speciesA.id, nome: 'Curto', ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: speciesA.id, nomeComum: speciesA.nomeComum, ativo: true } }
const varietyB = { ...varietyA, id: 'variety-b', especieId: speciesB.id, nome: 'Longo', especie: { id: speciesB.id, nomeComum: speciesB.nomeComum, ativo: true } }
const savedAnimal = { id: 'animal-1', codigoInterno: 'AN-000001', nome: 'Bela', especieId: speciesA.id, racaId: breedA.id, variedadeId: varietyA.id, sexo: 2 as const, dataNascimento: '2020-02-29', escopo: 1 as const, ativo: true, createdAtUtc: '2026-09-08T12:00:00Z', updatedAtUtc: '2026-09-08T12:00:00Z', especie: { id: speciesA.id, nomeComum: speciesA.nomeComum, ativo: true }, raca: { id: breedA.id, nome: breedA.nome, ativo: true }, variedade: { id: varietyA.id, nome: varietyA.nome, ativo: true } }
const savedBird = {
  ...savedAnimal, especieId: speciesB.id, racaId: null, variedadeId: null, raca: null, variedade: null,
  especie: { id: speciesB.id, nomeComum: speciesB.nomeComum, ativo: true },
}

function renderPage(path = '/animais/nova', state?: unknown) {
  return render(<MemoryRouter initialEntries={[{ pathname: path, state }]}><Routes><Route element={<AnimalFormPage />} path="/animais/nova" /><Route element={<AnimalFormPage />} path="/animais/:id/editar" /><Route element={<h1>Animal destino</h1>} path="/animais/:id" /><Route element={<p>Lista de animais</p>} path="/animais" /></Routes></MemoryRouter>)
}

function SwitchToSecondAnimal() {
  const navigate = useNavigate()
  return <button onClick={() => navigate('/animais/animal-2/editar')} type="button">Trocar animal</button>
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getEspecieById).mockResolvedValue(speciesA)
  vi.mocked(listEspecies).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [speciesA], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [speciesB], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(listRacas).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [breedA], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [breedB], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(listVariedades).mockImplementation(({ page = 1 } = {}) => Promise.resolve(page === 1 ? { items: [varietyA], page: 1, pageSize: 100, totalItems: 2, totalPages: 2 } : { items: [varietyB], page: 2, pageSize: 100, totalItems: 2, totalPages: 2 }))
  vi.mocked(createAnimal).mockResolvedValue(savedAnimal)
  vi.mocked(getAnimalById).mockResolvedValue(savedAnimal)
  vi.mocked(updateAnimal).mockResolvedValue(savedAnimal)
  vi.mocked(listFiliations).mockResolvedValue([])
  vi.mocked(listAnimalIdentifications).mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 })
})

describe('AnimalFormPage', () => {
  it('keeps record tabs out of creation and loads the identification panel when its edit tab opens', async () => {
    const creationPage = renderPage()
    await screen.findByRole('heading', { name: 'Novo animal' })
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument()
    expect(listAnimalIdentifications).not.toHaveBeenCalled()
    creationPage.unmount()

    renderPage('/animais/animal-1/editar')
    expect(await screen.findByRole('tab', { name: 'Dados gerais' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.queryByRole('tab', { name: 'Evolução' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Identificações físicas' })).not.toBeInTheDocument()
    expect(listAnimalIdentifications).not.toHaveBeenCalled()
    expect(screen.getByLabelText('Código interno')).toHaveValue(savedAnimal.codigoInterno)
    fireEvent.click(screen.getByRole('tab', { name: 'Identificações' }))
    expect(await screen.findByRole('heading', { name: 'Identificações físicas' })).toBeVisible()
    await waitFor(() => expect(listAnimalIdentifications).toHaveBeenCalledWith(savedAnimal.id, { page: 1, pageSize: 10 }))
  })

  it('keeps drafts from general data and an opened record tab without reloading the record', async () => {
    renderPage('/animais/animal-1/editar')
    const general = await screen.findByRole('tab', { name: 'Dados gerais' })
    const identifications = screen.getByRole('tab', { name: 'Identificações' })
    fireEvent.click(identifications)
    fireEvent.click(screen.getByRole('button', { name: 'Adicionar identificação' }))
    fireEvent.change(screen.getByLabelText('Valor'), { target: { value: 'Anilha em edição' } })

    fireEvent.click(general)
    expect(screen.queryByRole('heading', { name: 'Nova identificação' })).not.toBeInTheDocument()
    const unload = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(unload)
    expect(unload.defaultPrevented).toBe(true)
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Nome em edição' } })

    fireEvent.click(identifications)
    expect(screen.getByLabelText('Valor')).toHaveValue('Anilha em edição')
    expect(await screen.findByText('Nenhuma identificação física cadastrada.')).toBeVisible()
    expect(listAnimalIdentifications).toHaveBeenCalledTimes(1)
    fireEvent.click(general)
    expect(screen.getByLabelText('Nome')).toHaveValue('Nome em edição')
  })

  it('opens the matching edit tab when coming from its consultation tab', async () => {
    const path = '/animais/animal-1'
    const state = recordEditState(parentReturnState(undefined, path, { section: 'identificacoes' }), '/animais', path)
    renderPage(`${path}/editar`, state)

    expect(await screen.findByRole('tab', { name: 'Identificações' })).toHaveAttribute('aria-selected', 'true')
    expect(await screen.findByRole('heading', { name: 'Identificações físicas' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'Voltar para animal' })).toHaveAttribute('href', path)
  })

  it('uses saved classification for Postura and keeps genealogy edits blocked until the general data is saved', async () => {
    renderPage('/animais/animal-1/editar')
    await screen.findByRole('tab', { name: 'Dados gerais' })
    expect(screen.queryByRole('tab', { name: 'Postura' })).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesB.id } })
    expect(screen.queryByRole('tab', { name: 'Postura' })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('tab', { name: 'Genealogia' }))
    expect(await screen.findByText('Sem filiações cadastradas.')).toBeVisible()
    expect(screen.getByText(/Salve as alterações de espécie, raça ou sexo/)).toBeVisible()
    expect(screen.getByRole('combobox', { name: 'Pesquisar progenitor por nome ou código' })).toBeDisabled()
  })

  it('preserves the Postura draft while saved eligibility still applies', async () => {
    vi.mocked(getAnimalById).mockResolvedValue(savedBird)
    renderPage('/animais/animal-1/editar')
    const general = await screen.findByRole('tab', { name: 'Dados gerais' })
    const posture = screen.getByRole('tab', { name: 'Postura' })
    expect(screen.queryByRole('heading', { name: 'Produção de ovos' })).not.toBeInTheDocument()
    fireEvent.click(posture)
    expect(screen.getByRole('heading', { name: 'Produção de ovos' })).toBeVisible()
    fireEvent.change(screen.getByLabelText('Observação da postura'), { target: { value: 'Rascunho' } })

    fireEvent.click(general)
    fireEvent.change(screen.getByLabelText('Sexo'), { target: { value: '1' } })
    fireEvent.click(posture)
    expect(screen.getByLabelText('Observação da postura')).toHaveValue('Rascunho')
    expect(getEspecieById).not.toHaveBeenCalled()
  })

  it('checks an inactive species for Postura without blocking the general form', async () => {
    vi.mocked(getAnimalById).mockResolvedValue({ ...savedBird, especie: { ...savedBird.especie, ativo: false } })
    let finishSpeciesLoad!: (species: typeof speciesB) => void
    vi.mocked(getEspecieById).mockImplementationOnce(() => new Promise(resolve => { finishSpeciesLoad = resolve }))
    renderPage('/animais/animal-1/editar')

    expect(await screen.findByRole('tab', { name: 'Dados gerais' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('option', { name: 'Galinha (inativa)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Salvar' })).toBeEnabled()
    expect(screen.queryByRole('tab', { name: 'Postura' })).not.toBeInTheDocument()
    expect(getEspecieById).toHaveBeenCalledWith(speciesB.id)

    finishSpeciesLoad({ ...speciesB, ativo: false })
    expect(await screen.findByRole('tab', { name: 'Postura' })).toBeVisible()
  })

  it('does not keep the previous animal panel while switching edit routes', async () => {
    let finishSecondLoad!: (animal: typeof savedAnimal) => void
    vi.mocked(getAnimalById)
      .mockResolvedValueOnce(savedAnimal)
      .mockImplementationOnce(() => new Promise((resolve) => { finishSecondLoad = resolve }))

    render(<MemoryRouter initialEntries={['/animais/animal-1/editar']}><SwitchToSecondAnimal /><Routes>
      <Route element={<AnimalFormPage />} path="/animais/:id/editar" />
    </Routes></MemoryRouter>)
    fireEvent.click(await screen.findByRole('tab', { name: 'Identificações' }))
    expect(await screen.findByRole('heading', { name: 'Identificações físicas' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Trocar animal' }))
    expect(screen.queryByRole('heading', { name: 'Identificações físicas' })).not.toBeInTheDocument()
    expect(listAnimalIdentifications).toHaveBeenCalledTimes(1)
    finishSecondLoad({ ...savedAnimal, id: 'animal-2', codigoInterno: 'AN-000002' })
    expect(await screen.findByRole('tab', { name: 'Dados gerais' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.queryByRole('heading', { name: 'Identificações físicas' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Código interno')).toHaveValue('AN-000002')
    fireEvent.click(screen.getByRole('tab', { name: 'Identificações' }))
    expect(await screen.findByText('Nenhuma identificação física cadastrada.')).toBeVisible()
    expect(listAnimalIdentifications).toHaveBeenLastCalledWith('animal-2', { page: 1, pageSize: 10 })
  })

  it('shows automatic-code help and omits untouched empty code, but rejects a touched empty value locally', async () => {
    renderPage()
    expect(await screen.findByText('O código será gerado automaticamente se permanecer vazio.')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByLabelText('Espécie')).not.toBeDisabled())
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesA.id } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createAnimal).toHaveBeenCalledWith(expect.objectContaining({ especieId: speciesA.id })))
    expect(vi.mocked(createAnimal).mock.calls[0][0]).not.toHaveProperty('codigoInterno')

    renderPage()
    await waitFor(() => expect(screen.getByLabelText('Espécie')).not.toBeDisabled())
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesA.id } })
    fireEvent.focus(screen.getByLabelText('Código interno'))
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('Informe um código interno não vazio.')).toBeInTheDocument()
    expect(createAnimal).toHaveBeenCalledOnce()
  })

  it('normalizes and sends explicit manual code, optional fields, all active catalog pages, and compatible independent classifications', async () => {
    renderPage()
    await screen.findByRole('heading', { name: 'Novo animal' })
    await waitFor(() => expect(listEspecies).toHaveBeenCalledWith({ page: 2, pageSize: 100, ativo: true }))
    expect(listRacas).toHaveBeenCalledWith({ page: 1, pageSize: 100, ativo: true })
    expect(listVariedades).toHaveBeenCalledWith({ page: 2, pageSize: 100, ativo: true })
    fireEvent.change(screen.getByLabelText('Código interno'), { target: { value: '  MANUAL   01 ' } })
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesA.id } })
    fireEvent.change(screen.getByLabelText('Raça'), { target: { value: breedA.id } })
    fireEvent.change(screen.getByLabelText('Variedade'), { target: { value: varietyA.id } })
    fireEvent.change(screen.getByLabelText('Sexo'), { target: { value: '1' } })
    fireEvent.change(screen.getByLabelText('Escopo'), { target: { value: '2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createAnimal).toHaveBeenCalledWith({ codigoInterno: 'MANUAL 01', nome: null, especieId: speciesA.id, racaId: breedA.id, variedadeId: varietyA.id, sexo: 1, dataNascimento: null, escopo: 2 }))
  })

  it('loads the animal before catalogs, retains only inactive current links, and keeps active replacements for an inactive current species', async () => {
    const inactiveAnimal = { ...savedAnimal, racaId: 'breed-old', variedadeId: 'variety-old', especie: { ...savedAnimal.especie, ativo: false }, raca: { id: 'breed-old', nome: 'Antiga', ativo: false }, variedade: { id: 'variety-old', nome: 'Antiga', ativo: false } }
    vi.mocked(getAnimalById).mockResolvedValue(inactiveAnimal)
    renderPage('/animais/animal-1/editar')
    expect(await screen.findByRole('heading', { name: 'Editar animal' })).toBeInTheDocument()
    expect(getAnimalById).toHaveBeenCalledWith('animal-1')
    expect(screen.getByRole('option', { name: 'Canina (inativa)' })).toBeInTheDocument()
    expect(screen.getAllByRole('option', { name: 'Antiga (inativa)' })).toHaveLength(2)
    expect(screen.queryByRole('option', { name: 'Siamês' })).not.toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Pastor' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Curto' })).toBeInTheDocument()
  })

  it('preserves incompatible classifications on a species change, blocks submission until explicit correction, and hides technical server details', async () => {
    renderPage('/animais/animal-1/editar')
    await screen.findByRole('heading', { name: 'Editar animal' })
    fireEvent.change(screen.getByLabelText('Espécie'), { target: { value: speciesB.id } })
    expect(await screen.findByText('A raça selecionada não pertence à espécie selecionada.')).toBeInTheDocument()
    expect(screen.getByLabelText('Raça')).toHaveValue(breedA.id)
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(updateAnimal).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText('Raça'), { target: { value: breedB.id } })
    fireEvent.change(screen.getByLabelText('Variedade'), { target: { value: varietyB.id } })
    vi.mocked(updateAnimal).mockRejectedValueOnce(new HttpError(409, 'SQLSTATE 23505 PostgreSQL Detail'))
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Já existe um animal com esse código interno.')
    expect(screen.queryByText(/SQLSTATE|PostgreSQL Detail/)).not.toBeInTheDocument()
  })
})
