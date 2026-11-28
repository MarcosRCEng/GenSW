import type { createBrowserRouter } from 'react-router-dom'
import { getSessionSnapshot, subscribeToSessionGenerationChange } from '../http/sessionManager'

const stateKey = 'genswNavigation'

export function createSessionNavigation() {
  const states = new Map<string, unknown>()
  subscribeToSessionGenerationChange(() => states.clear())

  return {
    attach(router: ReturnType<typeof createBrowserRouter>) {
      const navigate = router.navigate
      router.navigate = (to: number | Parameters<typeof navigate>[0], options?: Parameters<typeof navigate>[1]) => {
        if (typeof to === 'number') return navigate(to)
        if (options?.state == null) return navigate(to, options)
        if (getSessionSnapshot().accessToken === null) return navigate(to, { ...options, state: null })

        const key = crypto.randomUUID()
        states.set(key, options.state)
        if (states.size > 100) {
          const oldest = states.keys().next()
          if (!oldest.done) states.delete(oldest.value)
        }
        return navigate(to, { ...options, state: { [stateKey]: key } })
      }
    },
    read(state: unknown): unknown {
      if (state === null || typeof state !== 'object' || !(stateKey in state)) return null
      const key = state[stateKey]
      return typeof key === 'string' ? states.get(key) ?? null : null
    },
  }
}
