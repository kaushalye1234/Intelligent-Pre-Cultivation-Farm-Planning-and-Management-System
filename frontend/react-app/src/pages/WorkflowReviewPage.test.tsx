import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AuthContext } from '../auth/AuthContext'
import type { UserProfile, WorkflowReview } from '../types'
import { WorkflowReviewPage } from './WorkflowReviewPage'

const officer: UserProfile = {
  id: 'officer-1',
  fullName: 'Agricultural Officer',
  email: 'officer@example.test',
  role: 4,
  isActive: true,
  mustChangePassword: false,
}

const fieldOfficer: UserProfile = {
  ...officer,
  fullName: 'Field Officer',
  role: 2,
}

const review: WorkflowReview = {
  workflow: {
    id: 'workflow-1',
    cropPlanRequestId: 'plan-1',
    objective: 'Prepare the next crop cycle.',
    status: 8,
    currentStep: 'HumanApproval',
    candidateRevision: 2,
    revisionCount: 1,
    version: 7,
    createdAt: new Date().toISOString(),
  },
  farmId: 'farm-1',
  fieldId: 'field-1',
  budget: 12000,
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2026-10-08',
  steps: [],
  validations: [],
  decisions: [],
}

afterEach(() => vi.restoreAllMocks())

describe('WorkflowReviewPage', () => {
  it('shows a blocked proposal with its reason and source but no approval action', async () => {
    const blocked: WorkflowReview = {
      ...review,
      workflow: { ...review.workflow, status: 12, currentStep: 'CANDIDATE_BLOCKED' },
      steps: [{ id: 'scheduling-1', agentName: 'SchedulingValidationAgent', stepName: 'Scheduling',
        sequence: 4, candidateRevision: 2, status: 4, input: {}, output: {
          contractVersion: 2, status: 'CandidateBlocked', warnings: ['Weather risk is High'],
          constraints: [{ code: 'HIGH_WEATHER_RISK', severity: 'Blocking', message: 'High weather blocks approval.' }],
          candidateTasks: [{ title: 'Review Planting stage', dueAt: '2026-10-10T08:00:00Z',
            reason: 'Verified crop stage timing.', sources: [{ kind: 'CropStage', id: 'stage-1',
              label: 'Verified guide', sourceUrl: 'https://example.test/guide' }] }],
          candidateIrrigation: [], candidateReservations: [],
        } }],
    }
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/task-approval/workflows/workflow-1') return { data: blocked } as never
      if (url === '/crop-plans/plan-1/pre-planting-assessment') return { data: null } as never
      throw new Error('Unexpected GET ' + url)
    })

    render(
      <MemoryRouter initialEntries={['/task-approval/workflows/workflow-1']}>
        <AuthContext.Provider value={{ user: officer, token: 'token', isAuthenticated: true, isLoading: false, passwordChangeUser: null, hasPasswordChangeSession: false, login: vi.fn(), changeTemporaryPassword: vi.fn(), logout: vi.fn() }}>
          <Routes><Route path="/task-approval/workflows/:id" element={<WorkflowReviewPage />} /></Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Scheduling proposal')).toBeInTheDocument()
    expect(screen.getByText('Verified crop stage timing.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Verified guide' })).toHaveAttribute('href', 'https://example.test/guide')
    expect(screen.getByText(/new upstream workflow before generating another candidate/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /approve workflow/i })).not.toBeInTheDocument()
  })

  it('keeps Agent Evidence rendered when the assessment child throws', async () => {
    const reviewWithEvidence: WorkflowReview = {
      ...review,
      workflow: { ...review.workflow, currentStep: 'CropFieldAnalysisAgent', status: 2 },
      steps: [{
        id: 'field-step-1',
        agentName: 'CropFieldAnalysisAgent',
        stepName: 'FieldAnalysis',
        sequence: 2,
        candidateRevision: 1,
        status: 1,
        input: {},
        output: {},
      }],
    }
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/task-approval/workflows/workflow-1') return { data: reviewWithEvidence } as never
      if (url === '/crop-plans/plan-1/pre-planting-context') {
        return {
          data: {
            cropPlanRequestId: 'plan-1',
            workflowId: 'workflow-1',
            currentStep: 'CropFieldAnalysisAgent',
            farmerId: 'farmer-1',
            farmerName: { malformed: true },
            farmId: 'farm-1',
            farmName: 'North Farm',
            farmLocation: 'Anuradhapura',
            fieldId: 'field-1',
            fieldName: 'Paddy Block A',
            cropTypeId: 'crop-1',
            cropName: 'Rice',
            cropVarietyId: null,
            cropVarietyName: null,
            cultivationSeason: 1,
            preferredStartDate: '2026-10-01',
            preferredEndDate: '2027-01-01',
          },
        } as never
      }
      if (url === '/crop-plans/plan-1/pre-planting-assessment') return { data: null } as never
      throw new Error('Unexpected GET ' + url)
    })

    render(
      <MemoryRouter initialEntries={['/task-approval/workflows/workflow-1']}>
        <AuthContext.Provider value={{ user: fieldOfficer, token: 'token', isAuthenticated: true, isLoading: false, passwordChangeUser: null, hasPasswordChangeSession: false, login: vi.fn(), changeTemporaryPassword: vi.fn(), logout: vi.fn() }}>
          <Routes><Route path="/task-approval/workflows/:id" element={<WorkflowReviewPage />} /></Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(await screen.findByText(/pre-planting assessment could not be displayed/i)).toBeInTheDocument()
    expect(screen.getByText(/2\. FieldAnalysis/)).toBeInTheDocument()
  })

  it('submits the reviewed candidate revision and workflow version', async () => {
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/task-approval/workflows/workflow-1') return { data: review } as never
      if (url === '/crop-plans/plan-1/pre-planting-assessment') return { data: null } as never
      throw new Error('Unexpected GET ' + url)
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)

    render(
      <MemoryRouter initialEntries={['/task-approval/workflows/workflow-1']}>
        <AuthContext.Provider value={{ user: officer, token: 'token', isAuthenticated: true, isLoading: false, passwordChangeUser: null, hasPasswordChangeSession: false, login: vi.fn(), changeTemporaryPassword: vi.fn(), logout: vi.fn() }}>
          <Routes><Route path="/task-approval/workflows/:id" element={<WorkflowReviewPage />} /></Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await screen.findByText('Prepare the next crop cycle.')
    await userEvent.click(screen.getByRole('button', { name: /reject workflow/i }))
    await userEvent.type(screen.getByLabelText(/reason/i), 'The weather window is unsafe.')
    await userEvent.click(screen.getByRole('button', { name: /confirm decision/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/task-approval/workflows/workflow-1/reject', expect.objectContaining({
      candidateRevision: 2,
      expectedWorkflowVersion: 7,
      comment: 'The weather window is unsafe.',
      idempotencyKey: expect.any(String),
    })))
  })

  it('requires an explicit crop-health guidance decision before approval', async () => {
    const pendingGuidance: WorkflowReview = {
      ...review,
      steps: [{
        id: 'scheduling-1',
        agentName: 'SchedulingValidationAgent',
        stepName: 'Scheduling',
        sequence: 4,
        candidateRevision: 2,
        status: 3,
        input: {},
        output: {
          contractVersion: 2,
          status: 'CandidateReady',
          warnings: [],
          constraints: [],
          candidateTasks: [],
          candidateIrrigation: [],
          candidateReservations: [],
          cropHealthCandidateTasks: [],
          cropHealthGuidance: {
            cropHealthObservation: 'Yellowing was visible on the submitted leaf image.',
            possibleConcern: 'This may indicate a crop-health issue.',
            uncertaintyGuidance: 'One image does not confirm a diagnosis.',
            prePlantingActions: ['Complete field sanitation before planting.'],
            monitoringActions: ['Monitor the crop for recurring symptoms during early growth.'],
            escalationGuidance: 'Request further assessment if symptoms spread.',
            whyThisIsRecommended: 'The guidance is based on reviewed inspection evidence.',
            decision: 'PendingDecision',
          },
        },
      }],
    }
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      if (url === '/task-approval/workflows/workflow-1') return { data: pendingGuidance } as never
      if (url === '/crop-plans/plan-1/pre-planting-assessment') return { data: null } as never
      throw new Error('Unexpected GET ' + url)
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: pendingGuidance } as never)

    render(
      <MemoryRouter initialEntries={['/task-approval/workflows/workflow-1']}>
        <AuthContext.Provider value={{ user: officer, token: 'token', isAuthenticated: true, isLoading: false, passwordChangeUser: null, hasPasswordChangeSession: false, login: vi.fn(), changeTemporaryPassword: vi.fn(), logout: vi.fn() }}>
          <Routes><Route path="/task-approval/workflows/:id" element={<WorkflowReviewPage />} /></Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Yellowing was visible on the submitted leaf image.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /approve workflow/i })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: /include guidance/i }))
    await userEvent.click(screen.getByRole('button', { name: /confirm guidance decision/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(
      '/task-approval/workflows/workflow-1/crop-health-guidance-decision',
      expect.objectContaining({
        candidateRevision: 2,
        expectedWorkflowVersion: 7,
        decision: 'Included',
        rejectionReason: null,
      }),
    ))
  })
})
