import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../../shared/http/httpErrors'
import { createEspecie, getEspecieById, updateEspecie } from '../services/speciesService'
import type { Especie } from '../types/species'
import { SpeciesFormPage } from './SpeciesFormPage'

vi.mock('../services/speciesService', () => ({
  createEspecie: vi.fn(),
  getEspecieById: vi.fn(),
  updateEspecie: vi.fn(),
}))

const activeSpecies: Especie = {
  id: 'species-1', nomeComum: 'Cão doméstico', nomeCientifico: 'Canis familiaris', ativo: true,
  createdAtUtc: '2026-08-31T12:00:00Z', updatedAtUtc: '2026-08-31T12:00:00Z',
}

function renderPage(path = '/especies/nova') {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route element={<SpeciesFormPage />} path="/especies/nova" />
    <Route element={<SpeciesFormPage />} path="/especies/:id/editar" />
    <Route element={<h1>Espécies destino</h1>} path="/especies" />
  </Routes></MemoryRouter>)
}

beforeEach(() => {
  vi.mocked(createEspecie).mockReset(); vi.mocked(getEspecieById).mockReset(); vi.mocked(updateEspecie).mockReset()
  vi.mocked(createEspecie).mockResolvedValue(activeSpecies); vi.mocked(getEspecieById).mockResolvedValue(activeSpecies); vi.mocked(updateEspecie).mockResolvedValue(activeSpecies)
})

describe('SpeciesFormPage - criação', () => {
  it('normaliza os nomes e cria uma espécie', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Nome comum'), { target: { value: '  Cão\t doméstico  ' } })
    fireEvent.change(screen.getByLabelText('Nome científico'), { target: { value: ' Canis\n familiaris ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createEspecie).toHaveBeenCalledWith({ nomeComum: 'Cão doméstico', nomeCientifico: 'Canis familiaris' }))
    expect(await screen.findByRole('heading', { name: 'Espécies destino' })).toBeInTheDocument()
  })

  it('envia nome científico vazio como null', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Nome comum'), { target: { value: 'Cão' } })
    fireEvent.change(screen.getByLabelText('Nome científico'), { target: { value: '   ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createEspecie).toHaveBeenCalledWith({ nomeComum: 'Cão', nomeCientifico: null }))
  })

  it('valida nome comum entre 1 e 200 e nome científico com até 200 caracteres localmente', () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Nome comum'), { target: { value: ' ' } })
    fireEvent.change(screen.getByLabelText('Nome científico'), { target: { value: 'a'.repeat(201) } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(screen.getByText('Informe um nome comum entre 1 e 200 caracteres.')).toBeInTheDocument()
    expect(screen.getByText('Informe um nome científico com até 200 caracteres.')).toBeInTheDocument()
    expect(createEspecie).not.toHaveBeenCalled()
  })

  it('traduz conflito 409 ao criar', async () => {
    vi.mocked(createEspecie).mockRejectedValue(new HttpError(409))
    renderPage()
    fireEvent.change(screen.getByLabelText('Nome comum'), { target: { value: 'Cão' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Já existe uma espécie com esse nome comum ou científico.')
  })
})

describe('SpeciesFormPage - edição', () => {
  it('mostra loading, carrega espécie ativa e a atualiza', async () => {
    let resolveLoad!: (especie: Especie) => void
    vi.mocked(getEspecieById).mockReturnValue(new Promise<Especie>((resolve) => { resolveLoad = resolve }))
    renderPage('/especies/species-1/editar')
    expect(screen.getByRole('status')).toHaveTextContent('Carregando espécie…')
    resolveLoad(activeSpecies)
    expect(await screen.findByDisplayValue('Cão doméstico')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Nome comum'), { target: { value: ' Lobo  cinzento ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(updateEspecie).toHaveBeenCalledWith('species-1', { nomeComum: 'Lobo cinzento', nomeCientifico: 'Canis familiaris' }))
  })

  it('mantém campos e salvar habilitados para espécie inativa', async () => {
    vi.mocked(getEspecieById).mockResolvedValue({ ...activeSpecies, ativo: false })
    renderPage('/especies/species-1/editar')
    expect(await screen.findByDisplayValue('Cão doméstico')).toBeEnabled()
    expect(screen.getByLabelText('Nome científico')).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Salvar' })).toBeEnabled()
  })

  it('mostra espécie não encontrada ao receber 404', async () => {
    vi.mocked(getEspecieById).mockRejectedValue(new HttpError(404))
    renderPage('/especies/missing/editar')
    expect(await screen.findByText('Espécie não encontrada.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar para Espécies' })).toHaveAttribute('href', '/especies')
  })

  it('permite tentar novamente após erro genérico de carregamento', async () => {
    vi.mocked(getEspecieById).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(activeSpecies)
    renderPage('/especies/species-1/editar')
    expect(await screen.findByText('Não foi possível carregar a espécie.')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByDisplayValue('Cão doméstico')).toBeInTheDocument()
    expect(getEspecieById).toHaveBeenCalledTimes(2)
  })

  it('traduz conflito 409 ao atualizar', async () => {
    vi.mocked(updateEspecie).mockRejectedValue(new HttpError(409))
    renderPage('/especies/species-1/editar')
    await screen.findByDisplayValue('Cão doméstico')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Já existe uma espécie com esse nome comum ou científico.')
  })
})
