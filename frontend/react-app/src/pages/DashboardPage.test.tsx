import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AuthContext } from '../auth/AuthContext'
import type { ApplicationRole, DashboardSummary, UserProfile, WeatherResourceWorkItem } from '../types'
import { DashboardPage } from './DashboardPage'

const summaryUrl = '/dashboard/summary'
const queueUrl = '/crop-plans/weather-resource-work-queue'

const summary: DashboardSummary = {
  usersByRole: [],
  activeFarms: 4,
  activeCropPlans: 6,
  openCropIssues: 1,
  lowStockResources: 2,
  pendingTasks: 3,
  pendingApprovals: 5,
}

const item: WeatherResourceWorkItem = {
  workflowId: 'workflow-1',
  cropPlanRequestId: 'plan-1',
  weatherResourceStepId: 'step-1',
  objective: 'Plan the next paddy season.',
  farmName: 'North Farm',
  farmLocation: 'Anuradhapura',
  fieldId: 'field-1',
  fieldName: 'Paddy Block A',
  cropName: 'Rice',
  cropVarietyName: null,
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2027-01-01',
  candidateRevision: 1,
  workflowVersion: 1,
  stepStatus: 1,
  readyAt: '2026-09-27T08:00:00Z',
  startedAt: null,
  errorCode: null,
  errorMessageSafe: null,
}

function userWithRole(role: ApplicationRole): UserProfile {
  return { id: `user-${role}`, fullName: 'Staff User', email: 'staff@example.test', role, isActive: true, mustChangePassword: false }
}

function mockApi() {
  return vi.spyOn(api, 'get').mockImplementation(async (url: string) => {
    if (url === summaryUrl) return { data: summary } as never
    if (url === queueUrl) return { data: { items: [item], page: 1, pageSize: 10, totalCount: 7, totalPages: 1 } } as never
    throw new Error('Unexpected GET ' + url)
  })
}

function renderDashboard(role: ApplicationRole) {
  render(
    <AuthContext.Provider value={{ user: userWithRole(role), token: 'token', isAuthenticated: true, isLoading: false, passwordChangeUser: null, hasPasswordChangeSession: false, login: vi.fn(), changeTemporaryPassword: vi.fn(), logout: vi.fn() }}>
      <DashboardPage />
    </AuthContext.Provider>,
  )
}

afterEach(() => vi.restoreAllMocks())

describe('DashboardPage Weather/Resource queue', () => {
  it('shows the queue to Resource Officers without changing the Member 4 pending-task metric', async () => {
    const get = mockApi()

    renderDashboard(3)

    expect(await screen.findByRole('heading', { name: 'Resource Dashboard' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Weather/Resource Analysis Queue' })).toBeInTheDocument()
    expect(await screen.findByText('7 crop plans waiting for Weather/Resource Analysis')).toBeInTheDocument()

    const pendingTasks = screen.getAllByText('Pending tasks')[0].closest('article')
    expect(pendingTasks).toHaveTextContent('3')
    expect(pendingTasks).toHaveTextContent('Farm tasks waiting for action.')
    expect(pendingTasks).not.toHaveTextContent('7')
    expect(screen.getAllByText('Low stock resources').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Pending approvals').length).toBeGreaterThan(0)
    expect(get.mock.calls.filter(([url]) => url === summaryUrl)).toHaveLength(1)
  })

  it('does not reload the dashboard summary when the queue is searched', async () => {
    const get = mockApi()
    const user = userEvent.setup()
    renderDashboard(3)

    await screen.findByText('North Farm')
    await user.type(screen.getByRole('textbox', { name: /search weather\/resource work/i }), 'paddy')
    await user.click(screen.getByRole('button', { name: /^search$/i }))

    await waitFor(() => expect(get.mock.calls.filter(([url]) => url === queueUrl)).toHaveLength(2))
    expect(get.mock.calls.filter(([url]) => url === summaryUrl)).toHaveLength(1)
  })

  it.each([
    ['Field Officer', 2, 'Inspection Dashboard'],
    ['Agricultural Officer', 4, 'Agricultural Officer Dashboard'],
    ['Admin', 5, 'Admin Dashboard'],
  ] as const)('does not show the queue to %s', async (_label, role, title) => {
    const get = mockApi()

    renderDashboard(role)

    expect(await screen.findByRole('heading', { name: title })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Weather/Resource Analysis Queue' })).not.toBeInTheDocument()
    expect(get.mock.calls.some(([url]) => url === queueUrl)).toBe(false)
  })
})
