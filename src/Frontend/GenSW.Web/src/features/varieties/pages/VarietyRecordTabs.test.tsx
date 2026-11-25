import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { transferableAbortController } from 'node:util'
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AnimalRelatedDetailsPage } from '../../animals/pages/AnimalRelatedDetailsPage'
import type { Photo } from '../../animals/evolution/api'
import { httpRequest, type HttpRequestOptions } from '../../../shared/http/httpClient'
import { HttpError } from '../../../shared/http/httpErrors'
import { UnsavedChangesProvider } from '../../../shared/navigation/UnsavedChangesProvider'
import { VarietiesListPage } from './VarietiesListPage'
import { VarietyDetailsPage } from './VarietyDetailsPage'
import { VarietyFormPage } from './VarietyFormPage'

vi.mock('../../../shared/http/httpClient', () => ({ httpRequest: vi.fn() }))

const basePath = '/variedades'
const firstPath = basePath + '/variety-1'
const secondPath = basePath + '/variety-2'
const imagePath = firstPath + '/imagens'
const timestamps = { createdAtUtc: '2026-09-01T12:00:00Z', updatedAtUtc: '2026-09-01T12:00:00Z' }
const species = { ...timestamps, id: 'species-1', nomeComum: 'Espécie fictícia', nomeCientifico: null, ativo: true }
const firstVariety = { ...timestamps, id: 'variety-1', especieId: species.id, especie: species, nome: 'Variedade Alfa', ativo: true }
const secondVariety = { ...firstVariety, id: 'variety-2', nome: 'Variedade Beta' }
const primary: Photo = {
  id: 'active-1', legenda: 'Imagem ativa Alfa', dataCaptura: '2026-09-02', ordem: 1, ativa: true, representativa: true,
  thumbnailPath: imagePath + '/active-1/conteudo?versao=miniatura',
  conteudoPath: imagePath + '/active-1/conteudo?versao=visualizacao',
}
const otherActive: Photo = { ...primary, id: 'active-2', legenda: 'Outra imagem ativa', ordem: 2, representativa: false, thumbnailPath: '', conteudoPath: '' }

function inactivePhotos(count: number) {
  return Array.from({ length: count }, (_, index): Photo => ({
    ...primary, id: 'inactive-' + (index + 1), legenda: 'Imagem inativa ' + (index + 1), ordem: index + 1,
    ativa: false, representativa: false,
  }))
}

function paginated<T>(items: T[], page = 1, pageSize = 50) {
  return { items: items.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalItems: items.length, totalPages: Math.ceil(items.length / pageSize) }
}

let activeImages: Photo[]
let inactiveImages: Photo[]

async function respond(path: string, options?: HttpRequestOptions) {
  const url = new URL(path, 'https://example.invalid')
  const page = Number(url.searchParams.get('page') ?? 1)
  const pageSize = Number(url.searchParams.get('pageSize') ?? 50)
  if (path === firstPath || path === secondPath) {
    return path === firstPath ? firstVariety : secondVariety
  }
  if (url.pathname === '/especies') {
    return paginated([species], page, pageSize)
  }
  if (url.pathname === basePath) {
    return { ...paginated([firstVariety], 1, pageSize), page, totalPages: 2, totalItems: pageSize + 1 }
  }
  if (options?.responseType === 'blob') {
    return new Blob(['png'], { type: 'image/png' })
  }
  if (path.endsWith('/imagens/preferencial')) {
    const image = path.startsWith(imagePath) ? activeImages.find(photo => photo.representativa) : undefined
    return { origem: image ? 'representativa' : 'nenhuma', imagem: image ?? null }
  }
  if (url.pathname === imagePath && !options?.method) {
    const photos = url.searchParams.get('ativo') === 'false' ? inactiveImages : activeImages
    return paginated(photos, page, pageSize)
  }
  if (url.pathname === secondPath + '/imagens') {
    return paginated([], page, pageSize)
  }
  throw new Error('Consulta inesperada: ' + path)
}

function renderVarieties(initialEntries: NonNullable<Parameters<typeof createMemoryRouter>[1]>['initialEntries']) {
  const router = createMemoryRouter([{
    element: <UnsavedChangesProvider><Outlet /></UnsavedChangesProvider>,
    children: [
      { path: basePath, element: <VarietiesListPage /> },
      { path: basePath + '/nova', element: <VarietyFormPage /> },
      { path: basePath + '/:id', element: <VarietyDetailsPage /> },
      { path: basePath + '/:id/editar', element: <VarietyFormPage /> },
      { path: basePath + '/:varietyId/imagens/:recordId', element: <AnimalRelatedDetailsPage section="imagens" kind="variedades" /> },
    ],
  }], { initialEntries })
  render(<RouterProvider router={router} />)
  return router
}

function tab(name: string) {
  return screen.getByRole('tab', { name })
}

async function openImages() {
  const images = await screen.findByRole('tab', { name: 'Imagens' })
  await act(async () => { fireEvent.click(images) })
  return screen.getByRole('region', { name: 'Imagens da variedade' })
}

function photoLink(caption: string) {
  const article = screen.getByText(caption).closest('article')!
  return within(article).getByRole('link', { name: 'Visualizar' })
}

async function openSecondInactivePage() {
  await openImages()
  fireEvent.change(screen.getByLabelText('Exibir imagens'), { target: { value: 'false' } })
  fireEvent.click(await screen.findByRole('button', { name: 'Próximas imagens' }))
  await screen.findByText('Imagem inativa 51')
  return photoLink('Imagem inativa 51')
}

function imageCalls() {
  return vi.mocked(httpRequest).mock.calls.filter(([path]) => path.includes('/imagens'))
}

function expectSelected(name: string) {
  expect(tab(name)).toHaveAttribute('aria-selected', 'true')
  expect(screen.getByRole('tabpanel', { name })).toBeVisible()
}

beforeEach(() => {
  vi.stubGlobal('AbortController', transferableAbortController().constructor)
  vi.stubGlobal('URL', Object.assign(class extends URL {}, {
    createObjectURL: vi.fn(() => 'blob:variety-photo'),
    revokeObjectURL: vi.fn(),
  }))
  activeImages = [primary]
  inactiveImages = []
  vi.mocked(httpRequest).mockReset()
  vi.mocked(httpRequest).mockImplementation(respond)
})

afterEach(() => vi.unstubAllGlobals())

describe('abas da ficha de variedades', () => {
  it('mantém criação com os campos atuais e sem galeria', async () => {
    renderVarieties([basePath + '/nova'])
    await screen.findByRole('option', { name: species.nomeComum })
    expect(screen.getByLabelText('Nome')).toBeVisible()
    expect(screen.getByLabelText('Espécie')).toBeVisible()
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument()
    expect(imageCalls()).toHaveLength(0)
  })

  it('carrega apenas Dados gerais até abrir Imagens pelo teclado e só oferece leitura', async () => {
    renderVarieties([firstPath])
    const general = await screen.findByRole('tab', { name: 'Dados gerais' })
    expectSelected('Dados gerais')
    expect(within(screen.getByRole('tabpanel', { name: 'Dados gerais' })).getByText(firstVariety.nome)).toBeVisible()
    expect(vi.mocked(httpRequest).mock.calls.map(([path]) => path)).toEqual([firstPath])
    general.focus()
    fireEvent.keyDown(general, { key: 'End' })
    expectSelected('Imagens')
    expect(tab('Imagens')).toHaveFocus()
    expect(screen.queryByRole('tabpanel', { name: 'Dados gerais' })).not.toBeInTheDocument()
    expect(await screen.findByRole('img', { name: 'Foto de ' + primary.legenda })).toBeVisible()
    expect(screen.getByText('★ Representativa')).toBeVisible()
    expect(imageCalls().map(([path]) => path)).toEqual([
      imagePath + '?ativo=true&page=1&pageSize=50', imagePath + '/preferencial', primary.thumbnailPath,
    ])
    for (const [, options] of imageCalls()) {
      expect(options).toEqual(expect.objectContaining({ authenticated: true }))
      expect(options?.method ?? 'GET').toBe('GET')
    }
    expect(screen.queryByRole('button', { name: /Enviar|Metadados|Representativa|Inativar|Reativar/ })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Arquivo')).not.toBeInTheDocument()
    fireEvent.keyDown(tab('Imagens'), { key: 'Home' })
    expectSelected('Dados gerais')
    expect(general).toHaveFocus()
    fireEvent.keyDown(general, { key: 'ArrowRight' })
    expectSelected('Imagens')
    expect(screen.getByText(primary.legenda!)).toBeVisible()
    expect(imageCalls()).toHaveLength(3)
  })

  it('trata carregamento, falha e nova tentativa no painel de imagens', async () => {
    let rejectList!: (error: Error) => void
    const stalledList = new Promise<never>((_, reject) => { rejectList = reject })
    let firstList = true
    activeImages = []
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (firstList && path.startsWith(imagePath + '?')) {
        firstList = false
        return stalledList
      }
      return respond(path, options)
    })
    renderVarieties([firstPath])
    const gallery = await openImages()
    expect(within(gallery).getByRole('status')).toHaveTextContent('Carregando imagens')
    await act(async () => rejectList(new Error('offline')))
    expect(within(gallery).getByRole('alert')).toBeVisible()
    fireEvent.click(within(gallery).getByRole('button', { name: 'Recarregar galeria' }))
    expect(await within(gallery).findByText('Nenhuma imagem ativa.')).toBeVisible()
    expect(imageCalls().filter(([path]) => path.startsWith(imagePath + '?'))).toHaveLength(2)
  })

  it('reinicia a aba por ID e ignora a resposta atrasada da variedade anterior', async () => {
    let resolveOldList!: (value: unknown) => void
    const oldList = new Promise(resolve => { resolveOldList = resolve })
    vi.mocked(httpRequest).mockImplementation(async (path, options) => path.startsWith(imagePath + '?') ? oldList : respond(path, options))
    const router = renderVarieties([firstPath])
    await openImages()
    await act(async () => router.navigate(secondPath))
    expectSelected('Dados gerais')
    expect(screen.getAllByText(secondVariety.nome).length).toBeGreaterThan(0)
    expect(imageCalls().every(([path]) => path.startsWith(imagePath))).toBe(true)
    const gallery = await openImages()
    expect(await within(gallery).findByText('Nenhuma imagem ativa.')).toBeVisible()
    await act(async () => resolveOldList(paginated([primary])))
    expect(within(gallery).getByText('Nenhuma imagem ativa.')).toBeVisible()
    expect(within(gallery).queryByText(primary.legenda!)).not.toBeInTheDocument()
    expect(imageCalls().some(([path]) => path === primary.thumbnailPath)).toBe(false)
  })

  it.each([firstPath, firstPath + '/editar'])('ignora contexto vinculado a outra variedade em %s', async path => {
    renderVarieties([{ pathname: path, state: { varietyImages: { path: secondPath, page: 2, active: false } } }])
    await screen.findByRole('tab', { name: 'Dados gerais' })
    expectSelected('Dados gerais')
    expect(imageCalls()).toHaveLength(0)
  })

  it('ao salvar por Imagens revela Dados gerais e foca o campo inválido', async () => {
    renderVarieties([firstPath + '/editar'])
    fireEvent.change(await screen.findByLabelText('Nome'), { target: { value: ' ' } })
    await openImages()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expectSelected('Dados gerais')
    expect(screen.getByLabelText('Nome')).toHaveFocus()
    expect(screen.getByText('Informe um nome entre 1 e 200 caracteres.')).toBeVisible()
    expect(vi.mocked(httpRequest).mock.calls.some(([, options]) => options?.method)).toBe(false)
  })

  it('mantém os rascunhos de ficha, arquivo e outros metadados após salvar uma imagem', async () => {
    activeImages = [primary, otherActive]
    const updatedCaption = 'Legenda salva'
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (path === imagePath + '/active-2/metadados' && options?.method === 'PUT') {
        activeImages = [primary, { ...otherActive, legenda: updatedCaption }]
        return activeImages[1]
      }
      return respond(path, options)
    })
    renderVarieties([firstPath + '/editar'])
    fireEvent.change(await screen.findByLabelText('Nome'), { target: { value: 'Nome em edição' } })
    const gallery = await openImages()
    const photo = await within(gallery).findByText(primary.legenda!)
    const firstCard = within(photo.closest('article')!)
    const secondCard = within(screen.getByText(otherActive.legenda!).closest('article')!)
    const file = new File(['fictícia'], 'imagem.png', { type: 'image/png' })
    fireEvent.change(within(gallery).getByLabelText('Arquivo'), { target: { files: [file] } })
    fireEvent.change(within(gallery).getByLabelText('Legenda do upload'), { target: { value: 'Upload pendente' } })
    fireEvent.change(firstCard.getByLabelText('Legenda'), { target: { value: 'Legenda pendente' } })
    fireEvent.change(secondCard.getByLabelText('Legenda'), { target: { value: updatedCaption } })

    fireEvent.click(tab('Dados gerais'))
    expect(screen.getByLabelText('Nome')).toHaveValue('Nome em edição')
    expect(screen.queryByRole('region', { name: 'Imagens da variedade' })).not.toBeInTheDocument()
    fireEvent.click(tab('Imagens'))
    expect(firstCard.getByLabelText('Legenda')).toHaveValue('Legenda pendente')
    expect(secondCard.getByLabelText('Legenda')).toHaveValue(updatedCaption)
    fireEvent.click(secondCard.getByRole('button', { name: 'Salvar metadados' }))

    await within(gallery).findByText(updatedCaption)
    expect(within(gallery).getByText(/Metadados não salvos em 1 imagem/)).toBeVisible()
    expect(within(screen.getByText(primary.legenda!).closest('article')!).getByLabelText('Legenda')).toHaveValue('Legenda pendente')
    expect(within(gallery).getByLabelText('Legenda do upload')).toHaveValue('Upload pendente')
    expect((within(gallery).getByLabelText('Arquivo') as HTMLInputElement).files?.[0]).toBe(file)
    expect(within(gallery).getByRole('button', { name: 'Enviar imagem' })).toBeEnabled()
    expect(httpRequest).toHaveBeenCalledWith(imagePath + '/active-2/metadados', {
      authenticated: true, method: 'PUT', body: { legenda: updatedCaption, dataCaptura: primary.dataCaptura },
    })
    fireEvent.click(tab('Dados gerais'))
    expect(screen.getByLabelText('Nome')).toHaveValue('Nome em edição')
    expect(vi.mocked(httpRequest).mock.calls.filter(([path, options]) => path === firstPath && options?.method)).toHaveLength(0)
  })

  it('mantém upload, ordem, representativa e status como operações próprias da galeria', async () => {
    activeImages = [primary, otherActive]
    const uploaded = { ...otherActive, id: 'uploaded-1', legenda: 'Imagem enviada', ordem: 3 }
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (path === imagePath && options?.method === 'POST') {
        activeImages.push(uploaded)
        return uploaded
      }
      if (path === imagePath + '/ordem') {
        activeImages = [otherActive, primary, uploaded].map((photo, index) => ({ ...photo, ordem: index + 1 }))
        return {}
      }
      if (path === imagePath + '/active-2/representativa') {
        activeImages = activeImages.map(photo => ({ ...photo, representativa: photo.id === otherActive.id }))
        return {}
      }
      if (path === imagePath + '/active-1/ativo') {
        if ((options?.body as { ativo: boolean }).ativo) {
          inactiveImages = []
          activeImages.push({ ...primary, representativa: false })
        } else {
          activeImages = activeImages.filter(photo => photo.id !== primary.id)
          inactiveImages = [{ ...primary, ativa: false, representativa: false }]
        }
        return {}
      }
      return respond(path, options)
    })
    renderVarieties([firstPath + '/editar'])
    const gallery = await openImages()
    await within(gallery).findByText(primary.legenda!)
    const file = new File(['fictícia'], 'imagem.png', { type: 'image/png' })
    fireEvent.change(within(gallery).getByLabelText('Arquivo'), { target: { files: [file] } })
    fireEvent.change(within(gallery).getByLabelText('Legenda do upload'), { target: { value: uploaded.legenda } })
    fireEvent.submit(within(gallery).getByRole('button', { name: 'Enviar imagem' }).closest('form')!)
    await within(gallery).findByText(uploaded.legenda!)
    const upload = imageCalls().find(([path, options]) => path === imagePath && options?.method === 'POST')
    expect(upload?.[1]?.body).toBeInstanceOf(FormData)
    expect((upload?.[1]?.body as FormData).get('arquivo')).toBe(file)
    expect(within(gallery).getByLabelText('Legenda do upload')).toHaveValue('')

    fireEvent.click(within(gallery).getByRole('button', { name: 'Mover Imagem ativa Alfa para depois' }))
    await waitFor(() => expect(within(gallery).getAllByRole('article')[0]).toHaveTextContent(otherActive.legenda!))
    expect(httpRequest).toHaveBeenCalledWith(imagePath + '/ordem', {
      authenticated: true, method: 'PUT', body: { ids: [otherActive.id, primary.id, uploaded.id] },
    })

    const card = () => within(screen.getByText(otherActive.legenda!).closest('article')!)
    fireEvent.click(card().getByRole('button', { name: 'Usar como representativa' }))
    await waitFor(() => expect(card().getByText('★ Representativa')).toBeVisible())
    expect(httpRequest).toHaveBeenCalledWith(imagePath + '/active-2/representativa', {
      authenticated: true, method: 'PUT', body: { representativa: true },
    })

    fireEvent.click(within(screen.getByText(primary.legenda!).closest('article')!).getByRole('button', { name: 'Inativar imagem' }))
    await within(gallery).findByText(uploaded.legenda!)
    expect(within(gallery).queryByText(primary.legenda!)).not.toBeInTheDocument()
    fireEvent.change(within(gallery).getByLabelText('Exibir imagens'), { target: { value: 'false' } })
    fireEvent.click(await within(gallery).findByRole('button', { name: 'Reativar imagem' }))
    await within(gallery).findByText('Nenhuma imagem inativa.')
    for (const ativo of [false, true]) {
      expect(httpRequest).toHaveBeenCalledWith(imagePath + '/active-1/ativo', {
        authenticated: true, method: 'PUT', body: { ativo },
      })
    }
    expect(vi.mocked(httpRequest).mock.calls.filter(([path, options]) => path === firstPath && options?.method)).toHaveLength(0)
  })

  it.each([
    ['Variety already exists', 'Já existe uma variedade com esse nome', 'Nome'],
    ['Variety is referenced by an animal', 'A espécie não pode ser alterada', 'Espécie'],
  ])('preserva o rascunho e volta ao campo quando a API retorna %s', async (problem, message, field) => {
    vi.mocked(httpRequest).mockImplementation(async (path, options) => {
      if (path === firstPath && options?.method === 'PUT') throw new HttpError(409, undefined, problem)
      return respond(path, options)
    })
    const router = renderVarieties([firstPath + '/editar'])
    fireEvent.change(await screen.findByLabelText('Nome'), { target: { value: 'Nome pendente' } })
    await openImages()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(message)
    expectSelected('Dados gerais')
    expect(screen.getByLabelText(field)).toHaveFocus()
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Continuar editando' }))
    expect(router.state.location.pathname).toBe(firstPath + '/editar')
    expect(screen.getByLabelText('Nome')).toHaveValue('Nome pendente')
  })
})

describe('navegação entre a ficha e o detalhe da imagem', () => {
  it('restaura filtro, página, foco e origem da lista pelo retorno explícito da consulta', async () => {
    inactiveImages = inactivePhotos(51)
    const filters = { page: 2, search: 'Alfa', searchDraft: 'Alfa', especieId: species.id, ativo: true, sortBy: 'createdAtUtc', sortDirection: 'desc' }
    const router = renderVarieties([{ pathname: basePath, state: { list: { path: basePath, filters } } }])
    fireEvent.click(await screen.findByRole('link', { name: 'Visualizar' }))
    fireEvent.click(await openSecondInactivePage())
    await screen.findByRole('heading', { name: 'Visualizar imagem' })
    expect(await screen.findByText('Imagem inativa 51')).toBeVisible()
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Variedade' }))
    await screen.findByText('Imagem inativa 51')
    expectSelected('Imagens')
    expect(screen.getByLabelText('Exibir imagens')).toHaveValue('false')
    expect(screen.getByText('Página 2 de 2')).toBeVisible()
    await waitFor(() => expect(photoLink('Imagem inativa 51')).toHaveFocus())
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Variedades' }))
    await screen.findByRole('link', { name: 'Visualizar' })
    expect(router.state.location.pathname).toBe(basePath)
    expect(screen.getByLabelText('Buscar por nome')).toHaveValue('Alfa')
    expect(screen.getByLabelText('Espécie')).toHaveValue(species.id)
    expect(screen.getByLabelText('Status')).toHaveValue('true')
    expect(screen.getByLabelText('Ordenar por')).toHaveValue('createdAtUtc')
    expect(screen.getByLabelText('Direção')).toHaveValue('desc')
    expect(screen.getByText('Página 2 de 2')).toBeVisible()
  })

  it('consulta e mostra o original autenticado ao abrir o link direto de uma imagem ativa', async () => {
    renderVarieties([imagePath + '/active-1'])
    expect(await screen.findByRole('img', { name: 'Foto de Imagem ativa Alfa' })).toBeVisible()
    expect(imageCalls().map(([path]) => path)).toEqual([
      imagePath + '?page=1&pageSize=100&ativo=true', primary.conteudoPath,
    ])
    expect(httpRequest).toHaveBeenCalledWith(primary.conteudoPath, expect.objectContaining({
      authenticated: true, responseType: 'blob',
    }))
  })


  it('abre link direto além de 100 imagens e retorna ao pai canônico com o painel padrão', async () => {
    inactiveImages = inactivePhotos(101)
    const router = renderVarieties([imagePath + '/inactive-101'])
    expect(await screen.findByText('Imagem inativa 101')).toBeVisible()
    expect(screen.getByText(/conteúdo de imagens inativas não está disponível/)).toBeVisible()
    expect(imageCalls().map(([path]) => path)).toEqual([
      imagePath + '?page=1&pageSize=100&ativo=true',
      imagePath + '?page=1&pageSize=100&ativo=false',
      imagePath + '?page=2&pageSize=100&ativo=false',
    ])
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Variedade' }))
    const gallery = await screen.findByRole('region', { name: 'Imagens da variedade' })
    expect(router.state.location.pathname).toBe(firstPath)
    expectSelected('Imagens')
    expect(screen.getByLabelText('Exibir imagens')).toHaveValue('true')
    expect(await within(gallery).findByText('Página 1 de 1')).toBeVisible()
    await waitFor(() => expect(gallery).toHaveFocus())
  })

  it('retorna à consulta por Voltar para Variedade mesmo quando a imagem veio da edição', async () => {
    inactiveImages = inactivePhotos(51)
    const router = renderVarieties([firstPath + '/editar'])
    fireEvent.click(await openSecondInactivePage())
    await screen.findByRole('heading', { name: 'Visualizar imagem' })
    expect(await screen.findByText('Imagem inativa 51')).toBeVisible()
    fireEvent.click(screen.getByRole('link', { name: 'Voltar para Variedade' }))
    await screen.findByText('Imagem inativa 51')
    expect(router.state.location.pathname).toBe(firstPath)
    expectSelected('Imagens')
    expect(screen.getByLabelText('Exibir imagens')).toHaveValue('false')
    expect(screen.getByText('Página 2 de 2')).toBeVisible()
    await waitFor(() => expect(photoLink('Imagem inativa 51')).toHaveFocus())
    expect(screen.queryByRole('button', { name: 'Reativar imagem' })).not.toBeInTheDocument()
  })

  it('bloqueia a ida ao detalhe com rascunhos e o Voltar do navegador recupera só o contexto visual', async () => {
    inactiveImages = inactivePhotos(51)
    const origin = { list: { path: basePath, filters: { page: 2, search: 'Alfa', searchDraft: 'Alfa' } } }
    const router = renderVarieties([{ pathname: firstPath, state: origin }])
    fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
    fireEvent.change(await screen.findByLabelText('Nome'), { target: { value: 'Nome privado pendente' } })
    const link = await openSecondInactivePage()
    fireEvent.change(screen.getByLabelText('Legenda do upload'), { target: { value: 'Legenda privada pendente' } })
    fireEvent.click(link)
    fireEvent.click(await screen.findByRole('button', { name: 'Continuar editando' }))
    expect(screen.getByLabelText('Legenda do upload')).toHaveValue('Legenda privada pendente')
    expect(screen.getByText('Página 2 de 2')).toBeVisible()
    expect(screen.getByLabelText('Exibir imagens')).toHaveValue('false')
    expect(JSON.stringify(router.state.location.state)).not.toMatch(/privad[oa] pendente/)
    fireEvent.click(tab('Dados gerais'))
    expect(screen.getByLabelText('Nome')).toHaveValue('Nome privado pendente')
    fireEvent.click(tab('Imagens'))
    fireEvent.click(link)
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar e sair' }))
    await screen.findByRole('heading', { name: 'Visualizar imagem' })
    expect(JSON.stringify(router.state.location.state)).not.toMatch(/privad[oa] pendente/)
    await act(async () => router.navigate(-1))
    await screen.findByText('Imagem inativa 51')
    expect(router.state.location.pathname).toBe(firstPath + '/editar')
    expectSelected('Imagens')
    expect(screen.getByLabelText('Exibir imagens')).toHaveValue('false')
    expect(screen.getByText('Página 2 de 2')).toBeVisible()
    await waitFor(() => expect(photoLink('Imagem inativa 51')).toHaveFocus())
    expect(screen.getByLabelText('Legenda do upload')).toHaveValue('')
    fireEvent.click(tab('Dados gerais'))
    expect(screen.getByLabelText('Nome')).toHaveValue(firstVariety.nome)
    fireEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
    await screen.findByRole('heading', { name: 'Detalhes da variedade' })
    expect(router.state.location.pathname).toBe(firstPath)
    expect(router.state.location.state).toEqual(origin)
  })

  it('salva a ficha pelo cabeçalho após Voltar do navegador abrir a edição em Imagens', async () => {
    const origin = { list: { path: basePath, filters: { page: 2, search: 'Alfa', searchDraft: 'Alfa' } } }
    const router = renderVarieties([{ pathname: firstPath, state: origin }])
    fireEvent.click(await screen.findByRole('link', { name: 'Editar' }))
    const gallery = await openImages()
    fireEvent.click(await within(gallery).findByRole('link', { name: 'Visualizar' }))
    await screen.findByRole('heading', { name: 'Visualizar imagem' })
    await act(async () => router.navigate(-1))
    await screen.findByText(primary.legenda!)
    expect(router.state.location.pathname).toBe(firstPath + '/editar')
    expectSelected('Imagens')
    expect(screen.queryByRole('tabpanel', { name: 'Dados gerais' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('heading', { name: 'Detalhes da variedade' })
    expect(httpRequest).toHaveBeenCalledWith(firstPath, {
      authenticated: true, method: 'PUT', body: { nome: firstVariety.nome, especieId: species.id },
    })
    expect(router.state.location.pathname).toBe(firstPath)
    expect(router.state.location.state).toEqual({ ...origin, recordSavedPath: firstPath })
  })


  it('bloqueia Voltar do navegador para sair da edição sem criar entradas ao trocar de aba', async () => {
    const router = renderVarieties([firstPath, firstPath + '/editar'])
    fireEvent.change(await screen.findByLabelText('Nome'), { target: { value: 'Edição pendente' } })
    await openImages()
    await act(async () => router.navigate(-1))
    fireEvent.click(await screen.findByRole('button', { name: 'Continuar editando' }))
    expect(router.state.location.pathname).toBe(firstPath + '/editar')
    expectSelected('Imagens')
    fireEvent.click(tab('Dados gerais'))
    expect(screen.getByLabelText('Nome')).toHaveValue('Edição pendente')
    await act(async () => router.navigate(-1))
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar e sair' }))
    await screen.findByRole('link', { name: 'Editar' })
    expect(router.state.location.pathname).toBe(firstPath)
    expectSelected('Dados gerais')
  })
})
