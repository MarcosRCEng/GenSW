import { listReturnState, readParentReturnState } from '../../../shared/details/listNavigation'

export interface VarietyImageContext {
  active: boolean
  page: number
  imageId?: string
}

const varietyRoute = /^(\/variedades\/[a-zA-Z0-9-]+)(\/editar)?\/?$/
const recordId = /^[a-zA-Z0-9-]+$/

export function readVarietyImageContext(state: unknown, path: string): VarietyImageContext | undefined {
  if (!varietyRoute.test(path) || !state || typeof state !== 'object') return
  if (!('varietyImages' in state)) {
    const previous = readParentReturnState(state, path)?.parentReturn
    if (previous?.section !== 'imagens') return
    const active = previous.filters?.active
    return { active: typeof active === 'boolean' ? active : true, page: previous.page ?? 1 }
  }
  const context = state.varietyImages
  if (!context || typeof context !== 'object' || !('path' in context) || context.path !== path
    || !('active' in context) || typeof context.active !== 'boolean'
    || !('page' in context) || typeof context.page !== 'number' || !Number.isSafeInteger(context.page) || context.page < 1) return
  const imageId = 'imageId' in context && typeof context.imageId === 'string' && recordId.test(context.imageId)
    ? context.imageId : undefined
  return { active: context.active, page: context.page, imageId }
}

export function varietyImageNavigationState(state: unknown, path: string, context: VarietyImageContext) {
  const [, detailPath, editSegment] = varietyRoute.exec(path) ?? []
  const record = state && typeof state === 'object' && 'recordReturn' in state ? state.recordReturn : undefined
  const returnsToDetail = editSegment && record && typeof record === 'object'
    && 'path' in record && record.path === detailPath
  return {
    ...listReturnState(state, '/variedades'),
    ...(returnsToDetail ? { recordReturn: { path: detailPath } } : {}),
    varietyImages: { path, active: context.active, page: context.page, imageId: context.imageId },
  }
}

export function varietyImageReturnState(state: unknown, path: string, imageId?: string) {
  const saved = readVarietyImageContext(state, path)
  const context = saved && (!saved.imageId || saved.imageId === imageId) ? saved : { active: true, page: 1 }
  return varietyImageNavigationState(state, path, { ...context, imageId })
}
