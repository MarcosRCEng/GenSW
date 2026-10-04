import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useNavigate } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { BreedDetailsPage } from '../../features/breeds/pages/BreedDetailsPage'
import { BreedsListPage } from '../../features/breeds/pages/BreedsListPage'
import { PeopleDetailsPage } from '../../features/people/pages/PeopleDetailsPage'
import { PeopleListPage } from '../../features/people/pages/PeopleListPage'
import { SpeciesDetailsPage } from '../../features/species/pages/SpeciesDetailsPage'
import { SpeciesListPage } from '../../features/species/pages/SpeciesListPage'
import { VarietyDetailsPage } from '../../features/varieties/pages/VarietyDetailsPage'
import { VarietiesListPage } from '../../features/varieties/pages/VarietiesListPage'
import { httpRequest } from '../http/httpClient'
import { HttpError } from '../http/httpErrors'

vi.mock('../http/httpClient', () => ({ httpRequest: vi.fn() }))

const timestamps = { createdAtUtc: '2026-09-01T12:00:00Z', updatedAtUtc: '2026-09-01T12:00:00Z' }
const person = { ...timestamps, id: 'person-1', nome: 'Pessoa de teste', nomeFantasia: 'Nome público', tipoPessoa: 2, ativo: false }
const species = { ...timestamps, id: 'species-1', nomeComum: 'Espécie de teste', nomeCientifico: 'Species ficticia', ovipara: true, pesoPadraoOvoGramas: 25.5, ativo: false }
const breed = { ...timestamps, id: 'breed-1', especieId: species.id, nome: 'Raça de teste', ativo: false, especie: species }
const variety = { ...timestamps, id: 'variety-1', especieId: species.id, nome: 'Variedade de teste', ativo: false, especie: species }
const cases = [
  { path: '/pessoas', record: person, name: person.nome, list: PeopleListPage, detail: PeopleDetailsPage },
  { path: '/especies', record: species, name: species.nomeComum, list: SpeciesListPage, detail: SpeciesDetailsPage },
  { path: '/racas', record: breed, name: breed.nome, list: BreedsListPage, detail: BreedDetailsPage },
  { path: '/variedades', record: variety, name: variety.nome, list: VarietiesListPage, detail: VarietyDetailsPage },
]

function page(items: unknown[], pageNumber = 1) {
  return { items, page: pageNumber, pageSize: 25, totalPages: 2, totalItems: 26 }
}

function BrowserBack() {
  const navigate = useNavigate()
  return <button onClick={() => navigate(-1)}>Voltar no navegador</button>
}

function renderRegistry(entry: typeof cases[number], direct = true) {
  const List = entry.list
  const Detail = entry.detail
  return render(<MemoryRouter initialEntries={[direct ? `${entry.path}/${entry.record.id}` : entry.path]}>
    <BrowserBack />
    <Routes>
      <Route path={entry.path} element={<List />} />
      <Route path={`${entry.path}/:id`} element={<Detail />} />
    </Routes>
  </MemoryRouter>)
}

function expectReadOnlyRequests() {
  for (const [, options] of vi.mocked(httpRequest).mock.calls) {
    expect(options?.method ?? 'GET').toBe('GET')
    expect(options?.authenticated).toBe(true)
    expect(options?.body).toBeUndefined()
  }
}

beforeEach(() => {
  vi.mocked(httpRequest).mockReset()
  vi.mocked(httpRequest).mockImplementation(async (path) => {
    if (path.includes('/imagens/preferencial')) return { origem: 'nenhuma', imagem: null } as never
    if (path.includes('/imagens?')) return { ...page([]), totalItems: 0, totalPages: 0 } as never
    for (const entry of cases) {
      if (path === `${entry.path}/${entry.record.id}`) return entry.record as never
      if (path.startsWith(`${entry.path}?`)) {
        const query = new URLSearchParams(path.split('?')[1])
        const requestedPage = Number(query.get('page') ?? '1')
        if (entry.path === '/especies' && query.get('pageSize') === '100') return { ...page([species]), totalItems: 1, totalPages: 1 } as never
        return page([entry.record], requestedPage) as never
      }
    }
    throw new Error(`Consulta inesperada: ${path}`)
  })
})

describe('consulta dos cadastros básicos', () => {
  it.each(cases)('abre $path diretamente por ID e consulta inativo sem gravar', async (entry) => {
    const { container } = renderRegistry(entry)
    expect(screen.getByRole('status')).toHaveTextContent('Carregando detalhes')
    expect(await screen.findByText(entry.name)).toBeInTheDocument()
    expect(screen.getByText('Inativo')).toBeInTheDocument()
    expect(httpRequest).toHaveBeenCalledWith(`${entry.path}/${entry.record.id}`, { authenticated: true })
    expect(container.querySelector('dl')).toBeInTheDocument()
    expect(container.querySelector('form')).not.toBeInTheDocument()
    expect(container.querySelector('input')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1 })).toHaveFocus()
    if (entry.path === '/pessoas') {
      expect(screen.getByText('Pessoa jurídica')).toBeInTheDocument()
      expect(screen.getByText('Nome público')).toBeInTheDocument()
      expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument()
    } else {
      expect(screen.getByRole('link', { name: 'Editar' })).toHaveAttribute('href', `${entry.path}/${entry.record.id}/editar`)
    }
    if (entry.path === '/racas' || entry.path === '/variedades') expect(screen.getByText('Espécie de teste (inativa)')).toBeInTheDocument()
    if (entry.path === '/especies') expect(screen.getByText('25,5 g')).toBeInTheDocument()
    if (entry.path === '/variedades') await screen.findByText('Nenhuma imagem ativa.')
    expectReadOnlyRequests()
  })

  it.each(cases)('oferece Visualizar antes das demais ações em $path e abre o registro selecionado', async (entry) => {
    renderRegistry(entry, false)
    await screen.findByText(entry.name)
    const row = screen.getByText(entry.name).closest('tr')!
    const view = within(row).getByRole('link', { name: 'Visualizar' })
    expect(view).toHaveAttribute('href', `${entry.path}/${entry.record.id}`)
    expect(within(row).getByRole('button', { name: 'Reativar' })).toBeInTheDocument()
    expect(row.querySelector('a')).toBe(view)
    fireEvent.click(view)
    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Detalhes')
    await screen.findByText(entry.name)
    expectReadOnlyRequests()
  })

  it.each([
    [404, 'Registro não encontrado.'],
    [403, 'Você não tem permissão para visualizar este registro.'],
    [500, 'Não foi possível carregar os detalhes.'],
  ])('trata HTTP %i sem oferecer edição', async (status, message) => {
    vi.mocked(httpRequest).mockRejectedValue(new HttpError(Number(status)))
    renderRegistry(cases[0])
    expect(await screen.findByRole('alert')).toHaveTextContent(String(message))
    expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar para Pessoas' })).toHaveAttribute('href', '/pessoas')
    expectReadOnlyRequests()
  })

  it('permite repetir uma consulta que falhou', async () => {
    vi.mocked(httpRequest).mockRejectedValueOnce(new Error('offline'))
    renderRegistry(cases[0])
    fireEvent.click(await screen.findByRole('button', { name: 'Tentar novamente' }))
    await screen.findByText(person.nome)
    expect(httpRequest).toHaveBeenCalledTimes(2)
    expectReadOnlyRequests()
  })

  it('oferece o fluxo de edição atual para pessoa ativa', async () => {
    vi.mocked(httpRequest).mockResolvedValue({ ...person, ativo: true })
    renderRegistry(cases[0])
    expect(await screen.findByRole('link', { name: 'Editar' })).toHaveAttribute('href', '/pessoas/person-1/editar')
  })

  it.each(['Voltar para Pessoas', 'Voltar no navegador'])('preserva busca, status e página usando %s', async (returnLabel) => {
    renderRegistry(cases[0], false)
    await screen.findByText(person.nome)
    fireEvent.change(screen.getByLabelText('Buscar por nome ou nome fantasia'), { target: { value: 'teste' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Buscar' })).not.toBeDisabled())
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'false' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Próxima' })).not.toBeDisabled())
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    await screen.findByText('Página 2 de 2')
    fireEvent.click(screen.getByRole('link', { name: 'Visualizar' }))
    await screen.findByText(person.nome)
    fireEvent.click(screen.getByRole(returnLabel === 'Voltar no navegador' ? 'button' : 'link', { name: returnLabel }))
    await screen.findByText('Página 2 de 2')
    expect(screen.getByLabelText('Buscar por nome ou nome fantasia')).toHaveValue('teste')
    expect(screen.getByLabelText('Status')).toHaveValue('false')
    expectReadOnlyRequests()
  })
})
