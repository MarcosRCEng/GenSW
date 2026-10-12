import { useState } from 'react'
import { transferableAbortController } from 'node:util'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { createMemoryRouter, Link, Outlet, RouterProvider, useLocation, useNavigate } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { UnsavedChangesProvider } from './UnsavedChangesProvider'
import { useConfirmDiscard, useUnsavedChanges } from './useUnsavedChanges'

const showDescriptor = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'showModal')
const closeDescriptor = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'close')
beforeEach(() => {
  // React Router creates native Node Requests; their signals must use that realm too.
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
})
afterEach(() => {
  vi.unstubAllGlobals()
  if (showDescriptor) Object.defineProperty(HTMLDialogElement.prototype, 'showModal', showDescriptor)
  else Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal')
  if (closeDescriptor) Object.defineProperty(HTMLDialogElement.prototype, 'close', closeDescriptor)
  else Reflect.deleteProperty(HTMLDialogElement.prototype, 'close')
})

function Editor() {
  const [name, setName] = useState('Inicial')
  const [closed, setClosed] = useState(false)
  const { markSaved, confirmDiscard } = useUnsavedChanges(name !== 'Inicial')
  const confirmSession = useConfirmDiscard()
  const navigate = useNavigate()
  const location = useLocation()
  return <>
    <h1>Editor</h1>
    <label>Nome<input value={name} onChange={event => setName(event.target.value)} /></label>
    <Link to="/destino">Sair da página</Link>
    <button onClick={() => { markSaved(); void navigate('/destino', { state: { saved: true } }) }}>Salvar e retornar</button>
    <button onClick={() => void navigate('/editor', { replace: true, state: { list: { path: '/editor', filters: { page: 2 } } } })}>Lembrar contexto</button>
    <button onClick={() => confirmDiscard(() => setClosed(true))}>Fechar editor local</button>
    <button onClick={() => confirmSession(() => void navigate('/login'))}>Sair da sessão</button>
    {closed && <p>Editor local fechado</p>}
    <p>Estado: {location.state?.saved ? 'salvo' : location.state?.list?.filters.page ?? 'padrão'}</p>
  </>
}

function start(initialEntries = ['/editor']) {
  const router = createMemoryRouter([{ path: '/', element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>, children: [
    { path: 'editor', element: <Editor /> },
    { path: 'destino', element: <h1>Destino</h1> },
    { path: 'login', element: <h1>Login</h1> },
  ] }], { initialEntries, initialIndex: initialEntries.length - 1 })
  render(<RouterProvider router={router} />)
  fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Rascunho' } })
  return router
}

describe('Proteção compartilhada de edição', () => {
  it('permanece no rascunho e restaura foco ao cancelar a navegação', async () => {
    start()
    const link = screen.getByRole('link', { name: 'Sair da página' })
    link.focus(); fireEvent.click(link)
    const stay = await screen.findByRole('button', { name: 'Continuar editando' })
    expect(stay).toHaveFocus()
    fireEvent.click(stay)
    expect(screen.getByLabelText('Nome')).toHaveValue('Rascunho')
    await waitFor(() => expect(link).toHaveFocus())
    expect(screen.queryByRole('heading', { name: 'Destino' })).not.toBeInTheDocument()
  })

  it('descarta somente após confirmação e prossegue ao destino solicitado', async () => {
    start()
    fireEvent.click(screen.getByRole('link', { name: 'Sair da página' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar e sair' }))
    expect(await screen.findByRole('heading', { name: 'Destino' })).toBeInTheDocument()
  })

  it('bloqueia Voltar no histórico e mantém a entrada de edição ao continuar', async () => {
    const router = start(['/destino', '/editor'])
    await act(async () => { await router.navigate(-1) })
    fireEvent.click(await screen.findByRole('button', { name: 'Continuar editando' }))
    expect(screen.getByLabelText('Nome')).toHaveValue('Rascunho')
    expect(router.state.location.pathname).toBe('/editor')
  })

  it('não bloqueia gravação bem sucedida nem replace de contexto na mesma URL', async () => {
    start()
    fireEvent.click(screen.getByRole('button', { name: 'Lembrar contexto' }))
    expect(await screen.findByText('Estado: 2')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar e retornar' }))
    expect(await screen.findByRole('heading', { name: 'Destino' })).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('protege fechamento local sem navegar ou descartar ao usar Escape', async () => {
    start()
    fireEvent.click(screen.getByRole('button', { name: 'Fechar editor local' }))
    const modal = await screen.findByRole('dialog')
    fireEvent(modal, new Event('cancel', { cancelable: true }))
    expect(screen.getByLabelText('Nome')).toHaveValue('Rascunho')
    expect(screen.queryByText('Editor local fechado')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Fechar editor local' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar e sair' }))
    expect(screen.getByText('Editor local fechado')).toBeInTheDocument()
  })

  it('avisa em saída/reload somente enquanto há alterações', async () => {
    start()
    const dirtyEvent = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(dirtyEvent)
    expect(dirtyEvent.defaultPrevented).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Salvar e retornar' }))
    await screen.findByRole('heading', { name: 'Destino' })
    const savedEvent = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(savedEvent)
    expect(savedEvent.defaultPrevented).toBe(false)
  })

  it('confirma saída da sessão antes de executar o comando', async () => {
    start()
    fireEvent.click(screen.getByRole('button', { name: 'Sair da sessão' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Continuar editando' }))
    expect(screen.getByLabelText('Nome')).toHaveValue('Rascunho')
    fireEvent.click(screen.getByRole('button', { name: 'Sair da sessão' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar e sair' }))
    expect(await screen.findByRole('heading', { name: 'Login' })).toBeInTheDocument()
  })
})
