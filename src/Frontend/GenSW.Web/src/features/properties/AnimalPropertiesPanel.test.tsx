import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../shared/http/httpErrors'
import { AnimalPropertiesPanel } from './AnimalPropertiesPanel'
import { desvincularAnimalPropriedade, getAnimalPropriedades, listPropriedades, transferirAnimalPropriedade } from './propertiesService'

vi.mock('./propertiesService', () => ({ desvincularAnimalPropriedade: vi.fn(), getAnimalPropriedades: vi.fn(), listPropriedades: vi.fn(), transferirAnimalPropriedade: vi.fn() }))
const property = { id: 'property-1', nome: 'Unidade Norte', localizacao: null, observacao: null, ativo: true, createdAtUtc: '2026-09-01T00:00:00Z', updatedAtUtc: '2026-09-01T00:00:00Z' }
const destination = { ...property, id: 'property-2', nome: 'Unidade Sul' }
const link = { id: 'link-1', animalId: 'animal-1', propriedadeId: property.id, propriedadeNome: property.nome, propriedadeAtiva: false, dataInicio: '2026-09-01', dataFim: null, observacao: 'Entrada inicial', createdAtUtc: property.createdAtUtc, updatedAtUtc: property.updatedAtUtc }
const newLink = { ...link, id: 'link-2', propriedadeId: destination.id, propriedadeNome: destination.nome, propriedadeAtiva: true, dataInicio: '2026-09-02', observacao: 'Transferência' }
const transferred = { atual: newLink, historico: [newLink, { ...link, dataFim: '2026-09-02' }] }
function panel(readOnly = false) { return render(<MemoryRouter><AnimalPropertiesPanel animalId="animal-1" readOnly={readOnly} /></MemoryRouter>) }

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getAnimalPropriedades).mockResolvedValue({ atual: link, historico: [link] })
  vi.mocked(listPropriedades).mockResolvedValue({ items: [destination], page: 1, pageSize: 100, totalItems: 1, totalPages: 1 })
  vi.mocked(transferirAnimalPropriedade).mockResolvedValue(transferred)
  vi.mocked(desvincularAnimalPropriedade).mockResolvedValue({ atual: null, historico: [{ ...link, dataFim: '2026-09-02' }] })
})

describe('AnimalPropertiesPanel', () => {
  it('exibe unidade inativa e histórico em Visualizar sem formulário, catálogos ou mutações', async () => {
    panel(true)
    const table = await screen.findByRole('table', { name: 'Histórico de propriedades' })
    expect(within(table).getByText('Entrada inicial')).toBeInTheDocument()
    expect(screen.getByText(/A propriedade atual está inativa/)).toBeInTheDocument()
    expect(document.querySelector('form')).toBeNull()
    expect(screen.queryByRole('button', { name: /Vincular|Transferir|Encerrar/ })).not.toBeInTheDocument()
    expect(listPropriedades).not.toHaveBeenCalled()
    expect(transferirAnimalPropriedade).not.toHaveBeenCalled()
    expect(desvincularAnimalPropriedade).not.toHaveBeenCalled()
  })

  it('aceita animal sem propriedade e inicia vínculo com expectativa nula', async () => {
    vi.mocked(getAnimalPropriedades).mockResolvedValue({ atual: null, historico: [] })
    panel()
    expect(await screen.findByText('Sem propriedade')).toBeInTheDocument()
    expect(screen.getByText(/inclusive para animais de referência/)).toBeInTheDocument()
    await screen.findByRole('option', { name: 'Unidade Sul' })
    fireEvent.change(screen.getByLabelText('Propriedade de destino'), { target: { value: destination.id } })
    fireEvent.change(screen.getByLabelText('Data da movimentação'), { target: { value: '2026-09-02' } })
    fireEvent.click(screen.getByRole('button', { name: 'Vincular propriedade' }))
    await waitFor(() => expect(transferirAnimalPropriedade).toHaveBeenCalledWith('animal-1', { propriedadeId: destination.id, dataInicio: '2026-09-02', vinculoAtualIdEsperado: null, observacao: null }))
  })

  it('transfere com o vínculo esperado e mostra os dois períodos preservados', async () => {
    panel()
    await screen.findByRole('option', { name: 'Unidade Sul' })
    expect(screen.queryByRole('option', { name: 'Unidade Norte' })).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Propriedade de destino'), { target: { value: destination.id } })
    fireEvent.change(screen.getByLabelText('Data da movimentação'), { target: { value: '2026-09-02' } })
    fireEvent.change(screen.getByLabelText('Observação do novo vínculo'), { target: { value: ' Transferência ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Transferir propriedade' }))
    await screen.findByText('Propriedade atualizada. Histórico preservado.')
    expect(transferirAnimalPropriedade).toHaveBeenCalledWith('animal-1', { propriedadeId: destination.id, dataInicio: '2026-09-02', vinculoAtualIdEsperado: link.id, observacao: 'Transferência' })
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(3)
  })

  it('encerra vínculo sem excluir o histórico e mantém animal sem propriedade', async () => {
    panel()
    await screen.findByRole('button', { name: 'Encerrar vínculo' })
    fireEvent.change(screen.getByLabelText('Data da movimentação'), { target: { value: '2026-09-02' } })
    fireEvent.click(screen.getByRole('button', { name: 'Encerrar vínculo' }))
    expect(await screen.findByText('Sem propriedade')).toBeInTheDocument()
    expect(desvincularAnimalPropriedade).toHaveBeenCalledWith('animal-1', { dataFim: '2026-09-02', vinculoAtualIdEsperado: link.id })
    expect(screen.getByRole('table')).toHaveTextContent('Entrada inicial')
    expect(screen.queryByRole('button', { name: 'Encerrar vínculo' })).not.toBeInTheDocument()
  })

  it('bloqueia data anterior ao vínculo e futura antes de gravar', async () => {
    panel()
    await screen.findByRole('button', { name: 'Encerrar vínculo' })
    for (const date of ['2026-08-31', '2999-01-01']) {
      fireEvent.change(screen.getByLabelText('Data da movimentação'), { target: { value: date } })
      fireEvent.click(screen.getByRole('button', { name: 'Encerrar vínculo' }))
      expect(await screen.findByRole('alert')).toHaveTextContent('Informe uma data válida a partir de 01/09/2026')
    }
    expect(desvincularAnimalPropriedade).not.toHaveBeenCalled()
  })

  it('recarrega após conflito para não repetir uma operação com vínculo desatualizado', async () => {
    vi.mocked(transferirAnimalPropriedade).mockRejectedValue(new HttpError(409))
    panel()
    await screen.findByRole('option', { name: 'Unidade Sul' })
    vi.mocked(getAnimalPropriedades).mockResolvedValue(transferred)
    fireEvent.change(screen.getByLabelText('Propriedade de destino'), { target: { value: destination.id } })
    fireEvent.change(screen.getByLabelText('Data da movimentação'), { target: { value: '2026-09-02' } })
    fireEvent.click(screen.getByRole('button', { name: 'Transferir propriedade' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('A movimentação não foi aplicada')
    await waitFor(() => expect(getAnimalPropriedades).toHaveBeenCalledTimes(2))
    expect(await screen.findByText('Transferência')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Transferir propriedade' })).toBeDisabled()
  })

  it('carrega destinos além da primeira página e permite recuperação de falha na consulta', async () => {
    vi.mocked(getAnimalPropriedades).mockRejectedValueOnce(new Error('offline'))
    vi.mocked(listPropriedades).mockImplementation(async ({ page } = {}) => ({ items: page === 1 ? [property] : [destination], page: page ?? 1, pageSize: 100, totalItems: 101, totalPages: 2 }))
    panel()
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar o histórico')
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar histórico' }))
    expect(await screen.findByRole('option', { name: 'Unidade Sul' })).toBeInTheDocument()
    expect(listPropriedades).toHaveBeenCalledWith(expect.objectContaining({ page: 2, ativo: true }))
  })
})
