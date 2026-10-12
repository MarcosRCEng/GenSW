import { useEffect, useRef, type ReactNode } from 'react'
import { Link, useLocation, type To } from 'react-router-dom'

interface HeaderLink {
  to: To
  label: string
  state?: unknown
}

interface PageHeaderProps {
  title: string
  description?: string
  context?: ReactNode
  back?: HeaderLink
  actions?: ReactNode
  breadcrumb?: readonly { label: string; to?: To; state?: unknown }[]
}

export function PageHeader({ title, description, context, back, actions, breadcrumb }: PageHeaderProps) {
  const heading = useRef<HTMLHeadingElement>(null)
  const { pathname, search, state } = useLocation()
  const saved = Boolean(state && typeof state === 'object' && 'recordSavedPath' in state && state.recordSavedPath === `${pathname}${search}`)
  useEffect(() => { heading.current?.focus() }, [pathname])

  return <header className="page-header">
    {breadcrumb?.length ? <nav aria-label="Caminho da página" className="page-breadcrumb">
      <ol>{breadcrumb.map((item, index) => <li key={`${item.label}-${index}`}>
        {item.to ? <Link to={item.to} state={item.state}>{item.label}</Link> : <span>{item.label}</span>}
      </li>)}</ol>
    </nav> : null}
    {back ? <Link className="page-back" to={back.to} state={back.state}>
      <span aria-hidden="true">←</span> {back.label}
    </Link> : null}
    <div className="page-header-row">
      <div className="page-header-copy">
        <h1 className="page-title" ref={heading} tabIndex={-1}>{title}</h1>
        {context ? <div className="page-context">{context}</div> : null}
        {description ? <p className="page-description">{description}</p> : null}
      </div>
      {actions ? <div className="page-header-actions">{actions}</div> : null}
    </div>
    {saved ? <p className="page-saved-notice" role="status">Registro salvo.</p> : null}
  </header>
}
