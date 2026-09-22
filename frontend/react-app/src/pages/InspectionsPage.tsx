import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Camera, CheckCircle2, MoreHorizontal, Plus, XCircle } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, Modal, Notice, PageHeader, Toolbar } from '../components/Ui'
import { formatDateTime } from '../format'
import { inspectionStatus } from '../labels'
import type { Field, Inspection, PagedResult } from '../types'
import './InspectionsPage.css'

type ConfirmAction = { title: string; message: string; label: string; action: () => Promise<void>; variant?: 'primary' | 'danger' } | null

const summaryStatuses = [
  { status: 1, label: 'Scheduled', tone: 'scheduled' },
  { status: 2, label: 'In progress', tone: 'progress' },
  { status: 3, label: 'Completed', tone: 'completed' },
  { status: 4, label: 'Escalated', tone: 'escalated' },
  { status: 5, label: 'Cancelled', tone: 'cancelled' },
] as const

function statusTone(status: number) {
  if (status === 3) return 'good'
  if (status === 4) return 'warn'
  if (status === 5) return 'bad'
  if (status === 2) return 'info'
  return 'neutral'
}

function InspectionActionMenu({
  inspection,
  isOpen,
  onToggle,
  onClose,
  onImage,
  onSubmit,
  onCloseInspection,
}: {
  inspection: Inspection
  isOpen: boolean
  onToggle: () => void
  onClose: () => void
  onImage: () => void
  onSubmit: () => void
  onCloseInspection: () => void
}) {
  const containerRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuId = `inspection-actions-${inspection.id}`
  const actionLabel = inspection.summary || 'inspection'

  useEffect(() => {
    if (!isOpen) return

    const menuItems = () => Array.from(
      containerRef.current?.querySelectorAll<HTMLButtonElement>('[role=menuitem]') ?? [],
    )

    menuItems()[0]?.focus()

    function handlePointerDown(event: PointerEvent) {
      if (event.target instanceof Node && !containerRef.current?.contains(event.target)) {
        onClose()
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        triggerRef.current?.focus()
        return
      }

      if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return

      const items = menuItems()
      if (!items.length) return

      event.preventDefault()
      const activeIndex = items.indexOf(document.activeElement as HTMLButtonElement)
      let nextIndex = 0

      if (event.key === 'End') nextIndex = items.length - 1
      else if (event.key === 'ArrowUp') nextIndex = activeIndex <= 0 ? items.length - 1 : activeIndex - 1
      else if (event.key === 'ArrowDown') nextIndex = activeIndex === items.length - 1 ? 0 : activeIndex + 1

      items[nextIndex]?.focus()
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isOpen, onClose])

  function runMenuAction(action: () => void) {
    onClose()
    action()
  }

  return (
    <div className="inspection-action-menu" ref={containerRef}>
      <button
        ref={triggerRef}
        type="button"
        className="inspection-action-trigger"
        aria-label={'More actions for ' + actionLabel}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        aria-controls={isOpen ? menuId : undefined}
        onClick={onToggle}
      >
        <MoreHorizontal size={20} aria-hidden="true" />
      </button>
      {isOpen ? (
        <div id={menuId} className="inspection-action-popover" role="menu" aria-label={'Actions for ' + actionLabel}>
          <button type="button" role="menuitem" onClick={() => runMenuAction(onImage)}>
            <Camera size={16} aria-hidden="true" />
            <span>Image</span>
          </button>
          {inspection.status !== 3 ? (
            <button type="button" role="menuitem" onClick={() => runMenuAction(onSubmit)}>
              <CheckCircle2 size={16} aria-hidden="true" />
              <span>Submit</span>
            </button>
          ) : null}
          {inspection.status !== 5 ? (
            <button className="inspection-menu-danger" type="button" role="menuitem" onClick={() => runMenuAction(onCloseInspection)}>
              <XCircle size={16} aria-hidden="true" />
              <span>Close</span>
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  )
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
  const [openActionMenuId, setOpenActionMenuId] = useState<string | null>(null)
  const [form, setForm] = useState({ fieldId: '', scheduledAt: '', status: 1, summary: '' })

  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const fieldNameById = useMemo(() => new Map(fields.map((field) => [field.id, field.name])), [fields])
  const statusSummary = useMemo(() => {
    const counts = new Map<number, number>()
    for (const inspection of inspections) {
      counts.set(inspection.status, (counts.get(inspection.status) ?? 0) + 1)
    }

    return summaryStatuses.map((item) => ({
      ...item,
      count: counts.get(item.status) ?? 0,
    }))
  }, [inspections])

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
    <section className="page-stack inspections-command-center">
      <PageHeader
        eyebrow="Field Operations"
        title="Inspections"
        description="Schedule field inspections, submit records and attach Cloudinary-backed image evidence."
        actions={<Button icon={<Plus size={16} aria-hidden="true" />} onClick={() => setShowCreate(true)}>Schedule</Button>}
      />

      <section className="inspection-status-overview" aria-label="Current inspection results" aria-busy={isLoading}>
        <div className="inspection-status-heading">
          <p>Current results</p>
          <strong>{inspections.length} {inspections.length === 1 ? 'inspection' : 'inspections'}</strong>
          <span>Counts reflect only the loaded queue.</span>
        </div>
        <ul className="inspection-status-list">
          {statusSummary.map((item) => (
            <li key={item.status} className={'inspection-status-stat status-stat-' + item.tone} aria-label={item.label + ': ' + item.count}>
              <span><i aria-hidden="true" />{item.label}</span>
              <strong>{item.count}</strong>
            </li>
          ))}
        </ul>
      </section>

      <section className="inspection-filter-panel" aria-label="Inspection filters">
        <div className="inspection-filter-heading">
          <div>
            <p>Find an inspection</p>
            <span>Search the loaded work queue by summary or status.</span>
          </div>
          <Link className="ui-button ui-button-secondary inspection-history-link" to="/inspections/history">History</Link>
        </div>
        <Toolbar>
          <TextInput label="Search" value={search} onChange={setSearch} placeholder="Search by summary" />
          <SelectInput label="Status" value={statusFilter} onChange={setStatusFilter} options={[{ value: '', label: 'All statuses' }, ...Object.entries(inspectionStatus).map(([value, label]) => ({ value, label }))]} />
          <Button onClick={() => void loadData(search, statusFilter)}>Apply Filters</Button>
        </Toolbar>
      </section>

      {success ? <Notice tone="success">{success}</Notice> : null}
      {actionError ? <Notice tone="error">{actionError}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <section className="work-section inspection-work-queue" aria-labelledby="inspection-queue-title">
          <div className="inspection-queue-heading">
            <div>
              <p>Field workload</p>
              <h2 id="inspection-queue-title">Inspection work queue</h2>
            </div>
            <span>{inspections.length} {inspections.length === 1 ? 'record' : 'records'} in this view</span>
          </div>
          <DataTable
            rows={inspections}
            emptyTitle="No inspections found"
            emptyMessage="Try another filter or schedule a new inspection."
            getRowKey={(row) => row.id}
            columns={[
              { header: 'Scheduled', render: (row) => <span className="inspection-scheduled-at">{formatDateTime(row.scheduledAt)}</span> },
              { header: 'Field', render: (row) => <strong className="inspection-field-name">{fieldNameById.get(row.fieldId) ?? row.fieldId.slice(0, 8)}</strong> },
              { header: 'Summary', render: (row) => <span className="inspection-summary-copy">{row.summary || 'No summary'}</span> },
              { header: 'Status', render: (row) => <StatusPill label={inspectionStatus[row.status] ?? String(row.status)} tone={statusTone(row.status)} /> },
              { header: 'Actions', className: 'actions-cell', render: (row) => (
                <div className="row-actions inspection-row-actions">
                  <Link className="ui-button inspection-details-action" to={'/inspections/' + row.id}>Details</Link>
                  <InspectionActionMenu
                    inspection={row}
                    isOpen={openActionMenuId === row.id}
                    onToggle={() => setOpenActionMenuId((currentId) => currentId === row.id ? null : row.id)}
                    onClose={() => setOpenActionMenuId(null)}
                    onImage={() => setImageInspectionId(row.id)}
                    onSubmit={() => setConfirmAction({ title: 'Submit inspection?', message: 'This marks the inspection as completed.', label: 'Submit', action: async () => { await api.post('/inspections/' + row.id + '/submit') } })}
                    onCloseInspection={() => setConfirmAction({ title: 'Close inspection?', message: 'This closes the inspection as cancelled.', label: 'Close', variant: 'danger', action: async () => { await api.post('/inspections/' + row.id + '/close') } })}
                  />
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
