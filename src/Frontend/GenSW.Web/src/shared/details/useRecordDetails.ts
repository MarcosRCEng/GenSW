import { useEffect, useState } from 'react'
import { isHttpError } from '../http/httpErrors'

export type DetailLoadState = 'loading' | 'ready' | 'not-found' | 'forbidden' | 'error'

export function useRecordDetails<T>(id: string | undefined, load: (id: string) => Promise<T>) {
  const [record, setRecord] = useState<T | null>(null)
  const [state, setState] = useState<DetailLoadState>('loading')
  const [retryKey, setRetryKey] = useState(0)

  useEffect(() => {
    let current = true
    setRecord(null)
    if (!id) {
      setState('not-found')
      return
    }
    setState('loading')
    void load(id).then((result) => {
      if (!current) return
      setRecord(result)
      setState('ready')
    }).catch((error: unknown) => {
      if (!current) return
      setState(isHttpError(error) && error.status === 404 ? 'not-found'
        : isHttpError(error) && error.status === 403 ? 'forbidden' : 'error')
    })
    return () => { current = false }
  }, [id, load, retryKey])

  return { record, state, retry: () => setRetryKey((key) => key + 1) }
}
