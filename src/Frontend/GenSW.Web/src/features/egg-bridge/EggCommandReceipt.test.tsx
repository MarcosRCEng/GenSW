import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { EggCommandReceipt } from './EggBridgeControls'
import { parseEggCommandResult } from './eggBridgeService'
import { eggDetail, eggGroup } from './eggBridgeFixtures'
import type { EggCommandOperation } from './types'

describe('Recibo do comando F03 e origem histórica', () => {
  it.each<[EggCommandOperation, string, string]>([
    ['Compensacao', 'Compensação integral confirmada', '-1'],
    ['Invalidacao', 'Invalidação documental confirmada', '0'],
    ['Anotacao', 'Anotação documental registrada', '0'],
    ['Entrada', 'Entrada individual confirmada', '+1'],
    ['Associacao', 'Associação histórica confirmada', '0'],
  ])('apresenta %s e seu efeito sem confundir com a operação original do grupo', (operation, label, delta) => {
    const original = JSON.stringify(eggDetail)
    const result = parseEggCommandResult(operation, eggDetail)
    render(<MemoryRouter><EggCommandReceipt result={result} /></MemoryRouter>)
    expect(screen.getByText(new RegExp(`^${label}`))).toBeInTheDocument()
    expect(screen.getByText(`Delta de estoque do comando: ${delta} un.`)).toBeInTheDocument()
    if (['Compensacao', 'Invalidacao', 'Anotacao'].includes(operation)) expect(screen.queryByText(/^Entrada individual confirmada/)).not.toBeInTheDocument()
    expect(screen.getByText(/^Grupo original preservado: 1 un/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Consultar estado atual do grupo' })).toHaveAttribute('href', `/producao/ponte-ovos/grupos/${eggGroup.id}`)
    expect(JSON.stringify(eggDetail)).toBe(original)
    expect(eggDetail.grupo.operacao).toBe('Entrada')
  })
})
