import { useEffect, useRef, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../features/auth/hooks/useAuth'
import { activeNavigation, modulePages, modulesForArea, sortedFunctionalAreas, type NavigationModule } from '../../features/auth/navigation/modules'
import { ModalDialog } from './ModalDialog'
import { useConfirmDiscard } from '../navigation/useUnsavedChanges'
import './AppShell.css'

export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const active = activeNavigation(location.pathname)
  const area = sortedFunctionalAreas.find((entry) => entry.id === active?.module.area)
  const [menuOpen, setMenuOpen] = useState(false)
  const [selectedModule, setSelectedModule] = useState<NavigationModule | null>(null)
  const [isLoggingOut, setIsLoggingOut] = useState(false)
  const content = useRef<HTMLDivElement>(null)
  const menuContent = useRef<HTMLDivElement>(null)
  const confirmDiscard = useConfirmDiscard()
  const modalTitle = selectedModule?.name ?? 'Módulos'

  useEffect(() => {
    setMenuOpen(false)
    setSelectedModule(null)
    const frame = requestAnimationFrame(() => {
      const heading = content.current?.querySelector<HTMLHeadingElement>('h1')
      if (heading) { heading.tabIndex = -1; heading.focus() }
    })
    return () => cancelAnimationFrame(frame)
  }, [location.pathname])

  useEffect(() => {
    if (!menuOpen) return
    const frame = requestAnimationFrame(() => menuContent.current?.querySelector<HTMLElement>('a[href],button')?.focus())
    return () => cancelAnimationFrame(frame)
  }, [menuOpen, selectedModule])

  const openModule = (module: NavigationModule) => {
    setSelectedModule(module)
    setMenuOpen(true)
  }

  const openMenu = () => {
    setSelectedModule(null)
    setMenuOpen(true)
  }

  const handleLogout = async () => {
    if (isLoggingOut) return
    setIsLoggingOut(true)
    try {
      await logout()
    } catch {
      // AuthProvider clears the local session even if remote revocation fails.
    } finally {
      navigate('/login', { replace: true })
    }
  }

  const moduleList = (idPrefix: string) => <nav aria-label="Módulos disponíveis" className="app-module-groups">
    {sortedFunctionalAreas.map((group) => <div role="group" key={group.id} aria-labelledby={`${idPrefix}-${group.id}`}>
      <h2 id={`${idPrefix}-${group.id}`}>{group.name}</h2>
      <ul>{modulesForArea(group.id, true).map((module) => <li key={module.id}>
        <button type="button" className={`app-module-button ${active?.module.id === module.id ? 'is-active' : ''}`}
          aria-expanded={menuOpen && selectedModule?.id === module.id}
          aria-controls="app-navigation-pages" aria-haspopup="dialog" onClick={() => openModule(module)}>
          <span>{module.name}</span><span aria-hidden="true">›</span>
        </button>
      </li>)}</ul>
    </div>)}
  </nav>

  return <div className="app-shell">
    <a className="app-skip-link" href="#app-content" onClick={(event) => {
      event.preventDefault()
      const heading = content.current?.querySelector<HTMLHeadingElement>('h1')
      if (heading) { heading.tabIndex = -1; heading.focus() }
      else content.current?.focus()
    }}>Ir para o conteúdo</a>
    <nav className="app-rail" aria-label="Navegação global">
      <Link className="app-brand" to="/" aria-label="Ir para Início" aria-current={location.pathname === '/' ? 'page' : undefined}>G</Link>
      <button type="button" onClick={openMenu} aria-label="Explorar módulos" aria-haspopup="dialog"
        aria-expanded={menuOpen}><span aria-hidden="true">▦</span></button>
      <span className="app-rail-name" aria-hidden="true">GenSW</span>
    </nav>
    <aside className="app-modules" aria-label="Menu principal">
      <div className="app-modules-brand">GenSW<span>Gestão agropecuária</span></div>
      {moduleList('sidebar-area')}
    </aside>
    <div className="app-workspace">
      <header className="app-topbar">
        <button className="app-menu-toggle" type="button" onClick={openMenu}
          aria-label="Abrir navegação" aria-haspopup="dialog" aria-expanded={menuOpen}><span aria-hidden="true">☰</span></button>
        <p className="app-location">{active ? `${area?.name} / ${active.module.name}` : 'Início'}</p>
        <div className="app-session">
          <span className="app-session-name">{user?.nome}</span>
          <button type="button" onClick={() => confirmDiscard(() => { void handleLogout() })} disabled={isLoggingOut}>{isLoggingOut ? 'Saindo…' : 'Sair'}</button>
        </div>
      </header>
      <div className="app-content" id="app-content" ref={content} tabIndex={-1}><Outlet /></div>
    </div>
    <ModalDialog open={menuOpen} title={modalTitle} onClose={() => setMenuOpen(false)}
      className="app-navigation-dialog" closeLabel="Fechar navegação">
      {selectedModule ? <div id="app-navigation-pages" ref={menuContent}>
        <button className="app-module-back" type="button" onClick={() => setSelectedModule(null)}><span aria-hidden="true">← </span>Voltar aos módulos</button>
        <p className="app-module-description">{selectedModule.description}</p>
        <nav aria-label={`Páginas de ${selectedModule.name}`} className="app-pages">
          <h3>Páginas</h3>
          <ul>{modulePages(selectedModule).map((page) => <li key={page.id}>
            <Link to={page.route} aria-current={active?.module.id === selectedModule.id && active?.page?.id === page.id ? 'page' : undefined}
              onClick={() => setMenuOpen(false)}>{page.name}<span aria-hidden="true">→</span></Link>
          </li>)}</ul>
        </nav>
      </div> : <div id="app-navigation-pages" ref={menuContent}>
        <Link className="app-drawer-home" to="/" aria-current={location.pathname === '/' ? 'page' : undefined} onClick={() => setMenuOpen(false)}>Início</Link>
        {moduleList('drawer-area')}
      </div>}
    </ModalDialog>
  </div>
}
