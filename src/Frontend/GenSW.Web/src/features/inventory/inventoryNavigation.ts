import { listReturnState } from '../../shared/details/listNavigation'

const lists = {
  locais: { path: '/estoque/locais', label: 'Locais' },
  lotes: { path: '/estoque/lotes', label: 'Lotes' },
  saldos: { path: '/estoque/saldos', label: 'Saldos' },
  movimentos: { path: '/estoque/movimentos', label: 'Movimentos' },
  reconciliacao: { path: '/estoque/reconciliacao', label: 'Reconciliação' },
} as const

// Only known inventory lists can supply a return path; retain the shared history contract.
export function inventoryOrigin(state: unknown, fallback: keyof typeof lists) {
  for (const list of Object.values(lists)) {
    const saved = listReturnState(state, list.path)
    if (saved) return { ...list, state: saved }
  }
  return { ...lists[fallback], state: undefined }
}
