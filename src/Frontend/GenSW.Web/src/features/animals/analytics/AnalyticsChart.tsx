import { useId, useLayoutEffect, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react'
import { TableRegion } from '../../../shared/layout/TableRegion'
import './analyticsChart.css'

export type AnalyticsChartPoint = {
  id: string
  x: number
  y: number
  details?: ReadonlyArray<{ label: string; value: string }>
}

type AnalyticsChartSeries = {
  id: string
  label: string
  points: ReadonlyArray<AnalyticsChartPoint>
}

type AnalyticsChartProps = {
  title: string
  xLabel: string
  yLabel: string
  xDomain: readonly [number, number]
  formatX: (value: number) => string
  formatY?: (value: number) => string
  series: ReadonlyArray<AnalyticsChartSeries>
  emptyMessage?: string
  variant?: 'line' | 'bars'
}

const colors = ['#176348', '#214998', '#a84223', '#703b86', '#745414', '#1a6c78', '#a23060', '#414555']
const shapePaths = [
  'M -4.6 0 a 4.6 4.6 0 1 0 9.2 0 a 4.6 4.6 0 1 0 -9.2 0',
  'M -4.25 -4.25 h 8.5 v 8.5 h -8.5 Z',
  'M 0 -5.5 L 5.5 0 L 0 5.5 L -5.5 0 Z',
  'M 0 -5.5 L 5.25 4.25 H -5.25 Z',
  'M -5 -4.25 H 5 L 0 5.5 Z',
  'M -4.75 0 L -2.4 -4.5 H 2.4 L 4.75 0 L 2.4 4.5 H -2.4 Z',
]
const lineDashes = ['', '6 3', '2 3', '8 3 2 3']
const numberFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })

function Marker({ index }: { index: number }) {
  return <path className="analytics-chart-marker" d={shapePaths[index % shapePaths.length]} fill={colors[index % colors.length]} />
}

export function AnalyticsSeriesMarker({ index }: { index: number }) {
  return <svg className="analytics-series-marker" viewBox="-8 -8 16 16" aria-hidden="true"><Marker index={index} /></svg>
}

function ticksForDays(start: number, end: number, count: number) {
  const size = Math.min(count, end - start + 1)
  if (size <= 1) return [start]
  return Array.from({ length: size }, (_, index) => Math.round(start + (end - start) * index / (size - 1)))
}

function ticksForValues(maximum: number, integer: boolean) {
  const target = maximum / 4
  const magnitude = 10 ** Math.floor(Math.log10(target))
  const step = ([1, 2, 2.5, 5, 10].find(factor => factor * magnitude >= target) ?? 10) * magnitude
  const interval = integer ? Math.max(1, Math.ceil(step)) : step
  return Array.from({ length: Math.ceil(maximum / interval) + 1 }, (_, index) => index * interval)
}

function linePath(points: ReadonlyArray<{ x: number; plotX: number; plotY: number }>) {
  return points.map((point, index) => {
    const previous = points[index - 1]
    const isolated = previous?.x === point.x || points[index + 1]?.x === point.x || points[index - 2]?.x === previous?.x
    return `${!previous || isolated ? 'M' : 'L'} ${point.plotX} ${point.plotY}`
  }).join(' ')
}

export function AnalyticsChart({
  title, xLabel, yLabel, xDomain, formatX, formatY = value => numberFormat.format(value),
  series, emptyMessage = 'Sem registros na janela.', variant = 'line',
}: AnalyticsChartProps) {
  const titleId = useId()
  const instructionsId = useId()
  const container = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(640)
  const [activeKey, setActiveKey] = useState<string | null>(null)
  const [tableOpen, setTableOpen] = useState(false)
  const [start, end] = xDomain
  const height = 236
  const left = 62
  const right = width - 20
  const top = 17
  const bottom = height - 32
  const plotWidth = right - left

  useLayoutEffect(() => {
    const element = container.current
    if (!element) return
    const resize = () => {
      if (element.clientWidth > 0) setWidth(Math.max(220, element.clientWidth))
    }
    resize()
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(resize)
    observer?.observe(element)
    window.addEventListener('resize', resize)
    return () => {
      observer?.disconnect()
      window.removeEventListener('resize', resize)
    }
  }, [])

  const useBars = variant === 'bars'
  const plotted = series.map(item => item.points.filter(point => point.x >= start && point.x <= end).sort((a, b) => a.x - b.x))
  const maximum = plotted.reduce((largest, points) => points.reduce((peak, point) => Math.max(peak, point.y), largest), 0)
  const yTicks = ticksForValues(maximum || 1, useBars)
  const yMaximum = yTicks[yTicks.length - 1]
  const xTicks = ticksForDays(start, end, width < 360 ? 2 : width < 565 ? 4 : 6)
  const plotX = (x: number) => start === end ? (left + right) / 2 : left + (x - start) / (end - start) * plotWidth
  const plotY = (y: number) => bottom - y / yMaximum * (bottom - top)
  const bucketPositions = useBars ? Array.from(new Set(plotted.flatMap(points => points.map(point => point.x)))).sort((a, b) => a - b) : []
  const groupWidth = bucketPositions.reduce((size, position, index) => index === 0
    ? size
    : Math.min(size, (plotX(position) - plotX(bucketPositions[index - 1])) * .8), 34)
  const barWidth = Math.min(12, groupWidth / Math.max(1, series.length))
  const denseBars = useBars && barWidth < 8
  const groups = plotted.map((points, index) => points.map(point => ({
    ...point,
    key: JSON.stringify([series[index].id, point.id]),
    seriesIndex: index,
    plotX: plotX(point.x) + (useBars ? (index - (series.length - 1) / 2) * barWidth : 0),
    plotY: plotY(point.y),
  })))
  const observations = groups.flat()
  const active = observations.find(point => point.key === activeKey) ?? observations[0]
  const detailColumns = tableOpen
    ? Array.from(new Set(observations.flatMap(point => point.details?.map(detail => detail.label) ?? [])))
      .filter(label => label !== xLabel && label !== yLabel)
    : []
  const overlaps = new Map<string, { x: number; y: number; count: number }>()
  if (!useBars) {
    for (const point of observations) {
      const key = `${point.x},${point.y}`
      const shown = overlaps.get(key)
      overlaps.set(key, { x: point.plotX, y: point.plotY, count: (shown?.count ?? 0) + 1 })
    }
  }

  function navigate(event: KeyboardEvent<SVGGElement>, index: number) {
    let next: number
    switch (event.key) {
      case 'ArrowLeft':
      case 'ArrowUp':
        next = Math.max(0, index - 1)
        break
      case 'ArrowRight':
      case 'ArrowDown':
        next = Math.min(observations.length - 1, index + 1)
        break
      case 'Home':
        next = 0
        break
      case 'End':
        next = observations.length - 1
        break
      case 'Enter':
      case ' ':
        next = index
        break
      default:
        return
    }
    event.preventDefault()
    selectPoint(event.currentTarget.ownerSVGElement, next)
  }

  function selectPoint(svg: SVGSVGElement | null, index: number) {
    setActiveKey(observations[index].key)
    svg?.querySelector<SVGGElement>(`[data-point-index="${index}"]`)?.focus()
  }

  function selectFromPointer(event: MouseEvent<SVGGElement>, index: number) {
    const svg = event.currentTarget.ownerSVGElement
    const matrix = !useBars && event.detail > 0 ? svg?.getScreenCTM?.() : null
    if (!matrix) {
      selectPoint(svg, index)
      return
    }
    // Nearby markers may share a touch target. Use the marker closest to the tap.
    const { x, y } = new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse())
    const distanceTo = (point: typeof observations[number]) => Math.hypot(x - point.plotX, y - point.plotY)
    let closest = index
    let distance = distanceTo(observations[index])
    observations.forEach((point, candidate) => {
      const current = distanceTo(point)
      if (current < distance) {
        closest = candidate
        distance = current
      }
    })
    selectPoint(svg, closest)
  }

  return <section className="analytics-chart" aria-labelledby={titleId}>
    <h3 id={titleId}>{title}</h3>
    {series.length > 1 && <ul className="analytics-chart-legend" aria-label="Aves">
      {series.map((item, index) => <li key={item.id}><AnalyticsSeriesMarker index={index} />{item.label}</li>)}
    </ul>}
    <div className="analytics-chart-plot" ref={container}>
      <svg viewBox={`0 0 ${width} ${height}`} role="group" aria-label={`${xLabel} × ${yLabel}`}
        aria-describedby={active ? instructionsId : undefined}>
        <g aria-hidden="true" className="analytics-chart-axes">
          {yTicks.map(value => <g key={value}>
            <line x1={left} x2={right} y1={plotY(value)} y2={plotY(value)} />
            <text x={left - 8} y={plotY(value)} textAnchor="end" dominantBaseline="middle">{formatY(value)}</text>
          </g>)}
          <line className="analytics-chart-axis" x1={left} x2={left} y1={top} y2={bottom} />
          <line className="analytics-chart-axis" x1={left} x2={right} y1={bottom} y2={bottom} />
          {xTicks.map((value, index) => {
            const anchor = xTicks.length === 1 ? 'middle' : index === 0 ? 'start' : index === xTicks.length - 1 ? 'end' : 'middle'
            return <g key={value}>
              <line x1={plotX(value)} x2={plotX(value)} y1={bottom} y2={bottom + 5} />
              <text x={plotX(value)} y={bottom + 19} textAnchor={anchor}>{formatX(value)}</text>
            </g>
          })}
          <text className="analytics-chart-axis-label" transform={`translate(12 ${(top + bottom) / 2}) rotate(-90)`} textAnchor="middle">{yLabel}</text>
        </g>
        {!useBars && groups.map((points, index) => <path key={series[index].id}
          aria-hidden="true" className="analytics-chart-line" d={linePath(points)} stroke={colors[index % colors.length]}
          strokeDasharray={lineDashes[index % lineDashes.length]} />)}
        {useBars && observations.map(point => <rect key={point.key} aria-hidden="true" className="analytics-chart-bar"
          x={point.plotX - barWidth * .45} y={point.y === 0 ? bottom - 1 : point.plotY} width={barWidth * .9}
          height={Math.max(1, bottom - point.plotY)} fill={colors[point.seriesIndex % colors.length]} />)}
        {observations.map((point, index) => <g key={point.key} className="analytics-chart-point"
          data-active={point.key === active?.key}
          data-dense={denseBars}
          data-point-index={index}
          transform={`translate(${point.plotX} ${point.plotY})`}
          role="button" tabIndex={point.key === active?.key ? 0 : -1}
          aria-pressed={point.key === active?.key}
          aria-label={`${series[point.seriesIndex].label}, ${formatX(point.x)}, ${formatY(point.y)}`}
          onFocus={() => setActiveKey(point.key)}
          onClick={event => selectFromPointer(event, index)}
          onKeyDown={event => navigate(event, index)}>
          {useBars && <rect className="analytics-chart-hit-area" x={-barWidth / 2} y={-2}
            width={barWidth} height={Math.max(4, bottom - point.plotY + 2)} fill="transparent" />}
          {!denseBars && <circle className="analytics-chart-hit-area" r="12" fill="transparent" />}
          <circle className="analytics-chart-halo" r="7" />
          <Marker index={point.seriesIndex} />
        </g>)}
        {Array.from(overlaps.entries()).filter(([, point]) => point.count > 1).map(([key, point]) => <text
          key={key} aria-hidden="true" className="analytics-chart-overlap" x={point.x} y={point.y - 10} textAnchor="middle">×{point.count}</text>)}
      </svg>
      <p className="analytics-chart-x-label" aria-hidden="true">{xLabel}</p>
    </div>
    {active ? <>
      <p className="sr-only" id={instructionsId}>Use as setas do teclado para consultar os pontos do gráfico e Home ou End para ir ao primeiro ou último ponto.</p>
      <dl className="analytics-chart-details" aria-live="polite" aria-atomic="true">
        {series.length > 1 && <div><dt>Ave</dt><dd>{series[active.seriesIndex].label}</dd></div>}
        <div><dt>{xLabel}</dt><dd>{formatX(active.x)}</dd></div>
        <div><dt>{yLabel}</dt><dd>{formatY(active.y)}</dd></div>
        {active.details?.filter(detail => detail.label !== xLabel && detail.label !== yLabel).map(detail => <div key={detail.label}><dt>{detail.label}</dt><dd>{detail.value}</dd></div>)}
      </dl>
      <details className="analytics-chart-table" open={tableOpen} onToggle={event => setTableOpen(event.currentTarget.open)}>
        <summary>Consultar dados ({observations.length})</summary>
        {tableOpen && <TableRegion label={`Tabela de ${title}`}>
          <table aria-label={title}>
            <thead><tr>
              {series.length > 1 && <th scope="col">Ave</th>}
              <th scope="col">{xLabel}</th><th scope="col">{yLabel}</th>
              {detailColumns.map(label => <th scope="col" key={label}>{label}</th>)}
            </tr></thead>
            <tbody>{observations.map(point => <tr key={point.key}>
              {series.length > 1 && <th scope="row">{series[point.seriesIndex].label}</th>}
              <td>{formatX(point.x)}</td><td>{formatY(point.y)}</td>
              {detailColumns.map(label => <td key={label}>{point.details?.find(detail => detail.label === label)?.value ?? '—'}</td>)}
            </tr>)}</tbody>
          </table>
        </TableRegion>}
      </details>
    </> : <p className="analytics-chart-empty">{emptyMessage}</p>}
  </section>
}
