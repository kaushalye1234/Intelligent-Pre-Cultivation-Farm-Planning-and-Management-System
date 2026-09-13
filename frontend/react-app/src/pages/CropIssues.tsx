import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { AlertTriangle } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, Notice, PageHeader, Toolbar } from '../components/Ui'
import { issueSeverity, issueStatus } from '../labels'
import type { CropIssue, PagedResult } from '../types'

function severityTone(severity: number) {
  if (severity >= 4) return 'bad'
  if (severity >= 3) return 'warn'
  if (severity >= 2) return 'info'
  return 'neutral'
}

export function CropIssues({ escalatedOnly = false }: { escalatedOnly?: boolean }) {
  const [issues, setIssues] = useState<CropIssue[]>([])
  const [search, setSearch] = useState('')
  const [severity, setSeverity] = useState('')
  const [status, setStatus] = useState(escalatedOnly ? '2' : '')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [escalateId, setEscalateId] = useState<string | null>(null)

  const loadIssues = useCallback(async (nextSearch: string, nextSeverity: string, nextStatus: string) => {
    setIsLoading(true)
    setError('')
    try {
      const response = await api.get<PagedResult<CropIssue>>('/inspections/issues', { params: { search: nextSearch || undefined, severity: nextSeverity || undefined, status: escalatedOnly ? 2 : nextStatus || undefined, sortBy: 'createdAt', sortDirection: 'desc', pageSize: 50 } })
      setIssues(response.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }, [escalatedOnly])

  useEffect(() => {
    void loadIssues('', '', escalatedOnly ? '2' : '')
  }, [escalatedOnly, loadIssues])

  async function escalateIssue() {
    if (!escalateId) return
    setIsSubmitting(true)
    setError('')
    try {
      await api.post(`/inspections/issues/${escalateId}/escalate`)
      setSuccess('Crop issue escalated.')
      setEscalateId(null)
      await loadIssues(search, severity, status)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="page-stack">
      <PageHeader eyebrow="Field Operations" title={escalatedOnly ? 'Escalated Issues' : 'Crop Issues'} description={escalatedOnly ? 'Serious crop issues already escalated for review.' : 'Filter, review and escalate reported crop issues.'} />
      {!escalatedOnly ? (
        <Toolbar>
          <TextInput label="Search" value={search} onChange={setSearch} placeholder="Issue title or description" />
          <SelectInput label="Severity" value={severity} onChange={setSeverity} options={[{ value: '', label: 'All severities' }, ...Object.entries(issueSeverity).map(([value, label]) => ({ value, label }))]} />
          <SelectInput label="Status" value={status} onChange={setStatus} options={[{ value: '', label: 'All statuses' }, ...Object.entries(issueStatus).map(([value, label]) => ({ value, label }))]} />
          <Button variant="secondary" onClick={() => void loadIssues(search, severity, status)}>Apply Filters</Button>
          <Link className="ui-button ui-button-secondary" to="/inspections/issues/escalated">Escalated</Link>
        </Toolbar>
      ) : null}
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <section className="work-section">
          <DataTable
            rows={issues}
            emptyTitle={escalatedOnly ? 'No escalated issues' : 'No crop issues'}
            emptyMessage="No crop issues matched the current filters."
            getRowKey={(row) => row.id}
            columns={[
              { header: 'Title', render: (row) => row.title },
              { header: 'Severity', render: (row) => <StatusPill label={issueSeverity[row.severity] ?? String(row.severity)} tone={severityTone(row.severity)} /> },
              { header: 'Status', render: (row) => <StatusPill label={issueStatus[row.status] ?? String(row.status)} tone={row.status === 2 ? 'bad' : 'neutral'} /> },
              { header: 'Actions', className: 'actions-cell', render: (row) => (
                <div className="row-actions">
                  <Link className="ui-button ui-button-ghost" to={`/inspections/issues/${row.id}`}>Details</Link>
                  {row.status !== 2 ? <Button variant="ghost" icon={<AlertTriangle size={14} aria-hidden="true" />} onClick={() => setEscalateId(row.id)}>Escalate</Button> : null}
                </div>
              ) },
            ]}
          />
        </section>
      )}
      <ConfirmDialog open={Boolean(escalateId)} title="Escalate crop issue?" message="Only high or critical crop issues can be escalated by the backend." confirmLabel="Escalate" variant="danger" isSubmitting={isSubmitting} onCancel={() => setEscalateId(null)} onConfirm={escalateIssue} />
    </section>
  )
}
