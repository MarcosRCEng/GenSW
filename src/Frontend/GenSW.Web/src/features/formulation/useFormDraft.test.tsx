import { useState } from 'react'
import { transferableAbortController } from 'node:util'
import { fireEvent, render, screen } from '@testing-library/react'
import { createMemoryRouter, Link, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { UnsavedChangesProvider } from '../../shared/navigation/UnsavedChangesProvider'
import { useFormDraft } from './useFormDraft'

beforeEach(() => vi.stubGlobal('AbortController', transferableAbortController().constructor))
afterEach(() => vi.unstubAllGlobals())

function DraftRetry() {
  const [value, setValue] = useState('Original')
  const [loading, setLoading] = useState(false)
  useFormDraft({ value }, !loading)
  return <><label>Nome<input value={value} onChange={event => setValue(event.target.value)} /></label><button onClick={() => setLoading(true)}>Recarregar</button>{loading && <p role="status">Consultando nova revisão…</p>}<Link to="/destino">Cancelar edição</Link></>
}

it('mantém a proteção do preenchimento enquanto a consulta de retry está em curso', async () => {
  const router = createMemoryRouter([{ path: '/', element: <UnsavedChangesProvider><DraftRetry /></UnsavedChangesProvider> }, { path: '/destino', element: <h1>Destino</h1> }])
  render(<RouterProvider router={router} />)
  fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Preenchimento preservado' } })
  fireEvent.click(screen.getByRole('button', { name: 'Recarregar' }))
  await screen.findByRole('status')
  fireEvent.click(screen.getByRole('link', { name: 'Cancelar edição' }))
  await screen.findByRole('dialog', { name: 'Descartar alterações?' })
  expect(router.state.location.pathname).toBe('/')
  expect(screen.getByLabelText('Nome')).toHaveValue('Preenchimento preservado')
  fireEvent.click(screen.getByRole('button', { name: 'Continuar editando' }))
  expect(screen.getByRole('status')).toHaveTextContent('Consultando nova revisão')
})
