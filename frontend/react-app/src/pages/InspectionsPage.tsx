import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { AlertTriangle, Camera, Plus } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, Modal, Notice, PageHeader, Tabs } from '../components/Ui'
import { formatDateTime } from '../format'
import { inspectionStatus, issueSeverity, issueStatus } from '../labels'
import type { CropIssue, Field, Inspection, Observation, PagedResult } from '../types'

type InspectionTab = 'inspections' | 'observations' | 'issues' | 'images'
type InspectionModal = 'inspection' | 'observation' | 'issue' | 'image' | null

type ConfirmAction = {
  title: string
  message: string
  label: string
  variant?: 'primary' | 'danger'
  action: () => Promise<void>
} | null

function severityTone(severity: number) {
  if (severity >= 4) return 'bad'
  if (severity >= 3) return 'warn'
  if (severity >= 2) return 'info'
  return 'good'
}

function statusTone(status: number) {
  if (status === 3) return 'good'
  if (status === 4 || status === 5) return 'bad'
  if (status === 2) return 'info'
  return 'warn'
}

export function InspectionsPage() {
  const [fields, setFields] = useState<Field[]>([])
  const [inspections, setInspections] = useState<Inspection[]>([])
  const [observations, setObservations] = useState<Observation[]>([])
  const [issues, setIssues] = useState<CropIssue[]>([])
  const [activeTab, setActiveTab] = useState<InspectionTab>('inspections')
  const [activeModal, setActiveModal] = useState<InspectionModal>(null)
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [inspectionForm, setInspectionForm] = useState({ fieldId: '', scheduledAt: '', status: 1, summary: '' })
  const [observationForm, setObservationForm] = useState({ fieldInspectionId: '', observationType: '', notes: '' })
  const [issueForm, setIssueForm] = useState({ fieldInspectionId: '', title: '', description: '', severity: 1, status: 1 })
  const [imageInspectionId, setImageInspectionId] = useState('')
  const [imageFile, setImageFile] = useState<File | null>(null)

  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const inspectionOptions = inspections.map((inspection) => ({ value: inspection.id, label: `${formatDateTime(inspection.scheduledAt)} - ${inspection.summary || inspection.id.slice(0, 8)}` }))
  const fieldNameById = useMemo(() => new Map(fields.map((field) => [field.id, field.name])), [fields])
  const inspectionNameById = useMemo(() => new Map(inspections.map((inspection) => [inspection.id, inspection.summary || formatDateTime(inspection.scheduledAt)])), [inspections])

  async function loadData() {
    setIsLoading(true)
    setError('')
    try {
      const [fieldResult, inspectionResult, observationResult, issueResult] = await Promise.all([
        api.get<PagedResult<Field>>('/crop-planning/fields', { params: { sortBy: 'name' } }),
        api.get<PagedResult<Inspection>>('/inspections', { params: { sortBy: 'scheduledAt', sortDirection: 'desc' } }),
        api.get<PagedResult<Observation>>('/inspections/observations'),
        api.get<PagedResult<CropIssue>>('/inspections/issues', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
      ])
      setFields(fieldResult.data.items)
      setInspections(inspectionResult.data.items)
      setObservations(observationResult.data.items)
      setIssues(issueResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData()
  }, [])

  function closeModal() {
    setActiveModal(null)
    setActionError('')
  }

  function openObservation(inspectionId?: string) {
    setObservationForm((current) => ({ ...current, fieldInspectionId: inspectionId ?? current.fieldInspectionId }))
    setActiveModal('observation')
  }

  function openIssue(inspectionId?: string) {
    setIssueForm((current) => ({ ...current, fieldInspectionId: inspectionId ?? current.fieldInspectionId }))
    setActiveModal('issue')
  }

  function openImage(inspectionId?: string) {
    setImageInspectionId(inspectionId ?? '')
    setImageFile(null)
    setActiveModal('image')
  }

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setActionError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      closeModal()
      setConfirmAction(null)
      await loadData()
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function createInspection(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/inspections', { ...inspectionForm, scheduledAt: new Date(inspectionForm.scheduledAt).toISOString() })
      setInspectionForm({ fieldId: '', scheduledAt: '', status: 1, summary: '' })
    }, 'Inspection scheduled successfully.')
  }

  async function createObservation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/inspections/observations', observationForm)
      setObservationForm({ fieldInspectionId: '', observationType: '', notes: '' })
    }, 'Observation added successfully.')
  }

  async function createIssue(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/inspections/issues', issueForm)
      setIssueForm({ fieldInspectionId: '', title: '', description: '', severity: 1, status: 1 })
    }, 'Crop issue reported successfully.')
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
      setImageInspectionId('')
      setImageFile(null)
    }, 'Inspection image uploaded successfully.')
  }

  const tabs = [
    { id: 'inspections', label: 'Inspections', count: inspections.length },
    { id: 'observations', label: 'Observations', count: observations.length },
    { id: 'issues', label: 'Crop Issues', count: issues.length },
    { id: 'images', label: 'Images' },
  ]

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Field Operations"
        title="Inspections"
        description="Schedule inspections, record observations, report crop issues and upload field images."
        actions={<Button icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('inspection')}>Schedule Inspection</Button>}
      />

      <Tabs tabs={tabs} activeTab={activeTab} onChange={(tab) => setActiveTab(tab as InspectionTab)} ariaLabel="Inspection sections" />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      {isLoading ? <LoadingState /> : (
        <>
          {activeTab === 'inspections' ? (
            <section className="work-section">
              <DataTable
                rows={inspections}
                emptyTitle="No inspections scheduled"
                emptyMessage="Schedule a field inspection when a field needs review."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Date', render: (row) => formatDateTime(row.scheduledAt) },
                  { header: 'Field', render: (row) => fieldNameById.get(row.fieldId) ?? row.fieldId.slice(0, 8) },
                  { header: 'Summary', render: (row) => row.summary },
                  { header: 'Status', render: (row) => <StatusPill label={inspectionStatus[row.status] ?? String(row.status)} tone={statusTone(row.status)} /> },
                  { header: 'Actions', className: 'actions-cell', render: (row) => (
                    <div className="row-actions">
                      <Button variant="ghost" onClick={() => openObservation(row.id)}>Observation</Button>
                      <Button variant="ghost" onClick={() => openIssue(row.id)}>Issue</Button>
                      <Button variant="ghost" onClick={() => openImage(row.id)}>Upload</Button>
                    </div>
                  ) },
                ]}
              />
            </section>
          ) : null}

          {activeTab === 'observations' ? (
            <section className="work-section">
              <div className="section-title section-title-actions">
                <h2>Observations</h2>
                <Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => openObservation()}>Add Observation</Button>
              </div>
              <DataTable
                rows={observations}
                emptyTitle="No observations recorded"
                emptyMessage="Add observations from a scheduled inspection."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Inspection', render: (row) => inspectionNameById.get(row.fieldInspectionId) ?? row.fieldInspectionId.slice(0, 8) },
                  { header: 'Type', render: (row) => row.observationType },
                  { header: 'Notes', render: (row) => row.notes },
                ]}
              />
            </section>
          ) : null}

          {activeTab === 'issues' ? (
            <section className="work-section">
              <div className="section-title section-title-actions">
                <h2>Crop Issues</h2>
                <Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => openIssue()}>Report Issue</Button>
              </div>
              <DataTable
                rows={issues}
                emptyTitle="No crop issues"
                emptyMessage="Report a crop issue from an inspection when staff attention is required."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Title', render: (row) => row.title },
                  { header: 'Inspection', render: (row) => inspectionNameById.get(row.fieldInspectionId) ?? row.fieldInspectionId.slice(0, 8) },
                  { header: 'Severity', render: (row) => <StatusPill label={issueSeverity[row.severity] ?? String(row.severity)} tone={severityTone(row.severity)} /> },
                  { header: 'Status', render: (row) => <StatusPill label={issueStatus[row.status] ?? String(row.status)} tone={row.status === 2 ? 'bad' : 'neutral'} /> },
                  { header: 'Actions', className: 'actions-cell', render: (row) => row.status === 2 ? <span className="muted-text">Escalated</span> : (
                    <Button variant="ghost" icon={<AlertTriangle size={14} aria-hidden="true" />} onClick={() => setConfirmAction({ title: 'Escalate crop issue?', message: 'This will mark the issue for higher-priority review using the existing backend action.', label: 'Escalate Issue', variant: 'danger', action: async () => { await api.post(`/inspections/issues/${row.id}/escalate`) } })}>Escalate</Button>
                  ) },
                ]}
              />
            </section>
          ) : null}

          {activeTab === 'images' ? (
            <section className="work-section image-section">
              <div className="section-title section-title-actions">
                <div>
                  <h2>Inspection Images</h2>
                  <p className="muted-text">Images upload through the existing Cloudinary-backed API. There is no image listing endpoint in the current frontend contract.</p>
                </div>
                <Button variant="secondary" icon={<Camera size={16} aria-hidden="true" />} onClick={() => openImage()}>Upload Image</Button>
              </div>
              <EmptyState title="Image list unavailable" message="You can upload an image for an inspection, but the backend does not expose a list endpoint for previously uploaded images." />
            </section>
          ) : null}
        </>
      )}

      <Modal open={activeModal === 'inspection'} title="Schedule Inspection" description="Create a field inspection using the existing inspections API." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="inspection-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Inspection'}</Button></>}>
        <form id="inspection-form" className="form-grid" onSubmit={(event) => void createInspection(event)}>
          <SelectInput label="Field" value={inspectionForm.fieldId} required options={fieldOptions} onChange={(value) => setInspectionForm({ ...inspectionForm, fieldId: value })} />
          <TextInput label="Scheduled at" type="datetime-local" value={inspectionForm.scheduledAt} required onChange={(value) => setInspectionForm({ ...inspectionForm, scheduledAt: value })} />
          <SelectInput label="Status" value={inspectionForm.status} options={Object.entries(inspectionStatus).map(([value, label]) => ({ value, label }))} onChange={(value) => setInspectionForm({ ...inspectionForm, status: Number(value) })} />
          <TextAreaInput label="Summary" value={inspectionForm.summary} required onChange={(value) => setInspectionForm({ ...inspectionForm, summary: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'observation'} title="Add Observation" description="Record a field observation for a selected inspection." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="observation-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Observation'}</Button></>}>
        <form id="observation-form" className="form-grid" onSubmit={(event) => void createObservation(event)}>
          <SelectInput label="Inspection" value={observationForm.fieldInspectionId} required options={inspectionOptions} onChange={(value) => setObservationForm({ ...observationForm, fieldInspectionId: value })} />
          <TextInput label="Type" value={observationForm.observationType} placeholder="Growth, pest, disease" required onChange={(value) => setObservationForm({ ...observationForm, observationType: value })} />
          <TextAreaInput label="Notes" value={observationForm.notes} required onChange={(value) => setObservationForm({ ...observationForm, notes: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'issue'} title="Report Crop Issue" description="Create a crop issue tied to an inspection." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="issue-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Issue'}</Button></>}>
        <form id="issue-form" className="form-grid" onSubmit={(event) => void createIssue(event)}>
          <SelectInput label="Inspection" value={issueForm.fieldInspectionId} required options={inspectionOptions} onChange={(value) => setIssueForm({ ...issueForm, fieldInspectionId: value })} />
          <TextInput label="Title" value={issueForm.title} required onChange={(value) => setIssueForm({ ...issueForm, title: value })} />
          <TextAreaInput label="Description" value={issueForm.description} required onChange={(value) => setIssueForm({ ...issueForm, description: value })} />
          <SelectInput label="Severity" value={issueForm.severity} options={Object.entries(issueSeverity).map(([value, label]) => ({ value, label }))} onChange={(value) => setIssueForm({ ...issueForm, severity: Number(value) })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'image'} title="Upload Inspection Image" description="Upload one image through the existing inspection image endpoint." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="image-form" disabled={isSubmitting}>{isSubmitting ? 'Uploading...' : 'Upload Image'}</Button></>}>
        <form id="image-form" className="form-grid" onSubmit={(event) => void uploadImage(event)}>
          <SelectInput label="Inspection" value={imageInspectionId} required options={inspectionOptions} onChange={setImageInspectionId} />
          <label className="field-control field-control-wide">
            <span>Image file *</span>
            <input type="file" accept="image/*" required onChange={(event) => setImageFile(event.target.files?.[0] ?? null)} />
          </label>
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
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
        onConfirm={() => confirmAction ? runAction(confirmAction.action, 'Crop issue updated successfully.') : undefined}
      />
    </section>
  )
}

