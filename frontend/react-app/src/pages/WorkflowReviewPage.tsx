import { Component, useCallback, useEffect, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { ArrowLeft, Check, Play, RotateCcw, Search, X } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { TextAreaInput, TextInput } from '../components/FormControls'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Modal, Notice, PageHeader } from '../components/Ui'
import { ResourceRequirementResearchPanel } from '../components/ResourceRequirementResearchPanel'
import { formatDateTime } from '../format'
import { isDecisionRole } from '../routing'
import type { CropType, CropVariety, PagedResult, PrePlantingContext, WorkflowReview } from '../types'
import { PrePlantingAssessmentPanel } from './PrePlantingAssessmentPanel'
import { parseSchedulingOutput, safeSourceUrl } from './schedulingProposal'
import type { ProposalSource } from './schedulingProposal'
import { workflowStatusLabels } from './workflowStatusLabels'

type DecisionKind = 'approve' | 'reject' | 'request-revision'

async function allItems<T>(path: string): Promise<T[]> {
  const first = await api.get<PagedResult<T>>(path, { params: { page: 1, pageSize: 100 } })
  const pages = await Promise.all(Array.from({ length: Math.max(0, first.data.totalPages - 1) }, (_, index) =>
    api.get<PagedResult<T>>(path, { params: { page: index + 2, pageSize: 100 } })))
  return [first.data, ...pages.map((page) => page.data)].flatMap((page) => page.items)
}

class PrePlantingAssessmentErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  render() {
    if (this.state.failed) {
      return (
        <section className="preplant-panel" aria-label="Pre-planting assessment error">
          <ErrorState message="The pre-planting assessment could not be displayed. Review the remaining workflow evidence or try again." />
        </section>
      )
    }

    return this.props.children
  }
}

function tone(status: number) {
  if (status === 4) return 'good'
  if ([5, 6, 9, 11, 12].includes(status)) return 'bad'
  if (status === 8) return 'warn'
  return 'info'
}

function ProposalSources({ sources }: { sources: ProposalSource[] }) {
  return <p className="muted-text">Source: {sources.map((source, index) => {
    const url = safeSourceUrl(source.sourceUrl)
    return <span key={`${source.kind}-${source.id}`}>
      {index > 0 ? ', ' : ''}{url ? <a href={url} target="_blank" rel="noopener noreferrer">{source.label}</a> : source.label}
      {' '}({source.kind}, {source.id})
    </span>
  })}</p>
}

export function WorkflowReviewPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { user } = useAuth()
  const [review, setReview] = useState<WorkflowReview | null>(null)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [decision, setDecision] = useState<DecisionKind | null>(null)
  const [comment, setComment] = useState('')
  const [guidanceDecision, setGuidanceDecision] = useState<'Included' | 'Rejected' | null>(null)
  const [guidanceReason, setGuidanceReason] = useState('')
  const [retryReason, setRetryReason] = useState('')
  const [retryWindow, setRetryWindow] = useState<{ version: number; start: string; end: string } | null>(null)
  const [researchOpen, setResearchOpen] = useState(false)
  const [researchLoading, setResearchLoading] = useState(false)
  const [researchError, setResearchError] = useState('')
  const [researchContext, setResearchContext] = useState<PrePlantingContext | null>(null)
  const [crops, setCrops] = useState<CropType[]>([])
  const [varieties, setVarieties] = useState<CropVariety[]>([])

  const canDecide = isDecisionRole(user?.role)

  const loadReview = useCallback(async () => {
    if (!id) return
    try {
      const response = await api.get<WorkflowReview>(`/task-approval/workflows/${id}`)
      setReview(response.data)
      setError('')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }, [id])

  useEffect(() => {
    void loadReview()
  }, [loadReview])

  async function generateCandidate() {
    if (!id) return
    setIsSubmitting(true)
    setError('')
    setSuccess('')
    try {
      const response = await api.post<WorkflowReview>(`/task-approval/workflows/${id}/generate-candidate`)
      setReview(response.data)
      setSuccess(response.data.workflow.status === 8 ? 'Scheduling candidate is ready for officer review.' :
        'Scheduling proposal recorded. Review its blocking reasons and validation result.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function retryScheduling(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!id || !review) return
    if (!retryReason.trim()) {
      setError('Explain what evidence or date-window change should be rechecked.')
      return
    }
    const preferredStartDate = retryWindow?.version === review.workflow.version ? retryWindow.start : review.preferredStartDate
    const preferredEndDate = retryWindow?.version === review.workflow.version ? retryWindow.end : review.preferredEndDate
    if (!preferredStartDate || !preferredEndDate || preferredEndDate <= preferredStartDate) {
      setError('Choose a valid preferred date window. The end date must be after the start date.')
      return
    }
    setIsSubmitting(true)
    setError('')
    setSuccess('')
    try {
      const response = await api.post<WorkflowReview>(`/task-approval/workflows/${id}/retry-scheduling`, {
        expectedWorkflowVersion: review.workflow.version,
        reason: retryReason.trim(),
        preferredStartDate,
        preferredEndDate,
      })
      setReview(response.data)
      setRetryWindow(null)
      setRetryReason('')
      setSuccess(response.data.workflow.status === 8
        ? 'Updated weather/resource evidence was used. A new scheduling candidate is ready for officer review.'
        : 'Evidence refresh and retry completed. Review the new blocking reasons and update verified evidence if needed.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function openResourceResearch() {
    const currentReview = review
    if (!currentReview) return
    if (researchOpen) {
      setResearchOpen(false)
      return
    }
    if (!currentReview.workflow.cropPlanRequestId) {
      setResearchError('This workflow has no linked crop request for research context.')
      return
    }
    setResearchOpen(true)
    setResearchLoading(true)
    setResearchError('')
    try {
      const [context, nextCrops, nextVarieties] = await Promise.all([
        api.get<PrePlantingContext>(`/crop-plans/${currentReview.workflow.cropPlanRequestId}/pre-planting-context`),
        allItems<CropType>('/crop-planning/crop-types'),
        allItems<CropVariety>('/crop-planning/crop-varieties'),
      ])
      setResearchContext(context.data)
      setCrops(nextCrops)
      setVarieties(nextVarieties)
    } catch (err) {
      setResearchError(getErrorMessage(err))
    } finally {
      setResearchLoading(false)
    }
  }

  async function submitDecision(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!id || !review || !decision) return
    if (decision !== 'approve' && !comment.trim()) {
      setError('A reason is required for rejection or revision.')
      return
    }
    setIsSubmitting(true)
    setError('')
    setSuccess('')
    try {
      await api.post(`/task-approval/workflows/${id}/${decision}`, {
        candidateRevision: review.workflow.candidateRevision,
        expectedWorkflowVersion: review.workflow.version,
        idempotencyKey: crypto.randomUUID(),
        comment: comment.trim(),
      })
      setDecision(null)
      setComment('')
      setSuccess('Workflow decision recorded successfully.')
      await loadReview()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function submitGuidanceDecision(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!id || !review || !guidanceDecision) return
    if (guidanceDecision === 'Rejected' && !guidanceReason.trim()) {
      setError('A staff-only reason is required to reject crop-health guidance.')
      return
    }
    setIsSubmitting(true)
    setError('')
    try {
      const response = await api.post<WorkflowReview>(`/task-approval/workflows/${id}/crop-health-guidance-decision`, {
        candidateRevision: review.workflow.candidateRevision,
        expectedWorkflowVersion: review.workflow.version,
        decision: guidanceDecision,
        idempotencyKey: crypto.randomUUID(),
        rejectionReason: guidanceDecision === 'Rejected' ? guidanceReason.trim() : null,
      })
      setReview(response.data)
      setGuidanceDecision(null)
      setGuidanceReason('')
      setSuccess(`Crop-health guidance ${guidanceDecision.toLowerCase()}.`)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function toggleCropHealthAction(actionKey: string, included: boolean, dueAt: string, assignedToUserId: string, schedulingNote?: string) {
    if (!id || !review) return
    setIsSubmitting(true)
    setError('')
    try {
      const response = await api.put<WorkflowReview>(`/task-approval/workflows/${id}/crop-health-actions/${actionKey}`, {
        candidateRevision: review.workflow.candidateRevision,
        expectedWorkflowVersion: review.workflow.version,
        included,
        dueAt,
        assignedToUserId,
        schedulingNote: schedulingNote ?? null,
      })
      setReview(response.data)
      setSuccess(included ? 'Crop-health action included.' : 'Crop-health action excluded from approved work.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) return <LoadingState />
  if (!review) return <ErrorState message={error || 'Workflow review is unavailable.'} />

  const pendingApproval = review.workflow.status === 8
  const activeRetryWindow = retryWindow?.version === review.workflow.version ? retryWindow : null
  const preferredStartDate = activeRetryWindow?.start ?? review.preferredStartDate
  const preferredEndDate = activeRetryWindow?.end ?? review.preferredEndDate
  const schedulingStep = [...review.steps].reverse().find((step) => step.agentName === 'SchedulingValidationAgent' &&
    step.candidateRevision === review.workflow.candidateRevision)
  const proposal = parseSchedulingOutput(schedulingStep?.output)
  const needsCropReference = proposal?.blocking.some((message) => /verified crop profile|crop reference.*stage/i.test(message)) ?? false
  const guidanceDecisionPending = proposal?.cropHealthGuidance?.decision === 'PendingDecision'
  const canGenerate = canDecide && review.workflow.currentStep === 'SchedulingValidationAgent'
    && review.workflow.status === 2
  const canRetry = canDecide && [5, 10, 11, 12].includes(review.workflow.status)

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Scheduling Validation"
        title="Workflow Review"
        description={review.workflow.objective}
        actions={<Button variant="secondary" icon={<ArrowLeft size={16} aria-hidden="true" />} onClick={() => navigate('/task-approval')}>Back to Queue</Button>}
      />

      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      <section className="work-section">
        <div className="row-actions">
          <StatusPill label={workflowStatusLabels[review.workflow.status] ?? String(review.workflow.status)} tone={tone(review.workflow.status)} />
          <span className="muted-text">Candidate revision {review.workflow.candidateRevision} · Version {review.workflow.version} · {review.preferredStartDate} to {review.preferredEndDate} · Budget {review.budget}</span>
        </div>
        <div className="row-actions">
          {canGenerate ? <Button icon={<Play size={15} aria-hidden="true" />} onClick={() => void generateCandidate()} disabled={isSubmitting}>Generate Candidate</Button> : null}
          {canDecide && pendingApproval ? <>
            <Button icon={<Check size={15} aria-hidden="true" />} disabled={guidanceDecisionPending} onClick={() => setDecision('approve')}>Approve Workflow</Button>
            <Button variant="secondary" icon={<RotateCcw size={15} aria-hidden="true" />} onClick={() => setDecision('request-revision')}>Request Revision</Button>
            <Button variant="danger" icon={<X size={15} aria-hidden="true" />} onClick={() => setDecision('reject')}>Reject Workflow</Button>
          </> : null}
        </div>
      </section>

      {canRetry ? <section className="work-section" aria-label="Retry scheduling">
        <h2>Resolve the blocker and retry</h2>
        <p>AI research can find cited suggestions, but it does not verify them. Only an Admin can save a researched resource requirement. Retry refreshes Member 3 weather/resource analysis and then runs Member 4 again.</p>
        {needsCropReference ? <Notice tone="warning">This workflow also needs a matching active crop reference profile with verified stages. Add or activate that sourced profile under <Link to="/crop-planning">Crop Planning → Verified crop references</Link>, then retry. AI will not invent stage durations.</Notice> : null}
        <div className="row-actions">
          <Button variant="secondary" icon={<Search size={15} aria-hidden="true" />} onClick={() => void openResourceResearch()} disabled={researchLoading || isSubmitting}>
            {researchOpen ? 'Hide AI research' : 'Research missing resource data'}
          </Button>
        </div>
        {researchLoading ? <p role="status">Loading crop and region context…</p> : null}
        {researchError ? <Notice tone="error">{researchError}</Notice> : null}
        {researchOpen && !researchLoading && researchContext ? <ResourceRequirementResearchPanel
          crops={crops}
          varieties={varieties}
          canVerify={user?.role === 5}
          initialValues={{ cropTypeId: researchContext.cropTypeId, cropVarietyId: researchContext.cropVarietyId, region: researchContext.farmDistrict || researchContext.farmLocation }}
          onSaved={() => setSuccess('Admin-verified resource evidence was saved. Retry scheduling to refresh Member 3 and Member 4.')}
        /> : null}
        <form className="form-grid" onSubmit={(event) => void retryScheduling(event)}>
          <TextInput label="Preferred start date" type="date" value={preferredStartDate} required onChange={(start) => setRetryWindow({ version: review.workflow.version, start, end: preferredEndDate })} />
          <TextInput label="Preferred end date" type="date" value={preferredEndDate} required onChange={(end) => setRetryWindow({ version: review.workflow.version, start: preferredStartDate, end })} />
          <TextAreaInput label="Reason for retry" value={retryReason} required rows={3} onChange={setRetryReason} />
          <Button type="submit" icon={<RotateCcw size={15} aria-hidden="true" />} disabled={isSubmitting || !retryReason.trim() || !preferredStartDate || !preferredEndDate || preferredEndDate <= preferredStartDate}>
            {isSubmitting ? 'Refreshing evidence…' : 'Retry scheduling'}
          </Button>
        </form>
      </section> : null}

      <PrePlantingAssessmentErrorBoundary key={`${review.workflow.cropPlanRequestId}:${review.workflow.version}`}>
        <PrePlantingAssessmentPanel
          review={review}
          role={user?.role}
          onWorkflowChanged={loadReview}
        />
      </PrePlantingAssessmentErrorBoundary>

      {review.workflow.status === 12 ? <Notice tone="error">This proposal is blocked and cannot be approved. Use Retry scheduling after the underlying evidence or date window changes. No farm work has been created.</Notice> : null}

      {proposal ? <section className="work-section" aria-label="Scheduling proposal">
        <h2>Scheduling proposal</h2>
        <p className="muted-text">These are candidate items only. Farm work is created after officer approval.</p>
        {proposal.blocking.map((message, index) => <Notice key={`block-${index}`} tone="error">{message}</Notice>)}
        {proposal.warnings.map((message, index) => <Notice key={`warning-${index}`} tone="info">{message}</Notice>)}
        {proposal.cropHealthGuidance && proposal.cropHealthGuidance.decision !== 'NotApplicable' ? <section className="work-section" aria-label="Locked crop-health guidance">
          <div className="row-actions"><h3>Crop-health guidance</h3><StatusPill label={proposal.cropHealthGuidance.decision} tone={proposal.cropHealthGuidance.decision === 'Included' ? 'good' : proposal.cropHealthGuidance.decision === 'Rejected' ? 'bad' : 'warn'} /></div>
          <Notice tone="info">This wording is locked to the reviewed Member 2 evidence. It cannot be rewritten into a diagnosis or chemical treatment.</Notice>
          <dl className="preplant-readonly">
            <div><dt>Observation</dt><dd>{proposal.cropHealthGuidance.cropHealthObservation}</dd></div>
            <div><dt>Possible concern</dt><dd>{proposal.cropHealthGuidance.possibleConcern}</dd></div>
            <div><dt>Uncertainty</dt><dd>{proposal.cropHealthGuidance.uncertaintyGuidance}</dd></div>
            <div><dt>Why recommended</dt><dd>{proposal.cropHealthGuidance.whyThisIsRecommended}</dd></div>
          </dl>
          {proposal.cropHealthGuidance.prePlantingActions.length ? <ul>{proposal.cropHealthGuidance.prePlantingActions.map((item) => <li key={item}>{item}</li>)}</ul> : null}
          {proposal.cropHealthGuidance.monitoringActions.length ? <ul>{proposal.cropHealthGuidance.monitoringActions.map((item) => <li key={item}>{item}</li>)}</ul> : null}
          {proposal.cropHealthGuidance.escalationGuidance ? <p>{proposal.cropHealthGuidance.escalationGuidance}</p> : null}
          {proposal.cropHealthGuidance.rejectionReason ? <p className="muted-text">Staff-only rejection reason: {proposal.cropHealthGuidance.rejectionReason}</p> : null}
          {canDecide && pendingApproval ? <div className="row-actions">
            <Button disabled={isSubmitting} onClick={() => setGuidanceDecision('Included')}>Include guidance</Button>
            <Button variant="danger" disabled={isSubmitting} onClick={() => setGuidanceDecision('Rejected')}>Reject guidance</Button>
          </div> : null}
          {guidanceDecisionPending ? <Notice tone="warning">Include or reject this guidance before final proposal approval.</Notice> : null}
        </section> : null}
        {proposal.cropHealthTasks.length ? <section className="work-section" aria-label="Crop-health candidate work">
          <h3>Crop-health candidate work ({proposal.cropHealthTasks.length})</h3>
          <p className="muted-text">Titles and descriptions are catalog-controlled. Officers may include or exclude each action and adjust only its operational schedule.</p>
          {proposal.cropHealthTasks.map((task) => <article key={task.actionKey} className="work-section">
            <div className="row-actions"><strong>{task.title}</strong><StatusPill label={task.included ? 'Included' : 'Excluded'} tone={task.included ? 'good' : 'bad'} /></div>
            <p>{task.description}</p><p className="muted-text">{task.taskCategory} · {task.timingCategory} · {formatDateTime(task.dueAt)}</p>
            {canDecide && pendingApproval ? <Button variant={task.included ? 'danger' : 'secondary'} disabled={isSubmitting} onClick={() => void toggleCropHealthAction(task.actionKey, !task.included, task.dueAt, task.assignedToUserId, task.schedulingNote)}>{task.included ? 'Exclude action' : 'Include action'}</Button> : null}
          </article>)}
        </section> : null}
        <h3>Farm tasks ({proposal.tasks.length})</h3>
        {proposal.tasks.map((task, index) => <article key={`task-${index}`} className="work-section">
          <strong>{task.title}</strong><p>{formatDateTime(task.dueAt)}</p><p>{task.reason}</p>
          <ProposalSources sources={task.sources} />
        </article>)}
        <h3>Irrigation ({proposal.irrigation.length})</h3>
        {proposal.irrigation.length === 0 ? <p className="muted-text">No verified irrigation schedule rule produced an entry.</p> : null}
        {proposal.irrigation.map((item, index) => <article key={`irrigation-${index}`} className="work-section">
          <strong>{formatDateTime(item.scheduledAt)} · {item.durationMinutes} min</strong><p>{item.reason}</p>
          <ProposalSources sources={item.sources} />
        </article>)}
        <h3>Resource reservations ({proposal.reservations.length})</h3>
        {proposal.reservations.map((item, index) => <article key={`reservation-${index}`} className="work-section">
          <strong>{item.quantity} units · stock {item.inventoryStockId}</strong><p>{item.reason}</p>
          <ProposalSources sources={item.sources} />
        </article>)}
      </section> : null}

      <section className="work-section">
        <h2>Agent evidence</h2>
        {review.steps.length === 0 ? <EmptyState title="No agent evidence" message="No workflow steps were recorded." /> : review.steps.map((step) => (
          <article key={step.id} className="work-section">
            <div className="row-actions"><strong>{step.sequence}. {step.stepName}</strong><StatusPill label={step.errorCode ?? `Status ${step.status}`} tone={step.status === 3 ? 'good' : step.status === 4 ? 'bad' : 'info'} /></div>
            <p className="muted-text">{step.agentName} · candidate revision {step.candidateRevision}{step.completedAt ? ` · completed ${formatDateTime(step.completedAt)}` : ''}</p>
            {step.errorMessageSafe ? <Notice tone="error">{step.errorMessageSafe}</Notice> : null}
            <details><summary>Stored output</summary><pre>{JSON.stringify(step.output, null, 2)}</pre></details>
          </article>
        ))}
      </section>

      <section className="work-section">
        <h2>Deterministic validation</h2>
        {review.validations.length === 0 ? <EmptyState title="No validation evidence" message="Generate a candidate to run backend validation." /> : review.validations.map((validation) => (
          <article key={validation.id} className="work-section">
            <div className="row-actions"><strong>{validation.validatorName}</strong><StatusPill label={validation.isValid ? 'Valid' : 'Blocked'} tone={validation.isValid ? 'good' : 'bad'} /></div>
            <p className="muted-text">Revision {validation.candidateRevision} · {formatDateTime(validation.createdAt)}</p>
            {validation.errors.map((item) => <Notice key={item} tone="error">{item}</Notice>)}
            {validation.warnings.map((item) => <Notice key={item} tone="info">{item}</Notice>)}
          </article>
        ))}
      </section>

      <section className="work-section">
        <h2>Decision history</h2>
        {review.decisions.length === 0 ? <EmptyState title="No decisions" message="Officer decisions will appear here." /> : review.decisions.map((item) => (
          <article key={item.id}><strong>Decision {item.decision}</strong><p>{item.comment || 'No comment'}</p><p className="muted-text">{formatDateTime(item.createdAt)}</p></article>
        ))}
      </section>

      <Modal open={Boolean(decision)} title={decision === 'approve' ? 'Approve workflow?' : decision === 'reject' ? 'Reject workflow?' : 'Request revision?'} description="This decision is tied to the candidate revision and workflow version currently displayed." onClose={() => setDecision(null)} footer={<><Button variant="secondary" onClick={() => setDecision(null)} disabled={isSubmitting}>Back</Button><Button variant={decision === 'reject' ? 'danger' : 'primary'} type="submit" form="workflow-decision-form" disabled={isSubmitting}>{isSubmitting ? 'Working...' : 'Confirm Decision'}</Button></>}>
        <form id="workflow-decision-form" className="form-grid" onSubmit={(event) => void submitDecision(event)}>
          <TextAreaInput label={decision === 'approve' ? 'Comment (optional)' : 'Reason'} value={comment} required={decision !== 'approve'} rows={4} onChange={setComment} />
        </form>
      </Modal>
      <Modal open={Boolean(guidanceDecision)} title={guidanceDecision === 'Included' ? 'Include crop-health guidance?' : 'Reject crop-health guidance?'} description="This explicit decision applies only to the locked farmer guidance for the current proposal version." onClose={() => setGuidanceDecision(null)} footer={<><Button variant="secondary" onClick={() => setGuidanceDecision(null)} disabled={isSubmitting}>Back</Button><Button variant={guidanceDecision === 'Rejected' ? 'danger' : 'primary'} type="submit" form="guidance-decision-form" disabled={isSubmitting}>{isSubmitting ? 'Working...' : 'Confirm Guidance Decision'}</Button></>}>
        <form id="guidance-decision-form" className="form-grid" onSubmit={(event) => void submitGuidanceDecision(event)}>
          {guidanceDecision === 'Rejected' ? <TextAreaInput label="Staff-only rejection reason" value={guidanceReason} required rows={4} onChange={setGuidanceReason} /> : <Notice tone="info">Included guidance will appear in the farmer-safe final plan only after the overall proposal is approved.</Notice>}
        </form>
      </Modal>
    </section>
  )
}
