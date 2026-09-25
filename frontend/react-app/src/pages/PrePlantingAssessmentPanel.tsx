import { useCallback, useEffect, useRef, useState } from 'react'
import { Camera, CheckCircle2, Save, Send, Sparkles, Upload } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Notice } from '../components/Ui'
import { Roles } from '../routing'
import type {
  ApplicationRole,
  FieldAnalysisResult,
  PrePlantingAssessment,
  PrePlantingAssessmentInput,
  PrePlantingContext,
  PrePlantingDrainageCondition,
  PrePlantingGeneralFieldCondition,
  PrePlantingIrrigationAvailability,
  PrePlantingPlantingReadiness,
  PrePlantingRisk,
  PrePlantingSoilCondition,
  PrePlantingSoilMoisture,
  PrePlantingSoilType,
  PrePlantingWaterAvailability,
  PrePlantingWaterloggingRisk,
  PrePlantingWaterReliability,
  WorkflowReview,
} from '../types'
import './PrePlantingAssessmentPanel.css'

type RiskAssessmentState = 'unassessed' | 'none' | 'selected'
type ActionState = 'save' | 'upload' | 'submit' | 'run' | null

const emptyAssessment: PrePlantingAssessmentInput = {
  soilType: null,
  soilCondition: null,
  soilMoisture: null,
  soilNotes: null,
  waterAvailability: null,
  mainWaterSource: null,
  irrigationAvailability: null,
  waterReliability: null,
  waterConcerns: null,
  drainageCondition: null,
  waterloggingRisk: null,
  drainageNotes: null,
  generalFieldCondition: null,
  generalFieldNotes: null,
  plantingReadiness: null,
  identifiedRisks: null,
  riskNotes: null,
  risksAndConcerns: null,
  officerNotes: null,
}

const assessmentStatus: Record<number, string> = {
  1: 'Scheduled',
  2: 'Saved draft',
  3: 'Submitted',
  4: 'Escalated',
  5: 'Cancelled',
}

const seasonLabels: Record<number, string> = {
  0: 'Not sure',
  1: 'Maha',
  2: 'Yala',
  3: 'Off season',
}

const soilTypeOptions = options<PrePlantingSoilType>(['Sandy', 'Clay', 'Loamy', 'Silty', 'Mixed', 'Unknown', 'Other'])
const soilConditionOptions = options<PrePlantingSoilCondition>(['Good', 'Moderate', 'Poor', 'Compacted', 'Eroded', 'Unknown', 'Other'])
const soilMoistureOptions = options<PrePlantingSoilMoisture>(['Dry', 'Moist', 'Wet', 'Waterlogged', 'Unknown'])
const waterAvailabilityOptions = options<PrePlantingWaterAvailability>(['Adequate', 'Limited', 'Unavailable', 'Seasonal', 'Unknown'])
const irrigationOptions = options<PrePlantingIrrigationAvailability>(['Available', 'Limited', 'Unavailable', 'NotRequired', 'Unknown'])
const waterReliabilityOptions = options<PrePlantingWaterReliability>(['Reliable', 'Intermittent', 'Seasonal', 'Unreliable', 'Unknown'])
const drainageOptions = options<PrePlantingDrainageCondition>(['Good', 'Moderate', 'Poor', 'Unknown'])
const waterloggingOptions = options<PrePlantingWaterloggingRisk>(['NoneObserved', 'Low', 'Moderate', 'High', 'Unknown'])
const fieldConditionOptions = options<PrePlantingGeneralFieldCondition>([
  'ClearAndPrepared',
  'RequiresLandPreparation',
  'UnevenField',
  'Waterlogged',
  'TooDry',
  'ErosionPresent',
  'AccessLimitation',
  'Other',
])
const readinessOptions = options<PrePlantingPlantingReadiness>([
  'Ready',
  'ReadyWithMinorPreparation',
  'RequiresPreparation',
  'NotReady',
  'RequiresFurtherAssessment',
])
const riskOptions: PrePlantingRisk[] = [
  'WaterShortageRisk',
  'FloodingRisk',
  'PoorDrainage',
  'SoilSuitabilityConcern',
  'SoilErosion',
  'FieldAccessProblem',
  'LandPreparationRequired',
  'Other',
]

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
  const fieldStep = review.steps.find((step) => step.agentName === 'CropFieldAnalysisAgent')
  const fieldStepStatus = fieldStep?.status
  const mayViewRawAssessment = role === Roles.FieldOfficer || role === Roles.AgriculturalOfficer || role === Roles.Admin
  const isFieldOfficer = role === Roles.FieldOfficer
  const hasFieldStep = Boolean(fieldStep)
  const [context, setContext] = useState<PrePlantingContext | null>(null)
  const [assessment, setAssessment] = useState<PrePlantingAssessment | null>(null)
  const [form, setForm] = useState<PrePlantingAssessmentInput>(() => ({ ...emptyAssessment }))
  const [riskState, setRiskState] = useState<RiskAssessmentState>('unassessed')
  const [selectedRisks, setSelectedRisks] = useState<PrePlantingRisk[]>([])
  const [result, setResult] = useState<FieldAnalysisResult | null>(null)
  const [files, setFiles] = useState<File[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [action, setAction] = useState<ActionState>(null)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const formRef = useRef<HTMLFormElement>(null)

  const load = useCallback(async () => {
    if (!mayViewRawAssessment || !requestId || !hasFieldStep) {
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError('')
    try {
      const resultRequest = fieldStepStatus === 3 || fieldStepStatus === 4
        ? api.get<FieldAnalysisResult>('/crop-plans/' + requestId + '/field-analysis-result')
        : Promise.resolve(null)
      const [contextResponse, assessmentResponse, resultResponse] = await Promise.all([
        api.get<PrePlantingContext>('/crop-plans/' + requestId + '/pre-planting-context'),
        api.get<PrePlantingAssessment | null>('/crop-plans/' + requestId + '/pre-planting-assessment'),
        resultRequest,
      ])
      setContext(contextResponse.data)
      setAssessment(assessmentResponse.data)
      setResult(resultResponse?.data ?? null)
      if (assessmentResponse.data) applySavedAssessment(assessmentResponse.data, setForm, setRiskState, setSelectedRisks)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }, [fieldStepStatus, hasFieldStep, mayViewRawAssessment, requestId])

  useEffect(() => {
    void load()
  }, [load])

  if (!mayViewRawAssessment || !requestId || !hasFieldStep) return null
  if (isLoading) return <LoadingState label="Loading pre-planting assessment..." />

  const waitingForFieldAnalysis = review.workflow.currentStep === 'CropFieldAnalysisAgent'
  const fieldAnalysisRunning = fieldStepStatus === 2
  const fieldAnalysisFailed = fieldStepStatus === 4
  const canEdit = isFieldOfficer && waitingForFieldAnalysis && (assessment === null || assessment.status === 2)
  const canRun = isFieldOfficer
    && assessment?.status === 3
    && waitingForFieldAnalysis
    && (fieldStepStatus === 1 || fieldAnalysisFailed)
  const busy = action !== null

  function updateField<K extends keyof PrePlantingAssessmentInput>(name: K, value: PrePlantingAssessmentInput[K]) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  function updateText(name: keyof PrePlantingAssessmentInput, value: string) {
    updateField(name, (value === '' ? null : value) as never)
  }

  function requestBody(): PrePlantingAssessmentInput {
    return {
      ...form,
      soilNotes: cleanText(form.soilNotes),
      mainWaterSource: cleanText(form.mainWaterSource),
      waterConcerns: cleanText(form.waterConcerns),
      drainageNotes: cleanText(form.drainageNotes),
      generalFieldNotes: cleanText(form.generalFieldNotes),
      riskNotes: cleanText(form.riskNotes),
      risksAndConcerns: null,
      officerNotes: cleanText(form.officerNotes),
      identifiedRisks: risksForRequest(riskState, selectedRisks),
    }
  }

  async function saveAssessment() {
    const response = await api.put<PrePlantingAssessment>(
      '/crop-plans/' + requestId + '/pre-planting-assessment',
      requestBody(),
    )
    setAssessment(response.data)
    return response.data
  }

  async function uploadSelectedFiles(inspectionId: string) {
    await Promise.all(files.map((file) => {
      const payload = new FormData()
      payload.append('file', file)
      return api.post('/inspections/' + inspectionId + '/images', payload, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
    }))
    setFiles([])
  }

  async function saveDraft() {
    setAction('save')
    setError('')
    setSuccess('')
    try {
      await saveAssessment()
      setSuccess('Pre-planting assessment draft saved.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  async function uploadEvidence() {
    if (!assessment || files.length === 0) return
    setAction('upload')
    setError('')
    setSuccess('')
    try {
      await uploadSelectedFiles(assessment.inspectionId)
      setSuccess('Assessment evidence uploaded.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  async function submitAssessment() {
    if (!formRef.current?.reportValidity()) return
    if (riskState === 'unassessed') {
      setError('Assess structured risks before submitting, even when none are identified.')
      return
    }
    if (riskState === 'selected' && selectedRisks.length === 0) {
      setError('Select at least one structured risk or choose None identified.')
      return
    }

    setAction('submit')
    setError('')
    setSuccess('')
    try {
      const saved = await saveAssessment()
      if (files.length > 0) await uploadSelectedFiles(saved.inspectionId)
      const response = await api.post<PrePlantingAssessment>(
        '/crop-plans/' + requestId + '/pre-planting-assessment/submit',
      )
      setAssessment(response.data)
      setSuccess('Pre-planting assessment submitted. Field analysis is now available.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  async function runFieldAnalysis() {
    setAction('run')
    setError('')
    setSuccess('')
    try {
      await api.post('/crop-plans/' + requestId + '/run-field-analysis')
      setSuccess(fieldAnalysisFailed ? 'Field analysis retry requested.' : 'Field analysis requested.')
      await onWorkflowChanged()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  return (
    <section className="preplant-panel" aria-labelledby="preplant-title">
      <header className="preplant-header">
        <div>
          <p className="preplant-eyebrow">Field Officer stage</p>
          <h2 id="preplant-title">Pre-planting field assessment</h2>
          <span>Capture structured field evidence before CropFieldAnalysisAgent evaluates this crop plan.</span>
        </div>
        <StatusPill
          label={assessment ? assessmentStatus[assessment.status] ?? 'Saved' : 'Not started'}
          tone={assessment?.status === 3 ? 'good' : 'info'}
        />
      </header>

      {context ? <ContextSummary context={context} /> : null}
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}
      {fieldAnalysisRunning ? <Notice tone="info">Field analysis is running. The submitted assessment remains read-only.</Notice> : null}

      {canEdit ? (
        <form ref={formRef} className="preplant-form" onSubmit={(event) => event.preventDefault()}>
          <AssessmentSection title="Soil profile" description="Record present soil properties, not crop symptoms.">
            <SelectInput label="Soil type" value={form.soilType ?? ''} options={soilTypeOptions} required disabled={busy} onChange={(value) => updateField('soilType', (value || null) as PrePlantingSoilType | null)} />
            <SelectInput label="Soil condition" value={form.soilCondition ?? ''} options={soilConditionOptions} required disabled={busy} onChange={(value) => updateField('soilCondition', (value || null) as PrePlantingSoilCondition | null)} />
            <SelectInput label="Soil moisture" value={form.soilMoisture ?? ''} options={soilMoistureOptions} required disabled={busy} onChange={(value) => updateField('soilMoisture', (value || null) as PrePlantingSoilMoisture | null)} />
            <TextAreaInput label="Soil notes" value={form.soilNotes ?? ''} required={form.soilType === 'Other' || form.soilCondition === 'Other'} disabled={busy} rows={3} onChange={(value) => updateText('soilNotes', value)} />
          </AssessmentSection>

          <AssessmentSection title="Water and irrigation" description="These are Field Officer observations and become read-only context for Member 3.">
            <SelectInput label="Water availability" value={form.waterAvailability ?? ''} options={waterAvailabilityOptions} required disabled={busy} onChange={(value) => updateField('waterAvailability', (value || null) as PrePlantingWaterAvailability | null)} />
            <TextInput label="Main water source" value={form.mainWaterSource ?? ''} required={requiresWaterSource(form.waterAvailability)} disabled={busy} onChange={(value) => updateText('mainWaterSource', value)} />
            <SelectInput label="Irrigation availability" value={form.irrigationAvailability ?? ''} options={irrigationOptions} required disabled={busy} onChange={(value) => updateField('irrigationAvailability', (value || null) as PrePlantingIrrigationAvailability | null)} />
            <SelectInput label="Water reliability" value={form.waterReliability ?? ''} options={waterReliabilityOptions} required disabled={busy} onChange={(value) => updateField('waterReliability', (value || null) as PrePlantingWaterReliability | null)} />
            <TextAreaInput label="Water concerns" value={form.waterConcerns ?? ''} required={requiresWaterConcern(form.waterAvailability)} disabled={busy} rows={3} onChange={(value) => updateText('waterConcerns', value)} />
          </AssessmentSection>

          <AssessmentSection title="Drainage and field readiness" description="Keep drainage quality and waterlogging risk as separate observations.">
            <SelectInput label="Drainage condition" value={form.drainageCondition ?? ''} options={drainageOptions} required disabled={busy} onChange={(value) => updateField('drainageCondition', (value || null) as PrePlantingDrainageCondition | null)} />
            <SelectInput label="Waterlogging risk" value={form.waterloggingRisk ?? ''} options={waterloggingOptions} required disabled={busy} onChange={(value) => updateField('waterloggingRisk', (value || null) as PrePlantingWaterloggingRisk | null)} />
            <TextAreaInput label="Drainage notes" value={form.drainageNotes ?? ''} required={form.drainageCondition === 'Poor' || form.waterloggingRisk === 'Moderate' || form.waterloggingRisk === 'High'} disabled={busy} rows={3} onChange={(value) => updateText('drainageNotes', value)} />
            <SelectInput label="General field condition" value={form.generalFieldCondition ?? ''} options={fieldConditionOptions} required disabled={busy} onChange={(value) => updateField('generalFieldCondition', (value || null) as PrePlantingGeneralFieldCondition | null)} />
            <TextAreaInput label="General field notes" value={form.generalFieldNotes ?? ''} required={form.generalFieldCondition === 'Other'} disabled={busy} rows={3} onChange={(value) => updateText('generalFieldNotes', value)} />
            <SelectInput label="Planting readiness" value={form.plantingReadiness ?? ''} options={readinessOptions} required disabled={busy} onChange={(value) => updateField('plantingReadiness', (value || null) as PrePlantingPlantingReadiness | null)} />
          </AssessmentSection>

          <AssessmentSection title="Structured risks" description="Not assessed is different from an explicit finding of no risk.">
            <SelectInput
              label="Risk assessment"
              value={riskState}
              options={[
                { value: 'unassessed', label: 'Not assessed' },
                { value: 'none', label: 'None identified' },
                { value: 'selected', label: 'Risks identified' },
              ]}
              disabled={busy}
              onChange={(value) => {
                const next = value as RiskAssessmentState
                setRiskState(next)
                if (next !== 'selected') setSelectedRisks([])
              }}
            />
            {riskState === 'selected' ? (
              <fieldset className="preplant-risk-list">
                <legend>Identified risks</legend>
                {riskOptions.map((risk) => (
                  <label key={risk}>
                    <input
                      type="checkbox"
                      checked={selectedRisks.includes(risk)}
                      disabled={busy}
                      onChange={(event) => setSelectedRisks((current) => event.target.checked
                        ? [...new Set([...current, risk])]
                        : current.filter((item) => item !== risk))}
                    />
                    <span>{humanize(risk)}</span>
                  </label>
                ))}
              </fieldset>
            ) : null}
            <TextAreaInput label="Risk notes" value={form.riskNotes ?? ''} required={selectedRisks.includes('Other')} disabled={busy} rows={3} onChange={(value) => updateText('riskNotes', value)} />
            <TextAreaInput label="Officer notes" value={form.officerNotes ?? ''} disabled={busy} rows={3} onChange={(value) => updateText('officerNotes', value)} />
          </AssessmentSection>

          <label className="preplant-evidence">
            <span><Camera size={17} aria-hidden="true" /> Photos / evidence</span>
            <input type="file" accept="image/*" multiple disabled={busy} onChange={(event) => setFiles(Array.from(event.target.files ?? []))} />
            <small>{files.length === 0 ? 'Optional. Images are metadata-only evidence for AI and retained for staff review.' : files.length + ' image(s) selected.'}</small>
          </label>

          <div className="preplant-actions">
            <Button type="button" variant="secondary" icon={<Save size={16} aria-hidden="true" />} disabled={busy} onClick={() => void saveDraft()}>
              {action === 'save' ? 'Saving...' : 'Save draft'}
            </Button>
            <Button type="button" variant="secondary" icon={<Upload size={16} aria-hidden="true" />} disabled={busy || !assessment || files.length === 0} onClick={() => void uploadEvidence()}>
              {action === 'upload' ? 'Uploading...' : 'Upload evidence'}
            </Button>
            <Button type="button" icon={<Send size={16} aria-hidden="true" />} disabled={busy} onClick={() => void submitAssessment()}>
              {action === 'submit' ? 'Submitting...' : 'Submit assessment'}
            </Button>
          </div>
        </form>
      ) : assessment ? <AssessmentReadOnly assessment={assessment} /> : (
        <Notice tone="info">Waiting for the owning Field Officer to save the linked pre-planting assessment.</Notice>
      )}

      {assessment?.images.length ? (
        <div className="preplant-images" aria-label="Assessment evidence">
          {assessment.images.map((image) => <a key={image.id} href={image.url} target="_blank" rel="noreferrer">View evidence</a>)}
        </div>
      ) : null}

      {canRun ? (
        <div className="preplant-actions">
          <Button icon={<Sparkles size={16} aria-hidden="true" />} disabled={busy} onClick={() => void runFieldAnalysis()}>
            {action === 'run' ? 'Requesting...' : fieldAnalysisFailed ? 'Retry field analysis' : 'Run field analysis'}
          </Button>
        </div>
      ) : null}

      {result ? <FieldAnalysisResultPanel result={result} /> : null}
    </section>
  )
}

function ContextSummary({ context }: { context: PrePlantingContext }) {
  return (
    <section className="preplant-context" aria-label="Crop plan context">
      <div><span>Farmer</span><strong>{context.farmerName}</strong></div>
      <div><span>Farm</span><strong>{context.farmName}</strong><small>{context.farmLocation}</small></div>
      <div><span>Field</span><strong>{context.fieldName}</strong></div>
      <div><span>Crop</span><strong>{context.cropName}{context.cropVarietyName ? ' · ' + context.cropVarietyName : ''}</strong></div>
      <div><span>Season</span><strong>{seasonLabels[context.cultivationSeason] ?? 'Unknown'}</strong></div>
      <div><span>Preferred dates</span><strong>{context.preferredStartDate} → {context.preferredEndDate}</strong></div>
    </section>
  )
}

function AssessmentSection({ title, description, children }: { title: string; description: string; children: React.ReactNode }) {
  return (
    <fieldset className="preplant-form-section">
      <legend>{title}</legend>
      <p>{description}</p>
      <div className="preplant-form-grid">{children}</div>
    </fieldset>
  )
}

function AssessmentReadOnly({ assessment }: { assessment: PrePlantingAssessment }) {
  return (
    <dl className="preplant-readonly">
      <AssessmentItem label="Soil type" value={assessment.soilType} />
      <AssessmentItem label="Soil condition" value={assessment.soilCondition} />
      <AssessmentItem label="Soil moisture" value={assessment.soilMoisture} />
      <AssessmentItem label="Soil notes" value={assessment.soilNotes} />
      <AssessmentItem label="Water availability" value={assessment.waterAvailability} />
      <AssessmentItem label="Main water source" value={assessment.mainWaterSource} />
      <AssessmentItem label="Irrigation availability" value={assessment.irrigationAvailability} />
      <AssessmentItem label="Water reliability" value={assessment.waterReliability} />
      <AssessmentItem label="Water concerns" value={assessment.waterConcerns} />
      <AssessmentItem label="Drainage condition" value={assessment.drainageCondition} />
      <AssessmentItem label="Waterlogging risk" value={assessment.waterloggingRisk} />
      <AssessmentItem label="Drainage notes" value={assessment.drainageNotes} />
      <AssessmentItem label="General field condition" value={assessment.generalFieldCondition} />
      <AssessmentItem label="General field notes" value={assessment.generalFieldNotes} />
      <AssessmentItem label="Planting readiness" value={assessment.plantingReadiness} />
      <AssessmentItem label="Identified risks" value={assessment.identifiedRisks} />
      <AssessmentItem label="Risk notes" value={assessment.riskNotes ?? assessment.risksAndConcerns} />
      <AssessmentItem label="Officer notes" value={assessment.officerNotes} />
    </dl>
  )
}

function FieldAnalysisResultPanel({ result }: { result: FieldAnalysisResult }) {
  return (
    <section className="preplant-result" aria-labelledby="field-result-title">
      <div className="preplant-result-heading">
        <CheckCircle2 size={19} aria-hidden="true" />
        <h3 id="field-result-title">Field analysis result</h3>
        <StatusPill label={result.status} tone={result.status === 'Analyzed' ? 'good' : 'bad'} />
      </div>
      <p>{result.fieldCondition.summary || 'No field-condition summary was returned.'}</p>
      <dl>
        <AssessmentItem label="Priority" value={result.priority} />
        <AssessmentItem label="Field suitability" value={result.fieldSuitability} />
        <AssessmentItem label="Planting readiness" value={result.plantingReadiness} />
        <AssessmentItem label="Human review" value={result.requiresHumanReview ? 'Required' : 'Not required'} />
        <AssessmentItem label="Soil assessment" value={result.soilAssessment} />
        <AssessmentItem label="Water assessment" value={result.waterAssessment} />
        <AssessmentItem label="Drainage assessment" value={result.drainageAssessment} />
        <AssessmentItem label="Identified risks" value={result.identifiedRisks} />
      </dl>
      <ResultList title="Field preparation requirements" items={result.fieldPreparationRequirements} />
      <ResultList title="Recommended pre-planting actions" items={result.recommendedPrePlantingActions} />
      {result.warnings.map((warning) => <Notice key={warning} tone="warning">{warning}</Notice>)}
    </section>
  )
}

function ResultList({ title, items }: { title: string; items?: string[] }) {
  if (!items) return null
  return (
    <div className="preplant-result-list">
      <h4>{title}</h4>
      {items.length === 0 ? <p>None recorded.</p> : <ul>{items.map((item) => <li key={item}>{item}</li>)}</ul>}
    </div>
  )
}

function AssessmentItem({ label, value }: { label: string; value?: string | string[] | null }) {
  const display = Array.isArray(value)
    ? value.length === 0 ? 'None identified' : value.map(humanize).join(', ')
    : value ?? 'Not recorded'
  return <div><dt>{label}</dt><dd>{display}</dd></div>
}

function applySavedAssessment(
  assessment: PrePlantingAssessment,
  setForm: (value: PrePlantingAssessmentInput) => void,
  setRiskState: (value: RiskAssessmentState) => void,
  setSelectedRisks: (value: PrePlantingRisk[]) => void,
) {
  setForm({
    soilType: assessment.soilType,
    soilCondition: assessment.soilCondition,
    soilMoisture: assessment.soilMoisture,
    soilNotes: assessment.soilNotes,
    waterAvailability: assessment.waterAvailability,
    mainWaterSource: assessment.mainWaterSource,
    irrigationAvailability: assessment.irrigationAvailability,
    waterReliability: assessment.waterReliability,
    waterConcerns: assessment.waterConcerns,
    drainageCondition: assessment.drainageCondition,
    waterloggingRisk: assessment.waterloggingRisk,
    drainageNotes: assessment.drainageNotes,
    generalFieldCondition: assessment.generalFieldCondition,
    generalFieldNotes: assessment.generalFieldNotes,
    plantingReadiness: assessment.plantingReadiness,
    identifiedRisks: assessment.identifiedRisks,
    riskNotes: assessment.riskNotes ?? assessment.risksAndConcerns,
    risksAndConcerns: assessment.risksAndConcerns,
    officerNotes: assessment.officerNotes,
  })
  if (assessment.identifiedRisks === null) {
    setRiskState('unassessed')
    setSelectedRisks([])
  } else if (assessment.identifiedRisks.length === 0) {
    setRiskState('none')
    setSelectedRisks([])
  } else {
    setRiskState('selected')
    setSelectedRisks([...new Set(assessment.identifiedRisks)])
  }
}

function risksForRequest(state: RiskAssessmentState, selected: PrePlantingRisk[]): PrePlantingRisk[] | null {
  if (state === 'unassessed') return null
  if (state === 'none') return []
  return [...new Set(selected)]
}

function cleanText(value: string | null): string | null {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

function requiresWaterSource(value: PrePlantingWaterAvailability | null) {
  return value === 'Adequate' || value === 'Limited' || value === 'Seasonal'
}

function requiresWaterConcern(value: PrePlantingWaterAvailability | null) {
  return value === 'Limited' || value === 'Unavailable' || value === 'Seasonal'
}

function options<T extends string>(values: readonly T[]) {
  return values.map((value) => ({ value, label: humanize(value) }))
}

function humanize(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}
