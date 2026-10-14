import { useState } from 'react'
import { api, getErrorMessage } from '../api/client'
import { Button, Notice } from './Ui'
import { formatDateTime } from '../format'

export function FinalGuideRecoveryPanel({ workflowId, revision, status, generatedAt, canRetry, onRefresh }: {
  workflowId: string; revision: number; status: 'Ready' | 'Pending' | 'Unavailable';
  generatedAt?: string | null; canRetry: boolean; onRefresh: () => void | Promise<void>;
}) {
  const [resultStatus, setResultStatus] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function retry() {
    setBusy(true)
    setError('')
    try {
      const { data } = await api.post<{ status: string }>('/task-approval/workflows/' + workflowId + '/generate-final-guide', null, {
        params: { approvedRevision: revision },
      })
      setResultStatus(data.status)
      await onRefresh()
    } catch (cause) {
      setError(getErrorMessage(cause))
    } finally {
      setBusy(false)
    }
  }
  const currentStatus = resultStatus ?? status
  return <section className="work-section" aria-label="Farmer guide recovery">
    <h2>Farmer cultivation guide</h2>
    <p>Guide status: {currentStatus}</p>
    <p>Approved work remains available while the guide is being prepared.</p>
    {currentStatus === 'Ready' ? <p>Generated {formatDateTime(generatedAt)}. Guidance reflects evidence available at approval.</p>
      : <Notice tone="info">{currentStatus === 'Pending' ? 'Guide generation is in progress. Refresh to check its status.'
        : 'The guide could not be generated. An Agricultural Officer or Admin can retry.'}</Notice>}
    {error ? <Notice tone="error">{error}</Notice> : null}
    <div className="row-actions">
      <Button variant="secondary" disabled={busy} onClick={() => { setResultStatus(null); void onRefresh() }}>Refresh guide status</Button>
      {canRetry && currentStatus === 'Unavailable' ? <Button disabled={busy} onClick={() => void retry()}>Retry farmer guide</Button> : null}
    </div>
  </section>
}
