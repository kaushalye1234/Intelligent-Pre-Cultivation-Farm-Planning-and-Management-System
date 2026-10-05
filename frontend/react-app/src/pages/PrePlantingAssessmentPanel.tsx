import { useCallback, useEffect, useRef, useState } from 'react'
import axios from 'axios'
import { BrainCircuit, Camera, CheckCircle2, ChevronDown, Image as ImageIcon, Save, Send, Sparkles, Upload, X } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, Notice } from '../components/Ui'
import { Roles } from '../routing'
import type {
  ApplicationRole,
  CropHealthActionType,
  FieldAnalysisResult,
  InspectionImageAnalysisAuditItem,
  InspectionImageAnalysisResult,
  InspectionImageAnalysisState,
  InspectionNoteAssistanceResponse,
  InspectionNoteSuggestions,
  PrePlantingAssessment,
  PrePlantingAssessmentImage,
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
type AssessmentSectionKey = 'soil' | 'water' | 'readiness' | 'risks'
type ActionState = 'save' | 'upload' | 'submit' | 'run' | 'notes' | 'select-image' | 'analyze-image' | 'review-image' | null
type NoteFieldKey = keyof InspectionNoteSuggestions
type NoteDecision = 'Accepted' | 'Officer Edited' | 'Rejected'

const noteFieldToFormField: Record<NoteFieldKey, keyof PrePlantingAssessmentInput> = {
  soilNotes: 'soilNotes',
  waterConcerns: 'waterConcerns',
  drainageNotes: 'drainageNotes',
  generalFieldNotes: 'generalFieldNotes',
  riskNotes: 'riskNotes',
  officerNotes: 'officerNotes',
}

const cropHealthActions: CropHealthActionType[] = [
  'FieldSanitation',
  'RemoveAffectedResidue',
  'SeparateAffectedMaterial',
  'InspectNearbyPlants',
  'MonitorSymptoms',
  'PrePlantingCleanup',
  'RequestFurtherAssessment',
]

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

const soilTypeValues: PrePlantingSoilType[] = ['Sandy', 'Clay', 'Loamy', 'Silty', 'Mixed', 'Unknown', 'Other']
const soilConditionValues: PrePlantingSoilCondition[] = ['Good', 'Moderate', 'Poor', 'Compacted', 'Eroded', 'Unknown', 'Other']
const soilMoistureValues: PrePlantingSoilMoisture[] = ['Dry', 'Moist', 'Wet', 'Waterlogged', 'Unknown']
const waterAvailabilityValues: PrePlantingWaterAvailability[] = ['Adequate', 'Limited', 'Unavailable', 'Seasonal', 'Unknown']
const irrigationValues: PrePlantingIrrigationAvailability[] = ['Available', 'Limited', 'Unavailable', 'NotRequired', 'Unknown']
const waterReliabilityValues: PrePlantingWaterReliability[] = ['Reliable', 'Intermittent', 'Seasonal', 'Unreliable', 'Unknown']
const drainageValues: PrePlantingDrainageCondition[] = ['Good', 'Moderate', 'Poor', 'Unknown']
const waterloggingValues: PrePlantingWaterloggingRisk[] = ['NoneObserved', 'Low', 'Moderate', 'High', 'Unknown']
const fieldConditionValues: PrePlantingGeneralFieldCondition[] = [
  'ClearAndPrepared',
  'RequiresLandPreparation',
  'UnevenField',
  'Waterlogged',
  'TooDry',
  'ErosionPresent',
  'AccessLimitation',
  'Other',
]
const readinessValues: PrePlantingPlantingReadiness[] = [
  'Ready',
  'ReadyWithMinorPreparation',
  'RequiresPreparation',
  'NotReady',
  'RequiresFurtherAssessment',
]
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

const soilTypeOptions = options(soilTypeValues)
const soilConditionOptions = options(soilConditionValues)
const soilMoistureOptions = options(soilMoistureValues)
const waterAvailabilityOptions = options(waterAvailabilityValues)
const irrigationOptions = options(irrigationValues)
const waterReliabilityOptions = options(waterReliabilityValues)
const drainageOptions = options(drainageValues)
const waterloggingOptions = options(waterloggingValues)
const fieldConditionOptions = options(fieldConditionValues)
const readinessOptions = options(readinessValues)

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
  const [noteAssistance, setNoteAssistance] = useState<InspectionNoteAssistanceResponse | null>(null)
  const [noteDecisions, setNoteDecisions] = useState<Partial<Record<NoteFieldKey, NoteDecision>>>({})
  const [imageAnalysis, setImageAnalysis] = useState<InspectionImageAnalysisState | null>(null)
  const [imageEdit, setImageEdit] = useState<InspectionImageAnalysisResult | null>(null)
  const [imageHistory, setImageHistory] = useState<InspectionImageAnalysisAuditItem[] | null>(null)
  const [files, setFiles] = useState<File[]>([])
  const [openSections, setOpenSections] = useState<Set<AssessmentSectionKey>>(() => new Set(['soil']))
  const [isLoading, setIsLoading] = useState(true)
  const [assessmentLoadFailed, setAssessmentLoadFailed] = useState(false)
  const [action, setAction] = useState<ActionState>(null)
  const [error, setError] = useState('')
  const [warning, setWarning] = useState('')
  const [success, setSuccess] = useState('')
  const formRef = useRef<HTMLFormElement>(null)

  const loadImageAnalysis = useCallback(async () => {
    if (!requestId) return
    try {
      const response = await api.get<InspectionImageAnalysisState>(`/crop-plans/${requestId}/pre-planting-assessment/image-analysis`)
      setImageAnalysis(response.data)
      setImageEdit(response.data.result)
    } catch (err) {
      if (!isNotFound(err)) setWarning(getErrorMessage(err))
      setImageAnalysis(null)
      setImageEdit(null)
    }
  }, [requestId])

  const load = useCallback(async () => {
    if (!mayViewRawAssessment || !requestId || !hasFieldStep) {
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError('')
    setWarning('')
    setAssessmentLoadFailed(false)
    const resultRequest = fieldStepStatus === 3 || fieldStepStatus === 4
      ? api.get<unknown>('/crop-plans/' + requestId + '/field-analysis-result')
      : Promise.resolve(null)
    const [contextOutcome, assessmentOutcome, resultOutcome] = await Promise.allSettled([
      api.get<PrePlantingContext>('/crop-plans/' + requestId + '/pre-planting-context'),
      api.get<unknown>('/crop-plans/' + requestId + '/pre-planting-assessment'),
      resultRequest,
    ])
    const errors: string[] = []

    if (contextOutcome.status === 'fulfilled') {
      setContext(contextOutcome.value.data)
    } else {
      setContext(null)
      errors.push(getErrorMessage(contextOutcome.reason))
    }

    if (assessmentOutcome.status === 'fulfilled') {
      try {
        const nextAssessment = normalizeAssessmentResponse(assessmentOutcome.value, requestId)
        setAssessment(nextAssessment)
        if (nextAssessment) {
          applySavedAssessment(nextAssessment, setForm, setRiskState, setSelectedRisks)
          const nextRiskState = riskStateFromAssessment(nextAssessment)
          setOpenSections(initialOpenSections(nextAssessment, nextRiskState, nextAssessment.identifiedRisks ?? []))
          await loadImageAnalysis()
        } else {
          resetAssessmentForm(setForm, setRiskState, setSelectedRisks)
          setOpenSections(new Set(['soil']))
          setImageAnalysis(null)
        }
      } catch (err) {
        setAssessment(null)
        setAssessmentLoadFailed(true)
        errors.push(getErrorMessage(err))
      }
    } else if (isNotFound(assessmentOutcome.reason)) {
      setAssessment(null)
      resetAssessmentForm(setForm, setRiskState, setSelectedRisks)
      setOpenSections(new Set(['soil']))
    } else {
      setAssessment(null)
      setAssessmentLoadFailed(true)
      errors.push(getErrorMessage(assessmentOutcome.reason))
    }

    if (resultOutcome.status === 'fulfilled') {
      try {
        setResult(resultOutcome.value ? normalizeFieldAnalysisResult(resultOutcome.value.data) : null)
      } catch (err) {
        setResult(null)
        errors.push(getErrorMessage(err))
      }
    } else {
      setResult(null)
      errors.push(getErrorMessage(resultOutcome.reason))
    }

    setError([...new Set(errors)].join(' '))
    setIsLoading(false)
  }, [fieldStepStatus, hasFieldStep, loadImageAnalysis, mayViewRawAssessment, requestId])

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
  const submissionAiWarning = imageAnalysis?.status === 'Running'
    || (imageAnalysis?.status === 'Succeeded' && imageAnalysis.effectiveReview == null)
  const sectionCompletion = assessmentSectionCompletion(form, riskState, selectedRisks)
  const completedSectionCount = Object.values(sectionCompletion).filter(Boolean).length

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

  function toggleAssessmentSection(section: AssessmentSectionKey) {
    setOpenSections((current) => {
      const next = new Set(current)
      if (next.has(section)) next.delete(section)
      else next.add(section)
      return next
    })
  }

  function revealAssessmentSection(section: AssessmentSectionKey, focusTrigger = true) {
    setOpenSections((current) => new Set([...current, section]))
    if (focusTrigger) {
      window.requestAnimationFrame(() => {
        document.getElementById(`assessment-trigger-${section}`)?.focus()
      })
    }
  }

  async function generateNoteSuggestions() {
    setAction('notes')
    setError('')
    setWarning('')
    try {
      const draft = requestBody()
      const response = await api.post<InspectionNoteAssistanceResponse>(
        `/crop-plans/${requestId}/pre-planting-assessment/note-suggestions`,
        { ...draft, identifiedRisks: draft.identifiedRisks ?? [] },
      )
      setNoteAssistance(response.data)
      setNoteDecisions({})
      const suggestedSections = sectionsWithNoteSuggestions(response.data.suggestions)
      if (suggestedSections.length > 0) {
        setOpenSections((current) => new Set([...current, ...suggestedSections]))
      }
      if (response.data.status !== 'Available') {
        setWarning('AI note assistance is currently unavailable. You can continue entering the inspection manually.')
      }
    } catch (err) {
      setWarning('AI note assistance is currently unavailable. You can continue entering the inspection manually. ' + getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  function applyNoteSuggestion(field: NoteFieldKey, decision: Exclude<NoteDecision, 'Rejected'>) {
    const suggestion = noteAssistance?.suggestions?.[field]
    if (!suggestion) return
    updateField(noteFieldToFormField[field], suggestion as never)
    setNoteDecisions((current) => ({ ...current, [field]: decision }))
  }

  function rejectNoteSuggestion(field: NoteFieldKey) {
    setNoteDecisions((current) => ({ ...current, [field]: 'Rejected' }))
  }

  async function refreshAssessmentImages() {
    if (!requestId) return
    const response = await api.get<unknown>(`/crop-plans/${requestId}/pre-planting-assessment`)
    const refreshed = requireAssessment(normalizeLinkedAssessment(response.data, requestId))
    setAssessment(refreshed)
  }

  async function selectRepresentativeImage(imageId: string) {
    setAction('select-image')
    setError('')
    setWarning('')
    try {
      await api.put(`/crop-plans/${requestId}/pre-planting-assessment/representative-image/${imageId}`)
      await refreshAssessmentImages()
      await loadImageAnalysis()
      setSuccess('Representative AI-analysis image selected. Click Analyze Image when you are ready.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  async function analyzeRepresentativeImage() {
    setAction('analyze-image')
    setError('')
    setWarning('')
    try {
      const response = await api.post<InspectionImageAnalysisState>(`/crop-plans/${requestId}/pre-planting-assessment/image-analysis`)
      setImageAnalysis(response.data)
      setImageEdit(response.data.result)
      setSuccess(response.data.status === 'Succeeded'
        ? 'Image analysis is ready for Field Officer review.'
        : response.data.message ?? 'Image analysis status updated.')
    } catch (err) {
      setWarning('AI image analysis could not be completed. The manual inspection remains available. ' + getErrorMessage(err))
      await loadImageAnalysis()
    } finally {
      setAction(null)
    }
  }

  async function loadImageAnalysisHistory() {
    setAction('review-image')
    setError('')
    try {
      const response = await api.get<InspectionImageAnalysisAuditItem[]>(
        `/crop-plans/${requestId}/pre-planting-assessment/image-analysis/history`,
      )
      setImageHistory(response.data)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  async function reviewImageAnalysis(disposition: 'Accepted' | 'Edited' | 'Rejected') {
    if (disposition === 'Edited' && !imageEdit) return
    setAction('review-image')
    setError('')
    try {
      await api.post(`/crop-plans/${requestId}/pre-planting-assessment/image-analysis/review`, {
        disposition,
        editedProjection: disposition === 'Edited' ? {
          visibleFindings: normalizedLines(imageEdit!.visibleFindings),
          possibleConcerns: normalizedLines(imageEdit!.possibleIssues),
          severity: imageEdit!.severity,
          uncertainty: imageEdit!.uncertainty,
          actions: imageEdit!.recommendedNonChemicalActions,
          requiresFurtherAssessment: imageEdit!.requiresFurtherAssessment,
        } : null,
        staffNote: null,
      })
      await loadImageAnalysis()
      setSuccess(disposition === 'Rejected' ? 'Image analysis rejected and excluded from downstream planning.' :
        disposition === 'Edited' ? 'Officer Edited review recorded.' : 'Image analysis accepted for submission-time eligibility.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  async function saveAssessment() {
    const linkedRequestId = requestId
    if (!linkedRequestId) throw new Error('This workflow is not linked to a crop plan request.')
    const response = await api.put<unknown>(
      '/crop-plans/' + linkedRequestId + '/pre-planting-assessment',
      requestBody(),
    )
    const saved = requireAssessment(normalizeLinkedAssessment(response.data, linkedRequestId))
    setAssessment(saved)
    return saved
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
    setWarning('')
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
    setWarning('')
    setSuccess('')
    try {
      await uploadSelectedFiles(assessment.inspectionId)
      await refreshAssessmentImages()
      setSuccess('Assessment evidence uploaded.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAction(null)
    }
  }

  async function submitAssessment() {
    const linkedRequestId = requestId
    if (!linkedRequestId) {
      setError('This workflow is not linked to a crop plan request.')
      return
    }
    const invalidControl = formRef.current?.querySelector<HTMLElement>('input:invalid, select:invalid, textarea:invalid')
    if (invalidControl) {
      const section = invalidControl.closest<HTMLElement>('[data-assessment-section]')?.dataset.assessmentSection as AssessmentSectionKey | undefined
      if (section) revealAssessmentSection(section, false)
      window.requestAnimationFrame(() => {
        invalidControl.focus()
        formRef.current?.reportValidity()
      })
      return
    }
    if (riskState === 'unassessed') {
      setError('Assess structured risks before submitting, even when none are identified.')
      revealAssessmentSection('risks')
      return
    }
    if (riskState === 'selected' && selectedRisks.length === 0) {
      setError('Select at least one structured risk or choose None identified.')
      revealAssessmentSection('risks')
      return
    }

    setAction('submit')
    setError('')
    setWarning('')
    setSuccess('')
    try {
      const saved = await saveAssessment()
      let optionalEvidenceError = ''
      if (files.length > 0) {
        try {
          await uploadSelectedFiles(saved.inspectionId)
        } catch (err) {
          optionalEvidenceError = getErrorMessage(err)
        }
      }
      const response = await api.post<unknown>(
        '/crop-plans/' + linkedRequestId + '/pre-planting-assessment/submit',
      )
      setAssessment(requireAssessment(normalizeLinkedAssessment(response.data, linkedRequestId)))
      setFiles([])
      setSuccess('Pre-planting assessment submitted. Field analysis is now available.')
      if (optionalEvidenceError) {
        setWarning('Optional evidence upload failed: ' + optionalEvidenceError + ' The assessment was submitted without that evidence.')
      }
    } catch (err) {
      const message = getErrorMessage(err)
      setError(message)
      const section = sectionFromValidationMessage(message)
      if (section) revealAssessmentSection(section)
    } finally {
      setAction(null)
    }
  }

  async function runFieldAnalysis() {
    setAction('run')
    setError('')
    setWarning('')
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
      {warning ? <Notice tone="warning">{warning}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}
      {fieldAnalysisRunning ? <Notice tone="info">Field analysis is running. The submitted assessment remains read-only.</Notice> : null}

      {assessmentLoadFailed ? null : canEdit ? (
        <form ref={formRef} className="preplant-form" onSubmit={(event) => event.preventDefault()}>
          <section className="ai-assistance-panel" aria-label="Inspection note assistant">
            <div>
              <p className="preplant-eyebrow">Optional AI assistance</p>
              <h3>Draft narrative notes from the observations already entered</h3>
              <p>Suggestions remain local until you place them in a note field and use the normal Save draft or Submit action.</p>
            </div>
            <Button type="button" variant="secondary" icon={<BrainCircuit size={16} aria-hidden="true" />} disabled={busy} onClick={() => void generateNoteSuggestions()}>
              {action === 'notes' ? 'Generating...' : noteAssistance ? 'Regenerate suggestions' : 'Suggest notes'}
            </Button>
          </section>
          {(noteAssistance?.contradictionWarnings ?? []).map((item) => <Notice key={`contradiction-${item}`} tone="warning">Contradiction to verify: {item}</Notice>)}
          {(noteAssistance?.missingDataWarnings ?? []).map((item) => <Notice key={`missing-${item}`} tone="info">Missing data: {item}</Notice>)}
          <section className="assessment-progress" aria-label="Assessment completion">
            <div>
              <strong>{completedSectionCount} of 4 sections complete</strong>
              <span>Complete each observation group before submitting.</span>
            </div>
            <div className="assessment-progress-track" aria-hidden="true">
              <span style={{ width: `${completedSectionCount * 25}%` }} />
            </div>
          </section>
          <div className="assessment-accordion">
          <AssessmentSection sectionKey="soil" title="Soil profile" description="Record present soil properties, not crop symptoms." open={openSections.has('soil')} complete={sectionCompletion.soil} onToggle={() => toggleAssessmentSection('soil')}>
            <SelectInput label="Soil type" value={form.soilType ?? ''} options={soilTypeOptions} required disabled={busy} onChange={(value) => updateField('soilType', (value || null) as PrePlantingSoilType | null)} />
            <SelectInput label="Soil condition" value={form.soilCondition ?? ''} options={soilConditionOptions} required disabled={busy} onChange={(value) => updateField('soilCondition', (value || null) as PrePlantingSoilCondition | null)} />
            <SelectInput label="Soil moisture" value={form.soilMoisture ?? ''} options={soilMoistureOptions} required disabled={busy} onChange={(value) => updateField('soilMoisture', (value || null) as PrePlantingSoilMoisture | null)} />
            <SuggestedNoteField field="soilNotes" label="Soil notes" value={form.soilNotes ?? ''} suggestion={noteAssistance?.suggestions?.soilNotes} decision={noteDecisions.soilNotes} required={form.soilType === 'Other' || form.soilCondition === 'Other'} disabled={busy} onChange={(value) => updateText('soilNotes', value)} onAccept={() => applyNoteSuggestion('soilNotes', 'Accepted')} onEdit={() => applyNoteSuggestion('soilNotes', 'Officer Edited')} onReject={() => rejectNoteSuggestion('soilNotes')} />
          </AssessmentSection>

          <AssessmentSection sectionKey="water" title="Water and irrigation" description="These are Field Officer observations and become read-only context for Member 3." open={openSections.has('water')} complete={sectionCompletion.water} onToggle={() => toggleAssessmentSection('water')}>
            <SelectInput label="Water availability" value={form.waterAvailability ?? ''} options={waterAvailabilityOptions} required disabled={busy} onChange={(value) => updateField('waterAvailability', (value || null) as PrePlantingWaterAvailability | null)} />
            <TextInput label="Main water source" value={form.mainWaterSource ?? ''} required={requiresWaterSource(form.waterAvailability)} disabled={busy} onChange={(value) => updateText('mainWaterSource', value)} />
            <SelectInput label="Irrigation availability" value={form.irrigationAvailability ?? ''} options={irrigationOptions} required disabled={busy} onChange={(value) => updateField('irrigationAvailability', (value || null) as PrePlantingIrrigationAvailability | null)} />
            <SelectInput label="Water reliability" value={form.waterReliability ?? ''} options={waterReliabilityOptions} required disabled={busy} onChange={(value) => updateField('waterReliability', (value || null) as PrePlantingWaterReliability | null)} />
            <SuggestedNoteField field="waterConcerns" label="Water concerns" value={form.waterConcerns ?? ''} suggestion={noteAssistance?.suggestions?.waterConcerns} decision={noteDecisions.waterConcerns} required={requiresWaterConcern(form.waterAvailability)} disabled={busy} onChange={(value) => updateText('waterConcerns', value)} onAccept={() => applyNoteSuggestion('waterConcerns', 'Accepted')} onEdit={() => applyNoteSuggestion('waterConcerns', 'Officer Edited')} onReject={() => rejectNoteSuggestion('waterConcerns')} />
          </AssessmentSection>

          <AssessmentSection sectionKey="readiness" title="Drainage and field readiness" description="Keep drainage quality and waterlogging risk as separate observations." open={openSections.has('readiness')} complete={sectionCompletion.readiness} onToggle={() => toggleAssessmentSection('readiness')}>
            <SelectInput label="Drainage condition" value={form.drainageCondition ?? ''} options={drainageOptions} required disabled={busy} onChange={(value) => updateField('drainageCondition', (value || null) as PrePlantingDrainageCondition | null)} />
            <SelectInput label="Waterlogging risk" value={form.waterloggingRisk ?? ''} options={waterloggingOptions} required disabled={busy} onChange={(value) => updateField('waterloggingRisk', (value || null) as PrePlantingWaterloggingRisk | null)} />
            <SuggestedNoteField field="drainageNotes" label="Drainage notes" value={form.drainageNotes ?? ''} suggestion={noteAssistance?.suggestions?.drainageNotes} decision={noteDecisions.drainageNotes} required={form.drainageCondition === 'Poor' || form.waterloggingRisk === 'Moderate' || form.waterloggingRisk === 'High'} disabled={busy} onChange={(value) => updateText('drainageNotes', value)} onAccept={() => applyNoteSuggestion('drainageNotes', 'Accepted')} onEdit={() => applyNoteSuggestion('drainageNotes', 'Officer Edited')} onReject={() => rejectNoteSuggestion('drainageNotes')} />
            <SelectInput label="General field condition" value={form.generalFieldCondition ?? ''} options={fieldConditionOptions} required disabled={busy} onChange={(value) => updateField('generalFieldCondition', (value || null) as PrePlantingGeneralFieldCondition | null)} />
            <SuggestedNoteField field="generalFieldNotes" label="General field notes" value={form.generalFieldNotes ?? ''} suggestion={noteAssistance?.suggestions?.generalFieldNotes} decision={noteDecisions.generalFieldNotes} required={form.generalFieldCondition === 'Other'} disabled={busy} onChange={(value) => updateText('generalFieldNotes', value)} onAccept={() => applyNoteSuggestion('generalFieldNotes', 'Accepted')} onEdit={() => applyNoteSuggestion('generalFieldNotes', 'Officer Edited')} onReject={() => rejectNoteSuggestion('generalFieldNotes')} />
            <SelectInput label="Planting readiness" value={form.plantingReadiness ?? ''} options={readinessOptions} required disabled={busy} onChange={(value) => updateField('plantingReadiness', (value || null) as PrePlantingPlantingReadiness | null)} />
          </AssessmentSection>

          <AssessmentSection sectionKey="risks" title="Structured risks" description="Not assessed is different from an explicit finding of no risk." open={openSections.has('risks')} complete={sectionCompletion.risks} onToggle={() => toggleAssessmentSection('risks')}>
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
            <SuggestedNoteField field="riskNotes" label="Risk notes" value={form.riskNotes ?? ''} suggestion={noteAssistance?.suggestions?.riskNotes} decision={noteDecisions.riskNotes} required={selectedRisks.includes('Other')} disabled={busy} onChange={(value) => updateText('riskNotes', value)} onAccept={() => applyNoteSuggestion('riskNotes', 'Accepted')} onEdit={() => applyNoteSuggestion('riskNotes', 'Officer Edited')} onReject={() => rejectNoteSuggestion('riskNotes')} />
            <SuggestedNoteField field="officerNotes" label="Officer notes" value={form.officerNotes ?? ''} suggestion={noteAssistance?.suggestions?.officerNotes} decision={noteDecisions.officerNotes} disabled={busy} onChange={(value) => updateText('officerNotes', value)} onAccept={() => applyNoteSuggestion('officerNotes', 'Accepted')} onEdit={() => applyNoteSuggestion('officerNotes', 'Officer Edited')} onReject={() => rejectNoteSuggestion('officerNotes')} />
          </AssessmentSection>
          </div>

          <label className="preplant-evidence">
            <span><Camera size={17} aria-hidden="true" /> Photos / evidence</span>
            <input type="file" accept="image/*" multiple disabled={busy} onChange={(event) => setFiles(Array.from(event.target.files ?? []))} />
            <small>{files.length === 0 ? 'Optional. Images are metadata-only evidence for AI and retained for staff review.' : files.length + ' image(s) selected.'}</small>
          </label>

          {submissionAiWarning ? <Notice tone="warning">An AI image analysis is still pending or has not been reviewed. If you submit this inspection now, that analysis will not be included in downstream Field Analysis or the final plan.</Notice> : null}

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

      {assessment ? <ImageAnalysisPanel
        state={imageAnalysis}
        edit={imageEdit}
        history={imageHistory}
        images={assessment.images ?? []}
        canEdit={canEdit}
        busy={busy}
        onAnalyze={() => void analyzeRepresentativeImage()}
        onAccept={() => void reviewImageAnalysis('Accepted')}
        onEdit={() => void reviewImageAnalysis('Edited')}
        onReject={() => void reviewImageAnalysis('Rejected')}
        onSelectRepresentative={(imageId) => void selectRepresentativeImage(imageId)}
        onLoadHistory={() => void loadImageAnalysisHistory()}
        onEditChange={setImageEdit}
      /> : null}

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
      <div>
        <span>Farmer</span>
        <strong>{context.farmerName}</strong>
        <small>{context.farmerPhoneNumber ?? 'Phone not provided'}</small>
        <small>{context.farmerContactAddress ?? 'Contact address not provided'}</small>
      </div>
      <div><span>Farm</span><strong>{context.farmName}</strong><small>{[context.farmLocation, context.farmDistrict].filter(Boolean).join(' · ')}</small></div>
      <div><span>Field</span><strong>{context.fieldName}</strong></div>
      <div><span>Crop</span><strong>{context.cropName}{context.cropVarietyName ? ' · ' + context.cropVarietyName : ''}</strong></div>
      <div><span>Season</span><strong>{seasonLabels[context.cultivationSeason] ?? 'Unknown'}</strong></div>
      <div><span>Preferred dates</span><strong>{context.preferredStartDate} → {context.preferredEndDate}</strong></div>
    </section>
  )
}

function SuggestedNoteField({
  field,
  label,
  value,
  suggestion,
  decision,
  required = false,
  disabled = false,
  onChange,
  onAccept,
  onEdit,
  onReject,
}: {
  field: NoteFieldKey
  label: string
  value: string
  suggestion?: string | null
  decision?: NoteDecision
  required?: boolean
  disabled?: boolean
  onChange: (value: string) => void
  onAccept: () => void
  onEdit: () => void
  onReject: () => void
}) {
  return (
    <div className="suggested-note-field" data-note-field={field}>
      <TextAreaInput label={label} value={value} required={required} disabled={disabled} rows={3} onChange={onChange} />
      {suggestion && decision !== 'Rejected' ? (
        <aside className="ai-suggested-draft" aria-label={`${label} AI Suggested Draft`}>
          <div className="row-actions">
            <strong>AI Suggested Draft</strong>
            {decision ? <StatusPill label={decision} tone={decision === 'Officer Edited' ? 'warn' : 'good'} /> : null}
          </div>
          <p>{suggestion}</p>
          {!decision ? <div className="row-actions">
            <Button type="button" variant="secondary" onClick={onAccept} disabled={disabled}>Accept</Button>
            <Button type="button" variant="ghost" onClick={onEdit} disabled={disabled}>Edit</Button>
            <Button type="button" variant="ghost" icon={<X size={14} aria-hidden="true" />} onClick={onReject} disabled={disabled}>Reject</Button>
          </div> : null}
        </aside>
      ) : null}
    </div>
  )
}

function ImageAnalysisPanel({
  state,
  edit,
  history,
  images,
  canEdit,
  busy,
  onAnalyze,
  onAccept,
  onEdit,
  onReject,
  onSelectRepresentative,
  onLoadHistory,
  onEditChange,
}: {
  state: InspectionImageAnalysisState | null
  edit: InspectionImageAnalysisResult | null
  history: InspectionImageAnalysisAuditItem[] | null
  images: PrePlantingAssessmentImage[]
  canEdit: boolean
  busy: boolean
  onAnalyze: () => void
  onAccept: () => void
  onEdit: () => void
  onReject: () => void
  onSelectRepresentative: (imageId: string) => void
  onLoadHistory: () => void
  onEditChange: (value: InspectionImageAnalysisResult) => void
}) {
  const [editing, setEditing] = useState(false)
  const result = state?.result
  const representative = images.find((image) => image.isRepresentativeForAi) ?? null
  const failed = state && ['Failed', 'TimedOut', 'Interrupted', 'Stale', 'Unavailable'].includes(state.status)

  function beginEditing() {
    if (!result) return
    onEditChange(result)
    setEditing(true)
  }

  function cancelEditing() {
    if (result) onEditChange(result)
    setEditing(false)
  }

  function saveEditedReview() {
    onEdit()
    setEditing(false)
  }
  return (
    <section className="image-analysis-panel" aria-labelledby="image-analysis-title">
      <div className="image-analysis-heading">
        <div>
          <p className="preplant-eyebrow">Optional crop / leaf image assistance</p>
          <h3 id="image-analysis-title">Representative image analysis</h3>
          <p>One selected image can support possible crop-health concerns. It does not confirm a diagnosis.</p>
        </div>
        <StatusPill label={state?.status ?? 'Not analyzed'} tone={state?.status === 'Succeeded' ? 'good' : failed ? 'bad' : 'info'} />
      </div>
      {!representative ? <Notice tone="info">Select one uploaded crop or leaf image as representative before requesting analysis.</Notice> : null}
      {state?.status === 'Running' ? <Notice tone="info">Image analysis is running. You may keep editing or submit the inspection without waiting.</Notice> : null}
      {failed ? <Notice tone="warning">{state.message ?? 'AI image analysis is unavailable. Continue the inspection manually or retry explicitly while the draft is open.'}</Notice> : null}
      {state?.isFrozen ? <Notice tone="info">This submitted inspection's reviewed image evidence is frozen and read-only.</Notice> : null}

      <div className="image-analysis-workspace">
        <aside className="image-analysis-evidence" aria-label="Representative image selection">
          <div className="image-workspace-section-heading">
            <div><span>Selected evidence</span><h4>Image used for analysis</h4></div>
            {representative ? <StatusPill label="Representative" tone="good" /> : null}
          </div>

          {representative ? (
            <figure className="representative-image-preview">
              <a href={representative.url} target="_blank" rel="noreferrer" aria-label="Open representative evidence in a new tab">
                <img src={representative.url} alt="Representative evidence for AI analysis" />
              </a>
              <figcaption><span>{representative.contentType}</span><span>{Math.ceil(representative.sizeBytes / 1024)} KB</span></figcaption>
            </figure>
          ) : (
            <div className="representative-image-empty">
              <ImageIcon size={28} aria-hidden="true" />
              <strong>No representative image selected</strong>
              <span>Choose one uploaded crop or leaf image below.</span>
            </div>
          )}

          {images.length > 0 ? (
            <div className="evidence-selector" aria-label="Uploaded assessment evidence">
              {images.map((image, index) => (
                <article key={image.id} className={`evidence-selector-item${image.isRepresentativeForAi ? ' is-representative' : ''}`}>
                  <a href={image.url} target="_blank" rel="noreferrer" aria-label={`Open evidence ${index + 1} in a new tab`}>
                    <img src={image.url} alt={`Evidence ${index + 1}`} />
                  </a>
                  <div><strong>Evidence {index + 1}</strong><span>{Math.ceil(image.sizeBytes / 1024)} KB</span></div>
                  {image.isRepresentativeForAi ? (
                    <CheckCircle2 size={18} aria-label="Selected representative" />
                  ) : canEdit ? (
                    <Button type="button" variant="ghost" disabled={busy} aria-label={`Select evidence ${index + 1} for AI`} onClick={() => onSelectRepresentative(image.id)}>Select</Button>
                  ) : null}
                </article>
              ))}
            </div>
          ) : <p className="muted-text">Upload evidence from the assessment form to select an analysis image.</p>}

          {canEdit && representative && state?.status !== 'Running' ? (
            <Button type="button" variant="secondary" icon={<Sparkles size={16} aria-hidden="true" />} disabled={busy} onClick={() => { setEditing(false); onAnalyze() }}>
              {busy ? 'Working...' : result ? 'Analyze Image Again' : 'Analyze Image'}
            </Button>
          ) : null}
        </aside>

        <section className="image-analysis-findings" aria-labelledby="ai-findings-title">
          <div className="image-workspace-section-heading">
            <div><span>Analysis output</span><h3 id="ai-findings-title">AI findings</h3></div>
            {result ? <StatusPill label="Original AI result" tone="info" /> : null}
          </div>

          {result ? (
            <>
              <div className="image-analysis-result">
                <ResultList title="Visible findings" items={result.visibleFindings} />
                <ResultList title="Possible concerns" items={result.possibleIssues} />
                <dl className="image-analysis-summary">
                  <AssessmentItem label="Issue category" value={result.possibleIssueCategory} />
                  <AssessmentItem label="Severity" value={result.severity} />
                  <AssessmentItem label="Further assessment" value={result.requiresFurtherAssessment ? 'Required' : 'Monitor as reviewed'} />
                  <AssessmentItem label="Uncertainty" value={result.uncertainty} />
                </dl>
                <ResultList title="Suggested non-chemical actions" items={result.recommendedNonChemicalActions.map(humanize)} />
                {result.validatedSourceReferences.length ? <div className="source-reference-list">
                  <h4>Trusted supporting sources</h4>
                  {result.validatedSourceReferences.map((source) => {
                    const url = safeExternalUrl(source.url)
                    return <p key={`${source.sourcePolicyId}-${source.url}`}>{source.organization}: {url ? <a href={url} target="_blank" rel="noreferrer">{source.title}</a> : source.title} ({source.sourceStage})</p>
                  })}
                </div> : <Notice tone="warning">Trusted external grounding was unavailable; conservative precautions only.</Notice>}
              </div>

              {canEdit && state?.isReviewable && !editing ? (
                <div className="image-review-actions" aria-label="Image analysis review actions">
                  <Button type="button" disabled={busy} onClick={onAccept}>Accept result</Button>
                  <Button type="button" variant="secondary" disabled={busy} onClick={beginEditing}>Edit findings</Button>
                  <Button type="button" variant="danger" disabled={busy} onClick={onReject}>Reject result</Button>
                </div>
              ) : null}

              {edit && canEdit && state?.isReviewable && editing ? <section className="officer-review-editor" aria-label="Structured Field Officer image analysis review">
                <div className="row-actions"><strong>Field Officer review</strong><StatusPill label="Officer Edited when saved" tone="warn" /></div>
                <TextAreaInput label="Visible findings (one per line)" value={edit.visibleFindings.join('\n')} rows={4} disabled={busy} onChange={(value) => onEditChange({ ...edit, visibleFindings: editableLines(value) })} />
                <TextAreaInput label="Possible concerns (one per line)" value={edit.possibleIssues.join('\n')} rows={4} disabled={busy} onChange={(value) => onEditChange({ ...edit, possibleIssues: editableLines(value) })} />
                <SelectInput label="Severity" value={edit.severity} options={options(['Low', 'Moderate', 'High', 'Unknown'] as const)} disabled={busy} onChange={(value) => onEditChange({ ...edit, severity: value as InspectionImageAnalysisResult['severity'] })} />
                <TextAreaInput label="Uncertainty" value={edit.uncertainty} rows={3} disabled={busy} onChange={(value) => onEditChange({ ...edit, uncertainty: value })} />
                <fieldset className="preplant-risk-list">
                  <legend>Allowed non-chemical actions</legend>
                  {cropHealthActions.map((action) => <label key={action}><input type="checkbox" checked={edit.recommendedNonChemicalActions.includes(action)} disabled={busy} onChange={(event) => onEditChange({ ...edit, recommendedNonChemicalActions: event.target.checked ? [...edit.recommendedNonChemicalActions, action] : edit.recommendedNonChemicalActions.filter((item) => item !== action) })} /><span>{humanize(action)}</span></label>)}
                </fieldset>
                <label className="review-checkbox"><input type="checkbox" checked={edit.requiresFurtherAssessment} disabled={busy} onChange={(event) => onEditChange({ ...edit, requiresFurtherAssessment: event.target.checked })} /> Requires further assessment</label>
                <div className="image-review-actions">
                  <Button type="button" disabled={busy} onClick={saveEditedReview}>Save Officer Edited review</Button>
                  <Button type="button" variant="ghost" disabled={busy} onClick={cancelEditing}>Cancel edit</Button>
                </div>
              </section> : null}
            </>
          ) : (
            <div className="image-findings-empty">
              <BrainCircuit size={28} aria-hidden="true" />
              <strong>No AI findings to review</strong>
              <span>Manual inspection remains available whether or not image assistance is used.</span>
            </div>
          )}

          {state?.effectiveReview ? <Notice tone={state.effectiveReview.disposition === 'Rejected' ? 'warning' : 'success'}>
            Latest review: {state.effectiveReview.disposition}{state.effectiveReview.officerEditedFields.length ? ` / Officer Edited (${state.effectiveReview.officerEditedFields.join(', ')})` : ''}.
          </Notice> : null}
        </section>
      </div>
      <details className="image-analysis-history">
        <summary onClick={() => { if (history === null && !busy) onLoadHistory() }}>Staff audit history</summary>
        {history === null ? <p className="muted-text">Open to load immutable analysis and review history.</p> : history.length === 0
          ? <p className="muted-text">No image-analysis attempts have been recorded.</p>
          : history.map((item) => <article key={item.analysisId} className="audit-history-item">
            <div className="row-actions">
              <strong>{item.isFrozen ? 'Frozen' : item.isCurrent ? 'Current' : item.isSuperseded ? 'Superseded' : item.status}</strong>
              <StatusPill label={item.status} tone={item.status === 'Succeeded' ? 'good' : item.status === 'Running' ? 'info' : 'bad'} />
            </div>
            <p className="muted-text">Created {item.createdAt}{item.completedAt ? ` · completed ${item.completedAt}` : ''}</p>
            {item.failureMessage ? <Notice tone="warning">{item.failureMessage}</Notice> : null}
            {item.result ? <><ResultList title="Visible findings" items={item.result.visibleFindings} /><AssessmentItem label="Uncertainty" value={item.result.uncertainty} /></> : null}
            {item.reviews.length ? <ul>{item.reviews.map((review) => <li key={review.reviewId}>{review.disposition} · {review.reviewedAt}{review.officerEditedFields.length ? ' · Officer Edited' : ''}</li>)}</ul> : <p className="muted-text">No Field Officer review.</p>}
          </article>)}
      </details>
    </section>
  )
}

function editableLines(value: string): string[] {
  return value.split(/\r?\n/).filter((item) => item.length > 0)
}

function normalizedLines(items: string[]): string[] {
  return items.map((item) => item.trim()).filter(Boolean)
}

function safeExternalUrl(value: string): string | null {
  try {
    const url = new URL(value)
    return url.protocol === 'https:' || url.protocol === 'http:' ? url.href : null
  } catch {
    return null
  }
}

const sectionOrder: AssessmentSectionKey[] = ['soil', 'water', 'readiness', 'risks']

function assessmentSectionCompletion(
  form: PrePlantingAssessmentInput,
  riskState: RiskAssessmentState,
  selectedRisks: PrePlantingRisk[],
): Record<AssessmentSectionKey, boolean> {
  const soilNotesRequired = form.soilType === 'Other' || form.soilCondition === 'Other'
  const drainageNotesRequired = form.drainageCondition === 'Poor'
    || form.waterloggingRisk === 'Moderate'
    || form.waterloggingRisk === 'High'
  const generalNotesRequired = form.generalFieldCondition === 'Other'

  return {
    soil: Boolean(
      form.soilType
      && form.soilCondition
      && form.soilMoisture
      && (!soilNotesRequired || hasText(form.soilNotes)),
    ),
    water: Boolean(
      form.waterAvailability
      && form.irrigationAvailability
      && form.waterReliability
      && (!requiresWaterSource(form.waterAvailability) || hasText(form.mainWaterSource))
      && (!requiresWaterConcern(form.waterAvailability) || hasText(form.waterConcerns)),
    ),
    readiness: Boolean(
      form.drainageCondition
      && form.waterloggingRisk
      && form.generalFieldCondition
      && form.plantingReadiness
      && (!drainageNotesRequired || hasText(form.drainageNotes))
      && (!generalNotesRequired || hasText(form.generalFieldNotes)),
    ),
    risks: riskState === 'none' || (
      riskState === 'selected'
      && selectedRisks.length > 0
      && (!selectedRisks.includes('Other') || hasText(form.riskNotes))
    ),
  }
}

function hasText(value?: string | null): boolean {
  return Boolean(value?.trim())
}

function riskStateFromAssessment(assessment: PrePlantingAssessment): RiskAssessmentState {
  if (assessment.identifiedRisks === null) return 'unassessed'
  return assessment.identifiedRisks.length === 0 ? 'none' : 'selected'
}

function initialOpenSections(
  form: PrePlantingAssessmentInput,
  riskState: RiskAssessmentState,
  selectedRisks: PrePlantingRisk[],
): Set<AssessmentSectionKey> {
  const completion = assessmentSectionCompletion(form, riskState, selectedRisks)
  const firstIncomplete = sectionOrder.find((section) => !completion[section])
  return new Set(firstIncomplete ? [firstIncomplete] : [])
}

function sectionFromValidationMessage(message: string): AssessmentSectionKey | null {
  const normalized = message.toLowerCase()
  if (/soil|moisture/.test(normalized)) return 'soil'
  if (/water availability|water source|water concern|irrigation|reliability/.test(normalized)) return 'water'
  if (/drainage|waterlogging|field condition|field note|planting readiness/.test(normalized)) return 'readiness'
  if (/risk|officer note/.test(normalized)) return 'risks'
  return null
}

function sectionsWithNoteSuggestions(suggestions: InspectionNoteSuggestions | null): AssessmentSectionKey[] {
  if (!suggestions) return []
  const sections = new Set<AssessmentSectionKey>()
  if (suggestions.soilNotes) sections.add('soil')
  if (suggestions.waterConcerns) sections.add('water')
  if (suggestions.drainageNotes || suggestions.generalFieldNotes) sections.add('readiness')
  if (suggestions.riskNotes || suggestions.officerNotes) sections.add('risks')
  return [...sections]
}

function AssessmentSection({
  sectionKey,
  title,
  description,
  open,
  complete,
  onToggle,
  children,
}: {
  sectionKey: AssessmentSectionKey
  title: string
  description: string
  open: boolean
  complete: boolean
  onToggle: () => void
  children: React.ReactNode
}) {
  return (
    <section className={`preplant-form-section${complete ? ' is-complete' : ''}`} data-assessment-section={sectionKey}>
      <button
        id={`assessment-trigger-${sectionKey}`}
        className="assessment-section-trigger"
        type="button"
        aria-expanded={open}
        aria-controls={`assessment-section-${sectionKey}`}
        onClick={onToggle}
      >
        <span className="assessment-section-title">
          <span className="assessment-section-status" aria-hidden="true">
            {complete ? <CheckCircle2 size={18} /> : <span>{sectionOrder.indexOf(sectionKey) + 1}</span>}
          </span>
          <span><strong>{title}</strong><small>{description}</small></span>
        </span>
        <span className={`assessment-section-state${complete ? ' is-complete' : ''}`}>
          {complete ? 'Complete' : 'Needs attention'}
        </span>
        <ChevronDown className="assessment-section-chevron" size={19} aria-hidden="true" />
      </button>
      <div id={`assessment-section-${sectionKey}`} className="assessment-section-content" hidden={!open}>
        <div className="preplant-form-grid">{children}</div>
      </div>
    </section>
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
      <p>{result.fieldCondition?.summary || 'No field-condition summary was returned.'}</p>
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
      {(result.warnings ?? []).map((warning) => <Notice key={warning} tone="warning">{warning}</Notice>)}
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

function normalizeAssessment(value: unknown): PrePlantingAssessment | null {
  if (value === null) return null
  if (!isRecord(value)) throw invalidAssessmentResponse()

  return {
    inspectionId: requiredString(value.inspectionId, invalidAssessmentResponse),
    cropPlanRequestId: requiredString(value.cropPlanRequestId, invalidAssessmentResponse),
    fieldId: requiredString(value.fieldId, invalidAssessmentResponse),
    inspectorUserId: requiredString(value.inspectorUserId, invalidAssessmentResponse),
    status: requiredNumber(value.status, invalidAssessmentResponse),
    scheduledAt: requiredString(value.scheduledAt, invalidAssessmentResponse),
    completedAt: nullableString(value.completedAt, invalidAssessmentResponse),
    soilType: nullableEnum(value.soilType, soilTypeValues),
    soilCondition: nullableEnum(value.soilCondition, soilConditionValues),
    soilMoisture: nullableEnum(value.soilMoisture, soilMoistureValues),
    soilNotes: nullableString(value.soilNotes, invalidAssessmentResponse),
    waterAvailability: nullableEnum(value.waterAvailability, waterAvailabilityValues),
    mainWaterSource: nullableString(value.mainWaterSource, invalidAssessmentResponse),
    irrigationAvailability: nullableEnum(value.irrigationAvailability, irrigationValues),
    waterReliability: nullableEnum(value.waterReliability, waterReliabilityValues),
    waterConcerns: nullableString(value.waterConcerns, invalidAssessmentResponse),
    drainageCondition: nullableEnum(value.drainageCondition, drainageValues),
    waterloggingRisk: nullableEnum(value.waterloggingRisk, waterloggingValues),
    drainageNotes: nullableString(value.drainageNotes, invalidAssessmentResponse),
    generalFieldCondition: nullableEnum(value.generalFieldCondition, fieldConditionValues),
    generalFieldNotes: nullableString(value.generalFieldNotes, invalidAssessmentResponse),
    plantingReadiness: nullableEnum(value.plantingReadiness, readinessValues),
    identifiedRisks: normalizeIdentifiedRisks(value.identifiedRisks),
    riskNotes: nullableString(value.riskNotes, invalidAssessmentResponse),
    risksAndConcerns: nullableString(value.risksAndConcerns, invalidAssessmentResponse),
    officerNotes: nullableString(value.officerNotes, invalidAssessmentResponse),
    images: normalizeAssessmentImages(value.images),
  }
}

function normalizeFieldAnalysisResult(value: unknown): FieldAnalysisResult {
  if (!isRecord(value)) throw invalidFieldAnalysisResponse()
  const fieldCondition = value.fieldCondition
  const normalizedFieldCondition = fieldCondition == null
    ? { summary: '', evidenceInspectionIds: [] }
    : isRecord(fieldCondition)
      ? {
          summary: optionalString(fieldCondition.summary, invalidFieldAnalysisResponse) ?? '',
          evidenceInspectionIds: stringArray(fieldCondition.evidenceInspectionIds, invalidFieldAnalysisResponse),
        }
      : (() => { throw invalidFieldAnalysisResponse() })()

  return {
    workflowId: requiredString(value.workflowId, invalidFieldAnalysisResponse),
    status: requiredString(value.status, invalidFieldAnalysisResponse),
    requiresHumanReview: optionalBoolean(value.requiresHumanReview, invalidFieldAnalysisResponse) ?? false,
    warnings: stringArray(value.warnings, invalidFieldAnalysisResponse),
    fieldCondition: normalizedFieldCondition,
    openIssues: normalizeOpenIssues(value.openIssues),
    priority: optionalString(value.priority, invalidFieldAnalysisResponse) ?? 'Unknown',
    fieldSuitability: optionalString(value.fieldSuitability, invalidFieldAnalysisResponse) as FieldAnalysisResult['fieldSuitability'],
    soilAssessment: optionalString(value.soilAssessment, invalidFieldAnalysisResponse),
    waterAssessment: optionalString(value.waterAssessment, invalidFieldAnalysisResponse),
    drainageAssessment: optionalString(value.drainageAssessment, invalidFieldAnalysisResponse),
    fieldPreparationRequirements: stringArray(value.fieldPreparationRequirements, invalidFieldAnalysisResponse),
    plantingReadiness: nullableEnum(value.plantingReadiness, [...readinessValues, 'Unknown'] as const, invalidFieldAnalysisResponse) ?? 'Unknown',
    identifiedRisks: stringEnumArray(value.identifiedRisks, riskOptions, invalidFieldAnalysisResponse),
    recommendedPrePlantingActions: stringArray(value.recommendedPrePlantingActions, invalidFieldAnalysisResponse),
  }
}

function normalizeAssessmentImages(value: unknown): PrePlantingAssessment['images'] {
  if (value == null) return []
  if (!Array.isArray(value)) throw invalidAssessmentResponse()
  return value.map((item) => {
    if (!isRecord(item)) throw invalidAssessmentResponse()
    return {
      id: requiredString(item.id, invalidAssessmentResponse),
      url: requiredString(item.url, invalidAssessmentResponse),
      contentType: requiredString(item.contentType, invalidAssessmentResponse),
      sizeBytes: requiredNumber(item.sizeBytes, invalidAssessmentResponse),
      isRepresentativeForAi: optionalBoolean(item.isRepresentativeForAi, invalidAssessmentResponse) ?? false,
    }
  })
}

function normalizeIdentifiedRisks(value: unknown): PrePlantingRisk[] | null {
  if (value == null) return null
  return stringEnumArray(value, riskOptions, invalidAssessmentResponse)
}

function normalizeOpenIssues(value: unknown): FieldAnalysisResult['openIssues'] {
  if (value == null) return []
  if (!Array.isArray(value)) throw invalidFieldAnalysisResponse()
  return value.map((item) => {
    if (!isRecord(item)) throw invalidFieldAnalysisResponse()
    return {
      issueId: requiredString(item.issueId, invalidFieldAnalysisResponse),
      severity: requiredString(item.severity, invalidFieldAnalysisResponse),
      status: requiredString(item.status, invalidFieldAnalysisResponse),
      evidenceInspectionId: optionalString(item.evidenceInspectionId, invalidFieldAnalysisResponse),
    }
  })
}

function stringEnumArray<T extends string>(value: unknown, allowed: readonly T[], error: () => Error): T[] {
  const values = stringArray(value, error)
  if (values.some((item) => !allowed.includes(item as T))) throw error()
  return values as T[]
}

function stringArray(value: unknown, error: () => Error): string[] {
  if (value == null) return []
  if (!Array.isArray(value) || value.some((item) => typeof item !== 'string')) throw error()
  return value
}

function nullableEnum<T extends string>(value: unknown, allowed: readonly T[], error = invalidAssessmentResponse): T | null {
  if (value == null) return null
  if (typeof value !== 'string' || !allowed.includes(value as T)) throw error()
  return value as T
}

function requiredString(value: unknown, error: () => Error): string {
  if (typeof value !== 'string' || value.trim() === '') throw error()
  return value
}

function optionalString(value: unknown, error: () => Error): string | undefined {
  if (value == null) return undefined
  if (typeof value !== 'string') throw error()
  return value
}

function nullableString(value: unknown, error: () => Error): string | null {
  return optionalString(value, error) ?? null
}

function requiredNumber(value: unknown, error: () => Error): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) throw error()
  return value
}

function optionalBoolean(value: unknown, error: () => Error): boolean | undefined {
  if (value == null) return undefined
  if (typeof value !== 'boolean') throw error()
  return value
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function requireAssessment(value: PrePlantingAssessment | null): PrePlantingAssessment {
  if (!value) throw invalidAssessmentResponse()
  return value
}

function normalizeLinkedAssessment(value: unknown, requestId: string): PrePlantingAssessment | null {
  const assessment = normalizeAssessment(value)
  if (assessment && assessment.cropPlanRequestId !== requestId) throw invalidAssessmentResponse()
  return assessment
}

function normalizeAssessmentResponse(
  response: { status?: number; data: unknown },
  requestId: string,
): PrePlantingAssessment | null {
  if (
    response.status === 204
    || response.data === null
    || response.data === undefined
    || response.data === ''
  ) {
    return null
  }

  return normalizeLinkedAssessment(response.data, requestId)
}

function invalidAssessmentResponse() {
  return new Error('The pre-planting assessment response was invalid.')
}

function invalidFieldAnalysisResponse() {
  return new Error('The field analysis response was invalid.')
}

function isNotFound(error: unknown) {
  return axios.isAxiosError(error) && error.response?.status === 404
}

function resetAssessmentForm(
  setForm: (value: PrePlantingAssessmentInput) => void,
  setRiskState: (value: RiskAssessmentState) => void,
  setSelectedRisks: (value: PrePlantingRisk[]) => void,
) {
  setForm({ ...emptyAssessment })
  setRiskState('unassessed')
  setSelectedRisks([])
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
