import { useRef, useState } from 'react'
import { inventorySend } from './inventoryService'

// A lost response is retried with the same key and body; edits start a new command.
export function useInventoryMutation() {
  const attempt = useRef<{ payload: string; key: string } | null>(null)
  const sending = useRef(false)
  const [busy, setBusy] = useState(false)
  const submit = async <T,>(path: string, body: unknown, parse: (v: unknown) => T, method: 'POST' | 'PUT' | 'PATCH' = 'POST'): Promise<T | undefined> => {
    if (sending.current) return undefined
    const payload = JSON.stringify({ path, method, body })
    if (attempt.current?.payload !== payload) attempt.current = { payload, key: crypto.randomUUID() }
    sending.current = true; setBusy(true)
    try { return await inventorySend(path, body, parse, attempt.current.key, method) }
    finally { sending.current = false; setBusy(false) }
  }
  const resetAttempt = () => { if (!sending.current) attempt.current = null }
  return { submit, busy, resetAttempt }
}
