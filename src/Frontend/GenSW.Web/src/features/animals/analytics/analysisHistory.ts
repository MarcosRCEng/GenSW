import { useEffect, useRef, useState } from 'react'
import { isHttpError } from '../../../shared/http/httpErrors'
import { api, type Page, type Weight } from '../evolution/api'
import type { Animal } from '../types/animals'
import type { LayingEntry } from './analyticsMetrics'

type AvailableHistory<T> = { status: 'ready'; records: readonly T[] }
type History<T> = AvailableHistory<T> | { status: 'ineligible' }
export type HistoryResult<T> = History<T> | { status: 'error'; forbidden: boolean }
type Loader<T> = (animal: Animal, signal: AbortSignal) => Promise<History<T>>

const pageSize = 100
const historyPage = <T>(root: string, page: number, signal: AbortSignal) =>
  api<Page<T>>(`${root}?page=${page}&pageSize=${pageSize}`, signal)

async function collectPages<T extends { id: string }>(root: string, first: Page<T>, signal: AbortSignal) {
  const records = new Map(first.items.map(item => [item.id, item]))
  let result = first
  for (let page = 2; page <= result.totalPages; page += 1) {
    result = await historyPage<T>(root, page, signal)
    for (const item of result.items) records.set(item.id, item)
  }
  return [...records.values()]
}

export async function loadWeightHistory(animal: Animal, signal: AbortSignal): Promise<History<Weight>> {
  const root = `/animais/${animal.id}/pesagens`
  const first = await historyPage<Weight>(root, 1, signal)
  return { status: 'ready', records: await collectPages(root, first, signal) }
}

type LayingPage = Page<LayingEntry> & { metricas?: { evolucao?: LayingEntry[] } }

export async function loadLayingHistory(animal: Animal, signal: AbortSignal): Promise<History<LayingEntry>> {
  if (animal.sexo !== 2) return { status: 'ineligible' }
  const root = `/animais/${animal.id}/producoes-ovos`
  try {
    const first = await api<LayingPage>(`${root}?page=1&pageSize=${pageSize}`, signal)
    // The current endpoint returns the complete set in metricas.evolucao, including duplicates by date.
    const evolution = first.metricas?.evolucao
    const records = evolution?.length === first.totalItems ? evolution : await collectPages(root, first, signal)
    return { status: 'ready', records }
  } catch (error) {
    // This endpoint returns 404 when the animal does not meet its laying eligibility rules.
    if (isHttpError(error) && error.status === 404) return { status: 'ineligible' }
    throw error
  }
}

export function useAnalysisHistory<T>(animals: readonly Animal[], load: Loader<T>) {
  const cache = useRef(new Map<string, HistoryResult<T>>())
  const [results, setResults] = useState<ReadonlyMap<string, HistoryResult<T>>>(cache.current)
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    const { signal } = controller
    const pending = animals.filter(animal => !cache.current.has(animal.id))
    const fetchHistory = async (animal: Animal) => {
      let result: HistoryResult<T>
      try {
        result = await load(animal, signal)
      } catch (error) {
        result = { status: 'error', forbidden: isHttpError(error) && error.status === 403 }
      }
      if (signal.aborted) return
      const next = new Map(cache.current)
      next.set(animal.id, result)
      cache.current = next
      setResults(next)
    }
    const fetchPending = async () => {
      const batchSize = 4
      for (let offset = 0; offset < pending.length && !signal.aborted; offset += batchSize) {
        await Promise.all(pending.slice(offset, offset + batchSize).map(fetchHistory))
      }
    }
    void fetchPending()
    return () => controller.abort()
  }, [animals, load, revision])

  const retry = () => {
    const next = new Map(cache.current)
    for (const animal of animals) {
      if (next.get(animal.id)?.status === 'error') next.delete(animal.id)
    }
    cache.current = next
    setResults(next)
    setRevision(value => value + 1)
  }

  return { results, retry }
}
