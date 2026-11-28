import { createMemoryRouter } from 'react-router-dom'
import { invalidateSession, setAccessToken } from '../http/sessionManager'
import { createSessionNavigation } from './sessionNavigation'

it('mantém dados privados de navegação em memória e descarta os de outra sessão', async () => {
  const navigation = createSessionNavigation()
  const router = createMemoryRouter([{ path: '*' }])
  navigation.attach(router)
  try {
    setAccessToken('token A', 'sessão A')
    const fromAccountA = { selected: [{ id: 'coincidente', nome: 'Conta A' }] }
    await router.navigate('/registro', { state: fromAccountA })
    const historyState = router.state.location.state
    expect(JSON.stringify(historyState)).not.toContain('Conta A')
    expect(navigation.read(historyState)).toEqual(fromAccountA)

    setAccessToken('token B', 'sessão B')
    expect(navigation.read(historyState)).toBeNull()
    expect(navigation.read(fromAccountA)).toBeNull()

    const fromAccountB = { selected: [{ id: 'coincidente', nome: 'Conta B' }] }
    await router.navigate('/registro', { state: fromAccountB })
    expect(navigation.read(router.state.location.state)).toEqual(fromAccountB)
  } finally {
    invalidateSession()
    router.dispose()
  }
})
