import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { CropIssues } from './CropIssues'
import { InspectionsPage } from './InspectionsPage'

const emptyPage = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }

const fields = [
  { id: 'field-1', farmId: 'farm-1', name: 'North Field', area: 2, soilType: 'Loam', isActive: true },
]

const inspections = [
  { id: 'inspection-scheduled', fieldId: 'field-1', inspectorUserId: 'officer-1', scheduledAt: '2026-09-14T08:00:00Z', status: 1, summary: 'Scheduled inspection' },
  { id: 'inspection-progress', fieldId: 'field-1', inspectorUserId: 'officer-1', scheduledAt: '2026-09-14T09:00:00Z', status: 2, summary: 'In progress inspection' },
  { id: 'inspection-completed', fieldId: 'field-1', inspectorUserId: 'officer-1', scheduledAt: '2026-09-14T10:00:00Z', status: 3, summary: 'Completed inspection' },
  { id: 'inspection-escalated', fieldId: 'field-1', inspectorUserId: 'officer-1', scheduledAt: '2026-09-14T11:00:00Z', status: 4, summary: 'Escalated inspection' },
  { id: 'inspection-cancelled', fieldId: 'field-1', inspectorUserId: 'officer-1', scheduledAt: '2026-09-14T12:00:00Z', status: 5, summary: 'Cancelled inspection' },
  { id: 'inspection-scheduled-2', fieldId: 'field-1', inspectorUserId: 'officer-1', scheduledAt: '2026-09-15T08:00:00Z', status: 1, summary: 'Second scheduled inspection' },
]

function mockInspectionLoad(items = inspections) {
  return vi.spyOn(api, 'get').mockImplementation((url: string, config?: unknown) => {
    if (url === '/crop-planning/fields') {
      return Promise.resolve({ data: { ...emptyPage, items: fields } })
    }

    const params = (config as { params?: Record<string, unknown> } | undefined)?.params
    const status = params?.status ? Number(params.status) : null
    const filteredItems = status ? items.filter((inspection) => inspection.status === status) : items
    return Promise.resolve({ data: { ...emptyPage, items: filteredItems } })
  })
}

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

  it('shows status counts for the currently loaded and filtered results', async () => {
    mockInspectionLoad()

    render(
      <MemoryRouter>
        <InspectionsPage />
      </MemoryRouter>,
    )

    const summary = await screen.findByRole('region', { name: /current inspection results/i })
    expect(within(summary).getByText('6 inspections')).toBeInTheDocument()
    expect(within(summary).getByLabelText('Scheduled: 2')).toBeInTheDocument()
    expect(within(summary).getByLabelText('In progress: 1')).toBeInTheDocument()
    expect(within(summary).getByLabelText('Completed: 1')).toBeInTheDocument()
    expect(within(summary).getByLabelText('Escalated: 1')).toBeInTheDocument()
    expect(within(summary).getByLabelText('Cancelled: 1')).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText(/^status$/i), '4')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))

    await waitFor(() => expect(within(summary).getByText('1 inspection')).toBeInTheDocument())
    expect(within(summary).getByLabelText('Scheduled: 0')).toBeInTheDocument()
    expect(within(summary).getByLabelText('Escalated: 1')).toBeInTheDocument()
  })

  it('opens one action menu at a time and supports keyboard navigation', async () => {
    mockInspectionLoad()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <InspectionsPage />
      </MemoryRouter>,
    )

    const firstTrigger = await screen.findByRole('button', { name: /more actions for scheduled inspection/i })
    const secondTrigger = screen.getByRole('button', { name: /more actions for in progress inspection/i })

    firstTrigger.focus()
    await user.keyboard('{Enter}')
    expect(firstTrigger).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('menuitem', { name: /^image$/i })).toHaveFocus()

    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: /^submit$/i })).toHaveFocus()

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(firstTrigger).toHaveFocus()

    await user.click(firstTrigger)
    await user.click(secondTrigger)
    expect(firstTrigger).toHaveAttribute('aria-expanded', 'false')
    expect(secondTrigger).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getAllByRole('menu')).toHaveLength(1)
  })

  it('closes the action menu after an outside click', async () => {
    mockInspectionLoad()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <InspectionsPage />
      </MemoryRouter>,
    )

    await user.click(await screen.findByRole('button', { name: /more actions for scheduled inspection/i }))
    expect(screen.getByRole('menu')).toBeInTheDocument()

    await user.click(document.body)
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('preserves the existing conditional inspection actions', async () => {
    mockInspectionLoad()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <InspectionsPage />
      </MemoryRouter>,
    )

    await user.click(await screen.findByRole('button', { name: /more actions for completed inspection/i }))
    expect(screen.queryByRole('menuitem', { name: /^submit$/i })).not.toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /^close$/i })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /more actions for cancelled inspection/i }))
    expect(screen.getByRole('menuitem', { name: /^submit$/i })).toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: /^close$/i })).not.toBeInTheDocument()
  })

  it('keeps image, submit and close wired to the existing handlers', async () => {
    mockInspectionLoad([inspections[0]])
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <InspectionsPage />
      </MemoryRouter>,
    )

    const trigger = await screen.findByRole('button', { name: /more actions for scheduled inspection/i })
    await user.click(trigger)
    await user.click(screen.getByRole('menuitem', { name: /^image$/i }))
    expect(screen.getByRole('dialog', { name: /upload inspection image/i })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /close dialog/i }))

    await user.click(trigger)
    await user.click(screen.getByRole('menuitem', { name: /^submit$/i }))
    expect(screen.getByRole('dialog', { name: /submit inspection/i })).toBeInTheDocument()
    await user.click(within(screen.getByRole('dialog', { name: /submit inspection/i })).getByRole('button', { name: /^submit$/i }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/inspections/inspection-scheduled/submit'))

    await user.click(trigger)
    await user.click(screen.getByRole('menuitem', { name: /^close$/i }))
    expect(screen.getByRole('dialog', { name: /close inspection/i })).toBeInTheDocument()
    await user.click(within(screen.getByRole('dialog', { name: /close inspection/i })).getByRole('button', { name: /^close$/i }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/inspections/inspection-scheduled/close'))
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


