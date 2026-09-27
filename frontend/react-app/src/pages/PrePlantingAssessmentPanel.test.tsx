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

function axiosError(status: number, message: string) {
  return Object.assign(new Error(message), {
    isAxiosError: true,
    response: { status, data: { message } },
  })
}

describe('PrePlantingAssessmentPanel', () => {
  it('renders a valid linked assessment payload', async () => {
    mockLoads(savedAssessment)

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByDisplayValue('Moist loam.')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Canal')).toBeInTheDocument()
  })

  it('does not crash when optional assessment fields and images are null', async () => {
    mockLoads({
      ...savedAssessment,
      soilNotes: null,
      waterConcerns: null,
      drainageNotes: null,
      generalFieldNotes: null,
      identifiedRisks: null,
      riskNotes: null,
      officerNotes: null,
      images: null,
    } as unknown as PrePlantingAssessment)

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByRole('button', { name: /save draft/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/risk assessment/i)).toHaveValue('unassessed')
  })

  it.each([
    ['an HTTP 204 response', { status: 204, data: '' }],
    ['a null response body', { status: 200, data: null }],
    ['an undefined response body', { status: 200, data: undefined }],
    ['an empty response body', { status: 200, data: '' }],
  ])('treats %s as not created yet', async (_label, assessmentResponse) => {
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/crop-plans/plan-1/pre-planting-context') return { data: context } as never
      if (url === '/crop-plans/plan-1/pre-planting-assessment') return assessmentResponse as never
      throw new Error('Unexpected GET ' + url)
    })

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByRole('button', { name: /save draft/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /submit assessment/i })).toBeInTheDocument()
    expect(screen.queryByText(/assessment response was invalid/i)).not.toBeInTheDocument()
  })

  it('treats an assessment 404 as not created yet', async () => {
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/crop-plans/plan-1/pre-planting-context') return { data: context } as never
      if (url === '/crop-plans/plan-1/pre-planting-assessment') throw axiosError(404, 'Assessment not found.')
      throw new Error('Unexpected GET ' + url)
    })

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByRole('button', { name: /save draft/i })).toBeInTheDocument()
    expect(screen.queryByText('Assessment not found.')).not.toBeInTheDocument()
  })

  it('shows a readable error for an assessment 500 while keeping the panel rendered', async () => {
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/crop-plans/plan-1/pre-planting-context') return { data: context } as never
      if (url === '/crop-plans/plan-1/pre-planting-assessment') throw axiosError(500, 'Assessment service unavailable.')
      throw new Error('Unexpected GET ' + url)
    })

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByText('Assessment service unavailable.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /pre-planting field assessment/i })).toBeInTheDocument()
  })

  it('shows a readable error for a malformed assessment payload', async () => {
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/crop-plans/plan-1/pre-planting-context') return { data: context } as never
      if (url === '/crop-plans/plan-1/pre-planting-assessment') return { data: { unexpected: 'payload' } } as never
      throw new Error('Unexpected GET ' + url)
    })

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByText(/assessment response was invalid/i)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /pre-planting field assessment/i })).toBeInTheDocument()
  })

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

  it('does not submit while structured risks remain unassessed', async () => {
    mockLoads({ ...savedAssessment, identifiedRisks: null })
    const put = vi.spyOn(api, 'put')
    const post = vi.spyOn(api, 'post')
    const user = userEvent.setup()
    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    await user.click(await screen.findByRole('button', { name: /submit assessment/i }))

    expect(screen.getByText(/assess structured risks before submitting/i)).toBeInTheDocument()
    expect(put).not.toHaveBeenCalled()
    expect(post).not.toHaveBeenCalled()
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

  it('submits successfully when an optional image upload fails and does not retry the failed file', async () => {
    mockLoads(null)
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: savedAssessment } as never)
    const post = vi.spyOn(api, 'post').mockImplementation(async (url) => {
      if (url === '/inspections/inspection-1/images') throw axiosError(502, 'Image upload failed.')
      if (url === '/crop-plans/plan-1/pre-planting-assessment/submit') return { data: submittedAssessment } as never
      if (url === '/crop-plans/plan-1/run-field-analysis') return { data: {} } as never
      throw new Error('Unexpected POST ' + url)
    })
    const user = userEvent.setup()
    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    await fillRequiredAssessment(user)
    await user.upload(screen.getByLabelText(/photos \/ evidence/i), new File(['field'], 'field.jpg', { type: 'image/jpeg' }))
    await user.click(screen.getByRole('button', { name: /submit assessment/i }))

    expect(await screen.findByText('Pre-planting assessment submitted. Field analysis is now available.')).toBeInTheDocument()
    expect(screen.getByText(/optional evidence upload failed.*image upload failed/i)).toBeInTheDocument()
    expect(put).toHaveBeenCalledOnce()
    expect(post).toHaveBeenCalledWith('/crop-plans/plan-1/pre-planting-assessment/submit')
    expect(post.mock.calls.filter(([url]) => url === '/inspections/inspection-1/images')).toHaveLength(1)
    expect(screen.queryByLabelText(/photos \/ evidence/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /save draft|submit assessment/i })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^run field analysis$/i }))
    expect(post.mock.calls.filter(([url]) => url === '/inspections/inspection-1/images')).toHaveLength(1)
  })

  it('submits successfully without selected image evidence', async () => {
    mockLoads(savedAssessment)
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: savedAssessment } as never)
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: submittedAssessment } as never)
    const user = userEvent.setup()
    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    await user.click(await screen.findByRole('button', { name: /submit assessment/i }))

    expect(await screen.findByText('Pre-planting assessment submitted. Field analysis is now available.')).toBeInTheDocument()
    expect(put).toHaveBeenCalledOnce()
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/crop-plans/plan-1/pre-planting-assessment/submit')
    expect(screen.getByRole('button', { name: /^run field analysis$/i })).toBeInTheDocument()
  })

  it('keeps a saved draft editable when dedicated submission validation fails', async () => {
    mockLoads(savedAssessment)
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: savedAssessment } as never)
    vi.spyOn(api, 'post').mockRejectedValue(axiosError(400, 'Drainage notes are required before submission.'))
    const user = userEvent.setup()
    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={vi.fn()} />)

    await user.click(await screen.findByRole('button', { name: /submit assessment/i }))

    expect(await screen.findByText('Drainage notes are required before submission.')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Moist loam.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /save draft/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /submit assessment/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /run field analysis/i })).not.toBeInTheDocument()
    expect(put).toHaveBeenCalledOnce()
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

  it('normalizes null optional field-analysis collections and nested field condition', async () => {
    const completedReview = {
      ...review,
      workflow: { ...review.workflow, currentStep: 'WeatherResourceAgent' },
      steps: review.steps.map((step) => ({ ...step, status: 3 })),
    }
    mockLoads(submittedAssessment, {
      ...fieldResult,
      warnings: null,
      fieldCondition: null,
      openIssues: null,
      fieldPreparationRequirements: null,
      identifiedRisks: null,
      recommendedPrePlantingActions: null,
    } as unknown as FieldAnalysisResult)

    render(<PrePlantingAssessmentPanel review={completedReview} role={Roles.AgriculturalOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByText('No field-condition summary was returned.')).toBeInTheDocument()
    expect(screen.getAllByText('None recorded.')).toHaveLength(2)
  })

  it('shows completed structured output read-only to Agricultural Officer and Admin', async () => {
    mockLoads(submittedAssessment, fieldResult)
    const completedReview = {
      ...review,
      workflow: { ...review.workflow, currentStep: 'WeatherResourceAgent' },
      steps: review.steps.map((step) => ({ ...step, status: 3 })),
    }
    const { rerender } = render(<PrePlantingAssessmentPanel review={completedReview} role={Roles.AgriculturalOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByText('The field is suitable for planting.')).toBeInTheDocument()
    expect(screen.getByText('SuitableWithConditions')).toBeInTheDocument()
    expect(screen.getByText('Canal water is adequate and reliable.')).toBeInTheDocument()
    expect(screen.getByText('Complete final harrowing.')).toBeInTheDocument()
    expect(screen.getByText('Recheck the field before sowing.')).toBeInTheDocument()
    expect(screen.getAllByText('None identified')).toHaveLength(2)
    expect(screen.queryByRole('button', { name: /save draft|submit assessment|run field analysis/i })).not.toBeInTheDocument()

    rerender(<PrePlantingAssessmentPanel review={completedReview} role={Roles.Admin} onWorkflowChanged={vi.fn()} />)
    expect(screen.getByText('The field is suitable for planting.')).toBeInTheDocument()
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
