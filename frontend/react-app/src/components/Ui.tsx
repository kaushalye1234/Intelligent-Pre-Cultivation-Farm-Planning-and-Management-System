import { X } from 'lucide-react'
import { useEffect } from 'react'
import type { ButtonHTMLAttributes, FormEvent, ReactNode } from 'react'

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  icon?: ReactNode
}

export function Button({ variant = 'primary', icon, children, className = '', type = 'button', ...props }: ButtonProps) {
  return (
    <button type={type} className={`ui-button ui-button-${variant} ${className}`.trim()} {...props}>
      {icon ? <span className="ui-button-icon">{icon}</span> : null}
      <span>{children}</span>
    </button>
  )
}

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
  children,
}: {
  eyebrow?: string
  title: string
  description?: string
  actions?: ReactNode
  children?: ReactNode
}) {
  return (
    <header className="page-header">
      <div className="page-header-copy">
        {eyebrow ? <p>{eyebrow}</p> : null}
        <h1>{title}</h1>
        {description ? <span>{description}</span> : null}
      </div>
      {actions ? <div className="page-header-actions">{actions}</div> : null}
      {children ? <div className="page-header-tools">{children}</div> : null}
    </header>
  )
}

export function MetricCard({
  label,
  value,
  description,
  icon,
  tone = 'neutral',
}: {
  label: string
  value: number | string
  description?: string
  icon?: ReactNode
  tone?: 'neutral' | 'good' | 'warn' | 'bad'
}) {
  return (
    <article className={`metric-card metric-${tone}`}>
      <div className="metric-card-top">
        {icon ? <span className="metric-icon">{icon}</span> : null}
        <span>{label}</span>
      </div>
      <strong>{value}</strong>
      {description ? <p>{description}</p> : null}
    </article>
  )
}

export type TabItem = {
  id: string
  label: string
  count?: number
}

export function Tabs({
  tabs,
  activeTab,
  onChange,
  ariaLabel,
}: {
  tabs: TabItem[]
  activeTab: string
  onChange: (tab: string) => void
  ariaLabel: string
}) {
  return (
    <div className="tabs" role="tablist" aria-label={ariaLabel}>
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={activeTab === tab.id}
          className={activeTab === tab.id ? 'active' : ''}
          onClick={() => onChange(tab.id)}
        >
          <span>{tab.label}</span>
          {typeof tab.count === 'number' ? <strong>{tab.count}</strong> : null}
        </button>
      ))}
    </div>
  )
}

export function Toolbar({ children }: { children: ReactNode }) {
  return <div className="toolbar">{children}</div>
}

export function Modal({
  open,
  title,
  description,
  onClose,
  children,
  footer,
}: {
  open: boolean
  title: string
  description?: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
}) {
  useEffect(() => {
    if (!open) return

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onClose, open])

  if (!open) return null

  return (
    <div className="modal-backdrop" role="presentation">
      <section className="modal-panel" role="dialog" aria-modal="true" aria-labelledby="modal-title">
        <header className="modal-header">
          <div>
            <h2 id="modal-title">{title}</h2>
            {description ? <p>{description}</p> : null}
          </div>
          <button type="button" className="icon-button surface-icon-button" onClick={onClose} aria-label="Close dialog">
            <X size={18} aria-hidden="true" />
          </button>
        </header>
        <div className="modal-body">{children}</div>
        {footer ? <footer className="modal-footer">{footer}</footer> : null}
      </section>
    </div>
  )
}

export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel,
  variant = 'primary',
  isSubmitting,
  onCancel,
  onConfirm,
}: {
  open: boolean
  title: string
  message: string
  confirmLabel: string
  variant?: 'primary' | 'danger'
  isSubmitting?: boolean
  onCancel: () => void
  onConfirm: () => void | Promise<void>
}) {
  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await onConfirm()
  }

  return (
    <Modal
      open={open}
      title={title}
      onClose={onCancel}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button variant={variant} type="submit" form="confirm-dialog-form" disabled={isSubmitting}>
            {isSubmitting ? 'Working...' : confirmLabel}
          </Button>
        </>
      }
    >
      <form id="confirm-dialog-form" onSubmit={(event) => void handleSubmit(event)}>
        <p className="modal-message">{message}</p>
      </form>
    </Modal>
  )
}

export function Notice({ tone = 'info', children }: { tone?: 'success' | 'warning' | 'error' | 'info'; children: ReactNode }) {
  return (
    <div className={`notice notice-${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  )
}
