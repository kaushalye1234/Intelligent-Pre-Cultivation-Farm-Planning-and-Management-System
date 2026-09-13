import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { AlertTriangle, Plus } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Modal, Notice, PageHeader } from '../components/Ui'
import { formatDateTime } from '../format'
import { inspectionStatus, issueSeverity, issueStatus } from '../labels'
import type { InspectionDetail } from '../types'

function severityTone(severity: number) {
  if (severity >= 4) return 'bad'
  if (severity >= 3) return 'warn'
  return 'neutral'
}

export function InspectionDetails() {
  const { id } = useParams()
  const [detail, setDetail] = useState<InspectionDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [modal, setModal] = useState<'observation' | 'issue' | null>(null)
  const [observation, setObservation] = useState({ observationType: '', notes: '' })
  const [issue, setIssue] = useState({ title: '', description: '', severity: 1, status: 1 })

  async function loadDetail() {
    if (!id) return
    setIsLoading(true)
    setError('')
    try {
      const response = await api.get<InspectionDetail>(`/inspections/${id}`)
      setDetail(response.data)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadDetail()
  }, [id])

  const title = useMemo(() => detail?.summary || 'Inspection details', [detail])

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      setModal(null)
      await loadDetail()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function addObservation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!id) return
    await runAction(async () => {
      await api.post('/inspections/observations', { fieldInspectionId: id, ...observation })
      setObservation({ observationType: '', notes: '' })
    }, 'Observation added.')
  }

  async function addIssue(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!id) return
    await runAction(async () => {
      await api.post('/inspections/issues', { fieldInspectionId: id, ...issue })
      setIssue({ title: '', description: '', severity: 1, status: 1 })
    }, 'Crop issue reported.')
  }

  if (isLoading) return <LoadingState />
  if (error && !detail) return <ErrorState message={error} />
  if (!detail) return <EmptyState title="Inspection unavailable" message="The inspection could not be loaded." />

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Inspection Details"
        title={title}
        description={`${formatDateTime(detail.scheduledAt)} - ${inspectionStatus[detail.status] ?? detail.status}`}
        actions={<><Link className="ui-button ui-button-secondary" to={`/inspections/${detail.id}/history`}>History</Link><Button icon={<Plus size={16} aria-hidden="true" />} onClick={() => setModal('observation')}>Observation</Button><Button variant="secondary" icon={<AlertTriangle size={16} aria-hidden="true" />} onClick={() => setModal('issue')}>Issue</Button></>}
      />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <Notice tone="error">{error}</Notice> : null}

      <section className="work-section">
        <div className="section-title"><h2>Observations</h2></div>
        <DataTable
          rows={detail.observations}
          emptyMessage="No observations recorded."
          getRowKey={(row) => row.id}
          columns={[
            { header: 'Type', render: (row) => row.observationType },
            { header: 'Notes', render: (row) => row.notes },
          ]}
        />
      </section>

      <section className="work-section">
        <div className="section-title"><h2>Crop Issues</h2></div>
        <DataTable
          rows={detail.issues}
          emptyMessage="No issues reported."
          getRowKey={(row) => row.id}
          columns={[
            { header: 'Title', render: (row) => row.title },
            { header: 'Severity', render: (row) => <StatusPill label={issueSeverity[row.severity] ?? String(row.severity)} tone={severityTone(row.severity)} /> },
            { header: 'Status', render: (row) => <StatusPill label={issueStatus[row.status] ?? String(row.status)} tone={row.status === 2 ? 'bad' : 'neutral'} /> },
            { header: 'Open', className: 'actions-cell', render: (row) => <Link className="ui-button ui-button-ghost" to={`/inspections/issues/${row.id}`}>Details</Link> },
          ]}
        />
      </section>

      <section className="work-section">
        <div className="section-title"><h2>Image Evidence</h2></div>
        <DataTable
          rows={detail.images}
          emptyMessage="No image evidence uploaded."
          getRowKey={(row) => row.id}
          columns={[
            { header: 'Content type', render: (row) => row.contentType },
            { header: 'Size', render: (row) => `${Math.round(row.sizeBytes / 1024)} KB` },
            { header: 'Cloudinary ID', render: (row) => row.publicId },
          ]}
        />
      </section>

      <Modal open={modal === 'observation'} title="Add Observation" onClose={() => setModal(null)} footer={<><Button variant="secondary" onClick={() => setModal(null)} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="observation-form" disabled={isSubmitting}>Add</Button></>}>
        <form id="observation-form" className="form-grid" onSubmit={(event) => void addObservation(event)}>
          <TextInput label="Type" value={observation.observationType} required onChange={(value) => setObservation({ ...observation, observationType: value })} />
          <TextAreaInput label="Notes" value={observation.notes} required onChange={(value) => setObservation({ ...observation, notes: value })} />
        </form>
      </Modal>

      <Modal open={modal === 'issue'} title="Report Crop Issue" onClose={() => setModal(null)} footer={<><Button variant="secondary" onClick={() => setModal(null)} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="issue-form" disabled={isSubmitting}>Report</Button></>}>
        <form id="issue-form" className="form-grid" onSubmit={(event) => void addIssue(event)}>
          <TextInput label="Title" value={issue.title} required onChange={(value) => setIssue({ ...issue, title: value })} />
          <TextAreaInput label="Description" value={issue.description} required onChange={(value) => setIssue({ ...issue, description: value })} />
          <SelectInput label="Severity" value={issue.severity} options={Object.entries(issueSeverity).map(([value, label]) => ({ value, label }))} onChange={(value) => setIssue({ ...issue, severity: Number(value) })} />
        </form>
      </Modal>
    </section>
  )
}
