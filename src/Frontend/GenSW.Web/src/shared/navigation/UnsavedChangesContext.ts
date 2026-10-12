import { createContext } from 'react'

export interface UnsavedChangesControl {
  setDirty: (owner: string, dirty: boolean) => void
  markSaved: (owner: string) => void
  confirmDiscard: (owner: string | null, action: () => void) => void
}

export const UnsavedChangesContext = createContext<UnsavedChangesControl | null>(null)
