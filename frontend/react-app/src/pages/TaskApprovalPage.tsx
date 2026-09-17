import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Ban, Check, Pencil, Plus, RotateCcw, Send, X } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { api, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Modal, Notice, PageHeader, Tabs } from '../components/Ui'
import { formatDateTime } from '../format'
import { approvalDecision, scheduleStatus, taskStatus } from '../labels'
import { isDecisionRole, Roles } from '../routing'
import type { ApprovalDecision, Farm, FarmTask, Field, IrrigationSchedule, PagedResult, UserProfile, WorkflowSummary } from '../types'

type ApprovalTab = 'workflows' | 'tasks' | 'schedules' | 'approvals'
type ApprovalModal = 'task' | 'schedule' | null

type DecisionAction = {
  title: string
  message: string
  label: string
  variant?: 'primary' | 'danger'
  commentRequired: boolean
  action: (comment: string) => Promise<void>
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

function toLocalInputValue(value: string) {
  const date = new Date(value)
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}

export function TaskApprovalPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [farms, setFarms] = useState<Farm[]>([])
  const [fields, setFields] = useState<Field[]>([])
  const [users, setUsers] = useState<UserProfile[]>([])
  const [tasks, setTasks] = useState<FarmTask[]>([])
  const [schedules, setSchedules] = useState<IrrigationSchedule[]>([])
  const [approvals, setApprovals] = useState<ApprovalDecision[]>([])
  const [workflows, setWorkflows] = useState<WorkflowSummary[]>([])
  const [activeTab, setActiveTab] = useState<ApprovalTab>('workflows')
  const [activeModal, setActiveModal] = useState<ApprovalModal>(null)
  const [editingTaskId, setEditingTaskId] = useState<string | null>(null)
  const [editingScheduleId, setEditingScheduleId] = useState<string | null>(null)
  const [decisionAction, setDecisionAction] = useState<DecisionAction>(null)
  const [decisionComment, setDecisionComment] = useState('')
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [taskForm, setTaskForm] = useState({ farmId: '', title: '', description: '', dueAt: '', assignedToUserId: '', status: 2 })
  const [scheduleForm, setScheduleForm] = useState({ fieldId: '', scheduledAt: '', durationMinutes: '', notes: '', status: 1 })

  const assignmentUsers = useMemo(() => users.length > 0 ? users : user ? [user] : [], [user, users])
  const farmOptions = farms.map((farm) => ({ value: farm.id, label: farm.name }))
  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const userOptions = assignmentUsers.map((item) => ({ value: item.id, label: item.fullName }))
  const farmNameById = useMemo(() => new Map(farms.map((farm) => [farm.id, farm.name])), [farms])
  const fieldNameById = useMemo(() => new Map(fields.map((field) => [field.id, field.name])), [fields])
  const userNameById = useMemo(() => new Map(assignmentUsers.map((item) => [item.id, item.fullName])), [assignmentUsers])
  const canDecide = isDecisionRole(user?.role)
  const canManage = user?.role === Roles.Admin || user?.role === Roles.FieldOfficer || user?.role === Roles.AgriculturalOfficer

  async function loadData() {
    setIsLoading(true)
    setError('')
    try {
      const [farmResult, fieldResult, taskResult, scheduleResult, approvalResult, workflowResult] = await Promise.all([
        api.get<PagedResult<Farm>>('/crop-planning/farms'),
        api.get<PagedResult<Field>>('/crop-planning/fields'),
        api.get<PagedResult<FarmTask>>('/task-approval/tasks', { params: { sortBy: 'dueAt' } }),
        api.get<PagedResult<IrrigationSchedule>>('/task-approval/schedules', { params: { sortBy: 'scheduledAt' } }),
        api.get<PagedResult<ApprovalDecision>>('/task-approval/approvals', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
        api.get<PagedResult<WorkflowSummary>>('/task-approval/workflows'),
      ])
      setFarms(farmResult.data.items)
      setFields(fieldResult.data.items)
      setTasks(taskResult.data.items)
      setSchedules(scheduleResult.data.items)
      setApprovals(approvalResult.data.items)
      setWorkflows(workflowResult.data.items)

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
    setEditingTaskId(null)
    setEditingScheduleId(null)
    setActionError('')
  }

  function closeDecision() {
    setDecisionAction(null)
    setDecisionComment('')
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
      closeDecision()
      await loadData()
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  function openCreateTask() {
    setEditingTaskId(null)
    setTaskForm({ farmId: '', title: '', description: '', dueAt: '', assignedToUserId: user?.role === Roles.Admin ? '' : user?.id ?? '', status: 2 })
    setActiveModal('task')
  }

  function openEditTask(task: FarmTask) {
    setEditingTaskId(task.id)
    setTaskForm({ farmId: task.farmId, title: task.title, description: task.description, dueAt: toLocalInputValue(task.dueAt), assignedToUserId: task.assignedToUserId, status: task.status })
    setActiveModal('task')
  }

  function openCreateSchedule() {
    setEditingScheduleId(null)
    setScheduleForm({ fieldId: '', scheduledAt: '', durationMinutes: '', notes: '', status: 1 })
    setActiveModal('schedule')
  }

  function openEditSchedule(schedule: IrrigationSchedule) {
    setEditingScheduleId(schedule.id)
    setScheduleForm({ fieldId: schedule.fieldId, scheduledAt: toLocalInputValue(schedule.scheduledAt), durationMinutes: String(schedule.durationMinutes), notes: schedule.notes, status: schedule.status })
    setActiveModal('schedule')
  }

  async function saveTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const payload = { ...taskForm, dueAt: new Date(taskForm.dueAt).toISOString() }
    await runAction(async () => {
      if (editingTaskId) await api.put(`/task-approval/tasks/${editingTaskId}`, payload)
      else await api.post('/task-approval/tasks', payload)
    }, editingTaskId ? 'Farm task updated successfully.' : 'Farm task created successfully.')
  }

  async function saveSchedule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const payload = { ...scheduleForm, scheduledAt: new Date(scheduleForm.scheduledAt).toISOString(), durationMinutes: Number(scheduleForm.durationMinutes) }
    await runAction(async () => {
      if (editingScheduleId) await api.put(`/task-approval/schedules/${editingScheduleId}`, payload)
      else await api.post('/task-approval/schedules', payload)
    }, editingScheduleId ? 'Irrigation schedule updated successfully.' : 'Irrigation schedule created successfully.')
  }

  function decisionPayload(comment: string) {
    return { comment, agentWorkflowId: null }
  }

  function openTaskDecision(task: FarmTask, action: 'approve' | 'reject' | 'request-revision') {
    const labels = {
      approve: ['Approve task?', 'Approve Task'],
      reject: ['Reject task?', 'Reject Task'],
      'request-revision': ['Request task revision?', 'Request Revision'],
    }
    const [title, label] = labels[action]
    setDecisionComment('')
    setDecisionAction({
      title,
      message: `Record an officer decision for "${task.title}".`,
      label,
      variant: action === 'reject' ? 'danger' : 'primary',
      commentRequired: action !== 'approve',
      action: async (comment) => { await api.post(`/task-approval/tasks/${task.id}/${action}`, decisionPayload(comment)) },
      success: 'Task decision recorded successfully.',
    })
  }

  function openScheduleDecision(schedule: IrrigationSchedule, action: 'approve' | 'reject' | 'request-revision') {
    const labels = {
      approve: ['Approve schedule?', 'Approve Schedule'],
      reject: ['Reject schedule?', 'Reject Schedule'],
      'request-revision': ['Request schedule revision?', 'Request Revision'],
    }
    const [title, label] = labels[action]
    setDecisionComment('')
    setDecisionAction({
      title,
      message: `Record an officer decision for the schedule at ${formatDateTime(schedule.scheduledAt)}.`,
      label,
      variant: action === 'reject' ? 'danger' : 'primary',
      commentRequired: action !== 'approve',
      action: async (comment) => { await api.post(`/task-approval/schedules/${schedule.id}/${action}`, decisionPayload(comment)) },
      success: 'Schedule decision recorded successfully.',
    })
  }

  function openTaskCancel(task: FarmTask) {
    setDecisionComment('')
    setDecisionAction({
      title: 'Cancel task?',
      message: `Cancel "${task.title}" and preserve the reason in decision history.`,
      label: 'Cancel Task',
      variant: 'danger',
      commentRequired: true,
      action: async (reason) => { await api.post(`/task-approval/tasks/${task.id}/cancel`, { reason }) },
      success: 'Task cancelled successfully.',
    })
  }

  function openScheduleCancel(schedule: IrrigationSchedule) {
    setDecisionComment('')
    setDecisionAction({
      title: 'Cancel schedule?',
      message: `Cancel the schedule at ${formatDateTime(schedule.scheduledAt)} and preserve the reason in decision history.`,
      label: 'Cancel Schedule',
      variant: 'danger',
      commentRequired: true,
      action: async (reason) => { await api.post(`/task-approval/schedules/${schedule.id}/cancel`, { reason }) },
      success: 'Schedule cancelled successfully.',
    })
  }

  function openSubmit(path: string, label: string) {
    setDecisionComment('')
    setDecisionAction({
      title: `Submit ${label}?`,
      message: `Return this ${label} to the pending officer approval queue.`,
      label: `Submit ${label}`,
      commentRequired: false,
      action: async () => { await api.post(`${path}/submit`) },
      success: `${label[0].toUpperCase()}${label.slice(1)} submitted successfully.`,
    })
  }

  async function submitDecision(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!decisionAction) return
    if (decisionAction.commentRequired && !decisionComment.trim()) {
      setActionError('A reason is required for this action.')
      return
    }
    await runAction(() => decisionAction.action(decisionComment.trim()), decisionAction.success)
  }

  const tabs = [
    { id: 'workflows', label: 'Workflow Review', count: workflows.length },
    { id: 'tasks', label: 'Farm Tasks', count: tasks.length },
    { id: 'schedules', label: 'Irrigation Schedules', count: schedules.length },
    { id: 'approvals', label: 'Approvals', count: approvals.length },
  ]

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Task Approval"
        title="Tasks & Approvals"
        description="Manage farm tasks, irrigation schedules and auditable officer decisions."
        actions={canManage ? <><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={openCreateTask}>Create Task</Button><Button icon={<Plus size={16} aria-hidden="true" />} onClick={openCreateSchedule}>Create Schedule</Button></> : undefined}
      />

      <Tabs tabs={tabs} activeTab={activeTab} onChange={(tab) => setActiveTab(tab as ApprovalTab)} ariaLabel="Task and approval sections" />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      {isLoading ? <LoadingState /> : (
        <>
          {activeTab === 'workflows' ? (
            <section className="work-section">
              <DataTable rows={workflows} emptyTitle="No scheduling workflows" emptyMessage="Run the crop planning agents to create a scheduling candidate for officer review." getRowKey={(row) => row.id} columns={[
                { header: 'Objective', render: (row) => <div><strong>{row.objective}</strong><p className="muted-text">Revision {row.candidateRevision} · Version {row.version}</p></div> },
                { header: 'State', render: (row) => <StatusPill label={row.currentStep || String(row.status)} tone={row.status === 4 ? 'good' : row.status === 5 || row.status === 9 ? 'bad' : row.status === 8 ? 'warn' : 'info'} /> },
                { header: 'Created', render: (row) => formatDateTime(row.createdAt) },
                { header: 'Actions', className: 'actions-cell', render: (row) => <div className="row-actions"><Button variant="ghost" onClick={() => navigate(`/task-approval/workflows/${row.id}`)}>Review</Button></div> },
              ]} />
            </section>
          ) : null}

          {activeTab === 'tasks' ? (
            <section className="work-section">
              <DataTable rows={tasks} emptyTitle="No farm tasks" emptyMessage="Create a task when farm work needs assignment or approval." getRowKey={(row) => row.id} columns={[
                { header: 'Task', render: (row) => <div><strong>{row.title}</strong><p className="muted-text">{row.description}</p></div> },
                { header: 'Farm', render: (row) => farmNameById.get(row.farmId) ?? row.farmId.slice(0, 8) },
                { header: 'Assigned To', render: (row) => userNameById.get(row.assignedToUserId) ?? row.assignedToUserId.slice(0, 8) },
                { header: 'Due Date', render: (row) => formatDateTime(row.dueAt) },
                { header: 'Status', render: (row) => <StatusPill label={taskStatus[row.status] ?? String(row.status)} tone={taskTone(row.status)} /> },
                { header: 'Actions', className: 'actions-cell', render: (row) => (
                  <div className="row-actions">
                    {canManage && [1, 2, 5].includes(row.status) ? <Button variant="ghost" icon={<Pencil size={14} aria-hidden="true" />} onClick={() => openEditTask(row)}>Edit</Button> : null}
                    {canManage && [1, 5].includes(row.status) ? <Button variant="ghost" icon={<Send size={14} aria-hidden="true" />} onClick={() => openSubmit(`/task-approval/tasks/${row.id}`, 'task')}>Submit</Button> : null}
                    {canDecide && row.status === 2 && !row.generatedByWorkflowId ? <><Button variant="ghost" icon={<Check size={14} aria-hidden="true" />} onClick={() => openTaskDecision(row, 'approve')}>Approve</Button><Button variant="ghost" icon={<RotateCcw size={14} aria-hidden="true" />} onClick={() => openTaskDecision(row, 'request-revision')}>Revision</Button><Button variant="ghost" icon={<X size={14} aria-hidden="true" />} onClick={() => openTaskDecision(row, 'reject')}>Reject</Button></> : null}
                    {row.generatedByWorkflowId ? <Button variant="ghost" onClick={() => navigate(`/task-approval/workflows/${row.generatedByWorkflowId}`)}>Workflow</Button> : null}
                    {canManage && [1, 2, 3, 5].includes(row.status) ? <Button variant="ghost" icon={<Ban size={14} aria-hidden="true" />} onClick={() => openTaskCancel(row)}>Cancel</Button> : null}
                    {!canManage && !canDecide ? <span className="muted-text">No action</span> : null}
                  </div>
                ) },
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
                { header: 'Actions', className: 'actions-cell', render: (row) => (
                  <div className="row-actions">
                    {canManage && [1, 4].includes(row.status) ? <Button variant="ghost" icon={<Pencil size={14} aria-hidden="true" />} onClick={() => openEditSchedule(row)}>Edit</Button> : null}
                    {canManage && row.status === 4 ? <Button variant="ghost" icon={<Send size={14} aria-hidden="true" />} onClick={() => openSubmit(`/task-approval/schedules/${row.id}`, 'schedule')}>Submit</Button> : null}
                    {canDecide && row.status === 1 && !row.generatedByWorkflowId ? <><Button variant="ghost" icon={<Check size={14} aria-hidden="true" />} onClick={() => openScheduleDecision(row, 'approve')}>Approve</Button><Button variant="ghost" icon={<RotateCcw size={14} aria-hidden="true" />} onClick={() => openScheduleDecision(row, 'request-revision')}>Revision</Button><Button variant="ghost" icon={<X size={14} aria-hidden="true" />} onClick={() => openScheduleDecision(row, 'reject')}>Reject</Button></> : null}
                    {row.generatedByWorkflowId ? <Button variant="ghost" onClick={() => navigate(`/task-approval/workflows/${row.generatedByWorkflowId}`)}>Workflow</Button> : null}
                    {canManage && [1, 2, 4].includes(row.status) ? <Button variant="ghost" icon={<Ban size={14} aria-hidden="true" />} onClick={() => openScheduleCancel(row)}>Cancel</Button> : null}
                    {!canManage && !canDecide ? <span className="muted-text">No action</span> : null}
                  </div>
                ) },
              ]} />
            </section>
          ) : null}

          {activeTab === 'approvals' ? (
            <section className="work-section">
              {approvals.length === 0 ? <EmptyState title="No approval decisions" message="Approval decisions will appear here after a task or schedule is decided." /> : (
                <DataTable rows={approvals} emptyMessage="No approvals found." getRowKey={(row) => row.id} columns={[
                  { header: 'Decision', render: (row) => <StatusPill label={approvalDecision[row.decision] ?? String(row.decision)} tone={row.decision === 1 ? 'good' : row.decision === 2 || row.decision === 4 ? 'bad' : 'info'} /> },
                  { header: 'Target', render: (row) => row.farmTaskId ? `Task ${row.farmTaskId.slice(0, 8)}` : row.irrigationScheduleId ? `Schedule ${row.irrigationScheduleId.slice(0, 8)}` : 'Not linked' },
                  { header: 'Comment', render: (row) => row.comment || 'No comment' },
                  { header: 'Created', render: (row) => formatDateTime(row.createdAt) },
                ]} />
              )}
            </section>
          ) : null}
        </>
      )}

      <Modal open={activeModal === 'task'} title={editingTaskId ? 'Edit Farm Task' : 'Create Farm Task'} description="Task status changes are controlled by workflow actions." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="task-form" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : editingTaskId ? 'Save Task' : 'Create Task'}</Button></>}>
        <form id="task-form" className="form-grid" onSubmit={(event) => void saveTask(event)}>
          <SelectInput label="Farm" value={taskForm.farmId} required options={farmOptions} onChange={(value) => setTaskForm({ ...taskForm, farmId: value })} />
          <TextInput label="Title" value={taskForm.title} required onChange={(value) => setTaskForm({ ...taskForm, title: value })} />
          <TextAreaInput label="Description" value={taskForm.description} required onChange={(value) => setTaskForm({ ...taskForm, description: value })} />
          <TextInput label="Due at" type="datetime-local" value={taskForm.dueAt} required onChange={(value) => setTaskForm({ ...taskForm, dueAt: value })} />
          <SelectInput label="Assigned to" value={taskForm.assignedToUserId} required options={userOptions} onChange={(value) => setTaskForm({ ...taskForm, assignedToUserId: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'schedule'} title={editingScheduleId ? 'Edit Irrigation Schedule' : 'Create Irrigation Schedule'} description="Overlapping schedules for the same field are rejected." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="schedule-form" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : editingScheduleId ? 'Save Schedule' : 'Create Schedule'}</Button></>}>
        <form id="schedule-form" className="form-grid" onSubmit={(event) => void saveSchedule(event)}>
          <SelectInput label="Field" value={scheduleForm.fieldId} required options={fieldOptions} onChange={(value) => setScheduleForm({ ...scheduleForm, fieldId: value })} />
          <TextInput label="Scheduled at" type="datetime-local" value={scheduleForm.scheduledAt} required onChange={(value) => setScheduleForm({ ...scheduleForm, scheduledAt: value })} />
          <TextInput label="Duration minutes" type="number" min="1" step="1" value={scheduleForm.durationMinutes} required onChange={(value) => setScheduleForm({ ...scheduleForm, durationMinutes: value })} />
          <TextAreaInput label="Notes" value={scheduleForm.notes} required onChange={(value) => setScheduleForm({ ...scheduleForm, notes: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={Boolean(decisionAction)} title={decisionAction?.title ?? ''} description={decisionAction?.message} onClose={closeDecision} footer={<><Button variant="secondary" onClick={closeDecision} disabled={isSubmitting}>Back</Button><Button variant={decisionAction?.variant} type="submit" form="decision-form" disabled={isSubmitting}>{isSubmitting ? 'Working...' : decisionAction?.label ?? 'Confirm'}</Button></>}>
        <form id="decision-form" className="form-grid" onSubmit={(event) => void submitDecision(event)}>
          <TextAreaInput label={decisionAction?.commentRequired ? 'Reason' : 'Comment (optional)'} value={decisionComment} required={decisionAction?.commentRequired} rows={4} onChange={setDecisionComment} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>
    </section>
  )
}

