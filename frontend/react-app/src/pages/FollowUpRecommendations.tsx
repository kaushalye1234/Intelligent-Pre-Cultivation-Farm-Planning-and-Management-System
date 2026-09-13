import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, getErrorMessage } from '../api/client'
import { SelectInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Notice, PageHeader, Toolbar } from '../components/Ui'
import type { FollowUpRecommendation, PagedResult } from '../types'

export function FollowUpRecommendations() {
  const [items, setItems] = useState<FollowUpRecommendation[]>([])
  const [isCompleted, setIsCompleted] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  async function loadData() {
    setIsLoading(true)
    setError('')
    try {
      const response = await api.get<PagedResult<FollowUpRecommendation>>('/inspections/recommendations', { params: { isCompleted: isCompleted || undefined, pageSize: 50 } })
      setItems(response.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData()
  }, [])

  async function complete(id: string) {
    setIsSubmitting(true)
    setSuccess('')
    setError('')
    try {
      await api.patch(`/inspections/recommendations/${id}`, { isCompleted: true })
      setSuccess('Follow-up completed.')
      await loadData()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="page-stack">
      <PageHeader eyebrow="Field Operations" title="Follow-up Recommendations" description="Track follow-up state for crop issues." />
      <Toolbar>
        <SelectInput label="State" value={isCompleted} onChange={setIsCompleted} options={[{ value: '', label: 'All' }, { value: 'false', label: 'Open' }, { value: 'true', label: 'Completed' }]} />
        <Button variant="secondary" onClick={() => void loadData()}>Apply Filter</Button>
      </Toolbar>
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <section className="work-section">
          <DataTable
            rows={items}
            emptyTitle="No follow-ups"
            emptyMessage="No follow-up recommendations matched the current filter."
            getRowKey={(row) => row.id}
            columns={[
              { header: 'Recommendation', render: (row) => row.recommendation },
              { header: 'Due', render: (row) => row.dueAt ? new Date(row.dueAt).toLocaleDateString() : 'No due date' },
              { header: 'State', render: (row) => <StatusPill label={row.isCompleted ? 'Completed' : 'Open'} tone={row.isCompleted ? 'good' : 'warn'} /> },
              { header: 'Issue', className: 'actions-cell', render: (row) => <Link className="ui-button ui-button-ghost" to={`/inspections/issues/${row.cropIssueId}`}>Open Issue</Link> },
              { header: 'Actions', className: 'actions-cell', render: (row) => row.isCompleted ? <span className="muted-text">Done</span> : <Button variant="ghost" disabled={isSubmitting} onClick={() => void complete(row.id)}>Complete</Button> },
            ]}
          />
        </section>
      )}
    </section>
  )
}
