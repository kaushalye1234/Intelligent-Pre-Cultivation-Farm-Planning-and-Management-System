import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Check, Plus, RotateCcw, X } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, Modal, Notice, PageHeader, Tabs } from '../components/Ui'
import { formatDateTime } from '../format'
import { approvalDecision, scheduleStatus, taskStatus } from '../labels'
import { isDecisionRole, Roles } from '../routing'
import type { ApprovalDecision, Farm, FarmTask, Field, IrrigationSchedule, PagedResult, UserProfile } from '../types'

type ApprovalTab = 'tasks' | 'schedules' | 'approvals'
type ApprovalModal = 'task' | 'schedule' | null

type ConfirmAction = {
  title: string
  message: string
  label: string
  variant?: 'primary' | 'danger'
  action: () => Promise<void>
  success: string
} | null

function taskTone(status: number) {
  if (status === 3 || status === 6) return 'good'
  if (status === 4 || status === 7) return 'bad'
  if (status === 5) return 'info'
  return 'warn'
}

function scheduleTone(status: number) {
  if (status === 2 || status === 5) return 'good'
  if (status === 3 || status === 6) return 'bad'
  if (status === 4) return 'info'
  return 'warn'
}

export function TaskApprovalPage() {
  const { user } = useAuth()
  const [farms, setFarms] = useState<Farm[]>([])
  const [fields, setFields] = useState<Field[]>([])
  const [users, setUsers] = useState<UserProfile[]>([])
  const [tasks, setTasks] = useState<FarmTask[]>([])
  const [schedules, setSchedules] = useState<IrrigationSchedule[]>([])
  const [approvals, setApprovals] = useState<ApprovalDecision[]>([])
  const [activeTab, setActiveTab] = useState<ApprovalTab>('tasks')
  const [activeModal, setActiveModal] = useState<ApprovalModal>(null)
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [taskForm, setTaskForm] = useState({ farmId: '', title: '', description: '', dueAt: '', assignedToUserId: '', status: 2 })
  const [scheduleForm, setScheduleForm] = useState({ fieldId: '', scheduledAt: '', durationMinutes: '', notes: '', status: 1 })

  const assignmentUsers = users.length > 0 ? users : user ? [user] : []
  const farmOptions = farms.map((farm) => ({ value: farm.id, label: farm.name }))
  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const userOptions = assignmentUsers.map((item) => ({ value: item.id, label: item.fullName }))
  const farmNameById = useMemo(() => new Map(farms.map((farm) => [farm.id, farm.name])), [farms])
  const fieldNameById = useMemo(() => new Map(fields.map((field) => [field.id, field.name])), [fields])
  const userNameById = useMemo(() => new Map(assignmentUsers.map((item) => [item.id, item.fullName])), [assignmentUsers])
  const canDecide = isDecisionRole(user?.role)
  const canCreate = user?.role === Roles.Admin || user?.role === Roles.FieldOfficer || user?.role === Roles.AgriculturalOfficer

  async function loadData() {
    setIsLoading(true)
    setError('')
    try {
      const [farmResult, fieldResult, taskResult, scheduleResult, approvalResult] = await Promise.all([
        api.get<PagedResult<Farm>>('/crop-planning/farms'),
        api.get<PagedResult<Field>>('/crop-planning/fields'),
        api.get<PagedResult<FarmTask>>('/task-approval/tasks', { params: { sortBy: 'dueAt' } }),
        api.get<PagedResult<IrrigationSchedule>>('/task-approval/schedules', { params: { sortBy: 'scheduledAt' } }),
        api.get<PagedResult<ApprovalDecision>>('/task-approval/approvals', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
      ])
      setFarms(farmResult.data.items)
      setFields(fieldResult.data.items)
      setTasks(taskResult.data.items)
      setSchedules(scheduleResult.data.items)
      setApprovals(approvalResult.data.items)

      if (user?.role === Roles.Admin) {
        const userResult = await api.get<PagedResult<UserProfile>>('/users', { params: { sortBy: 'fullName' } })
        setUsers(userResult.data.items)
      } else {
        setUsers([])
        if (user) setTaskForm((current) => ({ ...current, assignedToUserId: current.assignedToUserId || user.id }))
      }
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

  async function createTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/task-approval/tasks', { ...taskForm, dueAt: new Date(taskForm.dueAt).toISOString() })
      setTaskForm({ farmId: '', title: '', description: '', dueAt: '', assignedToUserId: user?.role === Roles.Admin ? '' : user?.id ?? '', status: 2 })
    }, 'Farm task created successfully.')
  }

  async function createSchedule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/task-approval/schedules', {
        ...scheduleForm,
        scheduledAt: new Date(scheduleForm.scheduledAt).toISOString(),
        durationMinutes: Number(scheduleForm.durationMinutes),
      })
      setScheduleForm({ fieldId: '', scheduledAt: '', durationMinutes: '', notes: '', status: 1 })
    }, 'Irrigation schedule created successfully.')
  }

  function decisionPayload(comment: string) {
    return { comment, agentWorkflowId: null }
  }

  function openTaskDecision(task: FarmTask, action: 'approve' | 'reject' | 'request-revision') {
    const labels = {
      approve: ['Approve task?', 'Approve Task', 'Approved from AgriAssist operations portal.'],
      reject: ['Reject task?', 'Reject Task', 'Rejected from AgriAssist operations portal.'],
      'request-revision': ['Request task revision?', 'Request Revision', 'Revision requested from AgriAssist operations portal.'],
    }
    const [title, label, comment] = labels[action]
    setConfirmAction({
      title,
      message: `This will record a manual decision for "${task.title}".`,
      label,
      variant: action === 'reject' ? 'danger' : 'primary',
      action: async () => { await api.post(`/task-approval/tasks/${task.id}/${action}`, decisionPayload(comment)) },
      success: 'Task decision recorded successfully.',
    })
  }

  function openScheduleDecision(schedule: IrrigationSchedule, action: 'approve' | 'reject' | 'request-revision') {
    const labels = {
      approve: ['Approve schedule?', 'Approve Schedule', 'Approved from AgriAssist operations portal.'],
      reject: ['Reject schedule?', 'Reject Schedule', 'Rejected from AgriAssist operations portal.'],
      'request-revision': ['Request schedule revision?', 'Request Revision', 'Revision requested from AgriAssist operations portal.'],
    }
    const [title, label, comment] = labels[action]
    setConfirmAction({
      title,
      message: `This will record a manual decision for the schedule at ${formatDateTime(schedule.scheduledAt)}.`,
      label,
      variant: action === 'reject' ? 'danger' : 'primary',
      action: async () => { await api.post(`/task-approval/schedules/${schedule.id}/${action}`, decisionPayload(comment)) },
      success: 'Schedule decision recorded successfully.',
    })
  }

  const tabs = [
    { id: 'tasks', label: 'Farm Tasks', count: tasks.length },
    { id: 'schedules', label: 'Irrigation Schedules', count: schedules.length },
    { id: 'approvals', label: 'Approvals', count: approvals.length },
  ]

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Task Approval"
        title="Tasks & Approvals"
        description="Manage farm tasks, irrigation schedules and manual approval decisions."
        actions={canCreate ? <><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('task')}>Create Task</Button><Button icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('schedule')}>Create Schedule</Button></> : undefined}
      />

      <Tabs tabs={tabs} activeTab={activeTab} onChange={(tab) => setActiveTab(tab as ApprovalTab)} ariaLabel="Task and approval sections" />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      {isLoading ? <LoadingState /> : (
        <>
          {activeTab === 'tasks' ? (
            <section className="work-section">
              <DataTable rows={tasks} emptyTitle="No farm tasks" emptyMessage="Create a task when farm work needs assignment or approval." getRowKey={(row) => row.id} columns={[
                { header: 'Task', render: (row) => <div><strong>{row.title}</strong><p className="muted-text">{row.description}</p></div> },
                { header: 'Farm', render: (row) => farmNameById.get(row.farmId) ?? row.farmId.slice(0, 8) },
                { header: 'Assigned To', render: (row) => userNameById.get(row.assignedToUserId) ?? row.assignedToUserId.slice(0, 8) },
                { header: 'Due Date', render: (row) => formatDateTime(row.dueAt) },
                { header: 'Status', render: (row) => <StatusPill label={taskStatus[row.status] ?? String(row.status)} tone={taskTone(row.status)} /> },
                { header: 'Actions', className: 'actions-cell', render: (row) => canDecide && row.status === 2 ? (
                  <div className="row-actions">
                    <Button variant="ghost" icon={<Check size={14} aria-hidden="true" />} onClick={() => openTaskDecision(row, 'approve')}>Approve</Button>
                    <Button variant="ghost" icon={<RotateCcw size={14} aria-hidden="true" />} onClick={() => openTaskDecision(row, 'request-revision')}>Revision</Button>
                    <Button variant="ghost" icon={<X size={14} aria-hidden="true" />} onClick={() => openTaskDecision(row, 'reject')}>Reject</Button>
                  </div>
                ) : <span className="muted-text">No action</span> },
              ]} />
            </section>
          ) : null}

          {activeTab === 'schedules' ? (
            <section className="work-section">
              <DataTable rows={schedules} emptyTitle="No irrigation schedules" emptyMessage="Create a schedule when irrigation work needs review." getRowKey={(row) => row.id} columns={[
                { header: 'Field', render: (row) => fieldNameById.get(row.fieldId) ?? row.fieldId.slice(0, 8) },
                { header: 'Scheduled At', render: (row) => formatDateTime(row.scheduledAt) },
                { header: 'Duration', render: (row) => `${row.durationMinutes} min` },
                { header: 'Notes', render: (row) => row.notes },
                { header: 'Status', render: (row) => <StatusPill label={scheduleStatus[row.status] ?? String(row.status)} tone={scheduleTone(row.status)} /> },
                { header: 'Actions', className: 'actions-cell', render: (row) => canDecide && row.status === 1 ? (
                  <div className="row-actions">
                    <Button variant="ghost" icon={<Check size={14} aria-hidden="true" />} onClick={() => openScheduleDecision(row, 'approve')}>Approve</Button>
                    <Button variant="ghost" icon={<RotateCcw size={14} aria-hidden="true" />} onClick={() => openScheduleDecision(row, 'request-revision')}>Revision</Button>
                    <Button variant="ghost" icon={<X size={14} aria-hidden="true" />} onClick={() => openScheduleDecision(row, 'reject')}>Reject</Button>
                  </div>
                ) : <span className="muted-text">No action</span> },
              ]} />
            </section>
          ) : null}

          {activeTab === 'approvals' ? (
            <section className="work-section">
              {approvals.length === 0 ? <EmptyState title="No approval decisions" message="Approval decisions will appear here after a task or schedule is decided." /> : (
                <DataTable rows={approvals} emptyMessage="No approvals found." getRowKey={(row) => row.id} columns={[
                  { header: 'Decision', render: (row) => <StatusPill label={approvalDecision[row.decision] ?? String(row.decision)} tone={row.decision === 1 ? 'good' : row.decision === 2 ? 'bad' : 'info'} /> },
                  { header: 'Target', render: (row) => row.farmTaskId ? `Task ${row.farmTaskId.slice(0, 8)}` : row.irrigationScheduleId ? `Schedule ${row.irrigationScheduleId.slice(0, 8)}` : 'Not linked' },
                  { header: 'Comment', render: (row) => row.comment || 'No comment' },
                  { header: 'Created', render: (row) => formatDateTime(row.createdAt) },
                ]} />
              )}
            </section>
          ) : null}
        </>
      )}

      <Modal open={activeModal === 'task'} title="Create Farm Task" description="Create a farm task using the existing task approval API." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="task-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Task'}</Button></>}>
        <form id="task-form" className="form-grid" onSubmit={(event) => void createTask(event)}>
          <SelectInput label="Farm" value={taskForm.farmId} required options={farmOptions} onChange={(value) => setTaskForm({ ...taskForm, farmId: value })} />
          <TextInput label="Title" value={taskForm.title} required onChange={(value) => setTaskForm({ ...taskForm, title: value })} />
          <TextAreaInput label="Description" value={taskForm.description} required onChange={(value) => setTaskForm({ ...taskForm, description: value })} />
          <TextInput label="Due at" type="datetime-local" value={taskForm.dueAt} required onChange={(value) => setTaskForm({ ...taskForm, dueAt: value })} />
          <SelectInput label="Assigned to" value={taskForm.assignedToUserId} required options={userOptions} onChange={(value) => setTaskForm({ ...taskForm, assignedToUserId: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'schedule'} title="Create Irrigation Schedule" description="Create a schedule for manual officer review." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="schedule-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Schedule'}</Button></>}>
        <form id="schedule-form" className="form-grid" onSubmit={(event) => void createSchedule(event)}>
          <SelectInput label="Field" value={scheduleForm.fieldId} required options={fieldOptions} onChange={(value) => setScheduleForm({ ...scheduleForm, fieldId: value })} />
          <TextInput label="Scheduled at" type="datetime-local" value={scheduleForm.scheduledAt} required onChange={(value) => setScheduleForm({ ...scheduleForm, scheduledAt: value })} />
          <TextInput label="Duration minutes" type="number" min="1" step="1" value={scheduleForm.durationMinutes} required onChange={(value) => setScheduleForm({ ...scheduleForm, durationMinutes: value })} />
          <TextAreaInput label="Notes" value={scheduleForm.notes} required onChange={(value) => setScheduleForm({ ...scheduleForm, notes: value })} />
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
        onConfirm={() => confirmAction ? runAction(confirmAction.action, confirmAction.success) : undefined}
      />
    </section>
  )
}

