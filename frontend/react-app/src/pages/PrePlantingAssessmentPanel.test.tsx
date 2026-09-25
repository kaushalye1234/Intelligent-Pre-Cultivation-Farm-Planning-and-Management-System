import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { Roles } from '../routing'
import type {
  FieldAnalysisResult,
  PrePlantingAssessment,
  PrePlantingContext,
  WorkflowReview,
} from '../types'
import { PrePlantingAssessmentPanel } from './PrePlantingAssessmentPanel'

const review: WorkflowReview = {
  workflow: {
    id: 'workflow-1',
    cropPlanRequestId: 'plan-1',
    objective: 'Prepare the next crop cycle.',
    status: 2,
    currentStep: 'CropFieldAnalysisAgent',
    candidateRevision: 1,
    revisionCount: 0,
    version: 2,
    createdAt: '2026-09-23T08:00:00Z',
  },
  farmId: 'farm-1',
  fieldId: 'field-1',
  budget: 12000,
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2027-01-01',
  steps: [{
    id: 'step-1',
    agentName: 'CropFieldAnalysisAgent',
    stepName: 'FieldAnalysis',
    sequence: 2,
    candidateRevision: 1,
    status: 1,
    input: {},
    output: {},
  }],
  validations: [],
  decisions: [],
}

const context: PrePlantingContext = {
  cropPlanRequestId: 'plan-1',
  workflowId: 'workflow-1',
  currentStep: 'CropFieldAnalysisAgent',
  farmerId: 'farmer-1',
  farmerName: 'Nimali Perera',
  farmId: 'farm-1',
  farmName: 'North Farm',
  farmLocation: 'Anuradhapura',
  fieldId: 'field-1',
  fieldName: 'Paddy Block A',
  cropTypeId: 'crop-1',
  cropName: 'Rice',
  cropVarietyId: 'variety-1',
  cropVarietyName: 'Bg 352',
  cultivationSeason: 1,
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2027-01-01',
}

const savedAssessment: PrePlantingAssessment = {
  inspectionId: 'inspection-1',
  cropPlanRequestId: 'plan-1',
  fieldId: 'field-1',
  inspectorUserId: 'officer-1',
  status: 2,
  scheduledAt: '2026-09-23T08:10:00Z',
  completedAt: null,
  soilType: 'Loamy',
  soilCondition: 'Good',
  soilMoisture: 'Moist',
  soilNotes: 'Moist loam.',
  waterAvailability: 'Adequate',
  mainWaterSource: 'Canal',
  irrigationAvailability: 'Available',
  waterReliability: 'Reliable',
  waterConcerns: null,
  drainageCondition: 'Good',
  waterloggingRisk: 'Low',
  drainageNotes: null,
  generalFieldCondition: 'ClearAndPrepared',
  generalFieldNotes: 'Field is cleared.',
  plantingReadiness: 'ReadyWithMinorPreparation',
  identifiedRisks: [],
  riskNotes: null,
  risksAndConcerns: null,
  officerNotes: 'Recheck before sowing.',
  images: [],
}

const submittedAssessment: PrePlantingAssessment = {
  ...savedAssessment,
  status: 3,
  completedAt: '2026-09-23T08:20:00Z',
  images: [{ id: 'image-1', url: 'https://example.test/field.jpg', contentType: 'image/jpeg', sizeBytes: 2048 }],
}

const fieldResult: FieldAnalysisResult = {
  workflowId: 'workflow-1',
  status: 'Analyzed',
  requiresHumanReview: false,
  warnings: [],
  fieldCondition: { summary: 'The field is suitable for planting.', evidenceInspectionIds: ['inspection-1'] },
  openIssues: [],
  priority: 'Low',
  fieldSuitability: 'SuitableWithConditions',
  soilAssessment: 'Loamy soil is in good condition.',
  waterAssessment: 'Canal water is adequate and reliable.',
  drainageAssessment: 'Drainage is good with low waterlogging risk.',
  fieldPreparationRequirements: ['Complete final harrowing.'],
  plantingReadiness: 'ReadyWithMinorPreparation',
  identifiedRisks: [],
  recommendedPrePlantingActions: ['Recheck the field before sowing.'],
}

afterEach(() => vi.restoreAllMocks())

function mockLoads(assessment: PrePlantingAssessment | null, result?: FieldAnalysisResult) {
  return vi.spyOn(api, 'get').mockImplementation(async (url) => {
    if (url === '/crop-plans/plan-1/pre-planting-context') return { data: context } as never
    if (url === '/crop-plans/plan-1/pre-planting-assessment') return { data: assessment } as never
    if (url === '/crop-plans/plan-1/field-analysis-result' && result) return { data: result } as never
    throw new Error('Unexpected GET ' + url)
  })
}

describe('PrePlantingAssessmentPanel', () => {
  it('shows exact crop-plan context and saves an incomplete draft with unassessed risks as null', async () => {
    mockLoads(null)
    const put = vi.spyOn(api, 'put').mockResolvedValue({
      data: { ...savedAssessment, soilType: null, identifiedRisks: null },
    } as never)
    const user = userEvent.setup()

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByText('Nimali Perera')).toBeInTheDocument()
    expect(screen.getByText('North Farm')).toBeInTheDocument()
    expect(screen.getByText('Paddy Block A')).toBeInTheDocument()
    expect(screen.getByText(/Rice · Bg 352/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /save draft/i }))

    expect(put).toHaveBeenCalledWith('/crop-plans/plan-1/pre-planting-assessment', expect.objectContaining({
      soilType: null,
      plantingReadiness: null,
      identifiedRisks: null,
    }))
    expect(await screen.findByText('Pre-planting assessment draft saved.')).toBeInTheDocument()
  })

  it('preserves an explicit no-risk assessment as an empty list', async () => {
    mockLoads(null)
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: savedAssessment } as never)
    const user = userEvent.setup()
    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    await user.selectOptions(await screen.findByLabelText(/risk assessment/i), 'none')
    await user.click(screen.getByRole('button', { name: /save draft/i }))

    expect(put).toHaveBeenCalledWith('/crop-plans/plan-1/pre-planting-assessment', expect.objectContaining({ identifiedRisks: [] }))
  })

  it('keeps save, evidence upload, submit, and AI run as separate actions', async () => {
    mockLoads(null)
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: savedAssessment } as never)
    const post = vi.spyOn(api, 'post').mockImplementation(async (url) => {
      if (url === '/crop-plans/plan-1/pre-planting-assessment/submit') return { data: submittedAssessment } as never
      return { data: {} } as never
    })
    const onWorkflowChanged = vi.fn().mockResolvedValue(undefined)
    const user = userEvent.setup()
    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={onWorkflowChanged} />)

    await fillRequiredAssessment(user)
    await user.upload(screen.getByLabelText(/photos \/ evidence/i), new File(['field'], 'field.jpg', { type: 'image/jpeg' }))
    await user.click(screen.getByRole('button', { name: /submit assessment/i }))

    await screen.findByText('Pre-planting assessment submitted. Field analysis is now available.')
    expect(put).toHaveBeenCalledOnce()
    expect(post).toHaveBeenNthCalledWith(1, '/inspections/inspection-1/images', expect.any(FormData), {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    expect(post).toHaveBeenNthCalledWith(2, '/crop-plans/plan-1/pre-planting-assessment/submit')
    expect(post).not.toHaveBeenCalledWith('/crop-plans/plan-1/run-field-analysis')

    await user.click(screen.getByRole('button', { name: /^run field analysis$/i }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-plans/plan-1/run-field-analysis'))
    expect(onWorkflowChanged).toHaveBeenCalledOnce()
  })

  it('uploads selected evidence separately once a draft exists', async () => {
    mockLoads(savedAssessment)
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)
    const user = userEvent.setup()
    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    await user.upload(await screen.findByLabelText(/photos \/ evidence/i), new File(['field'], 'field.jpg', { type: 'image/jpeg' }))
    await user.click(screen.getByRole('button', { name: /upload evidence/i }))

    expect(post).toHaveBeenCalledWith('/inspections/inspection-1/images', expect.any(FormData), {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    expect(await screen.findByText('Assessment evidence uploaded.')).toBeInTheDocument()
  })

  it('renders running, retryable failure, and completed read-only states', async () => {
    const runningReview = {
      ...review,
      steps: review.steps.map((step) => ({ ...step, status: 2 })),
    }
    mockLoads(submittedAssessment)
    const { unmount } = render(
      <PrePlantingAssessmentPanel review={runningReview} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />,
    )
    expect(await screen.findByText(/field analysis is running/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /run field analysis/i })).not.toBeInTheDocument()
    unmount()

    vi.restoreAllMocks()
    mockLoads(submittedAssessment, { ...fieldResult, status: 'SafeFailure', requiresHumanReview: true })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)
    const failedReview = { ...review, steps: review.steps.map((step) => ({ ...step, status: 4 })) }
    render(<PrePlantingAssessmentPanel review={failedReview} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: /retry field analysis/i }))
    expect(post).toHaveBeenCalledWith('/crop-plans/plan-1/run-field-analysis')
  })

  it('shows completed structured output read-only to Agricultural Officer and Admin', async () => {
    mockLoads(submittedAssessment, fieldResult)
    const completedReview = {
      ...review,
      workflow: { ...review.workflow, currentStep: 'WeatherResourceAgent' },
      steps: review.steps.map((step) => ({ ...step, status: 3 })),
    }
    render(<PrePlantingAssessmentPanel review={completedReview} role={Roles.AgriculturalOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByText('The field is suitable for planting.')).toBeInTheDocument()
    expect(screen.getByText('SuitableWithConditions')).toBeInTheDocument()
    expect(screen.getByText('Canal water is adequate and reliable.')).toBeInTheDocument()
    expect(screen.getByText('Complete final harrowing.')).toBeInTheDocument()
    expect(screen.getByText('Recheck the field before sowing.')).toBeInTheDocument()
    expect(screen.getAllByText('None identified')).toHaveLength(2)
    expect(screen.queryByRole('button', { name: /save draft|submit assessment|run field analysis/i })).not.toBeInTheDocument()
  })

  it('does not load or render the raw panel for Resource Officer or Farmer roles', async () => {
    const get = vi.spyOn(api, 'get')
    const { rerender } = render(
      <PrePlantingAssessmentPanel review={review} role={Roles.ResourceOfficer} onWorkflowChanged={vi.fn()} />,
    )
    expect(screen.queryByText(/pre-planting field assessment/i)).not.toBeInTheDocument()

    rerender(<PrePlantingAssessmentPanel review={review} role={Roles.Farmer} onWorkflowChanged={vi.fn()} />)
    await waitFor(() => expect(get).not.toHaveBeenCalled())
  })
})

async function fillRequiredAssessment(user: ReturnType<typeof userEvent.setup>) {
  await user.selectOptions(await screen.findByLabelText(/^soil type/i), 'Loamy')
  await user.selectOptions(screen.getByLabelText(/^soil condition/i), 'Good')
  await user.selectOptions(screen.getByLabelText(/^soil moisture/i), 'Moist')
  await user.selectOptions(screen.getByLabelText(/^water availability/i), 'Adequate')
  await user.type(screen.getByLabelText(/main water source/i), 'Canal')
  await user.selectOptions(screen.getByLabelText(/^irrigation availability/i), 'Available')
  await user.selectOptions(screen.getByLabelText(/^water reliability/i), 'Reliable')
  await user.selectOptions(screen.getByLabelText(/^drainage condition/i), 'Good')
  await user.selectOptions(screen.getByLabelText(/^waterlogging risk/i), 'Low')
  await user.selectOptions(screen.getByLabelText(/^general field condition/i), 'ClearAndPrepared')
  await user.selectOptions(screen.getByLabelText(/^planting readiness/i), 'ReadyWithMinorPreparation')
  await user.selectOptions(screen.getByLabelText(/risk assessment/i), 'none')
}
