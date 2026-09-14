import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { CropIssues } from './CropIssues'
import { InspectionsPage } from './InspectionsPage'

const emptyPage = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }

afterEach(() => {
  vi.restoreAllMocks()
})

describe('Inspection operations pages', () => {
  it('loads inspections and applies status filters', async () => {
    const get = vi.spyOn(api, 'get').mockImplementation((url: string, config?: unknown) => {
      const params = (config as { params?: Record<string, unknown> } | undefined)?.params
      if (url === '/crop-planning/fields') {
        return Promise.resolve({ data: { ...emptyPage, items: [{ id: 'field-1', farmId: 'farm-1', name: 'North Field', area: 2, soilType: 'Loam', isActive: true }] } })
      }

      return Promise.resolve({
        data: {
          ...emptyPage,
          items: [{ id: 'inspection-1', fieldId: 'field-1', inspectorUserId: 'officer-1', scheduledAt: '2026-09-14T08:00:00Z', status: 3, summary: params?.status ? 'Filtered inspection' : 'Submitted inspection' }],
        },
      })
    })

    render(
      <MemoryRouter>
        <InspectionsPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Submitted inspection')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText(/status/i), '3')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))

    await waitFor(() => expect(get).toHaveBeenLastCalledWith('/inspections', expect.objectContaining({ params: expect.objectContaining({ status: '3' }) })))
  })

  it('escalates a serious crop issue from the issue list', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({
      data: {
        ...emptyPage,
        items: [{ id: 'issue-1', fieldInspectionId: 'inspection-1', title: 'Leaf yellowing', description: 'Spreading', severity: 3, status: 1 }],
      },
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })

    render(
      <MemoryRouter>
        <CropIssues />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Leaf yellowing')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /escalate/i }))
    await userEvent.click(within(screen.getByRole('dialog', { name: /escalate crop issue/i })).getByRole('button', { name: /^escalate$/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/inspections/issues/issue-1/escalate'))
  })
})


