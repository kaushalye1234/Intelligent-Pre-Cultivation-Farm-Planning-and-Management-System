import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { AlertTriangle, Camera, CheckCircle2, ClipboardCheck } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { DataTable } from '../components/DataTable'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, MetricCard, PageHeader } from '../components/Ui'
import { formatDateTime } from '../format'
import { inspectionStatus, issueSeverity, issueStatus } from '../labels'
import type { CropIssue, Inspection, PagedResult } from '../types'

function issueTone(severity: number) {
  if (severity >= 4) return 'bad'
  if (severity >= 3) return 'warn'
  return 'neutral'
}

export function InspectionsDashboard() {
  const [inspections, setInspections] = useState<Inspection[]>([])
  const [issues, setIssues] = useState<CropIssue[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    Promise.all([
      api.get<PagedResult<Inspection>>('/inspections', { params: { sortBy: 'scheduledAt', sortDirection: 'desc', pageSize: 8 } }),
      api.get<PagedResult<CropIssue>>('/inspections/issues', { params: { status: 1, sortBy: 'createdAt', sortDirection: 'desc', pageSize: 8 } }),
    ])
      .then(([inspectionResult, issueResult]) => {
        setInspections(inspectionResult.data.items)
        setIssues(issueResult.data.items)
      })
      .catch((err) => setError(getErrorMessage(err)))
      .finally(() => setIsLoading(false))
  }, [])

  const completedCount = useMemo(() => inspections.filter((item) => item.status === 3).length, [inspections])
  const imageReadyCount = useMemo(() => inspections.filter((item) => item.status === 3 || item.status === 4).length, [inspections])

  if (isLoading) return <LoadingState />
  if (error) return <ErrorState message={error} />

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Field Operations"
        title="Inspection Dashboard"
        description="Current inspection workload, open crop issues and evidence readiness."
        actions={<Link className="ui-button ui-button-primary" to="/inspections">Open Inspections</Link>}
      />

      <div className="metric-grid">
        <MetricCard label="Recent inspections" value={inspections.length} description="Latest records visible to your role." icon={<ClipboardCheck size={20} aria-hidden="true" />} />
        <MetricCard label="Submitted" value={completedCount} description="Completed inspections in the current view." icon={<CheckCircle2 size={20} aria-hidden="true" />} tone="good" />
        <MetricCard label="Open crop issues" value={issues.length} description="Issues still requiring attention." icon={<AlertTriangle size={20} aria-hidden="true" />} tone={issues.length ? 'warn' : 'neutral'} />
        <MetricCard label="Evidence-ready" value={imageReadyCount} description="Submitted or escalated inspections ready for review." icon={<Camera size={20} aria-hidden="true" />} />
      </div>

      <section className="work-section">
        <div className="section-title section-title-actions">
          <h2>Recent Inspections</h2>
          <Button variant="secondary" onClick={() => window.location.assign('/inspections')}>View All</Button>
        </div>
        <DataTable
          rows={inspections}
          emptyTitle="No inspections"
          emptyMessage="No inspection records were returned."
          getRowKey={(row) => row.id}
          columns={[
            { header: 'Scheduled', render: (row) => formatDateTime(row.scheduledAt) },
            { header: 'Summary', render: (row) => row.summary || 'No summary' },
            { header: 'Status', render: (row) => <StatusPill label={inspectionStatus[row.status] ?? String(row.status)} tone={row.status === 3 ? 'good' : 'neutral'} /> },
            { header: 'Open', className: 'actions-cell', render: (row) => <Link className="ui-button ui-button-ghost" to={`/inspections/${row.id}`}>Details</Link> },
          ]}
        />
      </section>

      <section className="work-section">
        <div className="section-title section-title-actions">
          <h2>Open Crop Issues</h2>
          <Link className="ui-button ui-button-secondary" to="/inspections/issues">View Issues</Link>
        </div>
        {issues.length ? (
          <DataTable
            rows={issues}
            emptyMessage="No open crop issues."
            getRowKey={(row) => row.id}
            columns={[
              { header: 'Issue', render: (row) => row.title },
              { header: 'Severity', render: (row) => <StatusPill label={issueSeverity[row.severity] ?? String(row.severity)} tone={issueTone(row.severity)} /> },
              { header: 'Status', render: (row) => <StatusPill label={issueStatus[row.status] ?? String(row.status)} tone={row.status === 2 ? 'bad' : 'neutral'} /> },
              { header: 'Open', className: 'actions-cell', render: (row) => <Link className="ui-button ui-button-ghost" to={`/inspections/issues/${row.id}`}>Details</Link> },
            ]}
          />
        ) : <EmptyState title="No open crop issues" message="Reported crop issues will appear here when action is needed." />}
      </section>
    </section>
  )
}
