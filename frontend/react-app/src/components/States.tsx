import { AlertTriangle, CheckCircle2, Loader2 } from 'lucide-react'
import type { ReactNode } from 'react'

export function LoadingState({ label = 'Loading data' }: { label?: string }) {
  return (
    <div className="state-box" role="status">
      <Loader2 className="spin" size={20} aria-hidden="true" />
      <span>{label}</span>
    </div>
  )
}

export function ErrorState({ message }: { message: string }) {
  return (
    <div className="state-box state-box-error" role="alert">
      <AlertTriangle size={20} aria-hidden="true" />
      <span>{message}</span>
    </div>
  )
}

export function EmptyState({ message, title = 'Nothing to show yet', action }: { message: string; title?: string; action?: ReactNode }) {
  return (
    <div className="state-box">
      <CheckCircle2 size={20} aria-hidden="true" />
      <div>
        <strong>{title}</strong>
        <span>{message}</span>
      </div>
      {action ? <div className="state-action">{action}</div> : null}
    </div>
  )
}
