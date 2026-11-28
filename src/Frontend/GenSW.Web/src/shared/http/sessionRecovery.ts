import { SessionExpiredError } from './httpErrors'

const ownerKey = 'gensw.erp-recovery-session.v1'
const commandPrefix = 'gensw.erp-command.v1:'
const legacyCommandPrefixes = [
  'gensw.inventory.movement.retry.v1:',
  'gensw.production.retry.v1:',
  'gensw.egg-bridge.retry.v1',
  'gensw.material-cost.retry.v1:',
  'gensw.optimization.request.v1:',
]

let activeScope = { sessionId: null as string | null }

export function getRecoverySessionId(): string | null {
  try { return window.sessionStorage.getItem(ownerKey) }
  catch { return null }
}

export function bindSessionRecovery(sessionId: string | null): void {
  activeScope = { sessionId }
  try {
    const storage = window.sessionStorage
    if (sessionId !== null && storage.getItem(ownerKey) === sessionId) return

    storage.removeItem(ownerKey)
    for (let index = storage.length - 1; index >= 0; index--) {
      const key = storage.key(index)
      if (key && (key.startsWith(commandPrefix) || legacyCommandPrefixes.some(prefix => key.startsWith(prefix)))) {
        storage.removeItem(key)
      }
    }
    if (sessionId !== null) storage.setItem(ownerKey, sessionId)
  } catch {
    // The login can proceed. Recovery remains unavailable when storage cannot be bound.
  }
}

export function captureSessionRecovery(): Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> {
  const scope = activeScope

  function storage() {
    if (scope !== activeScope) throw new SessionExpiredError()
    const storage = window.sessionStorage
    if (scope.sessionId !== null && storage.getItem(ownerKey) !== scope.sessionId) {
      throw new Error('O navegador não conseguiu vincular a recuperação à sessão atual.')
    }
    return storage
  }

  const scopedKey = (key: string) => scope.sessionId === null ? key : `${commandPrefix}${scope.sessionId}:${key}`
  return {
    getItem: key => storage().getItem(scopedKey(key)),
    setItem: (key, value) => storage().setItem(scopedKey(key), value),
    removeItem: key => storage().removeItem(scopedKey(key)),
  }
}
