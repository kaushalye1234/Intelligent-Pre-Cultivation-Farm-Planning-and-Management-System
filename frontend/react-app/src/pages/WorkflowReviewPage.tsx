import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { ArrowLeft, Check, Play, RotateCcw, X } from 'lucide-react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { TextAreaInput } from '../components/FormControls'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Modal, Notice, PageHeader } from '../components/Ui'
import { formatDateTime } from '../format'
import { isDecisionRole } from '../routing'
import type { WorkflowReview } from '../types'

type DecisionKind = 'approve' | 'reject' | 'request-revision'

const workflowStatus: Record<number, string> = {
  1: 'Not Started',
  2: 'Pending',
  3: 'Running',
  4: 'Completed',
  5: 'Failed',
  6: 'Cancelled',
  7: 'Candidate Ready',
  8: 'Pending Officer Approval',
  9: 'Rejected',
  10: 'Revision Requested',
  11: 'Missing Dependency',
}

function tone(status: number) {
  if (status === 4) return 'good'
  if ([5, 6, 9, 11].includes(status)) return 'bad'
  if (status === 8) return 'warn'
  return 'info'
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

  const canDecide = isDecisionRole(user?.role)

  const loadReview = useCallback(async () => {
    if (!id) return
    setIsLoading(true)
    setError('')
    try {
      const response = await api.get<WorkflowReview>(`/task-approval/workflows/${id}`)
      setReview(response.data)
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
      setSuccess('Scheduling candidate generated and validated.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
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

  if (isLoading) return <LoadingState />
  if (!review) return <ErrorState message={error || 'Workflow review is unavailable.'} />

  const pendingApproval = review.workflow.status === 8
  const canGenerate = canDecide && ![3, 4, 6, 8, 9].includes(review.workflow.status)

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
          <StatusPill label={workflowStatus[review.workflow.status] ?? String(review.workflow.status)} tone={tone(review.workflow.status)} />
          <span className="muted-text">Candidate revision {review.workflow.candidateRevision} · Version {review.workflow.version} · {review.preferredStartDate} to {review.preferredEndDate} · Budget {review.budget}</span>
        </div>
        <div className="row-actions">
          {canGenerate ? <Button icon={<Play size={15} aria-hidden="true" />} onClick={() => void generateCandidate()} disabled={isSubmitting}>Generate Candidate</Button> : null}
          {canDecide && pendingApproval ? <>
            <Button icon={<Check size={15} aria-hidden="true" />} onClick={() => setDecision('approve')}>Approve Workflow</Button>
            <Button variant="secondary" icon={<RotateCcw size={15} aria-hidden="true" />} onClick={() => setDecision('request-revision')}>Request Revision</Button>
            <Button variant="danger" icon={<X size={15} aria-hidden="true" />} onClick={() => setDecision('reject')}>Reject Workflow</Button>
          </> : null}
        </div>
      </section>

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
    </section>
  )
}
