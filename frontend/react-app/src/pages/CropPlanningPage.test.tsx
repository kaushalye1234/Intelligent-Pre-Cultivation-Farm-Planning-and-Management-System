import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { CropPlanningPage } from './CropPlanningPage'

const paged = <T,>(items: T[]) => ({ data: { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 } })

afterEach(() => {
  vi.restoreAllMocks()
})

describe('CropPlanningPage AI workflow surface', () => {
  it('renders coordinator status, warnings, and downstream step summary', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/workflow-status')) {
        return Promise.resolve({ data: { workflowId: 'workflow-1', cropPlanRequestId: 'plan-1', status: 2, currentStep: 'CropFieldAnalysisAgent', createdAt: '2026-09-13T00:00:00Z', steps: [], warnings: ['Provider warning'] } })
      }
      if (url.includes('/planning-result')) {
        return Promise.resolve({ data: { workflowId: 'workflow-1', status: 'Planned', requiresHumanReview: false, warnings: ['Provider warning'], referenceDataStatus: 'Available', objectiveSummary: 'Prepare a safe season plan.', steps: [{ sequence: 1, stepType: 'FieldAnalysis', assignedAgent: 'CropFieldAnalysisAgent' }] } })
      }
      if (url.includes('/farms')) return Promise.resolve(paged([{ id: 'farm-1', name: 'North Farm', location: 'North', totalArea: 10, ownerUserId: 'farmer-1', createdAt: '2026-09-13T00:00:00Z' }]))
      if (url.includes('/fields')) return Promise.resolve(paged([{ id: 'field-1', farmId: 'farm-1', name: 'Field A', area: 2, soilType: 'Loam', isActive: true }]))
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: 'Demo', isActive: true }]))
      return Promise.resolve(paged([{ id: 'plan-1', farmId: 'farm-1', fieldId: 'field-1', cropTypeId: 'crop-1', preferredStartDate: '2026-10-01', preferredEndDate: '2027-01-01', budget: 12000, objective: 'Plan safely', status: 3, createdAt: '2026-09-13T00:00:00Z' }]))
    })

    render(<MemoryRouter><CropPlanningPage /></MemoryRouter>)

    await waitFor(() => expect(screen.getByText('AI Workflows')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('tab', { name: /Plan Requests/i }))
    expect(await screen.findByText('Prepare a safe season plan.')).toBeInTheDocument()
    expect(screen.getByText('Provider warning')).toBeInTheDocument()
    expect(screen.getByText(/CropFieldAnalysisAgent/i)).toBeInTheDocument()
  })
})


