import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { useState } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError, SessionExpiredError } from '../../shared/http/httpErrors'
import { getAnimalById, listAnimals } from '../animals/services/animalsService'
import type { Animal, AnimalsPage } from '../animals/types/animals'
import { BreedingAnimalSelector } from './BreedingAnimalSelector'

vi.mock('../animals/services/animalsService', () => ({ listAnimals: vi.fn(), getAnimalById: vi.fn() }))

// More than 100 records of EACH sex; none of these fixtures belong to the pilot.
const animal = (index: number, sexo: 1 | 2 = 1): Animal => ({
  id: `synthetic-${sexo}-${index}`, codigoInterno: `TEST-${sexo === 1 ? 'M' : 'F'}-${String(index).padStart(3, '0')}`,
  nome: `Animal sintético ${index}`, sexo, ativo: true, especieId: 'test-species', racaId: null,
  variedadeId: null, dataNascimento: null, escopo: 1, createdAtUtc: '', updatedAtUtc: '',
  especie: { id: 'test-species', nomeComum: 'Teste', ativo: true }, raca: null, variedade: null,
})
const records = Array.from({ length: 101 }, (_, index) => [animal(index + 1, 1), animal(index + 1, 2)]).flat()
const pageOf = (items: Animal[], page = 1, totalItems = items.length): AnimalsPage => ({ items, page, pageSize: 10, totalItems, totalPages: Math.ceil(totalItems / 10) })
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no })
  return { promise, resolve, reject }
}
function Controlled({ sexo = 1 }: { sexo?: 1 | 2 }) {
  const [value, setValue] = useState('')
  return <BreedingAnimalSelector id="animal" label={sexo === 1 ? 'Macho' : 'Fêmea'} sexo={sexo} value={value} onChange={setValue} />
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(listAnimals).mockImplementation(async ({ page = 1, sexo, search = '' } = {}) => {
    const matching = records.filter(item => item.sexo === sexo && `${item.codigoInterno} ${item.nome}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()))
    return pageOf(matching.slice((page - 1) * 10, page * 10), page, matching.length)
  })
  vi.mocked(getAnimalById).mockImplementation(async id => records.find(item => item.id === id) ?? animal(999))
})

describe('BreedingAnimalSelector', () => {
  it.each([1, 2] as const)('finds and selects the 101st record of sex %s through server search, keeping its label on another search', async sexo => {
    render(<Controlled sexo={sexo} />)
    const label = sexo === 1 ? 'Macho' : 'Fêmea'
    const code = `TEST-${sexo === 1 ? 'M' : 'F'}-101`
    const select = screen.getByLabelText(label)
    await screen.findByRole('option', { name: /TEST-.*-001/ })
    expect(screen.queryByRole('option', { name: new RegExp(code) })).not.toBeInTheDocument()
    const search = screen.getByLabelText(`Buscar ${label.toLocaleLowerCase()} por código ou nome`)
    fireEvent.change(search, { target: { value: code } })
    await screen.findByRole('option', { name: new RegExp(code) })
    select.focus()
    fireEvent.change(select, { target: { value: `synthetic-${sexo}-101` } })
    expect(select).toHaveFocus()
    expect(select).toHaveValue(`synthetic-${sexo}-101`)
    fireEvent.change(search, { target: { value: 'sintético 2' } })
    await screen.findByRole('option', { name: /TEST-.*-002/ })
    expect(select).toHaveValue(`synthetic-${sexo}-101`)
    expect(screen.getByRole('option', { name: new RegExp(`${code}.*selecionado`) })).toBeInTheDocument()
    expect(listAnimals).toHaveBeenCalledWith({ page: 1, pageSize: 10, sexo, search: code, sortBy: 'codigoInterno', sortDirection: 'asc' })
    for (const [params] of vi.mocked(listAnimals).mock.calls) expect(params).not.toHaveProperty('ativo')
  })

  it.each([1, 2] as const)('reaches the 101st record of sex %s through bounded pages and preserves selection on previous page', async sexo => {
    render(<Controlled sexo={sexo} />)
    const label = sexo === 1 ? 'Macho' : 'Fêmea'
    const nav = within(screen.getByRole('navigation', { name: `Paginação de animais: ${label}` }))
    for (let page = 1; page <= 10; page++) {
      await nav.findByText(`Página ${page} de 11`)
      fireEvent.click(nav.getByRole('button', { name: /Próximos animais:/ }))
    }
    await nav.findByText('Página 11 de 11')
    const select = screen.getByLabelText(label)
    expect(screen.getAllByRole('option')).toHaveLength(2)
    fireEvent.change(select, { target: { value: `synthetic-${sexo}-101` } })
    fireEvent.click(nav.getByRole('button', { name: /Animais anteriores:/ }))
    await nav.findByText('Página 10 de 11')
    expect(select).toHaveValue(`synthetic-${sexo}-101`)
    expect(screen.getAllByRole('option')).toHaveLength(12)
    expect(listAnimals).toHaveBeenCalledTimes(12)
  })

  it('preserves an inactive historical reference with another sex, resolving it by ID separately from candidates', async () => {
    const historical = { ...animal(900, 2), ativo: false }
    vi.mocked(getAnimalById).mockResolvedValue(historical)
    const onChange = vi.fn()
    render(<BreedingAnimalSelector id="male" label="Macho" sexo={1} value={historical.id} onChange={onChange} />)
    const option = await screen.findByRole('option', { name: /TEST-F-900.*inativo.*sexo diferente.*selecionado/ })
    expect(option).toHaveValue(historical.id)
    expect(screen.getByLabelText('Macho')).toHaveValue(historical.id)
    fireEvent.change(screen.getByLabelText('Buscar macho por código ou nome'), { target: { value: 'TEST-M-101' } })
    await screen.findByRole('option', { name: /TEST-M-101/ })
    expect(screen.getByLabelText('Macho')).toHaveValue(historical.id)
    expect(getAnimalById).toHaveBeenCalledWith(historical.id)
    expect(onChange).not.toHaveBeenCalled()
  })

  it('keeps a reference ID after detail failure and retries visibly without changing the link', async () => {
    const selected = animal(900)
    vi.mocked(getAnimalById).mockRejectedValueOnce(new HttpError(404)).mockResolvedValueOnce(selected)
    const onChange = vi.fn()
    render(<BreedingAnimalSelector id="male" label="Macho" sexo={1} value={selected.id} onChange={onChange} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar o animal selecionado. HTTP 404.')
    expect(screen.getByLabelText('Macho')).toHaveValue(selected.id)
    expect(screen.getByRole('option', { name: new RegExp(selected.id) })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar carregar animal selecionado: Macho' }))
    await screen.findByRole('option', { name: /TEST-M-900.*selecionado/ })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(onChange).not.toHaveBeenCalled()
  })

  it('distinguishes loading, empty results and failure, retrying the same search and page', async () => {
    const failed = deferred<AnimalsPage>()
    vi.mocked(listAnimals).mockResolvedValueOnce(pageOf([animal(1)], 1, 11)).mockReturnValueOnce(failed.promise).mockResolvedValueOnce(pageOf([animal(11)], 2, 11))
    render(<Controlled />)
    await screen.findByText('Página 1 de 2')
    fireEvent.click(screen.getByRole('button', { name: /Próximos animais:/ }))
    expect(screen.getByRole('status')).toHaveTextContent('Carregando animais…')
    expect(screen.queryByRole('option', { name: /TEST-M-001/ })).not.toBeInTheDocument()
    await act(async () => failed.reject(new Error('offline')))
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível carregar os animais.')
    expect(screen.queryByText('Nenhum animal encontrado.')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente: Macho' }))
    await screen.findByRole('option', { name: /TEST-M-011/ })
    expect(listAnimals).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, search: undefined }))
    fireEvent.change(screen.getByLabelText('Buscar macho por código ou nome'), { target: { value: 'nothing' } })
    await screen.findByText('Nenhum animal encontrado.')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(listAnimals).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, search: 'nothing' }))
  })

  it.each([401, 403])('shows HTTP %s for candidate and selected-reference errors without clearing the ID', async status => {
    vi.mocked(listAnimals).mockRejectedValue(new HttpError(status))
    vi.mocked(getAnimalById).mockRejectedValue(new HttpError(status))
    const onChange = vi.fn()
    render(<BreedingAnimalSelector id="male" label="Macho" sexo={1} value="historical" onChange={onChange} />)
    await waitFor(() => expect(screen.getAllByRole('alert')).toHaveLength(2))
    for (const alert of screen.getAllByRole('alert')) expect(alert).toHaveTextContent(`HTTP ${status}.`)
    expect(screen.getByLabelText('Macho')).toHaveValue('historical')
    expect(onChange).not.toHaveBeenCalled()
  })

  it('shows the expired session reported by the authenticated HTTP client after a failed 401 refresh', async () => {
    vi.mocked(listAnimals).mockRejectedValue(new SessionExpiredError())
    render(<Controlled />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Sua sessão expirou; entre novamente.')
    expect(screen.queryByText('Nenhum animal encontrado.')).not.toBeInTheDocument()
  })

  it('uses the current page to identify a selection without requesting its detail or changing its ID', async () => {
    const onChange = vi.fn()
    render(<BreedingAnimalSelector id="male" label="Macho" sexo={1} value={animal(1).id} onChange={onChange} />)
    await screen.findByRole('option', { name: /TEST-M-001.*selecionado/ })
    expect(getAnimalById).not.toHaveBeenCalled()
    expect(onChange).not.toHaveBeenCalled()
    expect(screen.getByLabelText('Macho')).toHaveValue(animal(1).id)
  })

  it('prevents implicit form submission only for Enter in search, preserving native selection keys and text composition', async () => {
    const onChange = vi.fn(), onSubmit = vi.fn()
    render(<form onSubmit={onSubmit}><BreedingAnimalSelector id="male" label="Macho" sexo={1} value={animal(1).id} onChange={onChange} /><button type="submit">Salvar</button></form>)
    await screen.findByRole('option', { name: /TEST-M-001.*selecionado/ })
    const search = screen.getByLabelText('Buscar macho por código ou nome')
    fireEvent.change(search, { target: { value: 'TEST-M-101' } })
    await screen.findByRole('option', { name: /TEST-M-101/ })
    expect(fireEvent.keyDown(search, { key: 'Enter' })).toBe(false)
    expect(fireEvent.keyDown(search, { key: 'ArrowDown' })).toBe(true)
    expect(fireEvent.keyDown(search, { key: 'Enter', isComposing: true })).toBe(true)
    expect(fireEvent.keyDown(screen.getByLabelText('Macho'), { key: 'Enter' })).toBe(true)
    expect(screen.getByLabelText('Macho')).toHaveValue(animal(1).id)
    expect(onChange).not.toHaveBeenCalled()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('discards older success and failure in A → B → A, even when an old key matches again', async () => {
    const firstA = deferred<AnimalsPage>(), b = deferred<AnimalsPage>(), latestA = deferred<AnimalsPage>()
    vi.mocked(listAnimals).mockResolvedValueOnce(pageOf([animal(1)])).mockReturnValueOnce(firstA.promise).mockReturnValueOnce(b.promise).mockReturnValueOnce(latestA.promise)
    render(<Controlled />)
    await screen.findByRole('option', { name: /TEST-M-001/ })
    const search = screen.getByLabelText('Buscar macho por código ou nome')
    for (const value of ['A', 'B', 'A']) fireEvent.change(search, { target: { value } })
    await act(async () => firstA.resolve(pageOf([animal(2)])))
    await act(async () => b.reject(new Error('old B')))
    expect(screen.getByRole('status')).toHaveTextContent('Carregando animais…')
    expect(screen.queryByRole('option', { name: /TEST-M-002/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    await act(async () => latestA.resolve(pageOf([animal(3)])))
    expect(screen.getByRole('option', { name: /TEST-M-003/ })).toBeInTheDocument()
  })

  it('discards candidate and detail responses when sex, route context, or selected ID changes', async () => {
    const candidate = deferred<AnimalsPage>(), oldDetail = deferred<Animal>()
    vi.mocked(listAnimals).mockReturnValueOnce(candidate.promise).mockResolvedValue(pageOf([animal(2, 2)]))
    vi.mocked(getAnimalById).mockReturnValueOnce(oldDetail.promise).mockResolvedValue(animal(901, 2))
    const props = { id: 'animal', label: 'Macho', sexo: 1 as const, value: 'historical', onChange: vi.fn(), contextKey: 'route-a' }
    const { rerender } = render(<BreedingAnimalSelector {...props} />)
    await act(async () => candidate.resolve(pageOf([animal(1)])))
    await waitFor(() => expect(getAnimalById).toHaveBeenCalledWith('historical'))
    rerender(<BreedingAnimalSelector {...props} sexo={2} label="Fêmea" value="synthetic-2-901" contextKey="route-b" />)
    await screen.findByRole('option', { name: /TEST-F-901/ })
    await act(async () => oldDetail.resolve(animal(900)))
    expect(screen.queryByRole('option', { name: /TEST-M-900/ })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Fêmea')).toHaveValue('synthetic-2-901')
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('discards a pending candidate page after changing sex and route context', async () => {
    const old = deferred<AnimalsPage>()
    vi.mocked(listAnimals).mockReturnValueOnce(old.promise).mockResolvedValueOnce(pageOf([animal(2, 2)]))
    const props = { id: 'animal', label: 'Macho', sexo: 1 as const, value: '', onChange: vi.fn(), contextKey: 'route-a' }
    const { rerender } = render(<BreedingAnimalSelector {...props} />)
    rerender(<BreedingAnimalSelector {...props} sexo={2} label="Fêmea" contextKey="route-b" />)
    await screen.findByRole('option', { name: /TEST-F-002/ })
    await act(async () => old.resolve(pageOf([animal(1)])))
    expect(screen.queryByRole('option', { name: /TEST-M-001/ })).not.toBeInTheDocument()
    expect(screen.getByRole('option', { name: /TEST-F-002/ })).toBeInTheDocument()
  })

  it('discards an old detail when the same ID is selected again in A → B → A', async () => {
    const firstA = deferred<Animal>(), b = deferred<Animal>(), latestA = deferred<Animal>()
    vi.mocked(getAnimalById).mockReturnValueOnce(firstA.promise).mockReturnValueOnce(b.promise).mockReturnValueOnce(latestA.promise)
    const props = { id: 'male', label: 'Macho', sexo: 1 as const, value: 'A', onChange: vi.fn() }
    const { rerender } = render(<BreedingAnimalSelector {...props} />)
    await waitFor(() => expect(getAnimalById).toHaveBeenCalledWith('A'))
    rerender(<BreedingAnimalSelector {...props} value="B" />)
    await waitFor(() => expect(getAnimalById).toHaveBeenCalledWith('B'))
    rerender(<BreedingAnimalSelector {...props} />)
    await waitFor(() => expect(getAnimalById).toHaveBeenCalledTimes(3))
    await act(async () => firstA.resolve({ ...animal(1), id: 'A' }))
    await act(async () => b.reject(new HttpError(404)))
    expect(screen.queryByRole('option', { name: /TEST-M-001.*selecionado/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    await act(async () => latestA.resolve({ ...animal(3), id: 'A' }))
    expect(screen.getByRole('option', { name: /TEST-M-003.*selecionado/ })).toBeInTheDocument()
    expect(screen.getByLabelText('Macho')).toHaveValue('A')
  })

  it('moves focus before disabling page and retry buttons, preserving later input focus', async () => {
    const page = deferred<AnimalsPage>(), retry = deferred<AnimalsPage>()
    vi.mocked(listAnimals).mockResolvedValueOnce(pageOf([animal(1)], 1, 11)).mockReturnValueOnce(page.promise).mockReturnValueOnce(retry.promise)
    render(<Controlled />)
    await screen.findByText('Página 1 de 2')
    const next = screen.getByRole('button', { name: /Próximos animais:/ })
    const summary = screen.getByRole('group', { name: 'Resumo dos animais: Macho' })
    next.focus()
    fireEvent.click(next)
    expect(summary).toHaveFocus()
    expect(next).toBeDisabled()
    await act(async () => page.reject(new Error('offline')))
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente: Macho' }))
    expect(summary).toHaveFocus()
    const search = screen.getByLabelText('Buscar macho por código ou nome')
    search.focus()
    await act(async () => retry.resolve(pageOf([animal(11)], 2, 11)))
    expect(search).toHaveFocus()
    expect(summary).toHaveTextContent('Página 2 de 2')
  })
})
