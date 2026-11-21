import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AnalyticsChart } from './AnalyticsChart'

describe('AnalyticsChart', () => {
  it('keeps coincident measurements in a one-day window and allows keyboard consultation', async () => {
    render(<AnalyticsChart
      title="Peso corporal"
      xLabel="Idade (dias)"
      yLabel="Peso (g)"
      xDomain={[8, 8]}
      formatX={String}
      series={[{
        id: 'a', label: 'Ave A', points: [
          { id: 'outside', x: 2, y: 5.7, details: [{ label: 'Marco', value: 'Fora do período' }] },
          { id: 'morning', x: 8, y: 11.1, details: [{ label: 'Marco', value: 'Manhã' }] },
          { id: 'afternoon', x: 8, y: 11.1, details: [{ label: 'Marco', value: 'Tarde' }] },
        ],
      }]}
    />)

    const chart = screen.getByRole('group', { name: 'Idade (dias) × Peso (g)' })
    const [first, second] = within(chart).getAllByRole('button', { name: 'Ave A, 8, 11,1' })
    expect(first).toHaveAttribute('tabindex', '0')
    expect(second).toHaveAttribute('tabindex', '-1')
    expect(chart).not.toHaveTextContent('NaN')
    expect(chart).toHaveTextContent('×2')

    fireEvent.keyDown(first, { key: 'ArrowRight' })
    expect(second).toHaveFocus()
    expect(second).toHaveAttribute('aria-pressed', 'true')
    expect(document.querySelector('dl[aria-live="polite"]')).toHaveTextContent('Tarde')

    fireEvent.click(screen.getByText('Consultar dados (2)'))
    const table = await screen.findByRole('table', { name: 'Peso corporal' })
    expect(within(table).getAllByRole('row')).toHaveLength(3)
    expect(within(table).getByText('Manhã')).toBeVisible()
    expect(within(table).getByText('Tarde')).toBeVisible()
    expect(screen.queryByText('Fora do período')).not.toBeInTheDocument()
  })

  it('exposes zero weekly launches as an observation', () => {
    render(<AnalyticsChart
      title="Lançamentos por semana"
      xLabel="Idade (dias)"
      yLabel="Lançamentos de postura"
      variant="bars"
      xDomain={[0, 14]}
      formatX={String}
      series={[{ id: 'a', label: 'Ave A', points: [{ id: '0', x: 0, y: 2 }, { id: '7', x: 7, y: 0 }] }]}
    />)

    const zero = screen.getByRole('button', { name: 'Ave A, 7, 0' })
    fireEvent.click(zero)
    expect(zero).toHaveFocus()
    expect(document.querySelector('dl[aria-live="polite"]')).toHaveTextContent('Lançamentos de postura0')
    expect(screen.getByText('Consultar dados (2)')).toBeVisible()
  })
})
