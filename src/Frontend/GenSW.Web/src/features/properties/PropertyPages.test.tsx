import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../shared/http/httpErrors'
import { PropertiesListPage } from './PropertiesListPage'
import { PropertyDetailsPage } from './PropertyDetailsPage'
import { PropertyFormPage } from './PropertyFormPage'
import { createPropriedade, getPropriedadeById, listPropriedades, setPropriedadeAtivo, updatePropriedade } from './propertiesService'

vi.mock('./propertiesService', () => ({ createPropriedade: vi.fn(), getPropriedadeById: vi.fn(), listPropriedades: vi.fn(), setPropriedadeAtivo: vi.fn(), updatePropriedade: vi.fn() }))
const property = { id: 'property-1', nome: 'Unidade Norte', localizacao: 'Estrada rural', observacao: 'Criação', ativo: true, createdAtUtc: '2026-09-01T00:00:00Z', updatedAtUtc: '2026-09-01T00:00:00Z' }
const page = { items: [property], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 }
function app(path: string) {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/propriedades" element={<PropertiesListPage />} />
    <Route path="/propriedades/nova" element={<PropertyFormPage />} />
    <Route path="/propriedades/:id" element={<PropertyDetailsPage />} />
    <Route path="/propriedades/:id/editar" element={<PropertyFormPage />} />
  </Routes></MemoryRouter>)
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(createPropriedade).mockResolvedValue(property)
  vi.mocked(getPropriedadeById).mockResolvedValue(property)
  vi.mocked(updatePropriedade).mockResolvedValue(property)
  vi.mocked(setPropriedadeAtivo).mockResolvedValue({ ...property, ativo: false })
  vi.mocked(listPropriedades).mockResolvedValue(page)
})

describe('fluxos de Propriedades', () => {
  it('cadastra uma unidade normalizada com localização e observação opcionais e retorna à lista', async () => {
    app('/propriedades/nova')
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: '  Unidade   Norte  ' } })
    fireEvent.change(screen.getByLabelText('Localização'), { target: { value: ' ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(createPropriedade).toHaveBeenCalledWith({ nome: 'Unidade Norte', localizacao: null, observacao: null }))
    expect(await screen.findByRole('heading', { name: 'Propriedades' })).toBeInTheDocument()
  })

  it('edita unidade inativa e distingue nome duplicado de bloqueio transitório', async () => {
    vi.mocked(getPropriedadeById).mockResolvedValue({ ...property, ativo: false })
    vi.mocked(updatePropriedade).mockRejectedValueOnce(new HttpError(409, undefined, undefined, 'propriedade_duplicada'))
      .mockRejectedValueOnce(new HttpError(409, undefined, undefined, 'conflito_transitorio'))
    app('/propriedades/property-1/editar')
    expect(await screen.findByDisplayValue(property.nome)).toBeEnabled()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Já existe uma propriedade com esse nome.')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Outra operação está em andamento.'))
    expect(updatePropriedade).toHaveBeenCalledWith(property.id, { nome: property.nome, localizacao: property.localizacao, observacao: property.observacao })
  })

  it('mantém filtros e paginação ao visualizar uma propriedade e retornar à lista', async () => {
    vi.mocked(listPropriedades).mockImplementation(async (query = {}) => ({ ...page, page: query.page ?? 1, totalItems: 26, totalPages: 2 }))
    app('/propriedades')
    await screen.findByRole('link', { name: 'Visualizar' })
    fireEvent.change(screen.getByLabelText('Buscar por nome'), { target: { value: 'Norte' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(listPropriedades).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'Norte', page: 1 })))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Próxima' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('Página 2 de 2')
    fireEvent.click(screen.getByRole('link', { name: 'Visualizar' }))
    expect(await screen.findByRole('heading', { name: 'Visualizar propriedade' })).toBeInTheDocument()
    expect(await screen.findByText(property.nome)).toBeInTheDocument()
    expect(document.querySelector('form')).toBeNull()
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Propriedades' }))
    await screen.findByText('Página 2 de 2')
    expect(screen.getByLabelText('Buscar por nome')).toHaveValue('Norte')
    expect(listPropriedades).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'Norte', page: 2 }))
  })

  it('inativa e reativa a unidade pela lista e filtra inativas', async () => {
    app('/propriedades')
    fireEvent.click(await screen.findByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(setPropriedadeAtivo).toHaveBeenCalledWith(property.id, false))
    vi.mocked(listPropriedades).mockResolvedValue({ ...page, items: [{ ...property, ativo: false }] })
    await waitFor(() => expect(screen.getByLabelText('Status')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'false' } })
    const reactivate = await screen.findByRole('button', { name: 'Reativar' })
    expect(within(screen.getByRole('table')).getByText('Inativo')).toBeInTheDocument()
    fireEvent.click(reactivate)
    await waitFor(() => expect(setPropriedadeAtivo).toHaveBeenLastCalledWith(property.id, true))
    expect(listPropriedades).toHaveBeenCalledWith(expect.objectContaining({ ativo: false, page: 1 }))
  })

  it('trata detalhe não encontrado e permite recuperar falha sem mutar registros', async () => {
    vi.mocked(getPropriedadeById).mockRejectedValueOnce(new HttpError(403)).mockResolvedValueOnce({ ...property, ativo: false })
    const first = app('/propriedades/property-1')
    expect(await screen.findByRole('alert')).toHaveTextContent('Você não tem permissão')
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText(property.nome)).toBeInTheDocument()
    expect(screen.getByText(/Esta propriedade está inativa/)).toBeInTheDocument()
    expect(updatePropriedade).not.toHaveBeenCalled()
    expect(setPropriedadeAtivo).not.toHaveBeenCalled()
    first.unmount()
    vi.mocked(getPropriedadeById).mockRejectedValue(new HttpError(404))
    app('/propriedades/missing')
    expect(await screen.findByRole('alert')).toHaveTextContent('Registro não encontrado.')
    expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument()
  })
})
