import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import type { ResourceRequirementRecommendation, ResourceRequirementResearchResponse } from '../resourceRequirementResearch'
import { ResourceRequirementResearchPanel } from './ResourceRequirementResearchPanel'

// SAMPLE TEST DATA ONLY: rates below are fixtures, not agricultural recommendations.
const paged = <T,>(items: T[]) => ({ data: { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 } })
const crops = [{ id: 'crop-1', name: 'Chili', isActive: true }, { id: 'crop-2', name: 'Old crop', isActive: false }]
const resources = [
  { id: 'res-1', resourceCategoryId: 'cat-1', name: 'Urea', unit: 'kg', isActive: true },
  { id: 'res-2', resourceCategoryId: 'cat-1', name: 'Retired fertilizer', unit: 'kg', isActive: false },
]

function recommendation(quantity: number, overrides: Partial<ResourceRequirementRecommendation> = {}): ResourceRequirementRecommendation {
  return {
    id: `rec-${quantity}`,
    quantityPerArea: quantity,
    resourceUnit: 'kg',
    areaUnit: 'hectare',
    unitMatchesInventory: true,
    basis: `${quantity} kg/hectare`,
    components: [{ label: 'Basal', quantity, evidenceText: `Urea (kg/ha) ${quantity}` }],
    evidenceStatus: 'Supported',
    cropContext: 'Fertilizer recommendation for chilli',
    source: {
      sourceId: 'local', sourceName: 'Crop guide', organizationName: 'Department of Agriculture',
      originalUrl: 'https://doa.gov.lk/fcrdi-crops/', finalUrl: 'https://doa.gov.lk/fcrdi-crops/',
      sourceCategory: 'Government', country: 'Sri Lanka', sourceClassification: 'Sri Lankan', stage: 1,
      evidenceText: `Urea (kg/ha) ${quantity}`,
    },
    warnings: [],
    ...overrides,
  }
}

function draft(overrides: Partial<ResourceRequirementResearchResponse> = {}): ResourceRequirementResearchResponse {
  return {
    requestId: 'request-1', status: 'PendingVerification', verified: false,
    cropTypeId: 'crop-1', cropName: 'Chili', resourceId: 'res-1', resourceName: 'Urea', resourceUnit: 'kg',
    suggestedQuantityPerArea: 195, suggestedResourceUnit: 'kg', suggestedAreaUnit: 'hectare',
    usedInternationalFallback: false, recommendations: [recommendation(195)], rejectedClaims: [], sources: [], warnings: [],
    ...overrides,
  }
}

async function renderAndResearch(response: ResourceRequirementResearchResponse) {
  vi.spyOn(api, 'get').mockResolvedValue(paged(resources))
  const post = vi.spyOn(api, 'post').mockImplementation((url: string) => {
    if (url === '/resources/requirement-research') return Promise.resolve({ data: response })
    return Promise.resolve({ data: { resourceName: 'Urea', quantityPerArea: 195, resourceUnit: 'kg', areaUnit: 'hectare' } })
  })
  const onSaved = vi.fn()
  render(<ResourceRequirementResearchPanel crops={crops} varieties={[]} onSaved={onSaved} />)
  const resourceSelect = screen.getByRole('combobox', { name: /^Resource/ })
  await waitFor(() => expect(within(resourceSelect).getByRole('option', { name: 'Urea (kg)' })).toBeInTheDocument())
  expect(within(resourceSelect).queryByRole('option', { name: /Retired/ })).not.toBeInTheDocument()
  expect(within(screen.getByRole('combobox', { name: /^Research crop/ })).queryByRole('option', { name: 'Old crop' })).not.toBeInTheDocument()

  fireEvent.change(screen.getByRole('combobox', { name: /^Research crop/ }), { target: { value: 'crop-1' } })
  fireEvent.change(resourceSelect, { target: { value: 'res-1' } })
  fireEvent.click(screen.getByRole('button', { name: 'Research requirement' }))
  await waitFor(() => expect(post).toHaveBeenCalledWith('/resources/requirement-research', {
    cropTypeId: 'crop-1', resourceId: 'res-1', cropVarietyId: null, region: null,
  }))
  return { post, onSaved }
}

afterEach(() => vi.restoreAllMocks())

describe('Resource requirement research panel', () => {
  it('shows a sourced pending draft and saves only after explicit Admin verification', async () => {
    const { post, onSaved } = await renderAndResearch(draft())

    expect(await screen.findByText('Pending verification')).toBeInTheDocument()
    expect(screen.getByText('Suggested requirement: 195 kg / hectare')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Open source/ })).toHaveAttribute('href', 'https://doa.gov.lk/fcrdi-crops/')
    const verify = screen.getByRole('button', { name: 'Verify & Save' })
    expect(verify).toBeDisabled()

    fireEvent.click(screen.getByRole('checkbox', { name: /I checked this value/ }))
    fireEvent.click(verify)

    await waitFor(() => expect(post).toHaveBeenCalledWith('/resources/requirement-research/verify', {
      cropTypeId: 'crop-1', resourceId: 'res-1', cropVarietyId: null, region: null,
      quantityPerArea: 195, resourceUnit: 'kg', areaUnit: 'hectare',
      sourceName: 'Department of Agriculture – Crop guide', sourceUrl: 'https://doa.gov.lk/fcrdi-crops/',
      evidence: 'Urea (kg/ha) 195', researchRequestId: 'request-1',
    }))
    expect(await screen.findByText(/Verified requirement saved: Urea 195 kg\/hectare for Chili/)).toBeInTheDocument()
    expect(onSaved).toHaveBeenCalled()
  })

  it('reject discards the draft without calling the save endpoint', async () => {
    const { post, onSaved } = await renderAndResearch(draft())
    await screen.findByText('Pending verification')

    fireEvent.click(screen.getByRole('button', { name: 'Reject' }))

    expect(screen.getByText('Research result rejected. Nothing was saved.')).toBeInTheDocument()
    expect(screen.queryByText('Suggested requirement: 195 kg / hectare')).not.toBeInTheDocument()
    expect(post.mock.calls.some(([url]) => url === '/resources/requirement-research/verify')).toBe(false)
    expect(onSaved).not.toHaveBeenCalled()
  })

  it('lists conflicting recommendations without preselecting one', async () => {
    await renderAndResearch(draft({
      status: 'ConflictingSources', suggestedQuantityPerArea: null, suggestedResourceUnit: null, suggestedAreaUnit: null,
      recommendations: [recommendation(195), recommendation(150)],
    }))

    expect(await screen.findByText('Conflicting sources')).toBeInTheDocument()
    expect(screen.getByText(/No value is preselected/)).toBeInTheDocument()
    expect(screen.getByText('Suggested requirement: 195 kg / hectare')).toBeInTheDocument()
    expect(screen.getByText('Suggested requirement: 150 kg / hectare')).toBeInTheDocument()
    screen.getAllByRole('button', { name: 'Verify & Save' }).forEach((button) => expect(button).toBeDisabled())
  })

  it('does not offer saving when no verified value or no inventory unit match exists', async () => {
    await renderAndResearch(draft({
      status: 'NoVerifiedRecommendationFound', suggestedQuantityPerArea: null,
      recommendations: [recommendation(4, { unitMatchesInventory: false, resourceUnit: 'bag' })],
      rejectedClaims: ['Rejected a proposed rate from https://doa.gov.lk/x: its evidence excerpt could not be matched.'],
    }))

    expect(await screen.findByText('No verified recommendation found')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Verify & Save' })).not.toBeInTheDocument()
    expect(screen.getByText(/Rejected claims \(1\)/)).toBeInTheDocument()
  })

  it('shows research errors and states that nothing was saved', async () => {
    vi.spyOn(api, 'get').mockResolvedValue(paged(resources))
    vi.spyOn(api, 'post').mockRejectedValue(new Error('The web research timed out.'))
    render(<ResourceRequirementResearchPanel crops={crops} varieties={[]} />)
    await waitFor(() => expect(screen.getByRole('option', { name: 'Urea (kg)' })).toBeInTheDocument())

    fireEvent.change(screen.getByRole('combobox', { name: /^Research crop/ }), { target: { value: 'crop-1' } })
    fireEvent.change(screen.getByRole('combobox', { name: /^Resource/ }), { target: { value: 'res-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Research requirement' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Nothing was saved.')
  })
})
