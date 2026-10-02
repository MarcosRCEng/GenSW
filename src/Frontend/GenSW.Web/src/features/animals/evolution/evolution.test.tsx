import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { httpRequest } from '../../../shared/http/httpClient'
import { invalidateSession, registerSessionRefreshHandler, setAccessToken } from '../../../shared/http/sessionManager'
import { AuthenticatedPhoto } from './AuthenticatedPhoto'
import { AnimalFiliationsPanel } from '../filiations/components/AnimalFiliationsPanel'
import { AnimalTree } from './AnimalTree'

afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
const json = (value: unknown) => new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } })

describe('Animal evolution', () => {
  it('refreshes multipart and binary with the same session and preserves FormData', async () => {
    setAccessToken('test-expired')
    const refresh = vi.fn().mockResolvedValue({ accessToken: 'test-renewed', expiresAtUtc: '2030-01-01T00:00:00Z' })
    registerSessionRefreshHandler(refresh)
    const fetcher = vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 })).mockResolvedValueOnce(json({ id: 'image' })).mockResolvedValueOnce(new Response('png-bytes', { headers: { 'Content-Type': 'image/png' } }))
    vi.stubGlobal('fetch', fetcher)
    const body = new FormData(); body.append('arquivo', new Blob(['input']), 'photo.png')
    await httpRequest('/animais/a/imagens', { authenticated: true, method: 'POST', body })
    expect(refresh).toHaveBeenCalledTimes(1)
    expect(fetcher.mock.calls[1][1].body).toBe(body)
    expect(fetcher.mock.calls[1][1].headers.has('Content-Type')).toBe(false)
    expect(fetcher.mock.calls[1][1].headers.get('Authorization')).toBe('Bearer test-renewed')
    const blob = await httpRequest<Blob>('/animais/a/imagens/i/conteudo', { authenticated: true, responseType: 'blob' })
    expect(blob.size).toBe(9)
    expect(blob.type).toBe('image/png')
  })

  it('revokes authenticated image URLs on session invalidation and unmount', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('photo')))
    const create = vi.fn().mockReturnValue('blob:test-photo'), revoke = vi.fn()
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke })
    const view = render(<AuthenticatedPhoto path="/api/v1/animais/a/imagens/i/conteudo" name="Bela" />)
    await screen.findByRole('img', { name: 'Foto de Bela' })
    act(() => { invalidateSession() })
    expect(revoke).toHaveBeenCalledWith('blob:test-photo')
    expect(screen.getByRole('img', { name: 'Bela: sem foto disponível' })).toBeInTheDocument()
    view.unmount()
  })

  it('ignores stale candidate responses and selects by keyboard without a UUID field', async () => {
    let resolveOld!: (response: Response) => void
    const fetcher = vi.fn((url: string) => {
      if (url.includes('/filiacoes')) return Promise.resolve(json([]))
      if (url.includes('search=old')) return new Promise<Response>(resolve => { resolveOld = resolve })
      return Promise.resolve(json({ items: [{ id: 'candidate', codigoInterno: 'PAI-2', nome: 'Novo' }], page: 1, totalPages: 1 }))
    })
    vi.stubGlobal('fetch', fetcher)
    render(<AnimalFiliationsPanel animalId="child" />)
    const input = screen.getByRole('combobox', { name: 'Pesquisar progenitor por nome ou código' })
    fireEvent.change(input, { target: { value: 'old' } })
    await waitFor(() => expect(resolveOld).toBeDefined())
    fireEvent.change(input, { target: { value: 'new' } })
    await screen.findByRole('option', { name: 'PAI-2 — Novo' })
    await act(async () => { resolveOld(json({ items: [{ id: 'stale', codigoInterno: 'OLD', nome: 'Obsoleto' }], totalPages: 1 })) })
    expect(screen.queryByRole('option', { name: /Obsoleto/ })).not.toBeInTheDocument()
    fireEvent.keyDown(input, { key: 'ArrowDown' }); fireEvent.keyDown(input, { key: 'Enter' })
    expect(screen.getByText(/Selecionado: PAI-2/)).toBeInTheDocument()
    expect(screen.queryByPlaceholderText('ID do progenitor')).not.toBeInTheDocument()
  })

  it('limits accumulated graph state and asks to recenter', async () => {
    vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} })
    const node = (id: string) => ({ animalId: id, codigoInterno: id, nome: null, sexo: 1, ativo: true, imagem: null, paiConhecido: false, maeConhecida: false, temAscendentesAdicionais: false, temDescendentesAdicionais: true })
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(json(url.includes('/relacoes')
      ? { raizId: 'root', nos: Array.from({ length: 200 }, (_, i) => node(`new-${i}`)), arestas: [], avisos: [], page: 1, totalPages: 2 }
      : { raizId: 'root', nos: [node('root')], arestas: [], avisos: [], truncada: false }))))
    render(<AnimalTree animalId="root" />)
    fireEvent.click(await screen.findByRole('button', { name: 'Expandir descendentes' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Limite da tela atingido')
    expect(screen.queryByRole('button', { name: 'Selecionar new-0' })).not.toBeInTheDocument()
  })
})
