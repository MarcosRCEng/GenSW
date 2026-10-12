import { act, render, screen } from '@testing-library/react'
import { TableRegion } from './TableRegion'

describe('TableRegion', () => {
  let clientWidth: number
  let scrollWidth: number
  let resize: ResizeObserverCallback
  const disconnect = vi.fn()

  beforeEach(() => {
    clientWidth = 300
    scrollWidth = 700
    vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockImplementation(() => clientWidth)
    vi.spyOn(HTMLElement.prototype, 'scrollWidth', 'get').mockImplementation(() => scrollWidth)
    vi.stubGlobal('ResizeObserver', class {
      constructor(callback: ResizeObserverCallback) { resize = callback }
      observe() {}
      disconnect = disconnect
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
    disconnect.mockClear()
  })

  it('identifica a região de rolagem, permite foco e explica como alcançar colunas externas', () => {
    render(<TableRegion label="Tabela de pessoas" className="rounded-xl"><table><tbody><tr><td>Pessoa</td></tr></tbody></table></TableRegion>)
    const region = screen.getByRole('region', { name: 'Tabela de pessoas' })
    expect(region).toHaveAttribute('tabindex', '0')
    expect(region).toHaveClass('rounded-xl')
    expect(region).toHaveAccessibleDescription(/use as setas esquerda e direita/)
    expect(screen.getByRole('table')).toBeInTheDocument()
  })

  it('retira aviso e parada de Tab quando o redimensionamento faz as colunas caberem', () => {
    const { unmount } = render(<TableRegion label="Tabela de pessoas"><table /></TableRegion>)
    clientWidth = 900
    act(() => resize([], {} as ResizeObserver))
    expect(screen.queryByRole('region')).not.toBeInTheDocument()
    expect(screen.queryByText(/Role a tabela horizontalmente/)).not.toBeInTheDocument()
    expect(screen.getByRole('table').parentElement).not.toHaveAttribute('tabindex')
    unmount()
    expect(disconnect).toHaveBeenCalled()
  })

  it('mede novamente quando novos resultados acrescentam colunas largas', () => {
    scrollWidth = 250
    const { rerender } = render(<TableRegion label="Resultados"><table><tbody><tr><td>Curto</td></tr></tbody></table></TableRegion>)
    expect(screen.queryByRole('region')).not.toBeInTheDocument()
    scrollWidth = 800
    rerender(<TableRegion label="Resultados"><table><tbody><tr><td>Conteúdo atualizado</td></tr></tbody></table></TableRegion>)
    expect(screen.getByRole('region', { name: 'Resultados' })).toHaveAttribute('tabindex', '0')
  })
})
