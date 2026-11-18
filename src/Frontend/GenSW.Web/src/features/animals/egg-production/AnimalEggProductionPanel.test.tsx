import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../../shared/http/httpClient'
import { HttpError } from '../../../shared/http/httpErrors'
import { AnimalEggProductionPanel } from './AnimalEggProductionPanel'

vi.mock('../../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))

const entry = { id: 'egg-1', dataPostura: '2026-11-15', pesoGramas: 12.5, observacao: 'Registro acidental' }
const page = (items = [entry], currentPage = 1, totalPages = 1, totalItems = items.length) => ({ items, page: currentPage, totalPages, totalItems, metricas: { totalLancamentos: totalItems, pesoMedioGramas: 12.5, pesoMinimoGramas: 12.5, pesoMaximoGramas: 12.5, pesoPadraoGramas: null, diasAtePesoPadrao: null } })
const show = (readOnly = false) => render(<MemoryRouter><AnimalEggProductionPanel animalId="animal-1" readOnly={readOnly} /></MemoryRouter>)

describe('Exclusão de postura', () => {
  beforeEach(() => vi.mocked(httpRequest).mockReset())

  it('confirma antes de excluir e atualiza a listagem e métricas', async () => {
    let deleted = false
    vi.mocked(httpRequest).mockImplementation(async (_path, options) => {
      if (options?.method === 'DELETE') { deleted = true; return undefined }
      return page(deleted ? [] : [entry])
    })
    show()
    fireEvent.click(await screen.findByRole('button', { name: 'Editar' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Observação' }), { target: { value: 'Rascunho da postura' } })
    fireEvent.click(screen.getByRole('button', { name: /Excluir postura de 2026-11-15/ }))
    const dialog = screen.getByRole('dialog', { name: 'Excluir lançamento de postura?' })
    expect(dialog).toHaveTextContent('Esta ação não pode ser desfeita.')
    const cancel = within(dialog).getByRole('button', { name: 'Cancelar' })
    expect(cancel).toHaveFocus()
    expect(screen.getByText('lançamentos')).toHaveTextContent(/^1\s*lançamentos$/)
    fireEvent.click(cancel)
    expect(httpRequest).not.toHaveBeenCalledWith(expect.anything(), expect.objectContaining({ method: 'DELETE' }))
    expect(screen.getByRole('textbox', { name: 'Observação' })).toHaveValue('Rascunho da postura')
    fireEvent.click(screen.getByRole('button', { name: /Excluir postura de 2026-11-15/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar exclusão' }))
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith('/animais/animal-1/producoes-ovos/egg-1', { method: 'DELETE', authenticated: true }))
    expect(await screen.findByText('Nenhuma produção de ovos registrada.')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Lançamento de postura excluído.')
    expect(screen.getByText('lançamentos')).toHaveTextContent(/^0\s*lançamentos$/)
    expect(screen.getByRole('textbox', { name: 'Observação' })).toHaveValue('')
  })

  it('informa conflito sem remover postura vinculada e oculta a ação no modo leitura', async () => {
    vi.mocked(httpRequest).mockImplementation(async (_path, options) => {
      if (options?.method === 'DELETE') throw new HttpError(409)
      return page()
    })
    const view = show()
    fireEvent.click(await screen.findByRole('button', { name: /Excluir postura de 2026-11-15/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar exclusão' }))
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Com ponte vitalícia')
    expect(alert).toHaveTextContent('administrador para revisar a prévia de tratamento')
    expect(alert).toHaveTextContent('Compensar entrada integral')
    expect(alert).toHaveTextContent('Invalidar associação documental')
    expect(alert).toHaveTextContent('Conferências e provas')
    expect(alert).toHaveTextContent('A postura permanece no histórico após o tratamento.')
    expect(screen.getByText('Registro acidental')).toBeInTheDocument()
    view.unmount()
    show(true)
    expect(await screen.findByText('Registro acidental')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Excluir postura/ })).not.toBeInTheDocument()
  })

  it('volta à última página existente se a página atual sumir durante a exclusão', async () => {
    const second = { ...entry, id: 'egg-2', dataPostura: '2026-11-14', observacao: 'Outra postura' }
    const firstPageEntry = { ...entry, id: 'egg-3', dataPostura: '2026-11-16', observacao: 'Postura da primeira página' }
    let deleted = false
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (options?.method === 'DELETE') { deleted = true; return undefined }
      if (path === '/animais/animal-1/producoes-ovos?page=2&pageSize=100') return deleted ? page([], 2, 1, 100) : page([entry, second], 2, 2, 102)
      return page([firstPageEntry], 1, deleted ? 1 : 2, deleted ? 100 : 102)
    })
    show()
    fireEvent.click(await screen.findByRole('button', { name: 'Editar' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Observação' }), { target: { value: 'Rascunho da primeira página' } })
    fireEvent.click(screen.getByRole('button', { name: 'Próximas produções' }))
    fireEvent.click(await screen.findByRole('button', { name: /Excluir postura de 2026-11-15/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar exclusão' }))
    expect(await screen.findByText(firstPageEntry.observacao)).toBeInTheDocument()
    expect(screen.getByText('lançamentos')).toHaveTextContent(/^100\s*lançamentos$/)
    expect(screen.getByRole('textbox', { name: 'Observação' })).toHaveValue('Rascunho da primeira página')
  })
})
