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
})
