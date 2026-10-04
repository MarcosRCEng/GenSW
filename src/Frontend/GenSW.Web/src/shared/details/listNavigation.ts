import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'

interface ListNavigationState<T = unknown> {
  list: { path: string; filters: T }
}

export function listReturnState(state: unknown, path: string): ListNavigationState | undefined {
  if (!state || typeof state !== 'object' || !('list' in state)) return undefined
  const list = state.list
  if (!list || typeof list !== 'object' || !('path' in list) || typeof list.path !== 'string' || !('filters' in list)) return undefined
  if (list.path !== path && (path.includes('?') || !list.path.startsWith(`${path}?`))) return undefined
  if (!list.filters || typeof list.filters !== 'object') return undefined
  return { list: { path: list.path, filters: list.filters } }
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
  const state = { list: { path, filters } }
  const serialized = JSON.stringify(state)
  useEffect(() => {
    if (JSON.stringify(listReturnState(location.state, path)) === serialized) return
    void navigate(`${location.pathname}${location.search}${location.hash}`, { replace: true, state: JSON.parse(serialized) })
  }, [location.hash, location.pathname, location.search, location.state, navigate, path, serialized])
  return state
}
