import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Camera, CheckCircle2, Save, Sparkles } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { TextAreaInput, TextInput } from '../components/FormControls'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Notice } from '../components/Ui'
import { Roles } from '../routing'
import type {
  ApplicationRole,
  FieldAnalysisResult,
  PrePlantingAssessment,
  PrePlantingAssessmentInput,
  WorkflowReview,
} from '../types'
import './PrePlantingAssessmentPanel.css'

const emptyAssessment: PrePlantingAssessmentInput = {
  soilCondition: '',
  waterAvailability: '',
  irrigationAvailability: '',
  drainageCondition: '',
  generalFieldCondition: '',
  plantingReadiness: '',
  risksAndConcerns: '',
  officerNotes: '',
}

const assessmentStatus: Record<number, string> = {
  1: 'Scheduled',
  2: 'Draft',
  3: 'Submitted',
  4: 'Escalated',
  5: 'Cancelled',
}

export function PrePlantingAssessmentPanel({
  review,
  role,
  onWorkflowChanged,
}: {
  review: WorkflowReview
  role?: ApplicationRole | null
  onWorkflowChanged: () => Promise<void>
}) {
  const requestId = review.workflow.cropPlanRequestId
  const [assessment, setAssessment] = useState<PrePlantingAssessment | null>(null)
  const [form, setForm] = useState<PrePlantingAssessmentInput>(emptyAssessment)
  const [result, setResult] = useState<FieldAnalysisResult | null>(null)
  const [files, setFiles] = useState<File[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const formRef = useRef<HTMLFormElement>(null)

  const isFieldOfficer = role === Roles.FieldOfficer
  const fieldStep = review.steps.find((step) => step.agentName === 'CropFieldAnalysisAgent')
  const fieldStepStatus = fieldStep?.status
  const waitingForFieldAnalysis = review.workflow.currentStep === 'CropFieldAnalysisAgent'
  const fieldAnalysisFailed = fieldStepStatus === 4
  const canEdit = isFieldOfficer && waitingForFieldAnalysis && assessment?.status !== 3
  const canRun = isFieldOfficer && assessment?.status === 3 && (waitingForFieldAnalysis || fieldAnalysisFailed)

  const load = useCallback(async () => {
    if (!requestId) {
      setError('This workflow is not linked to a crop-plan request.')
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError('')
    try {
      const assessmentResponse = await api.get<PrePlantingAssessment | null>(
        '/crop-plans/' + requestId + '/pre-planting-assessment',
      )
      const saved = assessmentResponse.data
      setAssessment(saved)
      if (saved) {
        setForm({
          soilCondition: saved.soilCondition,
          waterAvailability: saved.waterAvailability,
          irrigationAvailability: saved.irrigationAvailability,
          drainageCondition: saved.drainageCondition,
          generalFieldCondition: saved.generalFieldCondition,
          plantingReadiness: saved.plantingReadiness,
          risksAndConcerns: saved.risksAndConcerns,
          officerNotes: saved.officerNotes,
        })
      }

      if (fieldStepStatus === 3 || fieldStepStatus === 4) {
        const resultResponse = await api.get<FieldAnalysisResult>(
          '/crop-plans/' + requestId + '/field-analysis-result',
        )
        setResult(resultResponse.data)
      }
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }, [fieldStepStatus, requestId])

  useEffect(() => {
    void load()
  }, [load])

  function update(name: keyof PrePlantingAssessmentInput, value: string) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  async function saveAssessment() {
    if (!requestId) throw new Error('Crop-plan request is unavailable.')
    const response = await api.put<PrePlantingAssessment>(
      '/crop-plans/' + requestId + '/pre-planting-assessment',
      form,
    )
    setAssessment(response.data)
    return response.data
  }

  async function saveDraft(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setError('')
    setSuccess('')
    try {
      await saveAssessment()
      setSuccess('Pre-planting assessment draft saved.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function submitAndRun() {
    if (!requestId) return
    if (assessment?.status !== 3 && !formRef.current?.reportValidity()) return
    setIsSubmitting(true)
    setError('')
    setSuccess('')
    try {
      let saved = assessment
      if (saved?.status !== 3) {
        saved = await saveAssessment()
        await Promise.all(files.map((file) => {
          const payload = new FormData()
          payload.append('file', file)
          return api.post('/inspections/' + saved!.inspectionId + '/images', payload, {
            headers: { 'Content-Type': 'multipart/form-data' },
          })
        }))
        await api.post('/inspections/' + saved.inspectionId + '/submit')
        saved = { ...saved, status: 3, completedAt: new Date().toISOString() }
        setAssessment(saved)
      }

      await api.post('/crop-plans/' + requestId + '/run-field-analysis')
      setFiles([])
      setSuccess('Pre-planting assessment submitted and field analysis completed.')
      await onWorkflowChanged()
      await load()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) return <LoadingState label="Loading pre-planting assessment..." />

  return (
    <section className="preplant-panel" aria-labelledby="preplant-title">
      <header className="preplant-header">
        <div>
          <p className="preplant-eyebrow">Field Officer stage</p>
          <h2 id="preplant-title">Pre-planting field assessment</h2>
          <span>Record planting readiness before CropFieldAnalysisAgent evaluates this request.</span>
        </div>
        <StatusPill
          label={assessment ? assessmentStatus[assessment.status] ?? 'Saved' : 'Not started'}
          tone={assessment?.status === 3 ? 'good' : 'info'}
        />
      </header>

      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      {!isFieldOfficer && !assessment ? (
        <Notice tone="info">Waiting for the Field Officer to submit the linked pre-planting assessment.</Notice>
      ) : null}

      {canEdit || (isFieldOfficer && !assessment && waitingForFieldAnalysis) ? (
        <form ref={formRef} className="preplant-form" onSubmit={(event) => void saveDraft(event)}>
          <div className="preplant-form-grid">
            <TextInput label="Soil type / condition" value={form.soilCondition} required disabled={isSubmitting} onChange={(value) => update('soilCondition', value)} />
            <TextInput label="Water availability" value={form.waterAvailability} required disabled={isSubmitting} onChange={(value) => update('waterAvailability', value)} />
            <TextInput label="Irrigation availability" value={form.irrigationAvailability} required disabled={isSubmitting} onChange={(value) => update('irrigationAvailability', value)} />
            <TextInput label="Drainage condition" value={form.drainageCondition} required disabled={isSubmitting} onChange={(value) => update('drainageCondition', value)} />
            <TextAreaInput label="General field condition" value={form.generalFieldCondition} required disabled={isSubmitting} rows={3} onChange={(value) => update('generalFieldCondition', value)} />
            <TextAreaInput label="Planting readiness" value={form.plantingReadiness} required disabled={isSubmitting} rows={3} onChange={(value) => update('plantingReadiness', value)} />
            <TextAreaInput label="Risks / concerns" value={form.risksAndConcerns} required disabled={isSubmitting} rows={3} placeholder="Enter None identified when no risk is present." onChange={(value) => update('risksAndConcerns', value)} />
            <TextAreaInput label="Officer notes" value={form.officerNotes} required disabled={isSubmitting} rows={3} onChange={(value) => update('officerNotes', value)} />
          </div>

          <label className="preplant-evidence">
            <span><Camera size={17} aria-hidden="true" /> Photos / evidence</span>
            <input
              type="file"
              accept="image/*"
              multiple
              disabled={isSubmitting}
              onChange={(event) => setFiles(Array.from(event.target.files ?? []))}
            />
            <small>{files.length === 0 ? 'Optional. Images are retained for human review.' : files.length + ' image(s) selected.'}</small>
          </label>

          <div className="preplant-actions">
            <Button type="submit" variant="secondary" icon={<Save size={16} aria-hidden="true" />} disabled={isSubmitting}>
              {isSubmitting ? 'Saving...' : 'Save draft'}
            </Button>
            <Button type="button" icon={<Sparkles size={16} aria-hidden="true" />} disabled={isSubmitting} onClick={() => void submitAndRun()}>
              {isSubmitting ? 'Working...' : 'Submit & run field analysis'}
            </Button>
          </div>
        </form>
      ) : assessment ? (
        <dl className="preplant-readonly">
          <AssessmentItem label="Soil type / condition" value={assessment.soilCondition} />
          <AssessmentItem label="Water availability" value={assessment.waterAvailability} />
          <AssessmentItem label="Irrigation availability" value={assessment.irrigationAvailability} />
          <AssessmentItem label="Drainage condition" value={assessment.drainageCondition} />
          <AssessmentItem label="General field condition" value={assessment.generalFieldCondition} />
          <AssessmentItem label="Planting readiness" value={assessment.plantingReadiness} />
          <AssessmentItem label="Risks / concerns" value={assessment.risksAndConcerns} />
          <AssessmentItem label="Officer notes" value={assessment.officerNotes} />
        </dl>
      ) : null}

      {assessment?.images.length ? (
        <div className="preplant-images" aria-label="Assessment evidence">
          {assessment.images.map((image) => (
            <a key={image.id} href={image.url} target="_blank" rel="noreferrer">View evidence</a>
          ))}
        </div>
      ) : null}

      {canRun ? (
        <div className="preplant-actions">
          <Button icon={<Sparkles size={16} aria-hidden="true" />} disabled={isSubmitting} onClick={() => void submitAndRun()}>
            {fieldAnalysisFailed ? 'Retry field analysis' : 'Run field analysis'}
          </Button>
        </div>
      ) : null}

      {result ? (
        <section className="preplant-result" aria-labelledby="field-result-title">
          <div className="preplant-result-heading">
            <CheckCircle2 size={19} aria-hidden="true" />
            <h3 id="field-result-title">Field analysis result</h3>
            <StatusPill label={result.status} tone={result.status === 'Analyzed' ? 'good' : 'bad'} />
          </div>
          <p>{result.fieldCondition.summary || 'No field-condition summary was returned.'}</p>
          <dl>
            <div><dt>Priority</dt><dd>{result.priority}</dd></div>
            <div><dt>Human review</dt><dd>{result.requiresHumanReview ? 'Required' : 'Not required'}</dd></div>
          </dl>
          {result.warnings.map((warning) => <Notice key={warning} tone="warning">{warning}</Notice>)}
        </section>
      ) : null}
    </section>
  )
}

function AssessmentItem({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}
