import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, AxiosHeaders } from 'axios'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import type { Member3Handoff, PagedResult, WeatherResourceRunResult, WeatherResourceWorkItem } from '../types'
import { WeatherResourceWorkQueuePanel } from './WeatherResourceWorkQueuePanel'

const queueUrl = '/crop-plans/weather-resource-work-queue'
const handoffUrl = '/crop-plans/plan-1/member-3-handoff'
const runUrl = '/crop-plans/plan-1/run-weather-resource-analysis'

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
  cropVarietyName: 'BG 358',
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2027-01-01',
  candidateRevision: 1,
  workflowVersion: 3,
  stepStatus: 1,
  readyAt: '2026-09-27T08:00:00Z',
  startedAt: null,
  errorCode: null,
  errorMessageSafe: null,
}

const handoff: Member3Handoff = {
  workflowId: 'workflow-1',
  cropPlanRequestId: 'plan-1',
  fieldId: 'field-1',
  cropCycleId: null,
  fieldLocationContext: 'Paddy Block A at Anuradhapura',
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2027-01-01',
  fieldAnalysisSummary: 'Drainage preparation is required before planting.',
  priority: 'High',
  warnings: ['Human review remains required.'],
  requiresHumanReview: true,
  fieldSuitability: 'SuitableWithConditions',
  soilAssessment: 'Loamy soil in moderate condition.',
  waterAssessment: 'Canal water is adequate.',
  drainageAssessment: 'Drainage is poor.',
  fieldPreparationRequirements: ['Clear drainage channels.'],
  plantingReadiness: 'RequiresPreparation',
  identifiedRisks: ['PoorDrainage'],
  recommendedPrePlantingActions: ['Address the drainage concern.'],
}

const analyzed: WeatherResourceRunResult = {
  workflowId: 'workflow-1',
  cropPlanRequestId: 'plan-1',
  weatherResourceStepId: 'step-1',
  status: 'Analyzed',
  weatherRisk: 'Medium',
  requiresHumanReview: true,
  warnings: ['Urea stock is below the verified requirement.'],
  requirementStatus: 'Insufficient',
}

function paged(items: WeatherResourceWorkItem[], page = 1, totalCount = items.length): PagedResult<WeatherResourceWorkItem> {
  return { items, page, pageSize: 10, totalCount, totalPages: Math.max(1, Math.ceil(totalCount / 10)) }
}

function httpError(status: number, code?: string) {
  const headers = new AxiosHeaders()
  return new AxiosError('Request failed', 'ERR_BAD_RESPONSE', { headers }, undefined, {
    status,
    statusText: String(status),
    headers,
    config: { headers },
    data: { error: { code, message: 'Provider stack trace: secret-internal-detail' } },
  })
}

type GetConfig = { params?: Record<string, unknown> } | undefined

function mockGet(handler: (url: string, config: GetConfig) => unknown) {
  return vi.spyOn(api, 'get').mockImplementation(async (url: string, config?: unknown) => ({ data: await handler(url, config as GetConfig) }) as never)
}

function defaultGet(queue: () => PagedResult<WeatherResourceWorkItem> = () => paged([item])) {
  return mockGet((url) => {
    if (url === queueUrl) return queue()
    if (url === handoffUrl) return handoff
    throw new Error('Unexpected GET ' + url)
  })
}

function queueCalls(get: ReturnType<typeof mockGet>) {
  return get.mock.calls.filter(([url]) => url === queueUrl)
}

async function openReview(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: /review north farm/i }))
  return screen.findByRole('region', { name: /handoff review/i })
}

afterEach(() => vi.restoreAllMocks())

describe('WeatherResourceWorkQueuePanel', () => {
  it('loads the queue through the dedicated endpoint and shows safe plan metadata', async () => {
    const get = defaultGet(() => paged([item], 1, 1))

    render(<WeatherResourceWorkQueuePanel />)

    expect(screen.getByRole('status')).toHaveTextContent(/loading/i)
    expect(await screen.findByText('North Farm')).toBeInTheDocument()
    expect(screen.getByText(/Paddy Block A/)).toBeInTheDocument()
    expect(screen.getByText(/Rice/)).toBeInTheDocument()
    expect(screen.getByText(/BG 358/)).toBeInTheDocument()
    expect(screen.getByText('Plan the next paddy season.')).toBeInTheDocument()
    expect(screen.getByText(/1 crop plan waiting/i)).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith(queueUrl, expect.objectContaining({
      params: { page: 1, pageSize: 10, search: undefined, sortBy: 'readyAt', sortDirection: 'asc' },
    }))
  })

  it('shows the empty state when nothing is waiting', async () => {
    defaultGet(() => paged([]))

    render(<WeatherResourceWorkQueuePanel />)

    expect(await screen.findByText('No crop plans are waiting for Weather/Resource Analysis.')).toBeInTheDocument()
  })

  it('contains a queue load failure and retries', async () => {
    let fail = true
    const get = mockGet((url) => {
      if (url !== queueUrl) throw new Error('Unexpected GET ' + url)
      if (fail) throw httpError(500)
      return paged([item])
    })
    const user = userEvent.setup()

    render(<WeatherResourceWorkQueuePanel />)

    expect(await screen.findByText('Unable to load Weather/Resource work.')).toBeInTheDocument()
    expect(screen.queryByText(/secret-internal-detail/)).not.toBeInTheDocument()
    fail = false
    await user.click(screen.getByRole('button', { name: /retry/i }))
    expect(await screen.findByText('North Farm')).toBeInTheDocument()
    expect(queueCalls(get)).toHaveLength(2)
  })

  it('reviews only the existing safe Member 3 handoff', async () => {
    const get = defaultGet()
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)

    const review = await openReview(user)

    expect(get).toHaveBeenCalledWith(handoffUrl, expect.anything())
    expect(within(review).getByText('Drainage preparation is required before planting.')).toBeInTheDocument()
    expect(within(review).getByText('Suitable with conditions')).toBeInTheDocument()
    expect(within(review).getByText('Requires preparation')).toBeInTheDocument()
    expect(within(review).getByText('Clear drainage channels.')).toBeInTheDocument()
    expect(within(review).getByText('Address the drainage concern.')).toBeInTheDocument()
    expect(within(review).getByText(/Poor drainage/i)).toBeInTheDocument()
    expect(within(review).getByText('Human review remains required.')).toBeInTheDocument()
    const requested = get.mock.calls.map(([url]) => url)
    expect(requested.every((url) => url === queueUrl || url === handoffUrl)).toBe(true)
    expect(requested.some((url) => /pre-planting|field-analysis|inspections|task-approval/.test(url))).toBe(false)
  })

  it('never renders raw evidence or edit and approval controls', async () => {
    defaultGet()
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)

    const review = await openReview(user)

    expect(within(review).queryByText(/officer notes|risk notes|observation|image|evidence|open issue/i)).not.toBeInTheDocument()
    expect(within(review).queryByRole('textbox')).not.toBeInTheDocument()
    expect(within(review).queryByRole('combobox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /approve|reject|edit|save|submit assessment/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /task approval/i })).not.toBeInTheDocument()
  })

  it('keeps a handoff failure local to the review', async () => {
    mockGet((url) => {
      if (url === queueUrl) return paged([item])
      throw httpError(409, 'MEMBER3_FIELD_CONTEXT_NOT_READY')
    })
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)

    await user.click(await screen.findByRole('button', { name: /review north farm/i }))

    expect(await screen.findByText(/unable to load the field analysis handoff/i)).toBeInTheDocument()
    expect(screen.getByText('North Farm')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /run weather\/resource analysis/i })).not.toBeInTheDocument()
  })

  it('tolerates null text and missing arrays', async () => {
    const sparseItem = { ...item, objective: null, fieldName: null, cropVarietyName: null, farmLocation: null } as WeatherResourceWorkItem
    const sparseHandoff = {
      ...handoff,
      fieldAnalysisSummary: null,
      priority: null,
      fieldSuitability: null,
      warnings: undefined,
      identifiedRisks: null,
      fieldPreparationRequirements: undefined,
      recommendedPrePlantingActions: null,
    } as unknown as Member3Handoff
    mockGet((url) => {
      if (url === queueUrl) return { ...paged([sparseItem]), items: [sparseItem] }
      if (url === handoffUrl) return sparseHandoff
      throw new Error('Unexpected GET ' + url)
    })
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)

    const review = await openReview(user)

    expect(within(review).getByText(/no field analysis summary/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /run weather\/resource analysis/i })).toBeEnabled()
  })

  it('tolerates a queue response without an items array', async () => {
    mockGet((url) => {
      if (url === queueUrl) return { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
      throw new Error('Unexpected GET ' + url)
    })

    render(<WeatherResourceWorkQueuePanel />)

    expect(await screen.findByText('No crop plans are waiting for Weather/Resource Analysis.')).toBeInTheDocument()
  })

  it('disables Run while the analysis is already running elsewhere', async () => {
    defaultGet(() => paged([{ ...item, stepStatus: 2, startedAt: '2026-09-28T08:00:00Z' }]))
    const post = vi.spyOn(api, 'post')
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)

    expect(await screen.findByText(/in progress/i)).toBeInTheDocument()
    await openReview(user)

    expect(screen.getByRole('button', { name: /run weather\/resource analysis/i })).toBeDisabled()
    expect(post).not.toHaveBeenCalled()
  })

  it('sends one POST for rapid duplicate clicks and no client-authored body', async () => {
    defaultGet()
    let resolve: (value: unknown) => void = () => undefined
    const post = vi.spyOn(api, 'post').mockImplementation(() => new Promise((done) => { resolve = done }) as never)
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)
    await openReview(user)

    const run = screen.getByRole('button', { name: /run weather\/resource analysis/i })
    await user.click(run)
    await user.click(run)
    await user.dblClick(run)

    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith(runUrl)
    expect(screen.getByRole('button', { name: /running analysis/i })).toBeDisabled()
    resolve({ data: analyzed })
    expect(await screen.findByText(/scheduling validation is next/i)).toBeInTheDocument()
  })

  it('shows success for Analyzed and refreshes the queue', async () => {
    let analysed = false
    const get = defaultGet(() => (analysed ? paged([]) : paged([item])))
    vi.spyOn(api, 'post').mockImplementation(async () => {
      analysed = true
      return { data: analyzed } as never
    })
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)
    await openReview(user)

    await user.click(screen.getByRole('button', { name: /run weather\/resource analysis/i }))

    const success = await screen.findByText(/scheduling validation is next/i)
    expect(success).toHaveTextContent(/not been approved/i)
    expect(screen.getByText('Urea stock is below the verified requirement.')).toBeInTheDocument()
    expect(screen.getByText(/Medium/)).toBeInTheDocument()
    expect(await screen.findByText('No crop plans are waiting for Weather/Resource Analysis.')).toBeInTheDocument()
    expect(queueCalls(get)).toHaveLength(2)
    expect(screen.queryByRole('button', { name: /run weather\/resource analysis/i })).not.toBeInTheDocument()
  })

  it('treats SafeFailure as a contained failure, not success', async () => {
    defaultGet()
    vi.spyOn(api, 'post').mockResolvedValue({
      data: { ...analyzed, status: 'SafeFailure', weatherRisk: 'Unknown', warnings: ['AI service is unavailable or timed out.'] },
    } as never)
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)
    await openReview(user)

    await user.click(screen.getByRole('button', { name: /run weather\/resource analysis/i }))

    expect(await screen.findByText(/could not complete safely/i)).toBeInTheDocument()
    expect(screen.getByText('AI service is unavailable or timed out.')).toBeInTheDocument()
    expect(screen.queryByText(/scheduling validation is next/i)).not.toBeInTheDocument()
  })

  it('contains an already-running conflict, keeps the review and refreshes the queue', async () => {
    const get = defaultGet()
    vi.spyOn(api, 'post').mockRejectedValue(httpError(409, 'WEATHER_RESOURCE_ALREADY_RUNNING'))
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)
    const review = await openReview(user)

    await user.click(screen.getByRole('button', { name: /run weather\/resource analysis/i }))

    expect(await screen.findByText(/already running or has moved on/i)).toBeInTheDocument()
    expect(within(review).getByText('Drainage preparation is required before planting.')).toBeInTheDocument()
    await waitFor(() => expect(queueCalls(get)).toHaveLength(2))
    expect(screen.queryByText(/secret-internal-detail/)).not.toBeInTheDocument()
  })

  it('contains a server error without leaking detail and keeps Run available', async () => {
    defaultGet()
    vi.spyOn(api, 'post').mockRejectedValue(httpError(500))
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)
    const review = await openReview(user)

    await user.click(screen.getByRole('button', { name: /run weather\/resource analysis/i }))

    expect(await screen.findByText(/could not be started/i)).toBeInTheDocument()
    expect(screen.queryByText(/secret-internal-detail/)).not.toBeInTheDocument()
    expect(within(review).getByText('Drainage preparation is required before planting.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /run weather\/resource analysis/i })).toBeEnabled()
  })

  it('searches and pages through the queue endpoint only', async () => {
    const many = Array.from({ length: 10 }, (_, index) => ({ ...item, workflowId: `workflow-${index}`, cropPlanRequestId: `plan-${index}`, farmName: `Farm ${index}` }))
    const get = mockGet((url, config) => {
      if (url !== queueUrl) throw new Error('Unexpected GET ' + url)
      const page = Number(config?.params?.page ?? 1)
      return paged(page === 1 ? many : [{ ...item, farmName: 'Last Farm' }], page, 11)
    })
    const user = userEvent.setup()
    render(<WeatherResourceWorkQueuePanel />)

    await screen.findByText('Farm 0')
    await user.click(screen.getByRole('button', { name: /next/i }))
    expect(await screen.findByText('Last Farm')).toBeInTheDocument()
    expect(get).toHaveBeenLastCalledWith(queueUrl, expect.objectContaining({ params: expect.objectContaining({ page: 2 }) }))

    await user.type(screen.getByRole('textbox', { name: /search weather\/resource work/i }), 'paddy')
    await user.click(screen.getByRole('button', { name: /^search$/i }))
    await waitFor(() => expect(get).toHaveBeenLastCalledWith(queueUrl, expect.objectContaining({ params: expect.objectContaining({ page: 1, search: 'paddy' }) })))
    expect(get.mock.calls.every(([url]) => url === queueUrl)).toBe(true)
  })
})
