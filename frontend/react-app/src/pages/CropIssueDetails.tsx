import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Modal, Notice, PageHeader } from '../components/Ui'
import { issueSeverity, issueStatus } from '../labels'
import type { CropIssue, FollowUpRecommendation, PagedResult } from '../types'

export function CropIssueDetails() {
  const { id } = useParams()
  const [issue, setIssue] = useState<CropIssue | null>(null)
  const [followUps, setFollowUps] = useState<FollowUpRecommendation[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [showFollowUp, setShowFollowUp] = useState(false)
  const [followUpForm, setFollowUpForm] = useState({ recommendation: '', dueAt: '', isCompleted: false })
  const [statusForm, setStatusForm] = useState({ severity: 1, status: 1 })

  const loadData = useCallback(async () => {
    if (!id) return
    setIsLoading(true)
    setError('')
    try {
      const [issueResult, followUpResult] = await Promise.all([
        api.get<CropIssue>(`/inspections/issues/${id}`),
        api.get<PagedResult<FollowUpRecommendation>>('/inspections/recommendations', { params: { cropIssueId: id, pageSize: 50 } }),
      ])
      setIssue(issueResult.data)
      setStatusForm({ severity: issueResult.data.severity, status: issueResult.data.status })
      setFollowUps(followUpResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }, [id])

  useEffect(() => {
    void loadData()
  }, [loadData])

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      setShowFollowUp(false)
      await loadData()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) return <LoadingState />
  if (error && !issue) return <ErrorState message={error} />
  if (!issue || !id) return <EmptyState title="Issue unavailable" message="The crop issue could not be loaded." />

  return (
    <section className="page-stack">
      <PageHeader eyebrow="Crop Issue" title={issue.title} description={issue.description} actions={<Button onClick={() => setShowFollowUp(true)}>Add Follow-up</Button>} />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <Notice tone="error">{error}</Notice> : null}

      <section className="work-section">
        <div className="section-title"><h2>Status</h2></div>
        <form className="form-grid" onSubmit={(event) => { event.preventDefault(); void runAction(async () => { await api.patch(`/inspections/issues/${id}/status`, statusForm) }, 'Issue status updated.') }}>
          <SelectInput label="Severity" value={statusForm.severity} options={Object.entries(issueSeverity).map(([value, label]) => ({ value, label }))} onChange={(value) => setStatusForm({ ...statusForm, severity: Number(value) })} />
          <SelectInput label="Status" value={statusForm.status} options={Object.entries(issueStatus).map(([value, label]) => ({ value, label }))} onChange={(value) => setStatusForm({ ...statusForm, status: Number(value) })} />
          <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : 'Save Status'}</Button>
        </form>
      </section>

      <section className="work-section">
        <div className="section-title"><h2>Follow-up Recommendations</h2></div>
        <DataTable
          rows={followUps}
          emptyMessage="No follow-up recommendations."
          getRowKey={(row) => row.id}
          columns={[
            { header: 'Recommendation', render: (row) => row.recommendation },
            { header: 'Due', render: (row) => row.dueAt ? new Date(row.dueAt).toLocaleDateString() : 'No due date' },
            { header: 'State', render: (row) => <StatusPill label={row.isCompleted ? 'Completed' : 'Open'} tone={row.isCompleted ? 'good' : 'warn'} /> },
            { header: 'Actions', className: 'actions-cell', render: (row) => row.isCompleted ? <span className="muted-text">Done</span> : <Button variant="ghost" onClick={() => void runAction(async () => { await api.patch(`/inspections/recommendations/${row.id}`, { isCompleted: true }) }, 'Follow-up completed.')}>Complete</Button> },
          ]}
        />
      </section>

      <Modal open={showFollowUp} title="Add Follow-up" onClose={() => setShowFollowUp(false)} footer={<><Button variant="secondary" onClick={() => setShowFollowUp(false)} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="follow-up-form" disabled={isSubmitting}>Create</Button></>}>
        <form id="follow-up-form" className="form-grid" onSubmit={(event) => { event.preventDefault(); void runAction(async () => { await api.post('/inspections/recommendations', { cropIssueId: id, recommendation: followUpForm.recommendation, dueAt: followUpForm.dueAt || null, isCompleted: followUpForm.isCompleted }) }, 'Follow-up created.') }}>
          <TextAreaInput label="Recommendation" value={followUpForm.recommendation} required onChange={(value) => setFollowUpForm({ ...followUpForm, recommendation: value })} />
          <TextInput label="Due at" type="datetime-local" value={followUpForm.dueAt} onChange={(value) => setFollowUpForm({ ...followUpForm, dueAt: value })} />
        </form>
      </Modal>
    </section>
  )
}
