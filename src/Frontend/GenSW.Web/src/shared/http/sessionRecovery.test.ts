import { SessionExpiredError } from './httpErrors'
import { invalidateSession, setAccessToken } from './sessionManager'
import { bindSessionRecovery, captureSessionRecovery } from './sessionRecovery'

const key = 'gensw.production.retry.v1:order-1'

afterEach(() => { invalidateSession(); sessionStorage.clear() })

it('permite recuperação na mesma sessão e impede leitura ou gravação tardia após trocar a conta', () => {
  sessionStorage.setItem(key, 'comando legado sem sessão')
  sessionStorage.setItem('preferência local', 'preservada')
  setAccessToken('token A', 'sessão A')
  const sessionA = captureSessionRecovery()
  expect(sessionA.getItem(key)).toBeNull()
  sessionA.setItem(key, 'pedido A')

  bindSessionRecovery('sessão A')
  expect(captureSessionRecovery().getItem(key)).toBe('pedido A')

  setAccessToken('token B', 'sessão B')
  const sessionB = captureSessionRecovery()
  expect(sessionB.getItem(key)).toBeNull()
  expect(Object.values(sessionStorage)).not.toContain('pedido A')
  expect(() => sessionA.setItem(key, 'resposta tardia A')).toThrow(SessionExpiredError)
  sessionB.setItem(key, 'pedido B')
  expect(() => sessionA.removeItem(key)).toThrow(SessionExpiredError)
  expect(sessionB.getItem(key)).toBe('pedido B')

  invalidateSession()
  expect(Object.values(sessionStorage)).not.toContain('pedido B')
  expect(sessionStorage.getItem('preferência local')).toBe('preservada')
})
