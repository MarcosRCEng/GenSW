import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'

interface ListNavigationState<T = unknown> {
  list: { path: string; filters: T }
  parentReturn?: ParentNavigationState['parentReturn']
}

interface ParentNavigationState {
  list?: ListNavigationState['list']
  parentReturn: { path: string; section: string; page?: number; filters?: Record<string, unknown>; origin?: ListNavigationState }
}

const internalRecordPath = (path: string) => /^\/(?:[a-z0-9-]+\/)+[a-zA-Z0-9-]+$/.test(path)
const parentListPath = (path: string) => path.slice(0, path.lastIndexOf('/'))

// Record and parent destinations are supplied by the current route, never by
// browser history or an arbitrary return URL.
export function parentReturnState(state: unknown, parentPath: string, context: { section: string; page?: number; filters?: Record<string, unknown> }): ParentNavigationState | undefined {
  if (!internalRecordPath(parentPath) || !/^[a-z][a-z-]*$/.test(context.section)) return undefined
  const marker = state && typeof state === 'object' && 'parentReturn' in state ? state.parentReturn : undefined
  const nestedOrigin = marker && typeof marker === 'object' && 'path' in marker && marker.path === parentPath && 'origin' in marker ? marker.origin : undefined
  const list = listReturnState(nestedOrigin, parentListPath(parentPath)) ?? listReturnState(state, parentListPath(parentPath))
  return {
    ...list,
    parentReturn: {
      path: parentPath,
      section: context.section,
      ...(list ? { origin: list } : {}),
      ...(Number.isSafeInteger(context.page) && context.page! > 0 ? { page: context.page } : {}),
      ...(context.filters && typeof context.filters === 'object' && !Array.isArray(context.filters) ? { filters: context.filters } : {}),
    },
  }
}

// Relationship-filtered lists keep their explicit parent alongside their own
// filters. The hierarchy is finite: Prole → Ciclo → Cruzamento.
function linkedParentReturnState(state: unknown, listPath: string): ParentNavigationState | undefined {
  const parentRoot = listPath.split('?')[0] === '/ciclos-reprodutivos' ? '/cruzamentos' : listPath.split('?')[0] === '/proles' ? '/ciclos-reprodutivos' : undefined
  if (!parentRoot || !state || typeof state !== 'object' || !('parentReturn' in state)) return undefined
  const parent = state.parentReturn
  if (!parent || typeof parent !== 'object' || !('path' in parent) || typeof parent.path !== 'string' || !parent.path.startsWith(`${parentRoot}/`) || parentListPath(parent.path) !== parentRoot) return undefined
  return readParentReturnState(state, parent.path)
}

export function readParentReturnState(state: unknown, parentPath: string): ParentNavigationState | undefined {
  if (!state || typeof state !== 'object' || !('parentReturn' in state)) return undefined
  const parent = state.parentReturn
  if (!parent || typeof parent !== 'object' || !('path' in parent) || parent.path !== parentPath || !('section' in parent) || typeof parent.section !== 'string') return undefined
  return parentReturnState(state, parentPath, {
    section: parent.section,
    page: 'page' in parent && typeof parent.page === 'number' ? parent.page : undefined,
    filters: 'filters' in parent && parent.filters && typeof parent.filters === 'object' ? parent.filters as Record<string, unknown> : undefined,
  })
}

export function recordEditState(state: unknown, listPath: string, detailPath: string) {
  const origin = readParentReturnState(state, detailPath) ?? readParentReturnState(state, listPath) ?? listReturnState(state, listPath)
  return internalRecordPath(detailPath) ? { ...origin, recordReturn: { path: detailPath } } : origin
}

export function formReturnTarget(state: unknown, listPath: string, detailPath?: string): { to: string; state?: unknown } {
  const list = listReturnState(state, listPath)
  const parent = readParentReturnState(state, detailPath ?? listPath) ?? readParentReturnState(state, listPath)
  if (detailPath && internalRecordPath(detailPath)) {
    if (state && typeof state === 'object' && 'recordReturn' in state) {
      const record = state.recordReturn
      if (record && typeof record === 'object' && 'path' in record && record.path === detailPath) return { to: detailPath, state: parent ?? list }
    }
    if (!list) return { to: detailPath, state: parent }
  }
  return { to: list?.list.path ?? listPath, state: parent ?? list }
}

export function savedRecordState(state: unknown, targetPath: string) {
  const origin = state && typeof state === 'object' && !Array.isArray(state) ? state : undefined
  return /^\/[a-zA-Z0-9/?=&%_.-]*$/.test(targetPath) && !targetPath.startsWith('//') ? { ...origin, recordSavedPath: targetPath } : origin
}

export function useRestoredParentSection<T extends object>(parentPath: string, section: string, defaults: T): T {
  const location = useLocation()
  const [initial] = useState(() => {
    const saved = readParentReturnState(location.state, parentPath)?.parentReturn
    return saved?.section === section ? { ...defaults, ...saved.filters as Partial<T>, ...(saved.page ? { page: saved.page } : {}) } : defaults
  })
  return initial
}

export function useRestoreParentSection(parentPath: string, ready: boolean) {
  const location = useLocation()
  const section = readParentReturnState(location.state, parentPath)?.parentReturn.section
  useEffect(() => {
    if (!ready || !section || location.pathname !== parentPath) return
    const element = document.getElementById(`record-${section}`)
    element?.focus()
    element?.scrollIntoView?.({ block: 'start' })
  }, [location.pathname, parentPath, ready, section])
}

export function listReturnState(state: unknown, path: string): ListNavigationState | undefined {
  if (!state || typeof state !== 'object' || !('list' in state)) return undefined
  const list = state.list
  if (!list || typeof list !== 'object' || !('path' in list) || typeof list.path !== 'string' || !('filters' in list)) return undefined
  if (list.path !== path && (path.includes('?') || !list.path.startsWith(`${path}?`))) return undefined
  if (!list.filters || typeof list.filters !== 'object' || Array.isArray(list.filters)) return undefined
  return { ...linkedParentReturnState(state, path), list: { path: list.path, filters: list.filters } }
}

export function useRestoredListState<T extends object>(path: string, defaults: T): T {
  const location = useLocation()
  const [initial] = useState(() => {
    const saved = listReturnState(location.state, path)
    return saved ? { ...defaults, ...saved.list.filters as Partial<T> } : defaults
  })
  return initial
}

// History state keeps a list's filters and pagination when returning, without
// putting search terms in the URL or retaining records in browser storage.
export function useRememberListState<T extends object>(path: string, filters: T): ListNavigationState<T> {
  const location = useLocation()
  const navigate = useNavigate()
  const savedPath = location.state && typeof location.state === 'object' && location.state.recordSavedPath === `${location.pathname}${location.search}` ? location.state.recordSavedPath as string : undefined
  const feedback = savedPath ? { recordSavedPath: savedPath } : {}
  const state = { ...linkedParentReturnState(location.state, path), list: { path, filters }, ...feedback }
  const serialized = JSON.stringify(state)
  useEffect(() => {
    if (JSON.stringify({ ...listReturnState(location.state, path), ...(savedPath ? { recordSavedPath: savedPath } : {}) }) === serialized) return
    void navigate(`${location.pathname}${location.search}${location.hash}`, { replace: true, state: JSON.parse(serialized) })
  }, [location.hash, location.pathname, location.search, location.state, navigate, path, savedPath, serialized])
  return state
}
