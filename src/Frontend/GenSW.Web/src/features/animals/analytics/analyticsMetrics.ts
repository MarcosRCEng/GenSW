import type { Weight } from '../evolution/api'

export type TimeBasis = 'age' | 'calendar' | 'first-laying'
export type DayWindow = { from: number; to: number }
export type LayingEntry = {
  id: string
  dataPostura: string
  pesoGramas: number | null
  observacao: string | null
}

type DatedRecord<T> = { x: number; date: string; age: number | null; record: T }
export type TimelinePoint<T> = DatedRecord<T> & { y: number }
export type LayingTimeline = {
  entries: DatedRecord<LayingEntry>[]
  referenceDate: string | null
  alignable: boolean
}
type WeeklyLayingCount = DayWindow & { count: number; weekCount?: number }

const millisecondsPerDay = 86_400_000
const centigramsPerGram = 100
const maxIndividualWeeks = 260

export function dateToDay(date: string): number {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || date.startsWith('0000')) return NaN
  const value = new Date(`${date}T00:00:00Z`)
  if (!Number.isFinite(value.getTime()) || value.toISOString().slice(0, 10) !== date) return NaN
  return value.getTime() / millisecondsPerDay
}

export function dayToDate(day: number): string {
  return new Date(day * millisecondsPerDay).toISOString().slice(0, 10)
}

export function timelineExtent(points: readonly { x: number }[]): DayWindow | null {
  let from = Infinity
  let to = -Infinity
  for (const point of points) {
    from = Math.min(from, point.x)
    to = Math.max(to, point.x)
  }
  return points.length ? { from, to } : null
}

export function weightTimeline(
  weights: readonly Weight[],
  birthDate: string | null,
  basis: 'age' | 'calendar',
): TimelinePoint<Weight>[] {
  const birthDay = birthDate ? dateToDay(birthDate) : NaN
  return weights.flatMap(record => {
    const date = record.dataMedicao
    const day = dateToDay(date)
    const age = Number.isFinite(birthDay) ? day - birthDay : null
    const x = basis === 'calendar' ? day : age
    if (x === null || !Number.isFinite(x)) return []
    return [{ x, y: record.pesoGramas, date, age, record }]
  }).sort((left, right) => left.x - right.x)
}

export function layingTimeline(
  records: readonly LayingEntry[],
  birthDate: string | null,
  basis: TimeBasis,
): LayingTimeline {
  const birthDay = birthDate ? dateToDay(birthDate) : NaN
  const dated = records.flatMap(record => {
    const date = record.dataPostura
    const day = dateToDay(date)
    if (!Number.isFinite(day)) return []
    const age = Number.isFinite(birthDay) ? day - birthDay : null
    return [{ x: day, date, age, record }]
  }).sort((left, right) => left.x - right.x)

  if (basis === 'calendar') return { entries: dated, referenceDate: null, alignable: true }

  const referenceDate = basis === 'age' ? birthDate : dated[0]?.date ?? null
  const referenceDay = basis === 'age' ? birthDay : dated[0]?.x
  if (referenceDay === undefined || !Number.isFinite(referenceDay)) {
    return { entries: [], referenceDate: null, alignable: false }
  }
  const entries = dated.map(point => ({ ...point, x: point.x - referenceDay }))
  return { entries, referenceDate, alignable: true }
}

function inWindow(day: number, { from, to }: DayWindow): boolean {
  return day >= from && day <= to
}

function coverage(records: readonly { x: number }[], window: DayWindow) {
  return {
    observedDays: new Set(records.map(record => record.x)).size,
    totalDays: window.to - window.from + 1,
  }
}

function gramsToCentigrams(grams: number): number {
  return Math.round(grams * centigramsPerGram)
}

function measurementsAt(points: readonly TimelinePoint<Weight>[], day: number | null) {
  if (day === null) return null
  const measurements = points.filter(point => point.x === day)
  const count = measurements.length
  if (!count) return null
  const sum = measurements.reduce((total, point) => total + gramsToCentigrams(point.y), 0)
  return { average: sum / (count * centigramsPerGram), count, meanCentigrams: sum / count }
}

export function analyzeWeights(
  timeline: readonly TimelinePoint<Weight>[],
  window: DayWindow,
  measuredAt: number | null,
) {
  const points = timeline.filter(point => inWindow(point.x, window))
  const start = measurementsAt(points, window.from)
  const end = measurementsAt(points, window.to)
  const elapsedDays = window.to - window.from
  const totalGain = start && end && elapsedDays > 0
    ? (end.meanCentigrams - start.meanCentigrams) / centigramsPerGram
    : null
  return {
    points,
    coverage: coverage(points, window),
    selected: measurementsAt(points, measuredAt),
    start,
    end,
    totalGain,
    dailyGain: totalGain === null ? null : totalGain / elapsedDays,
  }
}

function weekStart(day: number, basis: TimeBasis): number {
  const offset = basis === 'calendar' ? 3 : 0 // The UTC epoch was a Thursday.
  return Math.floor((day + offset) / 7) * 7 - offset
}

function summarizeWeeks(counts: ReadonlyMap<number, number>, window: DayWindow, basis: TimeBasis) {
  const weeks: WeeklyLayingCount[] = []
  if (!Number.isSafeInteger(window.from) || !Number.isSafeInteger(window.to) || window.from > window.to) return weeks

  const first = weekStart(window.from, basis)
  const last = weekStart(window.to, basis)
  const append = (start: number, end: number, count: number) => {
    const weekCount = (end - start) / 7 + 1
    weeks.push({
      from: Math.max(start, window.from),
      to: Math.min(end + 6, window.to),
      count,
      ...(weekCount > 1 && { weekCount }),
    })
  }

  if ((last - first) / 7 + 1 <= maxIndividualWeeks) {
    for (let start = first; start <= last; start += 7) {
      append(start, start, counts.get(start) ?? 0)
    }
    return weeks
  }

  let gapStart = first
  for (const [start, count] of [...counts.entries()].sort(([left], [right]) => left - right)) {
    if (gapStart < start) append(gapStart, start - 7, 0)
    append(start, start, count)
    gapStart = start + 7
  }
  if (gapStart <= last) append(gapStart, last, 0)
  return weeks
}

export function analyzeLaying(timeline: LayingTimeline, window: DayWindow, basis: TimeBasis) {
  const entries = timeline.entries.filter(point => inWindow(point.x, window))
  const points: TimelinePoint<LayingEntry>[] = entries.flatMap(point => {
    const y = point.record.pesoGramas
    if (y === null || !Number.isFinite(y) || y <= 0) return []
    return [{ ...point, y }]
  })
  const weightCount = points.length
  const sum = points.reduce((total, point) => total + gramsToCentigrams(point.y), 0)
  const counts = new Map<number, number>()
  for (const point of entries) {
    const start = weekStart(point.x, basis)
    counts.set(start, (counts.get(start) ?? 0) + 1)
  }
  return {
    entries,
    points,
    weeks: timeline.alignable ? summarizeWeeks(counts, window, basis) : [],
    coverage: coverage(entries, window),
    entryCount: entries.length,
    weightCount,
    meanWeight: weightCount ? sum / (weightCount * centigramsPerGram) : null,
    sumWeight: weightCount ? sum / centigramsPerGram : null,
  }
}
