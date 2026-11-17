import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../../shared/http/httpClient'
import { HttpError } from '../../../shared/http/httpErrors'
import { AnimalEggProductionPanel } from './AnimalEggProductionPanel'

vi.mock('../../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))

const entry = { id: 'egg-1', dataPostura: '2026-11-15', pesoGramas: 12.5, observacao: 'Registro acidental' }
const page = (items = [entry]) => ({ items, page: 1, totalPages: 1, totalItems: items.length, metricas: { totalLancamentos: items.length, pesoMedioGramas: 12.5, pesoMinimoGramas: 12.5, pesoMaximoGramas: 12.5, pesoPadraoGramas: null, diasAtePesoPadrao: null } })
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
    fireEvent.click(await screen.findByRole('button', { name: /Excluir postura de 2026-11-15/ }))
    expect(screen.getByRole('alertdialog')).toHaveTextContent('Esta ação não pode ser desfeita.')
    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(httpRequest).not.toHaveBeenCalledWith(expect.anything(), expect.objectContaining({ method: 'DELETE' }))
    fireEvent.click(screen.getByRole('button', { name: /Excluir postura de 2026-11-15/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar exclusão' }))
    await waitFor(() => expect(httpRequest).toHaveBeenCalledWith('/animais/animal-1/producoes-ovos/egg-1', { method: 'DELETE', authenticated: true }))
    expect(await screen.findByText('Nenhuma produção de ovos registrada.')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Lançamento de postura excluído.')
  })

  it('informa conflito sem remover postura vinculada e oculta a ação no modo leitura', async () => {
    vi.mocked(httpRequest).mockImplementation(async (_path, options) => {
      if (options?.method === 'DELETE') throw new HttpError(409)
      return page()
    })
    const view = show()
    fireEvent.click(await screen.findByRole('button', { name: /Excluir postura de 2026-11-15/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar exclusão' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('vinculado à ponte de ovos')
    expect(screen.getByText('Registro acidental')).toBeInTheDocument()
    view.unmount()
    show(true)
    expect(await screen.findByText('Registro acidental')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Excluir postura/ })).not.toBeInTheDocument()
  })
})
