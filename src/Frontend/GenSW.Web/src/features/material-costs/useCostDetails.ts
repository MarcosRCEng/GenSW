import { useEffect, useState } from 'react'
import { inventoryError } from '../inventory/inventoryService'
import { costRead } from './materialCostService'

export function useCostDetails<T>(path: string | null, parse: (value: unknown) => T) {
  const [value, setValue] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    let live = true; setValue(null); setError(null)
    if (path) void costRead(path, parse).then(value => { if (live) setValue(value) }).catch(error => { if (live) setError(inventoryError(error)) })
    return () => { live = false }
  }, [path, parse, revision])
  return { value, error, retry: () => setRevision(value => value + 1) }
}
