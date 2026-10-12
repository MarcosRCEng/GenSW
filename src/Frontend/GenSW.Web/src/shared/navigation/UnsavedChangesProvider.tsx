import { useCallback, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react'
import { useBlocker } from 'react-router-dom'
import { ModalDialog } from '../layout/ModalDialog'
import { UnsavedChangesContext, type UnsavedChangesControl } from './UnsavedChangesContext'

export function UnsavedChangesProvider({ children }: PropsWithChildren) {
  const owners = useRef(new Set<string>())
  const [count, setCount] = useState(0)
  const [pending, setPending] = useState<{ owner: string | null; action: () => void } | null>(null)
  const continueButton = useRef<HTMLButtonElement>(null)
  const setDirty = useCallback((owner: string, dirty: boolean) => {
    if (dirty) owners.current.add(owner)
    else owners.current.delete(owner)
    setCount(owners.current.size)
  }, [])
  const markSaved = useCallback((owner: string) => setDirty(owner, false), [setDirty])
  const confirmDiscard = useCallback((owner: string | null, action: () => void) => {
    const dirty = owner === null ? owners.current.size > 0 : owners.current.has(owner)
    if (dirty) setPending({ owner, action })
    else action()
  }, [])
  const control = useMemo<UnsavedChangesControl>(() => ({ setDirty, markSaved, confirmDiscard }), [setDirty, markSaved, confirmDiscard])
  const blocker = useBlocker(({ currentLocation, nextLocation }) => owners.current.size > 0
    && nextLocation.pathname !== '/login'
    && (currentLocation.pathname !== nextLocation.pathname || currentLocation.search !== nextLocation.search || currentLocation.hash !== nextLocation.hash))

  useEffect(() => {
    if (!count) return
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (!owners.current.size) return
      event.preventDefault(); event.returnValue = ''
    }
    window.addEventListener('beforeunload', beforeUnload)
    return () => window.removeEventListener('beforeunload', beforeUnload)
  }, [count])

  const stay = () => {
    setPending(null)
    if (blocker.state === 'blocked') blocker.reset()
  }
  const discard = () => {
    if (pending) {
      if (pending.owner === null) owners.current.clear()
      else owners.current.delete(pending.owner)
      setCount(owners.current.size)
      setPending(null)
      pending.action()
    } else if (blocker.state === 'blocked') {
      owners.current.clear(); setCount(0); blocker.proceed()
    }
  }
  return <UnsavedChangesContext.Provider value={control}>
    {children}
    <ModalDialog open={pending !== null || blocker.state === 'blocked'} title="Descartar alterações?" onClose={stay} closeLabel="Fechar diálogo e continuar editando" initialFocusRef={continueButton} footer={<>
      <button className="rounded-lg border border-slate-300 bg-white px-4 py-2 font-semibold" onClick={stay} ref={continueButton} type="button">Continuar editando</button>
      <button className="rounded-lg bg-emerald-700 px-4 py-2 font-semibold text-white" onClick={discard} type="button">Descartar e sair</button>
    </>}>
      <p>Há alterações não salvas. Você pode continuar editando ou descartá-las para seguir ao destino escolhido.</p>
    </ModalDialog>
  </UnsavedChangesContext.Provider>
}
