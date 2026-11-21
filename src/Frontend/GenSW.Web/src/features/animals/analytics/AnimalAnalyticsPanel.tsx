import { useId, useState, type ReactNode } from 'react'
import { TableRegion } from '../../../shared/layout/TableRegion'
import type { Weight } from '../evolution/api'
import { weightMilestones } from '../evolution/weightMilestones'
import type { Animal } from '../types/animals'
import { AnalyticsChart, AnalyticsSeriesMarker } from './AnalyticsChart'
import { loadLayingHistory, loadWeightHistory, useAnalysisHistory, type HistoryResult } from './analysisHistory'
import {
  analyzeLaying, analyzeWeights, dateToDay, dayToDate, layingTimeline, timelineExtent, weightTimeline,
  type DayWindow, type LayingEntry, type TimeBasis,
} from './analyticsMetrics'
import './animalAnalytics.css'

type Analysis = 'growth' | 'laying'
type Metric = { label: string; unit?: string; missing: string }
type MetricValue = { value: number | null; note?: string }
type Result = {
  id: string
  label: string
  index: number
  records: number | null
  coverage: string
  values: readonly MetricValue[]
  status?: string
  reference?: string
}

const grams = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 })
const counts = new Intl.NumberFormat('pt-BR')
const daily = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const dateLabel = (iso: string) => iso.split('-').reverse().join('/')
const ageLabel = (age: number | null) => age === null ? 'Não informada' : `${counts.format(age)} dias`
const minDate = '0001-01-01'
const maxDate = '9999-12-31'
const maxRelativeDay = dateToDay(maxDate) - dateToDay(minDate)
const growthMetrics: Metric[] = [
  { label: 'Peso no dia consultado', unit: 'g', missing: 'Sem medição' },
  { label: 'Ganho total', unit: 'g', missing: 'Sem medição' },
  { label: 'Ganho médio diário', unit: 'g/dia', missing: 'Sem medição' },
]
const layingMetrics: Metric[] = [
  { label: 'Lançamentos de postura', missing: 'Sem dados comparáveis' },
  { label: 'Registros com peso', missing: 'Sem dados comparáveis' },
  { label: 'Peso médio', unit: 'g', missing: 'Sem medição' },
  { label: 'Soma dos pesos', unit: 'g', missing: 'Sem medição' },
]
const basisLabels: Record<TimeBasis, string> = {
  age: 'Idade real (dias)',
  calendar: 'Data de calendário',
  'first-laying': 'Dias desde a primeira postura registrada',
}
const inputLabels: Record<TimeBasis, [string, string]> = {
  age: ['Idade inicial (dias)', 'Idade final (dias)'],
  calendar: ['Data inicial', 'Data final'],
  'first-laying': ['Dia inicial desde a primeira postura', 'Dia final desde a primeira postura'],
}
const formatDay = (day: number, basis: TimeBasis) => basis === 'calendar' ? dateLabel(dayToDate(day)) : counts.format(day)
const inputDay = (day: number | undefined, basis: TimeBasis) => day === undefined ? '' : basis === 'calendar' ? dayToDate(day) : String(day)
const parseDay = (day: string, basis: TimeBasis) => day === '' ? NaN : basis === 'calendar' ? dateToDay(day) : Number(day)
const animalLabel = (animal: Animal) => animal.nome ? `${animal.nome} (${animal.codigoInterno})` : animal.codigoInterno

function useTimeWindow(basis: TimeBasis, extent: DayWindow | null) {
  const [from, setFrom] = useState<string>()
  const [to, setTo] = useState<string>()
  const fromInput = from ?? inputDay(extent?.from, basis)
  const toInput = to ?? inputDay(extent?.to, basis)
  const fromDay = parseDay(fromInput, basis)
  const toDay = parseDay(toInput, basis)
  const valid = Number.isSafeInteger(fromDay) && Number.isSafeInteger(toDay) && fromDay <= toDay
    && (basis === 'calendar' || (fromDay >= (basis === 'age' ? -maxRelativeDay : 0) && toDay <= maxRelativeDay))
  return {
    window: valid ? { from: fromDay, to: toDay } : null,
    invalid: (fromInput !== '' || toInput !== '') && !valid,
    from: fromInput, to: toInput, setFrom, setTo,
    reset: () => { setFrom(undefined); setTo(undefined) },
  }
}

function TimeInputs({ basis, range, children }: {
  basis: TimeBasis
  range: ReturnType<typeof useTimeWindow>
  children?: ReactNode
}) {
  const [startLabel, endLabel] = inputLabels[basis]
  const type = basis === 'calendar' ? 'date' : 'number'
  const min = basis === 'calendar' ? minDate : basis === 'age' ? -maxRelativeDay : 0
  const max = basis === 'calendar' ? maxDate : maxRelativeDay
  return <>
    <div className="flex flex-wrap items-end gap-3">
      <label className="analytics-field">{startLabel}<input type={type} min={min} max={max} step={1} value={range.from} onChange={event => range.setFrom(event.target.value)} /></label>
      <label className="analytics-field">{endLabel}<input type={type} min={min} max={max} step={1} value={range.to} onChange={event => range.setTo(event.target.value)} /></label>
      {children}
      <button className="analytics-button" type="button" onClick={range.reset}>Usar todo o histórico</button>
    </div>
    {range.invalid && <p role="status">Informe o início e o fim em ordem, com datas ou dias válidos.</p>}
  </>
}

function windowLabel(window: DayWindow | null, basis: TimeBasis) {
  if (!window) return 'Sem janela definida'
  const label = `${formatDay(window.from, basis)} a ${formatDay(window.to, basis)}`
  return basis === 'calendar' ? label : `${label} dias`
}

function coverageLabel(window: DayWindow | null, coverage?: { observedDays: number; totalDays: number }) {
  if (!window || !coverage) return '—'
  return `${counts.format(coverage.observedDays)} de ${counts.format(coverage.totalDays)} dias com registro`
}

function asMetric(value: number | null | undefined, note?: string): MetricValue {
  return { value: value ?? null, note }
}

function displayMetric({ value }: MetricValue, metric: Metric) {
  if (value === null) return metric.missing
  const formatter = metric.unit === 'g/dia' ? daily : metric.unit ? grams : counts
  return metric.unit ? `${formatter.format(value)} ${metric.unit}` : formatter.format(value)
}

function RankedResults({ rows, metrics, sortBy, onSort, window, basis }: {
  rows: Result[]
  metrics: Metric[]
  sortBy: number
  onSort: (index: number) => void
  window: DayWindow | null
  basis: TimeBasis
}) {
  const leaders = metrics.map((_, index) => rows.reduce<number | null>((best, row) => {
    const value = row.status ? null : row.values[index].value
    return value !== null && (best === null || value > best) ? value : best
  }, null))
  const ranked = [...rows].sort((a, b) => {
    const aValue = a.status ? -Infinity : a.values[sortBy].value ?? -Infinity
    const bValue = b.status ? -Infinity : b.values[sortBy].value ?? -Infinity
    return bValue === aValue ? a.label.localeCompare(b.label, 'pt-BR') : bValue - aValue
  })
  return <div className="space-y-3">
    <label className="analytics-field">Ordenar pelo indicador
      <select value={sortBy} onChange={event => onSort(Number(event.target.value))}>
        {metrics.map((metric, index) => <option value={index} key={metric.label}>{metric.label}</option>)}
      </select>
    </label>
    <TableRegion label="Resultados da comparação">
      <table className="analytics-results">
        <caption>Resultados da comparação</caption>
        <thead><tr>
          <th scope="col">Ave</th><th scope="col">Janela</th><th scope="col">Registros e cobertura</th>
          {metrics.map((metric, index) => <th key={metric.label} scope="col" aria-sort={sortBy === index ? 'descending' : undefined}>{metric.label}</th>)}
        </tr></thead>
        <tbody>{ranked.map(row => <tr key={row.id}>
          <th scope="row"><span className="analytics-bird"><AnalyticsSeriesMarker index={row.index} />{row.label}</span>
            {row.status && <small>{row.status}</small>}
            {row.reference && <small>{row.reference}</small>}
            {!row.status && row.values[sortBy].value === null && <small>Sem dados comparáveis para o indicador selecionado.</small>}
          </th>
          <td>{windowLabel(window, basis)}</td>
          <td>{row.records === null ? '—' : counts.format(row.records)}<small>{row.coverage}</small></td>
          {metrics.map((metric, index) => <td key={metric.label}>
            {displayMetric(row.values[index], metric)}
            {row.values[index].note && <small>{row.values[index].note}</small>}
            {!row.status && leaders[index] !== null && row.values[index].value === leaders[index] && <strong className="analytics-leader">Liderança</strong>}
          </td>)}
        </tr>)}</tbody>
      </table>
    </TableRegion>
    <p className="text-sm text-slate-600">A cobertura conta os dias da janela com pelo menos um registro.</p>
  </div>
}

function historyProblem(result: HistoryResult<unknown> | undefined) {
  if (result?.status === 'ineligible') return 'Sem dados comparáveis. Produção de postura indisponível para esta ave.'
  if (result?.status === 'error') return result.forbidden ? 'Consulta não autorizada.' : 'Histórico indisponível.'
  return undefined
}

function HistoryProblems({ results, retry }: { results: (HistoryResult<unknown> | undefined)[]; retry: () => void }) {
  if (results.some(result => result === undefined)) return <p role="status">Consultando todos os registros das aves…</p>
  if (!results.some(result => result?.status === 'error')) return null
  return <div className="space-y-2">
    <p role="alert">Alguns históricos não puderam ser carregados. Os indicadores usam os dados disponíveis.</p>
    <button className="analytics-button" onClick={retry} type="button">Tentar carregar novamente</button>
  </div>
}

type AnalysisProps = { animals: readonly Animal[]; comparison: boolean }

function GrowthAnalysis({ animals, comparison }: AnalysisProps) {
  const hasBirth = animals.some(animal => animal.dataNascimento !== null)
  const [basis, setBasis] = useState<'age' | 'calendar'>(hasBirth ? 'age' : 'calendar')
  const { results, retry } = useAnalysisHistory(animals, loadWeightHistory)
  const histories = animals.map(animal => results.get(animal.id))
  return <div className="space-y-4">
    <label className="analytics-field">Eixo do tempo
      <select value={basis} onChange={event => setBasis(event.target.value === 'age' ? 'age' : 'calendar')}>
        <option value="age" disabled={!hasBirth && basis !== 'age'}>{basisLabels.age}</option>
        <option value="calendar">{basisLabels.calendar}</option>
      </select>
    </label>
    {!comparison && !hasBirth && <p>Idade indisponível porque a data de nascimento está ausente. O histórico usa datas de calendário.</p>}
    <HistoryProblems results={histories} retry={retry} />
    <div className="space-y-4" hidden={!histories.every(Boolean)}>
      <GrowthTimeline key={basis} basis={basis} animals={animals} histories={results} comparison={comparison} />
    </div>
  </div>
}

function GrowthTimeline({ basis, animals, histories, comparison }: AnalysisProps & {
  basis: 'age' | 'calendar'
  histories: ReadonlyMap<string, HistoryResult<Weight>>
}) {
  const timelines = animals.map(animal => {
    const history = histories.get(animal.id)
    const points = history?.status === 'ready' ? weightTimeline(history.records, animal.dataNascimento, basis) : []
    return { animal, history, points }
  })
  const range = useTimeWindow(basis, timelineExtent(timelines.flatMap(timeline => timeline.points)))
  const [query, setQuery] = useState<string>()
  const [sortBy, setSortBy] = useState(2)
  const dateInput = basis === 'calendar'
  const queryInput = query ?? inputDay(range.window?.to, basis)
  const queryDay = parseDay(queryInput, basis)
  const measuredAt = range.window && Number.isSafeInteger(queryDay) && queryDay >= range.window.from && queryDay <= range.window.to ? queryDay : null
  const formatX = (day: number) => formatDay(day, basis)
  const analyses = timelines.map(timeline => ({
    ...timeline,
    result: range.window ? analyzeWeights(timeline.points, range.window, measuredAt) : null,
  }))
  const rows = analyses.map(({ animal, history, result }, index): Result => {
    const status = historyProblem(history)
      ?? (basis === 'age' && !animal.dataNascimento ? 'Sem dados comparáveis por idade. Data de nascimento ausente.' : undefined)
      ?? (range.window && !result?.points.length ? 'Sem dados comparáveis na janela.' : undefined)
    const sameDay = range.window && range.window.from === range.window.to && result?.start
    return {
      id: animal.id,
      index,
      label: animalLabel(animal),
      records: history?.status === 'ready' && !(basis === 'age' && !animal.dataNascimento) ? result?.points.length ?? 0 : null,
      coverage: coverageLabel(range.window, history?.status === 'ready' && !(basis === 'age' && !animal.dataNascimento) ? result?.coverage : undefined),
      values: [
        asMetric(result?.selected?.average, result?.selected && result.selected.count > 1 ? `Média de ${result.selected.count} pesagens` : undefined),
        asMetric(result?.totalGain, sameDay ? 'O ganho exige dois dias distintos.' : gainNote(result?.start, result?.end)),
        asMetric(result?.dailyGain, sameDay ? 'O ganho exige dois dias distintos.' : gainNote(result?.start, result?.end)),
      ],
      status,
    }
  })
  return <>
    <TimeInputs basis={basis} range={range}>
      {comparison && <label className="analytics-field">{dateInput ? 'Data para consultar' : 'Idade para consultar (dias)'}
        <input type={dateInput ? 'date' : 'number'} value={queryInput} min={range.from || undefined} max={range.to || undefined} step={1}
          onChange={event => setQuery(event.target.value === '' ? undefined : event.target.value)} />
      </label>}
    </TimeInputs>
    {comparison && queryInput && measuredAt === null && range.window && <p role="status">Escolha um dia de consulta dentro da janela.</p>}
    {range.window ? <AnalyticsChart
      title="Evolução do peso corporal"
      xLabel={basisLabels[basis]}
      yLabel="Peso (g)"
      xDomain={[range.window.from, range.window.to]}
      formatX={formatX}
      formatY={grams.format}
      emptyMessage="Nenhuma pesagem disponível na janela."
      series={analyses.map(({ animal, result }) => ({
        id: animal.id,
        label: animalLabel(animal),
        points: (result?.points ?? []).map(point => ({
          id: point.record.id, x: point.x, y: point.y,
          details: [
            { label: 'Data', value: dateLabel(point.date) },
            { label: 'Idade real', value: ageLabel(point.age) },
            { label: 'Marco', value: [weightMilestones[point.record.tipoMarco - 1], point.record.descricaoMarco].filter(Boolean).join(' · ') || 'Não informado' },
          ],
        })),
      }))}
    /> : !range.invalid && <p>Nenhuma pesagem disponível para definir a janela.</p>}
    {comparison && <>
      <p className="text-sm text-slate-600">O ganho usa as pesagens nos extremos exatos da janela. Quando há várias pesagens no mesmo dia, o comparativo usa a média desse dia e o gráfico preserva cada medição.</p>
      <RankedResults rows={rows} metrics={growthMetrics} sortBy={sortBy} onSort={setSortBy} window={range.window} basis={basis} />
    </>}
  </>
}

function gainNote(start: { count: number } | null | undefined, end: { count: number } | null | undefined) {
  if (!start || !end) return undefined
  const notes = []
  if (start.count > 1) notes.push(`início com média de ${start.count} pesagens`)
  if (end.count > 1) notes.push(`fim com média de ${end.count} pesagens`)
  return notes.length ? `Cálculo com ${notes.join(' e ')}` : undefined
}

function LayingAnalysis({ animals, comparison }: AnalysisProps) {
  const [basis, setBasis] = useState<TimeBasis>('calendar')
  const { results, retry } = useAnalysisHistory(animals, loadLayingHistory)
  const histories = animals.map(animal => results.get(animal.id))
  const canAlignByAge = animals.some((animal, index) => animal.dataNascimento && histories[index]?.status === 'ready')
  const canAlignByFirstLaying = histories.some(history => history?.status === 'ready' && history.records.length > 0)
  return <div className="space-y-4">
    <label className="analytics-field">Alinhar postura por
      <select value={basis} onChange={event => setBasis(event.target.value as TimeBasis)}>
        <option value="calendar">{basisLabels.calendar}</option>
        <option value="age" disabled={!canAlignByAge && basis !== 'age'}>{basisLabels.age}</option>
        <option value="first-laying" disabled={!canAlignByFirstLaying && basis !== 'first-laying'}>{basisLabels['first-laying']}</option>
      </select>
    </label>
    <HistoryProblems results={histories} retry={retry} />
    <div className="space-y-4" hidden={!histories.every(Boolean)}>
      <LayingTimeline key={basis} basis={basis} animals={animals} histories={results} comparison={comparison} />
    </div>
  </div>
}

function LayingTimeline({ basis, animals, histories, comparison }: AnalysisProps & {
  basis: TimeBasis
  histories: ReadonlyMap<string, HistoryResult<LayingEntry>>
}) {
  const timelines = animals.map(animal => {
    const history = histories.get(animal.id)
    const timeline = history?.status === 'ready' ? layingTimeline(history.records, animal.dataNascimento, basis) : null
    return { animal, history, timeline }
  })
  const range = useTimeWindow(basis, timelineExtent(timelines.flatMap(item => item.timeline?.entries ?? [])))
  const [sortBy, setSortBy] = useState(0)
  const formatX = (day: number) => formatDay(day, basis)
  const analyses = timelines.map(item => ({
    ...item,
    result: range.window && item.timeline?.alignable ? analyzeLaying(item.timeline, range.window, basis) : null,
  }))
  const rows = analyses.map(({ animal, history, timeline, result }, index): Result => {
    const status = historyProblem(history)
      ?? (timeline && !timeline.alignable ? (basis === 'age' ? 'Sem dados comparáveis por idade. Data de nascimento ausente.' : 'Sem dados comparáveis. Nenhuma primeira postura registrada.') : undefined)
      ?? (result?.entryCount ? undefined : range.window ? 'Sem dados comparáveis. Nenhum lançamento na janela.' : 'Nenhum lançamento disponível para a consulta.')
    return {
      id: animal.id,
      index,
      label: animalLabel(animal),
      records: timeline?.alignable ? result?.entryCount ?? 0 : null,
      coverage: coverageLabel(range.window, result?.coverage),
      values: [
        asMetric(result?.entryCount ?? (timeline?.alignable && !range.invalid ? 0 : null)),
        asMetric(result?.weightCount ?? (timeline?.alignable && !range.invalid ? 0 : null)),
        asMetric(result?.meanWeight),
        asMetric(result?.sumWeight),
      ],
      status,
      reference: basis === 'first-laying' && timeline?.referenceDate ? `Primeira postura em ${dateLabel(timeline.referenceDate)}` : undefined,
    }
  })
  const domain: [number, number] | null = range.window && [range.window.from, range.window.to]
  const hasGroupedWeeks = analyses.some(item => item.result?.weeks.some(week => week.weekCount))
  return <>
    <TimeInputs basis={basis} range={range} />
    {domain ? <>
      <AnalyticsChart
        title="Pesos registrados na postura" xLabel={basisLabels[basis]} yLabel="Peso (g)"
        xDomain={domain} formatX={formatX} formatY={grams.format}
        emptyMessage="Nenhum peso de postura registrado na janela."
        series={analyses.map(({ animal, result }) => ({
          id: animal.id,
          label: animalLabel(animal),
          points: (result?.points ?? []).map(point => ({
            id: point.record.id, x: point.x, y: point.y,
            details: [
              { label: 'Data', value: dateLabel(point.date) },
              { label: 'Idade real', value: ageLabel(point.age) },
              ...(point.record.observacao ? [{ label: 'Observação', value: point.record.observacao }] : []),
            ],
          })),
        }))}
      />
      <AnalyticsChart
        title="Lançamentos de postura por semana" xLabel={basisLabels[basis]} yLabel="Lançamentos de postura"
        xDomain={domain} formatX={formatX} formatY={counts.format} variant="bars"
        emptyMessage="Nenhum histórico alinhável para a janela."
        series={analyses.map(({ animal, result }) => ({
          id: animal.id,
          label: animalLabel(animal),
          points: (result?.weeks ?? []).map(week => ({
            id: String(week.from), x: week.from, y: week.count,
            details: [
              { label: 'Período na janela', value: windowLabel(week, basis) },
              ...(week.weekCount ? [{ label: 'Semanas consecutivas sem lançamentos', value: counts.format(week.weekCount) }] : []),
            ],
          })),
        }))}
      />
      <p className="text-sm text-slate-600">Uma semana com zero lançamentos indica ausência de registros nessa semana.</p>
      {hasGroupedWeeks && <p className="text-sm text-slate-600">Semanas consecutivas sem lançamentos aparecem agrupadas nesta janela.</p>}
    </> : !range.invalid && <p>Nenhum lançamento de postura disponível para definir a janela.</p>}
    {comparison
      ? <RankedResults rows={rows} metrics={layingMetrics} sortBy={sortBy} onSort={setSortBy} window={range.window} basis={basis} />
      : <div className="space-y-2">
        {rows[0]?.status && <p>{rows[0].status}</p>}
        {rows[0]?.reference && <p>{rows[0].reference}</p>}
        {rows[0] && rows[0].records !== null && <dl className="analytics-summary">
          {layingMetrics.map((metric, index) => <div key={metric.label}><dt>{metric.label}</dt><dd>{displayMetric(rows[0].values[index], metric)}</dd></div>)}
        </dl>}
      </div>}
  </>
}

export function AnimalAnalyticsPanel({ animals, comparison = false }: { animals: readonly Animal[]; comparison?: boolean }) {
  const id = useId()
  const [analysis, setAnalysis] = useState<Analysis>('growth')
  const [openedLaying, setOpenedLaying] = useState(false)
  return <section className="analytics-panel" aria-labelledby={id}>
    <h2 className="text-xl font-bold text-slate-900" id={id}>{comparison ? 'Indicadores das aves selecionadas' : 'Evolução'}</h2>
    <div className="flex flex-wrap gap-2" role="group" aria-label="Indicadores">
      <button className="analytics-button" type="button" aria-pressed={analysis === 'growth'} onClick={() => setAnalysis('growth')}>Peso corporal</button>
      <button className="analytics-button" type="button" aria-pressed={analysis === 'laying'} onClick={() => { setAnalysis('laying'); setOpenedLaying(true) }}>Postura</button>
    </div>
    <div hidden={analysis !== 'growth'}><GrowthAnalysis animals={animals} comparison={comparison} /></div>
    {openedLaying && <div hidden={analysis !== 'laying'}><LayingAnalysis animals={animals} comparison={comparison} /></div>}
  </section>
}
