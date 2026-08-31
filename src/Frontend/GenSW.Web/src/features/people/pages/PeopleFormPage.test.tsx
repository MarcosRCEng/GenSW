import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HttpError } from '../../../shared/http/httpErrors'
import {
  createPessoa,
  getPessoaById,
  updatePessoa,
} from '../services/peopleService'
import { TipoPessoa, type Pessoa } from '../types/people'
import { PeopleFormPage } from './PeopleFormPage'

vi.mock('../services/peopleService', () => ({
  createPessoa: vi.fn(),
  getPessoaById: vi.fn(),
  updatePessoa: vi.fn(),
}))

const activeCompany: Pessoa = {
  id: 'person-1',
  tipoPessoa: TipoPessoa.Juridica,
  nome: 'Empresa Original Ltda.',
  nomeFantasia: 'Empresa Original',
  ativo: true,
  createdAtUtc: '2026-08-20T12:00:00Z',
  updatedAtUtc: '2026-08-20T12:00:00Z',
}

function renderPage(path = '/pessoas/nova') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<PeopleFormPage />} path="/pessoas/nova" />
        <Route element={<PeopleFormPage />} path="/pessoas/:id/editar" />
        <Route element={<h1>Pessoas destino</h1>} path="/pessoas" />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  vi.mocked(createPessoa).mockReset()
  vi.mocked(getPessoaById).mockReset()
  vi.mocked(updatePessoa).mockReset()
  vi.mocked(createPessoa).mockResolvedValue(activeCompany)
  vi.mocked(getPessoaById).mockResolvedValue(activeCompany)
  vi.mocked(updatePessoa).mockResolvedValue(activeCompany)
})

describe('PeopleFormPage - criação', () => {
  it('cria pessoa física com nome normalizado e navega para a listagem', async () => {
    renderPage()

    fireEvent.change(screen.getByLabelText('Tipo de pessoa'), {
      target: { value: String(TipoPessoa.Fisica) },
    })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: '  Marina Silva  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() =>
      expect(createPessoa).toHaveBeenCalledWith({
        tipoPessoa: TipoPessoa.Fisica,
        nome: 'Marina Silva',
        nomeFantasia: null,
      }),
    )
    expect(await screen.findByRole('heading', { name: 'Pessoas destino' })).toBeInTheDocument()
  })

  it('cria pessoa jurídica com nome fantasia', async () => {
    renderPage()

    fireEvent.change(screen.getByLabelText('Tipo de pessoa'), {
      target: { value: String(TipoPessoa.Juridica) },
    })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Empresa Ltda.' } })
    fireEvent.change(screen.getByLabelText('Nome fantasia'), {
      target: { value: '  Minha Empresa  ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() =>
      expect(createPessoa).toHaveBeenCalledWith({
        tipoPessoa: TipoPessoa.Juridica,
        nome: 'Empresa Ltda.',
        nomeFantasia: 'Minha Empresa',
      }),
    )
  })

  it('limpa nome fantasia ao trocar de pessoa jurídica para física', async () => {
    renderPage()

    const typeSelect = screen.getByLabelText('Tipo de pessoa')
    fireEvent.change(typeSelect, { target: { value: String(TipoPessoa.Juridica) } })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Marina Silva' } })
    fireEvent.change(screen.getByLabelText('Nome fantasia'), { target: { value: 'Invisível' } })
    fireEvent.change(typeSelect, { target: { value: String(TipoPessoa.Fisica) } })

    expect(screen.queryByLabelText('Nome fantasia')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() =>
      expect(createPessoa).toHaveBeenCalledWith({
        tipoPessoa: TipoPessoa.Fisica,
        nome: 'Marina Silva',
        nomeFantasia: null,
      }),
    )
  })

  it('valida tipo obrigatório e nome entre 2 e 200 caracteres sem chamar o serviço', () => {
    renderPage()

    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'A' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(screen.getByText('Selecione o tipo de pessoa.')).toBeInTheDocument()
    expect(screen.getByText('Informe um nome entre 2 e 200 caracteres.')).toBeInTheDocument()
    expect(createPessoa).not.toHaveBeenCalled()
  })

  it('traduz erro 400 de criação sem expor detalhes técnicos', async () => {
    vi.mocked(createPessoa).mockRejectedValue(new HttpError(400, 'technical details'))
    renderPage()

    fireEvent.change(screen.getByLabelText('Tipo de pessoa'), {
      target: { value: String(TipoPessoa.Fisica) },
    })
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Marina' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Revise os dados informados.')
    expect(screen.queryByText(/technical details/i)).not.toBeInTheDocument()
  })
})

describe('PeopleFormPage - edição', () => {
  it('carrega pessoa ativa, mantém o tipo somente leitura e atualiza apenas os nomes', async () => {
    let resolveLoad!: (person: Pessoa) => void
    vi.mocked(getPessoaById).mockReturnValue(
      new Promise<Pessoa>((resolve) => {
        resolveLoad = resolve
      }),
    )
    renderPage('/pessoas/person-1/editar')

    expect(screen.getByRole('status')).toHaveTextContent('Carregando pessoa…')
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument()
    resolveLoad(activeCompany)

    expect(await screen.findByDisplayValue('Empresa Original Ltda.')).toBeInTheDocument()
    expect(screen.getByText('Pessoa jurídica')).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Tipo de pessoa' })).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: '  Empresa Nova Ltda. ' } })
    fireEvent.change(screen.getByLabelText('Nome fantasia'), { target: { value: ' Empresa Nova ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() =>
      expect(updatePessoa).toHaveBeenCalledWith('person-1', {
        nome: 'Empresa Nova Ltda.',
        nomeFantasia: 'Empresa Nova',
      }),
    )
    expect(await screen.findByRole('heading', { name: 'Pessoas destino' })).toBeInTheDocument()
  })

  it('bloqueia edição de pessoa inativa', async () => {
    vi.mocked(getPessoaById).mockResolvedValue({ ...activeCompany, ativo: false })
    renderPage('/pessoas/person-1/editar')

    expect(await screen.findByText('Esta pessoa está inativa.')).toBeInTheDocument()
    expect(screen.getByText('Reative-a na listagem antes de editar.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Salvar' })).not.toBeInTheDocument()
    expect(updatePessoa).not.toHaveBeenCalled()
  })

  it('mostra pessoa não encontrada ao receber 404 no carregamento', async () => {
    vi.mocked(getPessoaById).mockRejectedValue(new HttpError(404))
    renderPage('/pessoas/missing/editar')

    expect(await screen.findByText('Pessoa não encontrada.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar para Pessoas' })).toHaveAttribute(
      'href',
      '/pessoas',
    )
  })

  it('permite tentar novamente após erro genérico de carregamento', async () => {
    vi.mocked(getPessoaById)
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce(activeCompany)
    renderPage('/pessoas/person-1/editar')

    expect(await screen.findByText('Não foi possível carregar a pessoa.')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))

    expect(await screen.findByDisplayValue('Empresa Original Ltda.')).toBeInTheDocument()
    expect(getPessoaById).toHaveBeenCalledTimes(2)
  })

  it('traduz conflito 409 ao salvar edição', async () => {
    vi.mocked(updatePessoa).mockRejectedValue(new HttpError(409))
    renderPage('/pessoas/person-1/editar')

    await screen.findByDisplayValue('Empresa Original Ltda.')
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'A pessoa está inativa. Reative-a antes de editar.',
    )
  })

  it('desabilita o submit e impede chamada concorrente enquanto salva', async () => {
    let resolveSave!: (person: Pessoa) => void
    vi.mocked(updatePessoa).mockReturnValue(
      new Promise<Pessoa>((resolve) => {
        resolveSave = resolve
      }),
    )
    renderPage('/pessoas/person-1/editar')

    await screen.findByDisplayValue('Empresa Original Ltda.')
    const saveButton = screen.getByRole('button', { name: 'Salvar' })
    fireEvent.click(saveButton)

    expect(screen.getByRole('button', { name: 'Salvando…' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Salvando…' }))
    expect(updatePessoa).toHaveBeenCalledTimes(1)

    resolveSave(activeCompany)
    expect(await screen.findByRole('heading', { name: 'Pessoas destino' })).toBeInTheDocument()
  })
})
