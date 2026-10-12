import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { PageHeader } from './PageHeader'

describe('PageHeader feedback', () => {
  it('anuncia salvamento para o destino exato, incluindo sua relação na query', () => {
    render(<MemoryRouter initialEntries={[{ pathname: '/proles', search: '?cicloReprodutivoId=cycle', state: { recordSavedPath: '/proles?cicloReprodutivoId=cycle' } }]}>
      <PageHeader title="Proles" />
    </MemoryRouter>)
    expect(screen.getByRole('status')).toHaveTextContent('Registro salvo.')
    expect(screen.getByRole('heading', { name: 'Proles' })).toHaveFocus()
  })

  it.each(['/pessoas/outro', 'https://example.test/pessoas/record', '/pessoas/record?outro=1'])('ignora o marcador de outro destino %s e conteúdo arbitrário', recordSavedPath => {
    render(<MemoryRouter initialEntries={[{ pathname: '/pessoas/record', state: { recordSavedPath, message: 'Mensagem não autorizada' } }]}>
      <PageHeader title="Pessoa" />
    </MemoryRouter>)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(screen.queryByText('Mensagem não autorizada')).not.toBeInTheDocument()
  })
})
