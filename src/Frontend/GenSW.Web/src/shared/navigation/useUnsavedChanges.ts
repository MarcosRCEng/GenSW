import { useCallback, useContext, useEffect, useId } from 'react'
import { UnsavedChangesContext } from './UnsavedChangesContext'

export function useUnsavedChanges(dirty: boolean) {
  const control = useContext(UnsavedChangesContext)
  const owner = useId()
  useEffect(() => {
    control?.setDirty(owner, dirty)
    return () => control?.setDirty(owner, false)
  }, [control, owner, dirty])
  useEffect(() => {
    if (control || !dirty) return
    const beforeUnload = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = '' }
    window.addEventListener('beforeunload', beforeUnload)
    return () => window.removeEventListener('beforeunload', beforeUnload)
  }, [control, dirty])
  const markSaved = useCallback(() => control?.markSaved(owner), [control, owner])
  const confirmDiscard = useCallback((action: () => void) => {
    if (control) control.confirmDiscard(owner, action)
    else action()
  }, [control, owner])
  return { markSaved, confirmDiscard }
}

// Session commands are confirmed before auth state is cleared. Forced session
// invalidation still follows the existing login redirect and never exposes data.
export function useConfirmDiscard() {
  const control = useContext(UnsavedChangesContext)
  return useCallback((action: () => void) => {
    if (control) control.confirmDiscard(null, action)
    else action()
  }, [control])
}
