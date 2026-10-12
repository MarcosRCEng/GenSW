import { useId, useLayoutEffect, useRef, type ReactNode, type RefObject } from 'react'
import './ModalDialog.css'

interface ModalDialogProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
  className?: string
  closeLabel?: string
  initialFocusRef?: RefObject<HTMLElement>
}

export function ModalDialog({ open, title, onClose, children, footer, className = '', closeLabel = 'Fechar painel', initialFocusRef }: ModalDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const closeButton = useRef<HTMLButtonElement>(null)
  const titleId = useId()

  useLayoutEffect(() => {
    const node = dialog.current
    if (!node || !open) return
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null
    if (typeof node.showModal === 'function') node.showModal()
    else node.setAttribute('open', '')
    ;(initialFocusRef?.current ?? closeButton.current)?.focus()
    return () => {
      if (typeof node.close === 'function' && node.open) node.close()
      else node.removeAttribute('open')
      if (previous?.isConnected) previous.focus({ preventScroll: true })
    }
  }, [open, initialFocusRef])

  if (!open) return null
  return <dialog
    aria-labelledby={titleId}
    aria-modal="true"
    className={`gensw-dialog ${className}`}
    ref={dialog}
    onCancel={event => { event.preventDefault(); onClose() }}
    onClick={event => {
      if (event.target !== event.currentTarget) return
      const bounds = event.currentTarget.getBoundingClientRect()
      if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) onClose()
    }}
    onKeyDown={event => {
      if (event.key !== 'Tab') return
      const controls = [...event.currentTarget.querySelectorAll<HTMLElement>('a[href],button:not(:disabled),input:not(:disabled),select:not(:disabled),textarea:not(:disabled),[tabindex="0"]')].filter(element => element.getClientRects().length > 0)
      const first = controls[0], last = controls.at(-1)
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
    }}
  >
    <header className="gensw-dialog-header"><h2 id={titleId}>{title}</h2><button aria-label={closeLabel} className="gensw-dialog-close" onClick={onClose} ref={closeButton} type="button">×</button></header>
    <div className="gensw-dialog-body">{children}</div>
    {footer && <footer className="gensw-dialog-footer">{footer}</footer>}
  </dialog>
}
