import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../../../shared/http/httpErrors'
import {
  createAnimalIdentification,
  listAnimalIdentifications,
  setAnimalIdentificationAtivo,
  setAnimalIdentificationPrincipal,
  updateAnimalIdentificationMetadata,
} from '../services/identificationsService'
import type { AnimalIdentification, AnimalIdentificationPage, AnimalIdentificationType } from '../types'
import { AnimalIdentificationsPanel } from './AnimalIdentificationsPanel'

vi.mock('../services/identificationsService', () => ({
  createAnimalIdentification: vi.fn(),
  listAnimalIdentifications: vi.fn(),
  setAnimalIdentificationAtivo: vi.fn(),
  setAnimalIdentificationPrincipal: vi.fn(),
  updateAnimalIdentificationMetadata: vi.fn(),
}))

const active: AnimalIdentification = {
  id: 'identification-active', animalId: 'animal-1', tipo: 3, descricaoTipo: null,
  valor: '985141000000001', principal: true, dataAplicacao: '2026-09-10', observacao: 'Lado esquerdo',
  ativo: true, createdAtUtc: '2026-09-12T12:00:00Z', updatedAtUtc: '2026-09-12T12:00:00Z',
}
const inactive: AnimalIdentification = {
  ...active, id: 'identification-inactive', tipo: 1, valor: 'BR-0042', principal: false,
  dataAplicacao: null, observacao: null, ativo: false,
}

function page(items: AnimalIdentification[], overrides: Partial<AnimalIdentificationPage> = {}): AnimalIdentificationPage {
  return { items, page: 1, pageSize: 10, totalItems: items.length, totalPages: 1, ...overrides }
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(listAnimalIdentifications).mockResolvedValue(page([]))
  vi.mocked(createAnimalIdentification).mockResolvedValue(active)
  vi.mocked(updateAnimalIdentificationMetadata).mockResolvedValue(active)
  vi.mocked(setAnimalIdentificationAtivo).mockResolvedValue(active)
  vi.mocked(setAnimalIdentificationPrincipal).mockResolvedValue(active)
})

describe('AnimalIdentificationsPanel', () => {
  it('shows loading, a retryable load error, and the empty state', async () => {
    let rejectLoad!: (reason: unknown) => void
    vi.mocked(listAnimalIdentifications).mockImplementationOnce(() => new Promise((_resolve, reject) => { rejectLoad = reject }))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    expect(screen.getByRole('status')).toHaveTextContent('Carregando identificações físicas')
    rejectLoad(new Error('offline'))
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar as identificações físicas.')
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText('Nenhuma identificação física cadastrada.')).toBeInTheDocument()
    expect(listAnimalIdentifications).toHaveBeenLastCalledWith('animal-1', { page: 1, pageSize: 10 })
  })

  it('distinguishes principal, active and historical rows and paginates history', async () => {
    vi.mocked(listAnimalIdentifications).mockResolvedValueOnce(page([active, inactive], { totalItems: 12, totalPages: 2 }))
      .mockResolvedValueOnce(page([inactive], { page: 2, totalItems: 12, totalPages: 2 }))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    const principalRow = (await screen.findByText(active.valor)).closest('tr')!
    expect(within(principalRow).getByText('Principal')).toBeInTheDocument()
    expect(within(principalRow).getByRole('button', { name: 'Remover principal' })).toBeEnabled()
    const historicalRow = screen.getByText(inactive.valor).closest('tr')!
    expect(within(historicalRow).getByText('Histórico (inativa)')).toBeInTheDocument()
    expect(within(historicalRow).queryByRole('button', { name: /principal/i })).not.toBeInTheDocument()
    expect(within(historicalRow).getByRole('button', { name: 'Reativar' })).toBeEnabled()
    expect(screen.queryByRole('button', { name: /excluir|remover identificação/i })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima página' }))
    await waitFor(() => expect(listAnimalIdentifications).toHaveBeenLastCalledWith('animal-1', { page: 2, pageSize: 10 }))
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
  })

  it.each([
    ['Anilha', 1, false],
    ['Microchip', 3, false],
    ['Outro', 6, true],
  ] as const)('creates %s with conditional type description and refreshes instead of trusting the mutation response', async (typeName, tipo, needsDescription) => {
    let finishRefresh!: (value: AnimalIdentificationPage) => void
    vi.mocked(listAnimalIdentifications).mockResolvedValueOnce(page([]))
      .mockImplementationOnce(() => new Promise((resolve) => { finishRefresh = resolve }))
    vi.mocked(createAnimalIdentification).mockResolvedValue({ ...active, id: 'response-only', tipo, valor: 'response-only' })
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    await screen.findByText('Nenhuma identificação física cadastrada.')
    fireEvent.click(screen.getByRole('button', { name: 'Adicionar identificação' }))
    fireEvent.change(screen.getByLabelText('Tipo'), { target: { value: String(tipo) } })
    const description = screen.queryByLabelText('Descrição do tipo')
    expect(description === null).toBe(!needsDescription)
    fireEvent.change(screen.getByLabelText('Valor'), { target: { value: '  NOVO-01  ' } })
    if (needsDescription) {
      fireEvent.click(screen.getByRole('button', { name: 'Cadastrar identificação' }))
      expect(await screen.findByText('Informe a descrição do tipo.')).toBeInTheDocument()
      fireEvent.change(screen.getByLabelText('Descrição do tipo'), { target: { value: '  Sensor óptico  ' } })
    }
    fireEvent.change(screen.getByLabelText('Data de aplicação'), { target: { value: '2026-09-11' } })
    fireEvent.change(screen.getByLabelText('Observação'), { target: { value: '  conferida  ' } })
    fireEvent.click(screen.getByRole('checkbox', { name: 'Definir como principal' }))
    fireEvent.click(screen.getByRole('button', { name: 'Cadastrar identificação' }))
    await waitFor(() => expect(createAnimalIdentification).toHaveBeenCalledWith('animal-1', {
      tipo: tipo as AnimalIdentificationType,
      descricaoTipo: needsDescription ? 'Sensor óptico' : null,
      valor: 'NOVO-01', principal: true, dataAplicacao: '2026-09-11', observacao: 'conferida',
    }))
    expect(screen.queryByText('response-only')).not.toBeInTheDocument()
    const listed = { ...active, id: `listed-${tipo}`, tipo, descricaoTipo: needsDescription ? 'Sensor óptico' : null, valor: 'NOVO-01' } as AnimalIdentification
    finishRefresh(page([listed]))
    expect(await screen.findByText('NOVO-01')).toBeInTheDocument()
  })

  it('edits metadata only and refreshes the list after success', async () => {
    vi.mocked(listAnimalIdentifications).mockResolvedValueOnce(page([active])).mockResolvedValueOnce(page([{ ...active, observacao: 'Nova nota' }]))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    const row = (await screen.findByText(active.valor)).closest('tr')!
    fireEvent.click(within(row).getByRole('button', { name: 'Editar metadados' }))
    expect(screen.getAllByText('Microchip')).toHaveLength(2)
    expect(screen.getAllByText(active.valor)).toHaveLength(2)
    expect(screen.queryByLabelText('Tipo')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Valor')).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Data de aplicação'), { target: { value: '' } })
    fireEvent.change(screen.getByLabelText('Observação'), { target: { value: '  Nova nota  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar metadados' }))
    await waitFor(() => expect(updateAnimalIdentificationMetadata).toHaveBeenCalledWith('animal-1', active.id, { dataAplicacao: null, observacao: 'Nova nota' }))
    expect(listAnimalIdentifications).toHaveBeenCalledTimes(2)
    expect(await screen.findByText('Nova nota')).toBeInTheDocument()
  })

  it('omits untouched metadata fields from the PATCH payload', async () => {
    vi.mocked(listAnimalIdentifications).mockResolvedValue(page([active]))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    const row = (await screen.findByText(active.valor)).closest('tr')!
    fireEvent.click(within(row).getByRole('button', { name: 'Editar metadados' }))
    expect(screen.getAllByText('Microchip').length).toBeGreaterThan(0)
    expect(screen.getAllByText(active.valor).length).toBeGreaterThan(0)
    expect(screen.queryByLabelText('Tipo')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Descrição do tipo')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Valor')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar metadados' }))
    await waitFor(() => expect(updateAnimalIdentificationMetadata).toHaveBeenCalledWith('animal-1', active.id, {}))
  })

  it.each([
    ['Data de aplicação', '2026-09-11', { dataAplicacao: '2026-09-11' }],
    ['Observação', 'Nova nota', { observacao: 'Nova nota' }],
  ] as const)('sends only the changed %s metadata field', async (field, value, expected) => {
    vi.mocked(listAnimalIdentifications).mockResolvedValue(page([active]))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    const row = (await screen.findByText(active.valor)).closest('tr')!
    fireEvent.click(within(row).getByRole('button', { name: 'Editar metadados' }))
    fireEvent.change(screen.getByLabelText(field), { target: { value } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar metadados' }))
    await waitFor(() => expect(updateAnimalIdentificationMetadata).toHaveBeenCalledWith('animal-1', active.id, expected))
  })

  it.each([
    ['Data de aplicação', { dataAplicacao: null }],
    ['Observação', { observacao: null }],
  ] as const)('sends explicit null when %s is cleared', async (field, expected) => {
    vi.mocked(listAnimalIdentifications).mockResolvedValue(page([active]))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    const row = (await screen.findByText(active.valor)).closest('tr')!
    fireEvent.click(within(row).getByRole('button', { name: 'Editar metadados' }))
    fireEvent.change(screen.getByLabelText(field), { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar metadados' }))
    await waitFor(() => expect(updateAnimalIdentificationMetadata).toHaveBeenCalledWith('animal-1', active.id, expected))
  })

  it('keeps creation and metadata editors mutually exclusive and starts creation with clean values', async () => {
    vi.mocked(listAnimalIdentifications).mockResolvedValue(page([active]))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    const row = (await screen.findByText(active.valor)).closest('tr')!
    fireEvent.click(within(row).getByRole('button', { name: 'Editar metadados' }))
    expect(screen.getByRole('heading', { name: 'Editar metadados' })).toBeInTheDocument()
    expect(screen.getByLabelText('Observação')).toHaveValue('Lado esquerdo')
    fireEvent.click(screen.getByRole('button', { name: 'Adicionar identificação' }))
    expect(screen.queryByRole('heading', { name: 'Editar metadados' })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Nova identificação' })).toBeInTheDocument()
    expect(screen.getByLabelText('Data de aplicação')).toHaveValue('')
    expect(screen.getByLabelText('Observação')).toHaveValue('')
  })

  it('sets and removes principal and inactivates and reactivates only after refreshing server state', async () => {
    const nonPrincipal = { ...active, principal: false }
    vi.mocked(listAnimalIdentifications)
      .mockResolvedValueOnce(page([nonPrincipal]))
      .mockResolvedValueOnce(page([{ ...nonPrincipal, principal: true }]))
      .mockResolvedValueOnce(page([nonPrincipal]))
      .mockResolvedValueOnce(page([{ ...nonPrincipal, ativo: false }]))
      .mockResolvedValueOnce(page([nonPrincipal]))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    fireEvent.click(within((await screen.findByText(active.valor)).closest('tr')!).getByRole('button', { name: 'Definir como principal' }))
    await waitFor(() => expect(setAnimalIdentificationPrincipal).toHaveBeenCalledWith('animal-1', active.id, true))
    fireEvent.click(await screen.findByRole('button', { name: 'Remover principal' }))
    await waitFor(() => expect(setAnimalIdentificationPrincipal).toHaveBeenCalledWith('animal-1', active.id, false))
    fireEvent.click(await screen.findByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(setAnimalIdentificationAtivo).toHaveBeenCalledWith('animal-1', active.id, false))
    expect(await screen.findByText('Histórico (inativa)')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Reativar' }))
    await waitFor(() => expect(setAnimalIdentificationAtivo).toHaveBeenCalledWith('animal-1', active.id, true))
    expect(listAnimalIdentifications).toHaveBeenCalledTimes(5)
  })

  it.each([
    [400, 'Os dados informados para a identificação são inválidos.'],
    [404, 'Animal ou identificação não encontrada.'],
    [409, 'A identificação conflita com outro registro.'],
  ])('shows a safe mutation message for HTTP %s', async (status, message) => {
    vi.mocked(listAnimalIdentifications).mockResolvedValue(page([active]))
    vi.mocked(setAnimalIdentificationAtivo).mockRejectedValueOnce(new HttpError(status, 'SQLSTATE 23505 secret detail'))
    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    fireEvent.click(within((await screen.findByText(active.valor)).closest('tr')!).getByRole('button', { name: 'Inativar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(message)
    expect(screen.queryByText(/SQLSTATE|secret detail/)).not.toBeInTheDocument()
    expect(listAnimalIdentifications).toHaveBeenCalledOnce()
  })

  it('keeps the edit flow server-driven when a duplicate create is rejected', async () => {
    const nonPrincipal = { ...active, principal: false, valor: 'BR-0042' }
    vi.mocked(listAnimalIdentifications)
      .mockResolvedValueOnce(page([nonPrincipal]))
      .mockResolvedValueOnce(page([{ ...nonPrincipal, principal: true }]))
    vi.mocked(createAnimalIdentification).mockRejectedValueOnce(new HttpError(409, 'duplicate detail'))
    vi.mocked(setAnimalIdentificationPrincipal).mockResolvedValueOnce({ ...nonPrincipal, principal: true })

    render(<AnimalIdentificationsPanel animalId="animal-1" />)
    const row = (await screen.findByText(nonPrincipal.valor)).closest('tr')!
    fireEvent.click(within(row).getByRole('button', { name: 'Definir como principal' }))
    await waitFor(() => expect(setAnimalIdentificationPrincipal).toHaveBeenCalledWith('animal-1', nonPrincipal.id, true))
    expect(await screen.findByText('Principal')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Adicionar identificação' }))
    fireEvent.change(screen.getByLabelText('Valor'), { target: { value: nonPrincipal.valor } })
    fireEvent.click(screen.getByRole('button', { name: 'Cadastrar identificação' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('A identificação conflita com outro registro.')
    expect(screen.queryByText(/duplicate detail/)).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Nova identificação' })).toBeInTheDocument()
    expect(listAnimalIdentifications).toHaveBeenCalledTimes(2)
  })
})
