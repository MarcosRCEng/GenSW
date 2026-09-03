import { expect, it, vi } from 'vitest'
import { logAuthDiagnostic, type AuthDiagnosticDetails } from './authDiagnostics'

it('registra cada diagnóstico como uma linha JSON autocontida e segura', () => {
  const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => undefined)

  logAuthDiagnostic({
    event: 'login.flow_aborted',
    errorType: 'NetworkError',
    stage: 'request_access_token',
    tokenPresent: false,
  })

  expect(infoSpy).toHaveBeenCalledWith(
    '[GenSW auth] {"event":"login.flow_aborted","errorType":"NetworkError","stage":"request_access_token","tokenPresent":false}',
  )
})

it('descarta propriedades sensíveis adicionadas ao payload em runtime', () => {
  const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => undefined)
  const unsafeDetails = {
    event: 'login.post.started',
    password: 'SENSITIVE_PASSWORD_AUTH_309',
    accessToken: 'SENSITIVE_ACCESS_TOKEN_AUTH_309',
    refreshToken: 'SENSITIVE_REFRESH_TOKEN_AUTH_309',
    authorization: 'Bearer SENSITIVE_ACCESS_TOKEN_AUTH_309',
    cookie: 'GenSW.RefreshSession=SENSITIVE_REFRESH_TOKEN_AUTH_309',
  } as AuthDiagnosticDetails

  logAuthDiagnostic(unsafeDetails)

  expect(infoSpy).toHaveBeenCalledWith('[GenSW auth] {"event":"login.post.started"}')
})
