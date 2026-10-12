import { fireEvent, render, screen, within } from '@testing-library/react'
import { transferableAbortController } from 'node:util'
import { createMemoryRouter, Link, Outlet, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { UnsavedChangesProvider } from '../../../shared/navigation/UnsavedChangesProvider'
import { api, save, type Photo } from './api'
import { ImageGallery } from './ImageGallery'

vi.mock('./api', () => ({ api: vi.fn(), save: vi.fn(), message: () => 'Não foi possível salvar.' }))
const photo: Photo = { id: 'photo-1', legenda: 'Foto original', dataCaptura: '2026-10-01', ordem: 1, ativa: true, representativa: false, thumbnailPath: '', conteudoPath: '' }
const second: Photo = { ...photo, id: 'photo-2', legenda: 'Segunda foto', ordem: 2 }
afterEach(() => vi.unstubAllGlobals())
beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  vi.mocked(save).mockResolvedValue({})
})
function app() {
  const router = createMemoryRouter([{ element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [
    { path: '/animais/animal-1/editar', element: <><Link to="/animais">Voltar para Animais</Link><ImageGallery kind="animais" ownerId="animal-1" /></> },
    { path: '/animais', element: <h1>Animais</h1> },
  ] }], { initialEntries: ['/animais/animal-1/editar'] })
  render(<RouterProvider router={router} />)
}

describe('image metadata drafts', () => {
  it('retains metadata across pagination and filtering and guards leaving while the draft is hidden', async () => {
    vi.mocked(api).mockImplementation(async url => url.endsWith('/preferencial') ? { origem: 'nenhuma', imagem: null } : { items: [url.includes('page=2') ? second : photo], page: url.includes('page=2') ? 2 : 1, pageSize: 50, totalItems: 51, totalPages: 2 })
    app()
    const caption = await screen.findByLabelText('Legenda')
    fireEvent.change(caption, { target: { value: 'Legenda pendente' } })
    fireEvent.click(screen.getByRole('button', { name: 'Próximas imagens' }))
    await screen.findByText('Segunda foto')
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Animais' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Continuar editando' }))
    fireEvent.click(screen.getByRole('button', { name: 'Imagens anteriores' }))
    expect(await screen.findByLabelText('Legenda')).toHaveValue('Legenda pendente')
    fireEvent.change(screen.getByLabelText('Exibir imagens'), { target: { value: 'false' } })
    expect(await screen.findByLabelText('Legenda')).toHaveValue('Legenda pendente')
    expect(save).not.toHaveBeenCalled()
  })

  it('clears only the successfully saved image and retains drafts of another image through refresh', async () => {
    vi.mocked(api).mockImplementation(async url => url.endsWith('/preferencial') ? { origem: 'nenhuma', imagem: null } : { items: [photo, second], page: 1, pageSize: 50, totalItems: 2, totalPages: 1 })
    app()
    await screen.findByText('Segunda foto')
    const articles = [...document.querySelectorAll('article')]
    fireEvent.change(within(articles[0]).getByLabelText('Legenda'), { target: { value: 'Manter primeiro rascunho' } })
    fireEvent.change(within(articles[1]).getByLabelText('Legenda'), { target: { value: 'Salvar segunda' } })
    fireEvent.click(within(articles[1]).getByRole('button', { name: 'Salvar metadados' }))
    await screen.findByText(/Metadados não salvos em 1 imagem/)
    expect(save).toHaveBeenCalledWith('/animais/animal-1/imagens/photo-2/metadados', { legenda: 'Salvar segunda', dataCaptura: '2026-10-01' })
    const first = (await screen.findByText('Foto original')).closest('article')!
    expect(within(first).getByLabelText('Legenda')).toHaveValue('Manter primeiro rascunho')
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Animais' }))
    expect(await screen.findByRole('dialog', { name: 'Descartar alterações?' })).toBeInTheDocument()
  })
})
