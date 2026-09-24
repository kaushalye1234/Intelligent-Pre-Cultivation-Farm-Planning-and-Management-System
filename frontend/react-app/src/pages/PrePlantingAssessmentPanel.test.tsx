import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { Roles } from '../routing'
import type { FieldAnalysisResult, PrePlantingAssessment, WorkflowReview } from '../types'
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

const savedAssessment: PrePlantingAssessment = {
  inspectionId: 'inspection-1',
  cropPlanRequestId: 'plan-1',
  fieldId: 'field-1',
  status: 2,
  scheduledAt: '2026-09-23T08:10:00Z',
  soilCondition: 'Moist loam',
  waterAvailability: 'Canal supply available',
  irrigationAvailability: 'Pump is operational',
  drainageCondition: 'Drainage channels are clear',
  generalFieldCondition: 'Field is cleared and level',
  plantingReadiness: 'Ready after final harrowing',
  risksAndConcerns: 'Low area may retain water',
  officerNotes: 'Recheck the low area before sowing',
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
  priority: 'Normal',
}

afterEach(() => vi.restoreAllMocks())

describe('PrePlantingAssessmentPanel', () => {
  it('saves, uploads evidence, submits, and then runs field analysis in order', async () => {
    let assessmentLoad = 0
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/crop-plans/plan-1/pre-planting-assessment') {
        assessmentLoad += 1
        return { data: assessmentLoad === 1 ? null : submittedAssessment } as never
      }
      if (url === '/crop-plans/plan-1/field-analysis-result') return { data: fieldResult } as never
      throw new Error('Unexpected GET ' + url)
    })
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: savedAssessment } as never)
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)
    const onWorkflowChanged = vi.fn().mockResolvedValue(undefined)
    const user = userEvent.setup()

    render(<PrePlantingAssessmentPanel review={review} role={Roles.FieldOfficer} onWorkflowChanged={onWorkflowChanged} />)

    await user.type(await screen.findByLabelText(/Soil type \/ condition/i), savedAssessment.soilCondition)
    await user.type(screen.getByLabelText(/Water availability/i), savedAssessment.waterAvailability)
    await user.type(screen.getByLabelText(/Irrigation availability/i), savedAssessment.irrigationAvailability)
    await user.type(screen.getByLabelText(/Drainage condition/i), savedAssessment.drainageCondition)
    await user.type(screen.getByLabelText(/General field condition/i), savedAssessment.generalFieldCondition)
    await user.type(screen.getByLabelText(/Planting readiness/i), savedAssessment.plantingReadiness)
    await user.type(screen.getByLabelText(/Risks \/ concerns/i), savedAssessment.risksAndConcerns)
    await user.type(screen.getByLabelText(/Officer notes/i), savedAssessment.officerNotes)
    await user.upload(screen.getByLabelText(/Photos \/ evidence/i), new File(['field'], 'field.jpg', { type: 'image/jpeg' }))
    await user.click(screen.getByRole('button', { name: /submit & run field analysis/i }))

    await screen.findByText('Pre-planting assessment submitted and field analysis completed.')
    expect(put).toHaveBeenCalledWith('/crop-plans/plan-1/pre-planting-assessment', expect.objectContaining({
      soilCondition: savedAssessment.soilCondition,
      plantingReadiness: savedAssessment.plantingReadiness,
      risksAndConcerns: savedAssessment.risksAndConcerns,
    }))
    expect(post).toHaveBeenNthCalledWith(1, '/inspections/inspection-1/images', expect.any(FormData), {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    expect(post).toHaveBeenNthCalledWith(2, '/inspections/inspection-1/submit')
    expect(post).toHaveBeenNthCalledWith(3, '/crop-plans/plan-1/run-field-analysis')
    expect(put.mock.invocationCallOrder[0]).toBeLessThan(post.mock.invocationCallOrder[0])
    expect(post.mock.invocationCallOrder[0]).toBeLessThan(post.mock.invocationCallOrder[1])
    expect(post.mock.invocationCallOrder[1]).toBeLessThan(post.mock.invocationCallOrder[2])
    expect(onWorkflowChanged).toHaveBeenCalledOnce()
    expect(screen.queryByText(/pest|disease|weed|harvest/i)).not.toBeInTheDocument()
  })

  it('shows submitted assessment read-only to an Agricultural Officer', async () => {
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/crop-plans/plan-1/pre-planting-assessment') return { data: submittedAssessment } as never
      if (url === '/crop-plans/plan-1/field-analysis-result') return { data: fieldResult } as never
      throw new Error('Unexpected GET ' + url)
    })

    const reviewedWorkflow: WorkflowReview = {
      ...review,
      workflow: { ...review.workflow, currentStep: 'WeatherResourceAgent' },
      steps: review.steps.map((step) => ({ ...step, status: 3 })),
    }
    render(<PrePlantingAssessmentPanel review={reviewedWorkflow} role={Roles.AgriculturalOfficer} onWorkflowChanged={vi.fn()} />)

    expect(await screen.findByText(savedAssessment.soilCondition)).toBeInTheDocument()
    expect(screen.getByText(savedAssessment.plantingReadiness)).toBeInTheDocument()
    expect(screen.getByText('The field is suitable for planting.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /save draft/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /run field analysis/i })).not.toBeInTheDocument()
    await waitFor(() => expect(api.get).toHaveBeenCalledWith('/crop-plans/plan-1/field-analysis-result'))
  })
})
