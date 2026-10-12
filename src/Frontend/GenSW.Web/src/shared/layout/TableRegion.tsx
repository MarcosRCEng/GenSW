import { useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react'

type TableRegionProps = {
  label: string
  className?: string
  children: ReactNode
}

/** Keeps complete operational tables scrollable without widening the page. */
export function TableRegion({ label, className, children }: TableRegionProps) {
  const region = useRef<HTMLDivElement>(null)
  const descriptionId = useId()
  const [overflow, setOverflow] = useState(false)

  useLayoutEffect(() => {
    const element = region.current
    if (!element) return
    const measure = () => setOverflow(element.scrollWidth > element.clientWidth + 1)
    measure()
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(measure)
    observer?.observe(element)
    for (const table of element.querySelectorAll('table')) observer?.observe(table)
    window.addEventListener('resize', measure)
    return () => {
      observer?.disconnect()
      window.removeEventListener('resize', measure)
    }
  }, [children])

  return <div className="table-region">
    {overflow && <p className="table-region-notice" id={descriptionId}>Role a tabela horizontalmente para ver todas as colunas. Com o foco na tabela, use as setas esquerda e direita.</p>}
    <div
      className={`table-region-scroll${className ? ` ${className}` : ''}`}
      ref={region}
      role={overflow ? 'region' : undefined}
      aria-label={overflow ? label : undefined}
      aria-describedby={overflow ? descriptionId : undefined}
      tabIndex={overflow ? 0 : undefined}
    >{children}</div>
  </div>
}
