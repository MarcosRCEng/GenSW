import { useEffect, useRef } from 'react'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'

// Capture the loaded baseline once. Failed submissions/retries keep the draft.
export function useFormDraft(value: unknown, ready = true) {
  const serialized = JSON.stringify(value)
  const baseline = useRef<string | null>(ready ? serialized : null)
  useEffect(() => { if (ready && baseline.current === null) baseline.current = serialized }, [ready, serialized])
  return useUnsavedChanges(baseline.current !== null && baseline.current !== serialized)
}
