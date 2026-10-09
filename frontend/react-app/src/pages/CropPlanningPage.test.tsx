import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AuthContext } from '../auth/AuthContext'
import { CropPlanningPage } from './CropPlanningPage'

vi.mock('./AdminCropManagement', () => ({
  AdminCropManagement: ({ referenceOnly = false }: { referenceOnly?: boolean }) => (
    <div>{referenceOnly ? 'Reference manager interface' : 'Full catalog manager interface'}</div>
  ),
}))

const paged = <T,>(items: T[]) => ({ data: { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 } })

function renderForRole(role: 4 | 5) {
  return render(
    <AuthContext.Provider value={{
      user: { id: 'user-1', fullName: 'Staff', email: 'staff@example.test', role, isActive: true, mustChangePassword: false },
      token: 'token',
      isAuthenticated: true,
      isLoading: false,
      passwordChangeUser: null,
      hasPasswordChangeSession: false,
      login: vi.fn(),
      changeTemporaryPassword: vi.fn(),
      logout: vi.fn(),
    }}>
      <MemoryRouter><CropPlanningPage /></MemoryRouter>
    </AuthContext.Provider>,
  )
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('CropPlanningPage AI workflow surface', () => {
  it('lets an Admin cancel active requests only after entering a reason', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/workflow-status') || url.includes('/planning-result')) return Promise.reject(new Error('No workflow snapshot'))
      if (url.includes('/farms') || url.includes('/fields') || url.includes('/crop-types')) return Promise.resolve(paged([]))
      return Promise.resolve(paged([
        { id: 'active-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Active', status: 3, statusCode: 'field_analysis_running', statusLabel: 'Field Analysis in Progress', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' },
        { id: 'approved-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Approved', status: 4, statusCode: 'approved', statusLabel: 'Approved', overallStatusCode: 'approved', overallStatusLabel: 'Approved', createdAt: '2026-09-13T00:00:00Z' },
      ]))
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)

    renderForRole(5)
    await waitFor(() => expect(screen.getByText('AI Workflows')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('tab', { name: /Plan Requests/i }))
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel plan' }))

    const confirm = screen.getByRole('button', { name: 'Confirm cancellation' })
    expect(confirm).toBeDisabled()
    fireEvent.change(screen.getByRole('textbox', { name: /Cancellation reason/i }), { target: { value: 'Farmer selected another crop.' } })
    expect(confirm).toBeEnabled()
    fireEvent.click(confirm)

    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-planning/requests/active-plan/cancel', {
      reason: 'Farmer selected another crop.',
    }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByText(/farmer can now create a replacement plan/i)).toBeInTheDocument()
  })

  it('offers soft removal only for Cancelled and Rejected requests', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/workflow-status') || url.includes('/planning-result')) return Promise.reject(new Error('No workflow snapshot'))
      if (url.includes('/farms') || url.includes('/fields') || url.includes('/crop-types')) return Promise.resolve(paged([]))
      return Promise.resolve(paged([
        { id: 'cancelled-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Cancelled', status: 6, statusCode: 'cancelled', statusLabel: 'Cancelled', overallStatusCode: 'cancelled', overallStatusLabel: 'Cancelled', createdAt: '2026-09-13T00:00:00Z' },
        { id: 'rejected-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Rejected', status: 5, statusCode: 'rejected', statusLabel: 'Rejected', overallStatusCode: 'rejected', overallStatusLabel: 'Rejected', createdAt: '2026-09-13T00:00:00Z' },
        { id: 'approved-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Approved', status: 4, statusCode: 'approved', statusLabel: 'Approved', overallStatusCode: 'approved', overallStatusLabel: 'Approved', createdAt: '2026-09-13T00:00:00Z' },
      ]))
    })
    const remove = vi.spyOn(api, 'delete').mockResolvedValue({ data: undefined } as never)

    renderForRole(5)
    await waitFor(() => expect(screen.getByText('AI Workflows')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('tab', { name: /Plan Requests/i }))

    expect(await screen.findAllByRole('button', { name: 'Delete request' })).toHaveLength(2)
    expect(screen.queryByRole('button', { name: 'Cancel plan' })).not.toBeInTheDocument()
    fireEvent.click(screen.getAllByRole('button', { name: 'Delete request' })[0])
    expect(screen.getByText(/workflow history, evidence, decisions, and audit data will be retained/i)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Remove from list' }))

    await waitFor(() => expect(remove).toHaveBeenCalledWith('/crop-planning/requests/cancelled-plan'))
  })

  it('renders coordinator status, warnings, and downstream step summary', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/workflow-status')) {
        return Promise.resolve({ data: { workflowId: 'workflow-1', cropPlanRequestId: 'plan-1', status: 2, currentStep: 'CropFieldAnalysisAgent', statusCode: 'preliminary_plan_ready', statusLabel: 'Preliminary Plan Ready', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z', steps: [], warnings: ['Provider warning'] } })
      }
      if (url.includes('/planning-result')) {
        return Promise.resolve({ data: { workflowId: 'workflow-1', status: 'Planned', requiresHumanReview: false, warnings: ['Provider warning'], referenceDataStatus: 'Available', objectiveSummary: 'Prepare a safe season plan.', steps: [{ sequence: 1, stepType: 'FieldAnalysis', assignedAgent: 'CropFieldAnalysisAgent' }] } })
      }
      if (url.includes('/farms')) return Promise.resolve(paged([{ id: 'farm-1', name: 'North Farm', location: 'North', totalArea: 10, ownerUserId: 'farmer-1', createdAt: '2026-09-13T00:00:00Z' }]))
      if (url.includes('/fields')) return Promise.resolve(paged([{ id: 'field-1', farmId: 'farm-1', name: 'Field A', area: 2, soilType: 'Loam', isActive: true }]))
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: 'Demo', isActive: true }]))
      return Promise.resolve(paged([{ id: 'plan-1', farmId: 'farm-1', fieldId: 'field-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Plan safely', status: 3, statusCode: 'preliminary_plan_ready', statusLabel: 'Preliminary Plan Ready', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' }]))
    })

    render(<MemoryRouter><CropPlanningPage /></MemoryRouter>)

    await waitFor(() => expect(screen.getByText('AI Workflows')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('tab', { name: /Plan Requests/i }))
    expect(await screen.findByText('Prepare a safe season plan.')).toBeInTheDocument()
    expect(screen.getByText('Provider warning')).toBeInTheDocument()
    expect(screen.getByText(/CropFieldAnalysisAgent/i)).toBeInTheDocument()
  })

  it('requires and submits an approved Sri Lankan district when creating a farm', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/farms') || url.includes('/fields') || url.includes('/crop-types') || url.includes('/requests')) {
        return Promise.resolve(paged([]))
      }
      throw new Error(`Unexpected GET ${url}`)
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)
    render(<MemoryRouter><CropPlanningPage /></MemoryRouter>)

    await waitFor(() => expect(screen.getByRole('button', { name: /add farm/i })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: /add farm/i }))
    fireEvent.change(screen.getByPlaceholderText('Farm name'), { target: { value: 'Wariyapola Farm' } })
    fireEvent.change(screen.getByPlaceholderText('Farm address or town / city'), { target: { value: 'Wariyapola' } })
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'Kurunegala' } })
    fireEvent.change(screen.getByPlaceholderText('2.5'), { target: { value: '10' } })
    fireEvent.click(screen.getByRole('button', { name: /create farm/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-planning/farms', {
      name: 'Wariyapola Farm',
      location: 'Wariyapola',
      district: 'Kurunegala',
      totalArea: 10,
      ownerUserId: null,
    }))
  })

  it('shows Admin Start and Retry actions from authoritative lifecycle codes', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/workflow-status') || url.includes('/planning-result')) return Promise.reject(new Error('No workflow snapshot'))
      if (url.includes('/farms') || url.includes('/fields') || url.includes('/crop-types')) return Promise.resolve(paged([]))
      return Promise.resolve(paged([
        { id: 'pending-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Pending', status: 2, statusCode: 'pending', statusLabel: 'Pending', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' },
        { id: 'failed-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Failed', status: 2, statusCode: 'ai_planning_failed', statusLabel: 'AI Planning Failed — awaiting Admin retry', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' },
        { id: 'running-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Running', status: 2, statusCode: 'ai_planning', statusLabel: 'AI Planning', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' },
      ]))
    })

    renderForRole(5)
    await waitFor(() => expect(screen.getByText('AI Workflows')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('tab', { name: /Plan Requests/i }))

    expect(await screen.findByRole('button', { name: 'Start AI Plan' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry AI Plan' })).toBeInTheDocument()
    expect(screen.getByText('AI planning is running.')).toBeInTheDocument()
  })

  it('does not expose Start or Retry to an Agricultural Officer', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/workflow-status') || url.includes('/planning-result')) return Promise.reject(new Error('No workflow snapshot'))
      if (url.includes('/farms') || url.includes('/fields') || url.includes('/crop-types')) return Promise.resolve(paged([]))
      return Promise.resolve(paged([
        { id: 'pending-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Pending', status: 2, statusCode: 'pending', statusLabel: 'Pending', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' },
        { id: 'failed-plan', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Failed', status: 2, statusCode: 'ai_planning_failed', statusLabel: 'AI Planning Failed — awaiting Admin retry', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' },
      ]))
    })

    renderForRole(4)
    await waitFor(() => expect(screen.getByText('AI Workflows')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('tab', { name: /Plan Requests/i }))

    expect(screen.queryByRole('button', { name: 'Start AI Plan' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Retry AI Plan' })).not.toBeInTheDocument()
  })

  it('uses the same request-scoped endpoint for the Admin Start action', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/workflow-status')) {
        return Promise.resolve({ data: { workflowId: 'workflow-1', cropPlanRequestId: 'plan-1', status: 2, currentStep: 'CropFieldAnalysisAgent', statusCode: 'preliminary_plan_ready', statusLabel: 'Preliminary Plan Ready', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z', steps: [], warnings: [] } })
      }
      if (url.includes('/planning-result')) {
        return Promise.resolve({ data: { workflowId: 'workflow-1', status: 'Planned', requiresHumanReview: false, warnings: [], referenceDataStatus: 'Available', objectiveSummary: 'Plan ready.', steps: [] } })
      }
      if (url.includes('/farms') || url.includes('/fields') || url.includes('/crop-types')) return Promise.resolve(paged([]))
      return Promise.resolve(paged([
        { id: 'plan-1', farmId: 'farm-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Pending', status: 2, statusCode: 'pending', statusLabel: 'Pending', overallStatusCode: 'in_progress', overallStatusLabel: 'In Progress', createdAt: '2026-09-13T00:00:00Z' },
      ]))
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: { requiresHumanReview: false } } as never)

    renderForRole(5)
    await waitFor(() => expect(screen.getByText('AI Workflows')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('tab', { name: /Plan Requests/i }))
    fireEvent.click(await screen.findByRole('button', { name: 'Start AI Plan' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-plans/plan-1/start-ai-workflow'))
  })

  it('opens the reference-only manager for an Agricultural Officer', async () => {
    vi.spyOn(api, 'get').mockResolvedValue(paged([]))

    renderForRole(4)

    fireEvent.click(await screen.findByRole('tab', { name: /Crop Types/i }))
    expect(await screen.findByText('Reference manager interface')).toBeInTheDocument()
    expect(screen.queryByText('Full catalog manager interface')).not.toBeInTheDocument()
  })
})


