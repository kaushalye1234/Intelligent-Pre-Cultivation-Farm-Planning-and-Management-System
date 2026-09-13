import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Camera, CheckCircle2, Plus, XCircle } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, Modal, Notice, PageHeader, Toolbar } from '../components/Ui'
import { formatDateTime } from '../format'
import { inspectionStatus } from '../labels'
import type { Field, Inspection, PagedResult } from '../types'

type ConfirmAction = { title: string; message: string; label: string; action: () => Promise<void>; variant?: 'primary' | 'danger' } | null

function statusTone(status: number) {
  if (status === 3) return 'good'
  if (status === 4 || status === 5) return 'bad'
  if (status === 2) return 'info'
  return 'neutral'
}

export function InspectionsPage() {
  const [fields, setFields] = useState<Field[]>([])
  const [inspections, setInspections] = useState<Inspection[]>([])
  const [statusFilter, setStatusFilter] = useState('')
  const [search, setSearch] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [actionError, setActionError] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null)
  const [imageInspectionId, setImageInspectionId] = useState('')
  const [imageFile, setImageFile] = useState<File | null>(null)
  const [form, setForm] = useState({ fieldId: '', scheduledAt: '', status: 1, summary: '' })

  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const fieldNameById = useMemo(() => new Map(fields.map((field) => [field.id, field.name])), [fields])

  const loadData = useCallback(async (nextSearch: string, nextStatusFilter: string) => {
    setIsLoading(true)
    setError('')
    try {
      const [fieldResult, inspectionResult] = await Promise.all([
        api.get<PagedResult<Field>>('/crop-planning/fields', { params: { sortBy: 'name', pageSize: 100 } }),
        api.get<PagedResult<Inspection>>('/inspections', { params: { sortBy: 'scheduledAt', sortDirection: 'desc', search: nextSearch || undefined, status: nextStatusFilter || undefined, pageSize: 50 } }),
      ])
      setFields(fieldResult.data.items)
      setInspections(inspectionResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadData('', '')
  }, [loadData])

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setActionError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      setShowCreate(false)
      setImageInspectionId('')
      setImageFile(null)
      setConfirmAction(null)
      await loadData(search, statusFilter)
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function createInspection(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/inspections', { ...form, scheduledAt: new Date(form.scheduledAt).toISOString() })
      setForm({ fieldId: '', scheduledAt: '', status: 1, summary: '' })
    }, 'Inspection scheduled.')
  }

  async function uploadImage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!imageFile) {
      setActionError('Choose an image file before uploading.')
      return
    }
    await runAction(async () => {
      const payload = new FormData()
      payload.append('file', imageFile)
      await api.post(`/inspections/${imageInspectionId}/images`, payload, { headers: { 'Content-Type': 'multipart/form-data' } })
    }, 'Image uploaded for human review evidence.')
  }

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Field Operations"
        title="Inspections"
        description="Schedule field inspections, submit records and attach Cloudinary-backed image evidence."
        actions={<Button icon={<Plus size={16} aria-hidden="true" />} onClick={() => setShowCreate(true)}>Schedule</Button>}
      />

      <Toolbar>
        <TextInput label="Search" value={search} onChange={setSearch} placeholder="Summary" />
        <SelectInput label="Status" value={statusFilter} onChange={setStatusFilter} options={[{ value: '', label: 'All statuses' }, ...Object.entries(inspectionStatus).map(([value, label]) => ({ value, label }))]} />
        <Button variant="secondary" onClick={() => void loadData(search, statusFilter)}>Apply Filters</Button>
        <Link className="ui-button ui-button-secondary" to="/inspections/history">History</Link>
      </Toolbar>

      {success ? <Notice tone="success">{success}</Notice> : null}
      {actionError ? <Notice tone="error">{actionError}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <section className="work-section">
          <DataTable
            rows={inspections}
            emptyTitle="No inspections found"
            emptyMessage="Try another filter or schedule a new inspection."
            getRowKey={(row) => row.id}
            columns={[
              { header: 'Scheduled', render: (row) => formatDateTime(row.scheduledAt) },
              { header: 'Field', render: (row) => fieldNameById.get(row.fieldId) ?? row.fieldId.slice(0, 8) },
              { header: 'Summary', render: (row) => row.summary || 'No summary' },
              { header: 'Status', render: (row) => <StatusPill label={inspectionStatus[row.status] ?? String(row.status)} tone={statusTone(row.status)} /> },
              { header: 'Actions', className: 'actions-cell', render: (row) => (
                <div className="row-actions">
                  <Link className="ui-button ui-button-ghost" to={`/inspections/${row.id}`}>Details</Link>
                  <Button variant="ghost" icon={<Camera size={14} aria-hidden="true" />} onClick={() => setImageInspectionId(row.id)}>Image</Button>
                  {row.status !== 3 ? <Button variant="ghost" icon={<CheckCircle2 size={14} aria-hidden="true" />} onClick={() => setConfirmAction({ title: 'Submit inspection?', message: 'This marks the inspection as completed.', label: 'Submit', action: async () => { await api.post(`/inspections/${row.id}/submit`) } })}>Submit</Button> : null}
                  {row.status !== 5 ? <Button variant="ghost" icon={<XCircle size={14} aria-hidden="true" />} onClick={() => setConfirmAction({ title: 'Close inspection?', message: 'This closes the inspection as cancelled.', label: 'Close', variant: 'danger', action: async () => { await api.post(`/inspections/${row.id}/close`) } })}>Close</Button> : null}
                </div>
              ) },
            ]}
          />
        </section>
      )}

      <Modal open={showCreate} title="Schedule Inspection" onClose={() => setShowCreate(false)} footer={<><Button variant="secondary" onClick={() => setShowCreate(false)} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="inspection-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create'}</Button></>}>
        <form id="inspection-form" className="form-grid" onSubmit={(event) => void createInspection(event)}>
          <SelectInput label="Field" value={form.fieldId} required options={fieldOptions} onChange={(value) => setForm({ ...form, fieldId: value })} />
          <TextInput label="Scheduled at" type="datetime-local" value={form.scheduledAt} required onChange={(value) => setForm({ ...form, scheduledAt: value })} />
          <SelectInput label="Status" value={form.status} options={Object.entries(inspectionStatus).map(([value, label]) => ({ value, label }))} onChange={(value) => setForm({ ...form, status: Number(value) })} />
          <TextAreaInput label="Summary" value={form.summary} required onChange={(value) => setForm({ ...form, summary: value })} />
        </form>
      </Modal>

      <Modal open={Boolean(imageInspectionId)} title="Upload Inspection Image" description="Images are stored in Cloudinary by ASP.NET and exposed to AI as metadata only." onClose={() => setImageInspectionId('')} footer={<><Button variant="secondary" onClick={() => setImageInspectionId('')} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="image-form" disabled={isSubmitting}>{isSubmitting ? 'Uploading...' : 'Upload'}</Button></>}>
        <form id="image-form" className="form-grid" onSubmit={(event) => void uploadImage(event)}>
          <label className="field-control field-control-wide">
            <span>Image file *</span>
            <input type="file" accept="image/*" required onChange={(event) => setImageFile(event.target.files?.[0] ?? null)} />
          </label>
        </form>
      </Modal>

      <ConfirmDialog
        open={Boolean(confirmAction)}
        title={confirmAction?.title ?? ''}
        message={confirmAction?.message ?? ''}
        confirmLabel={confirmAction?.label ?? 'Confirm'}
        variant={confirmAction?.variant}
        isSubmitting={isSubmitting}
        onCancel={() => setConfirmAction(null)}
        onConfirm={() => confirmAction ? runAction(confirmAction.action, 'Inspection updated.') : undefined}
      />
    </section>
  )
}
