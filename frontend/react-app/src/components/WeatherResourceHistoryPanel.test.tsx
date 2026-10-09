import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import type { PagedResult, WeatherResourceHistoryDetail, WeatherResourceHistoryItem, WeatherResourceResult } from '../types'
import { WeatherResourceHistoryPanel } from './WeatherResourceHistoryPanel'

const historyUrl = '/crop-plans/weather-resource-history'
const detailUrl = `${historyUrl}/workflow-1`

const entry: WeatherResourceHistoryItem = {
  workflowId: 'workflow-1',
  cropPlanRequestId: 'plan-1',
  weatherResourceStepId: 'step-1',
  objective: 'Plan the next paddy season.',
  farmName: 'North Farm',
  farmLocation: 'Anuradhapura',
  fieldName: 'Paddy Block A',
  cropName: 'Rice',
  cropVarietyName: 'BG 358',
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2027-01-01',
  stepStatus: 3,
  analyzedAt: '2026-09-28T08:00:00Z',
  runByName: 'Rani Resource',
  status: 'Analyzed',
  weatherRisk: 'Medium',
  requirementStatus: 'Insufficient',
  requiresHumanReview: true,
  headline: 'Medium weather risk: 12 mm of rain is expected on 2026-09-15.',
}

// SAMPLE result only, not agronomic advice.
const result: WeatherResourceResult = {
  workflowId: 'workflow-1',
  status: 'Analyzed',
  requiresHumanReview: true,
  warnings: ['Urea: 50 kg required; 30 kg available after reservations; shortage 20 kg.'],
  weatherRisk: 'Medium',
  weatherSummary: 'Forecast for Anuradhapura from 2026-09-15 to 2026-09-15: Medium weather risk.',
  recommendations: ['Obtain at least 20 kg more Urea, or revise the crop plan, before scheduling.'],
  resourceRequirements: [{
    ruleId: 'rule-1', resourceId: 'urea', resourceName: 'Urea', unit: 'kg', requiredQuantity: 50, availableQuantity: 30,
    reservedQuantity: 40, shortageQuantity: 20, sufficient: false, requirementStatus: 'Insufficient',
    basis: '100 kg/acre x 0.5 acre = 50 kg', reason: null,
  }],
  requirementStatus: 'Insufficient',
  reason: 'Required resource quantity exceeds currently available inventory: Urea (shortage 20 kg). Weather risk is Medium.',
  weatherRiskAssessment: {
    riskLevel: 'Medium',
    headline: 'Medium weather risk: 12 mm of rain is expected on 2026-09-15.',
    explanation: 'The heaviest daily rain of 12 mm on 2026-09-15 reaches the 10 mm Medium threshold.',
    contributingFactors: [
      { metric: 'DailyRainfall', label: 'Heaviest daily rain', value: 12, unit: 'mm', observedOn: '2026-09-15', mediumThreshold: 10, highThreshold: 30, level: 'Medium', detail: '' },
      { metric: 'MaxTemperature', label: 'Highest temperature', value: 31, unit: 'C', observedOn: '2026-09-15', mediumThreshold: 34, highThreshold: 38, level: 'Low', detail: '' },
    ],
    potentialImpacts: ['Rain can delay land preparation and wash fertilizer away.'],
    recommendedActions: [{ action: 'Clear drainage channels before the rain.', timing: 'Before 2026-09-15', priority: 'Medium' }],
    monitoringAdvice: 'Check the forecast again the day before field work.',
    generatedBy: 'OpenAI',
  },
}

function paged(items: WeatherResourceHistoryItem[]): PagedResult<WeatherResourceHistoryItem> {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 }
}

type GetConfig = { params?: Record<string, unknown> } | undefined

function mockGet(detail: WeatherResourceHistoryDetail | (() => never) = { plan: entry, result }, list = () => paged([entry])) {
  return vi.spyOn(api, 'get').mockImplementation(async (url: string, config?: unknown) => {
    if (url === historyUrl) return { data: list(), config: config as GetConfig } as never
    if (url === detailUrl) return { data: typeof detail === 'function' ? detail() : detail } as never
    throw new Error('Unexpected GET ' + url)
  })
}

afterEach(() => vi.restoreAllMocks())

describe('WeatherResourceHistoryPanel', () => {
  it('lists saved analyses with their plan and AI result summary', async () => {
    mockGet()

    render(<WeatherResourceHistoryPanel />)

    expect(await screen.findByText('North Farm')).toBeInTheDocument()
    expect(screen.getByText(/Paddy Block A · Rice · BG 358/)).toBeInTheDocument()
    expect(screen.getByText('Plan the next paddy season.')).toBeInTheDocument()
    expect(screen.getByText(entry.headline!)).toBeInTheDocument()
    expect(screen.getByText('Rani Resource')).toBeInTheDocument()
    expect(screen.getByText('Completed')).toBeInTheDocument()
    expect(screen.getByText('1 saved analysis')).toBeInTheDocument()
  })

  it('shows the plan with the exact AI analysis stored for it', async () => {
    const get = mockGet()
    const user = userEvent.setup()
    render(<WeatherResourceHistoryPanel />)

    await user.click(await screen.findByRole('button', { name: /view analysis for north farm/i }))

    const saved = await screen.findByRole('region', { name: /saved analysis for north farm/i })
    expect(get).toHaveBeenCalledWith(detailUrl, expect.anything())
    expect(await within(saved).findByText(result.weatherRiskAssessment!.explanation)).toBeInTheDocument()
    expect(within(saved).getByText('AI explanation (OpenAI)')).toBeInTheDocument()
    expect(within(saved).getByRole('heading', { name: 'Why the risk is Medium' })).toBeInTheDocument()
    expect(within(saved).getByText('Heaviest daily rain')).toBeInTheDocument()
    expect(within(saved).getByText('Rain can delay land preparation and wash fertilizer away.')).toBeInTheDocument()
    expect(within(saved).getByRole('heading', { name: 'What the farmer should do' })).toBeInTheDocument()
    expect(within(saved).getByText('Clear drainage channels before the rain.')).toBeInTheDocument()
    expect(within(saved).getByText(/Check the forecast again the day before field work/)).toBeInTheDocument()
    expect(within(saved).getByText('100 kg/acre x 0.5 acre = 50 kg')).toBeInTheDocument()
    expect(within(saved).getByText(result.recommendations![0])).toBeInTheDocument()
    expect(within(saved).getByText('Plan the next paddy season.')).toBeInTheDocument()
    expect(within(saved).queryByRole('button', { name: /run|approve|reserve/i })).not.toBeInTheDocument()
  })

  it('shows older results without an AI explanation and contained safe failures', async () => {
    mockGet({
      plan: { ...entry, status: 'SafeFailure', weatherRisk: 'Unknown', headline: null },
      result: { ...result, status: 'SafeFailure', weatherRisk: 'Unknown', weatherSummary: '', weatherRiskAssessment: undefined, recommendations: [] },
    })
    const user = userEvent.setup()
    render(<WeatherResourceHistoryPanel />)

    await user.click(await screen.findByRole('button', { name: /view analysis for north farm/i }))

    expect(await screen.findByText(/did not complete safely/i)).toBeInTheDocument()
    expect(screen.getByText(/saved before detailed weather explanations/i)).toBeInTheDocument()
    expect(screen.queryByText('What the farmer should do')).not.toBeInTheDocument()
  })

  it('keeps a detail failure local to the selected entry', async () => {
    mockGet(() => { throw new Error('Provider stack trace: secret-internal-detail') })
    const user = userEvent.setup()
    render(<WeatherResourceHistoryPanel />)

    await user.click(await screen.findByRole('button', { name: /view analysis for north farm/i }))

    expect(await screen.findByText(/unable to load the saved analysis/i)).toBeInTheDocument()
    expect(screen.getByText('North Farm')).toBeInTheDocument()
    expect(screen.queryByText(/secret-internal-detail/)).not.toBeInTheDocument()
  })

  it('shows the empty state, contains a load failure and retries', async () => {
    let fail = true
    const get = mockGet(undefined, () => {
      if (fail) throw new Error('down')
      return paged([])
    })
    const user = userEvent.setup()
    render(<WeatherResourceHistoryPanel />)

    expect(await screen.findByText(/unable to load the analysis history/i)).toBeInTheDocument()
    fail = false
    await user.click(screen.getByRole('button', { name: /retry/i }))

    expect(await screen.findByText('No saved analyses yet')).toBeInTheDocument()
    expect(get.mock.calls.filter(([url]) => url === historyUrl)).toHaveLength(2)
  })

  it('searches through the history endpoint and reloads when the dashboard saves a new analysis', async () => {
    const get = mockGet()
    const user = userEvent.setup()
    const { rerender } = render(<WeatherResourceHistoryPanel reloadKey={0} />)
    await screen.findByText('North Farm')

    await user.type(screen.getByRole('textbox', { name: /search analysis history/i }), 'paddy')
    await user.click(screen.getByRole('button', { name: /search history/i }))
    await waitFor(() => expect(get).toHaveBeenLastCalledWith(historyUrl, expect.objectContaining({ params: expect.objectContaining({ search: 'paddy', page: 1 }) })))

    rerender(<WeatherResourceHistoryPanel reloadKey={1} />)
    await waitFor(() => expect(get.mock.calls.filter(([url]) => url === historyUrl)).toHaveLength(3))
  })
})
