export function StatusPill({ label, tone = 'neutral' }: { label: string; tone?: 'neutral' | 'good' | 'warn' | 'bad' | 'info' }) {
  return <span className={`status-pill status-${tone}`}>{label}</span>
}
