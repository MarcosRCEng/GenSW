import { describe, expect, it } from 'vitest'
import type { Weight } from '../evolution/api'
import {
  analyzeLaying, analyzeWeights, dateToDay, dayToDate, layingTimeline, timelineExtent, weightTimeline,
  type LayingEntry,
} from './analyticsMetrics'

function weight(id: string, dataMedicao: string, pesoGramas: number): Weight {
  return {
    id, dataMedicao, pesoGramas, tipoMarco: 3, descricaoMarco: null,
    idadeReferenciaDias: 10, idadeDiasNaMedicao: null, observacao: null,
  }
}

function laying(id: string, dataPostura: string, pesoGramas: number | null): LayingEntry {
  return { id, dataPostura, pesoGramas, observacao: null }
}

describe('evolução de peso corporal', () => {
  it('compara a idade real na mesma janela e exige uma medição no dia escolhido', () => {
    const a = weightTimeline([
      weight('a2', '2026-01-03', 5.7),
      weight('a8', '2026-01-09', 11.1),
      weight('a13', '2026-01-14', 19),
    ], '2026-01-01', 'age')
    const b = weightTimeline([
      weight('b2', '2026-01-19', 4.6),
      weight('b8', '2026-01-25', 9.7),
      weight('b13', '2026-01-30', 16.2),
    ], '2026-01-17', 'age')
    expect(a.map(point => point.x)).toEqual([2, 8, 13])
    expect(b.map(point => point.x)).toEqual([2, 8, 13])
    expect(a[0].age).toBe(2)
    expect(a[0].record.idadeReferenciaDias).toBe(10)

    const window = { from: 2, to: 13 }
    const first = analyzeWeights(a, window, 10)
    const second = analyzeWeights(b, window, 10)
    expect(first.dailyGain?.toFixed(2)).toBe('1.21')
    expect(second.dailyGain?.toFixed(2)).toBe('1.05')
    expect(first.totalGain).toBe(13.3)
    expect(second.totalGain).toBe(11.6)
    expect(first.selected).toBeNull()
    expect(second.selected).toBeNull()
    expect(first.coverage).toEqual({ observedDays: 3, totalDays: 12 })
  })

  it('preserva as pesagens no mesmo dia e explicita quantas compõem a média', () => {
    const series = weightTimeline([
      weight('birth-1', '2026-01-01', 4.5),
      weight('birth-2', '2026-01-01', 4.8),
      weight('second', '2026-01-03', 6.65),
    ], '2026-01-01', 'age')
    const analysis = analyzeWeights(series, { from: 0, to: 2 }, 0)
    expect(analysis.points.map(point => point.record.id)).toEqual(['birth-1', 'birth-2', 'second'])
    expect(analysis.selected).toMatchObject({ average: 4.65, count: 2 })
    expect(analysis.totalGain).toBe(2)
    expect(analysis.dailyGain).toBe(1)
    expect(analysis.coverage).toEqual({ observedDays: 2, totalDays: 3 })
    expect(analyzeWeights(series, { from: 0, to: 0 }, 0).dailyGain).toBeNull()
    expect(analyzeWeights(series, { from: 1, to: 2 }, 2).totalGain).toBeNull()
  })

  it('permite consultar o calendário sem data de nascimento', () => {
    const records = [weight('only', '2024-02-29', 12)]
    expect(weightTimeline(records, null, 'age')).toEqual([])
    expect(weightTimeline(records, null, 'calendar')[0]).toMatchObject({
      date: '2024-02-29', age: null, x: dateToDay('2024-02-29'), y: 12,
    })
    for (const date of ['0001-01-01', '2024-02-29', '9999-12-31']) {
      expect(dayToDate(dateToDay(date))).toBe(date)
    }
    expect(dateToDay('2024-02-30')).toBeNaN()
    expect(dateToDay('2024-03-11') - dateToDay('2024-03-09')).toBe(2)
  })
})

describe('evolução de postura', () => {
  it('conta os lançamentos na mesma data, agrega os pesos e mostra semana sem lançamentos', () => {
    const records = [laying('p1', '2026-01-12', 4.5), laying('p2', '2026-01-12', 4.8)]
    const timeline = layingTimeline(records, null, 'calendar')
    const monday = dateToDay('2026-01-12')
    const result = analyzeLaying(timeline, { from: monday, to: monday + 13 }, 'calendar')
    expect(result.entryCount).toBe(2)
    expect(result.weightCount).toBe(2)
    expect(result.meanWeight).toBe(4.65)
    expect(result.sumWeight).toBe(9.3)
    expect(result.points.map(point => point.y)).toEqual([4.5, 4.8])
    expect(result.coverage).toEqual({ observedDays: 1, totalDays: 14 })
    expect(result.weeks).toEqual([
      { from: monday, to: monday + 6, count: 2 }, { from: monday + 7, to: monday + 13, count: 0 },
    ])
  })

  it('agrupa semanas consecutivas sem lançamentos em uma janela extensa', () => {
    const records = [
      laying('p1', '2026-01-12', 4.5),
      laying('p2', '2026-01-14', 4.8),
      laying('p3', '2026-01-26', 4.9),
    ]
    const from = dateToDay('0001-01-02')
    const to = dateToDay('9999-12-31')
    const monday = dateToDay('2026-01-12')
    const result = analyzeLaying(layingTimeline(records, null, 'calendar'), { from, to }, 'calendar')

    expect(result.weeks).toEqual([
      { from, to: monday - 1, count: 0, weekCount: (monday - from + 1) / 7 },
      { from: monday, to: monday + 6, count: 2 },
      { from: monday + 7, to: monday + 13, count: 0 },
      { from: monday + 14, to: monday + 20, count: 1 },
      { from: monday + 21, to, count: 0, weekCount: Math.ceil((to - monday - 20) / 7) },
    ])
    expect(result.entryCount).toBe(3)
    expect(result.coverage).toEqual({ observedDays: 3, totalDays: to - from + 1 })
  })

  it('alinha pela primeira postura histórica mesmo quando o filtro começa depois', () => {
    const records = [
      laying('later', '2026-01-11', 4.5),
      laying('first', '2026-01-01', 4.8),
      laying('without-weight', '2026-01-22', null),
    ]
    const timeline = layingTimeline(records, '2025-12-29', 'first-laying')
    expect(timeline.referenceDate).toBe('2026-01-01')
    expect(timelineExtent(timeline.entries)).toEqual({ from: 0, to: 21 })
    const result = analyzeLaying(timeline, { from: 8, to: 21 }, 'first-laying')
    expect(result.points[0]).toMatchObject({ x: 10, age: 13 })
    expect(result.entryCount).toBe(2)
    expect(result.weightCount).toBe(1)
    expect(result.weeks).toEqual([
      { from: 8, to: 13, count: 1 },
      { from: 14, to: 20, count: 0 },
      { from: 21, to: 21, count: 1 },
    ])
    expect(layingTimeline(records, '2025-12-29', 'age').entries.map(point => point.x)).toEqual([3, 13, 24])
    expect(layingTimeline(records, null, 'age').alignable).toBe(false)
    expect(layingTimeline([], null, 'first-laying').alignable).toBe(false)
  })
})
