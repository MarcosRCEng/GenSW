import { useEffect, useRef, useState } from 'react'
import { message } from '../formulation/api'
import { isHttpError } from '../../shared/http/httpErrors'
import { isGuid } from './api'

export function useRecoverableCommand<TBody, TResult>(scope: string, send: (body: TBody, key: string) => Promise<TResult>, lookup: (key: string) => Promise<TResult>) {
  const storageKey = `gensw.optimization.request.v1:${scope}`
  const [key, setKey] = useState(() => {
    try {
      const stored = sessionStorage.getItem(storageKey)
      return isGuid(stored) ? stored : null
    } catch { return null }
  })
  const currentKey = useRef(key)
  const originalBody = useRef<TBody>()
  const inFlight = useRef(false)
  const mounted = useRef(false)
  const [busy, setBusy] = useState(false)
  const [absent, setAbsent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [errorStatus, setErrorStatus] = useState<number | null>(null)

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  function save(value: string | null) {
    if (value) sessionStorage.setItem(storageKey, value)
    else sessionStorage.removeItem(storageKey)
    currentKey.current = value
    setKey(value)
    setAbsent(false)
  }

  async function run(requestKey: string, body?: TBody) {
    if (inFlight.current) return
    inFlight.current = true
    setBusy(true)
    setAbsent(false)
    setError(null)
    setErrorStatus(null)
    try {
      const result = await (body === undefined ? lookup(requestKey) : send(body, requestKey))
      if (!mounted.current) return
      save(null)
      originalBody.current = undefined
      return result
    } catch (e) {
      if (!mounted.current) return
      if (isHttpError(e) && body === undefined && e.status === 404) {
        setAbsent(true)
      } else {
        setErrorStatus(isHttpError(e) ? e.status : null)
        if (isHttpError(e) && body !== undefined && [400, 403, 404, 409].includes(e.status)) {
          save(null)
          originalBody.current = undefined
        }
        setError(isHttpError(e) && e.status === 429
          ? 'Capacidade de cálculo ocupada. Consulte o pedido antes de reenviá-lo.'
          : message(e))
      }
    } finally {
      inFlight.current = false
      if (mounted.current) setBusy(false)
    }
  }

  function submit(body: TBody) {
    if (currentKey.current || inFlight.current) return
    const requestKey = crypto.randomUUID()
    try {
      save(requestKey)
    } catch {
      setError('O navegador não conseguiu guardar a chave para recuperar este pedido.')
      return
    }
    originalBody.current = body
    return run(requestKey, body)
  }

  return {
    key, busy, absent, error, errorStatus, canRetry: absent && originalBody.current !== undefined,
    submit,
    consult: () => currentKey.current ? run(currentKey.current) : undefined,
    retry: () => absent && currentKey.current && originalBody.current !== undefined ? run(currentKey.current, originalBody.current) : undefined,
    clearError: () => { if (!currentKey.current) { setError(null); setErrorStatus(null) } },
    clear: () => {
      if (!absent || inFlight.current) return
      save(null)
      originalBody.current = undefined
      setError(null)
      setErrorStatus(null)
    },
  }
}
