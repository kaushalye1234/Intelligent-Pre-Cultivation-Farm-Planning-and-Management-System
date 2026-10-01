import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AdminCropManagement } from './AdminCropManagement'

const paged = <T,>(items: T[]) => ({ data: { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 } })
const source = {
  sourceId: 'source-1', title: 'Rice recommendations', organizationName: 'Department of Agriculture',
  originalUrl: 'https://doa.gov.lk/rice', finalUrl: 'https://doa.gov.lk/rice', sourceCategory: 'Government',
  country: 'Sri Lanka', sourceClassification: 'Sri Lankan' as const, stage: 1, contentType: 'text/html',
  retrievalStatus: 'Retrieved' as const, retrievedAt: '2026-10-01T00:00:00Z', acceptanceReason: 'Approved government source.',
  rejectionReason: null, manualReviewRequired: false, pageCount: null, warnings: [], existingReference: false, existingReferenceId: null,
}
const provenance = [{
  sourceId: 'source-1', sourceName: 'Rice recommendations', organizationName: 'Department of Agriculture',
  originalUrl: source.originalUrl, finalUrl: source.finalUrl, sourceCategory: 'Government', country: 'Sri Lanka',
  sourceClassification: 'Sri Lankan' as const, stage: 1, evidenceText: 'Bg 352 is listed for cultivation.', pageNumber: null, section: 'Varieties',
}]
const fallbackSource = {
  ...source,
  sourceId: 'source-international',
  title: 'World Vegetable Center variety release',
  organizationName: 'World Vegetable Center',
  originalUrl: 'https://worldveg.org/varieties/yummy-hot',
  finalUrl: 'https://worldveg.org/varieties/yummy-hot',
  sourceCategory: 'International crop research institute',
  country: 'International',
  sourceClassification: 'International fallback' as const,
  stage: 2,
}
const fallbackProvenance = [{
  sourceId: fallbackSource.sourceId,
  sourceName: fallbackSource.title,
  organizationName: fallbackSource.organizationName,
  originalUrl: fallbackSource.originalUrl,
  finalUrl: fallbackSource.finalUrl,
  sourceCategory: fallbackSource.sourceCategory,
  country: fallbackSource.country,
  sourceClassification: fallbackSource.sourceClassification,
  stage: 2,
  evidenceText: 'This new variety, called Yummy Hot, is a high-yielding chili.',
  pageNumber: null,
  section: 'Varieties',
}]

afterEach(() => vi.restoreAllMocks())

function mockCatalog() {
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: '', isActive: true }]))
    if (url.includes('/crop-varieties')) return Promise.resolve(paged([{ id: 'variety-1', cropTypeId: 'crop-1', name: 'Bg 352', isActive: true }]))
    return Promise.resolve(paged([]))
  })
}

describe('CropFinding Admin review', () => {
  it('prefills the existing crop dialog without persisting an AI suggestion', async () => {
    mockCatalog()
    const post = vi.spyOn(api, 'post').mockImplementation((url: string) => {
      if (url === '/crop-finding/suggest-crops') return Promise.resolve({ data: {
        requestId: 'request-1', action: 'SuggestCrops', usedInternationalFallback: false, sources: [source],
        suggestions: [
          { id: 'existing', name: 'Rice', description: null, evidenceStatus: 'Supported', explanation: 'Listed.', provenance, warnings: [], alreadyExists: true, existingCropTypeId: 'crop-1' },
          { id: 'new', name: 'Kurakkan', description: 'Finger millet', evidenceStatus: 'Supported', explanation: 'Listed.', provenance, warnings: [], alreadyExists: false },
        ], analysis: ['Two candidates reviewed.'], recommendations: [], warnings: [],
      } })
      return Promise.resolve({ data: {} })
    })

    render(<AdminCropManagement />)
    await screen.findByText('Rice')
    fireEvent.click(screen.getByRole('button', { name: 'Suggest Sri Lankan Crops' }))
    await screen.findByText('Kurakkan')

    const acceptButtons = screen.getAllByRole('button', { name: 'Accept' })
    expect(acceptButtons[0]).toBeDisabled()
    fireEvent.click(acceptButtons[1])

    const dialog = screen.getByRole('dialog', { name: 'Add crop' })
    expect(within(dialog).getByRole('textbox', { name: /^Crop name/ })).toHaveValue('Kurakkan')
    expect(within(dialog).getByRole('textbox', { name: 'Notes' })).toHaveValue('Finger millet')
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).not.toHaveBeenCalledWith('/crop-planning/crop-types', expect.anything())
  })

  it('requires explicit confirmation before a partially supported variety prefills the existing form', async () => {
    mockCatalog()
    vi.spyOn(api, 'post').mockResolvedValue({ data: {
      requestId: 'request-2', action: 'SuggestVarieties', cropTypeId: 'crop-1', cropName: 'Rice', usedInternationalFallback: true,
      sources: [fallbackSource], suggestions: [{ id: 'partial', name: 'Candidate 1', description: null, evidenceStatus: 'Partially Supported', explanation: 'International fallback only.', provenance: fallbackProvenance, warnings: ['Local applicability is not established.'], alreadyExists: false }],
      analysis: [
        'Sri Lankan evidence: No explicit variety names were found.',
        'International fallback: One explicit variety was found.',
      ], recommendations: [], warnings: [],
    } })

    render(<AdminCropManagement />)
    await screen.findByText('Rice')
    fireEvent.click(screen.getByRole('tab', { name: /Varieties/ }))
    fireEvent.change(screen.getByRole('combobox', { name: 'Existing crop' }), { target: { value: 'crop-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Find Sri Lankan Varieties' }))
    await screen.findByText('Candidate 1')
    expect(screen.getByText('Sri Lankan evidence: No explicit variety names were found.')).toBeInTheDocument()
    expect(screen.getByText('International fallback: One explicit variety was found.')).toBeInTheDocument()
    expect(screen.getAllByText('International fallback').length).toBeGreaterThan(0)
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }))

    expect(screen.queryByRole('dialog', { name: 'Add variety' })).not.toBeInTheDocument()
    const warning = screen.getByRole('dialog', { name: 'Use partially supported value?' })
    fireEvent.click(within(warning).getByRole('button', { name: 'Use with warning' }))
    expect(within(screen.getByRole('dialog', { name: 'Add variety' })).getByRole('textbox', { name: /^Variety name/ })).toHaveValue('Candidate 1')
  })

  it('uses only the selected primary source to prefill the existing reference form', async () => {
    mockCatalog()
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {
      requestId: 'request-3', action: 'DiscoverReferences', cropTypeId: 'crop-1', cropName: 'Rice', cropVarietyId: null, varietyName: null,
      usedInternationalFallback: false, analysis: ['One Sri Lankan source retrieved.'], recommendations: ['Review the original source.'], unsupportedFields: [], warnings: [],
      sourceDrafts: [{ source, analysis: [], recommendations: [], items: [
        { id: 'source-name', field: 'sourceName', suggestedValue: 'DOA Rice Guide', displayValue: 'DOA Rice Guide', evidenceStatus: 'Supported', explanation: 'Retrieved title.', provenance, warnings: [], conflictGroupId: null },
        { id: 'source-url', field: 'sourceUrl', suggestedValue: source.finalUrl, displayValue: source.finalUrl, evidenceStatus: 'Supported', explanation: 'Approved URL.', provenance, warnings: [], conflictGroupId: null },
        { id: 'stage', field: 'growthStage', suggestedValue: { stageName: 'Vegetative', sequence: 1, typicalMinDays: 20, typicalMaxDays: 30, notes: 'Source evidence' }, displayValue: 'Vegetative (20–30 days)', evidenceStatus: 'Supported', explanation: 'Explicit stage.', provenance, warnings: [], conflictGroupId: null },
        { id: 'unsupported', field: 'region', suggestedValue: 'Sri Lanka', displayValue: 'Sri Lanka', evidenceStatus: 'Unsupported', explanation: 'Not explicit.', provenance: [], warnings: [], conflictGroupId: null },
      ] }],
    } })

    render(<AdminCropManagement />)
    await screen.findByText('Rice')
    fireEvent.click(screen.getByRole('tab', { name: /Verified References/ }))
    fireEvent.change(screen.getByRole('combobox', { name: /^Crop/ }), { target: { value: 'crop-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Find References with AI' }))
    await screen.findByText('DOA Rice Guide')

    const sourceNameItem = screen.getAllByText('Source name').find((element) => element.tagName === 'STRONG')!.closest('article')!
    expect(within(sourceNameItem).getByRole('button', { name: 'Accept' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Select primary source' }))
    fireEvent.click(within(sourceNameItem).getByRole('button', { name: 'Accept' }))
    fireEvent.click(within(screen.getByText('Growth stage').closest('article')!).getByRole('button', { name: 'Accept' }))

    expect(screen.getByRole('textbox', { name: /^Source name/ })).toHaveValue('DOA Rice Guide')
    expect(screen.getByRole('textbox', { name: /^Stage name/ })).toHaveValue('Vegetative')
    expect(screen.getByRole('spinbutton', { name: 'Minimum days' })).toHaveValue(20)
    expect(within(screen.getByText('Region / applicability').closest('article')!).getByRole('button', { name: 'Accept' })).toBeDisabled()
    expect(post).toHaveBeenCalledTimes(1)
  })

  it('keeps prior results and Admin form values when a retry fails', async () => {
    mockCatalog()
    vi.spyOn(api, 'post')
      .mockResolvedValueOnce({ data: { requestId: 'request-1', action: 'SuggestCrops', usedInternationalFallback: false, sources: [], suggestions: [], analysis: ['First result'], recommendations: [], warnings: [] } })
      .mockRejectedValueOnce(new Error('Provider timeout'))

    render(<AdminCropManagement />)
    await screen.findByText('Rice')
    fireEvent.click(screen.getByRole('button', { name: 'Suggest Sri Lankan Crops' }))
    await screen.findByText('First result')
    fireEvent.click(screen.getByRole('button', { name: 'Suggest Sri Lankan Crops' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Provider timeout')
    expect(screen.getByText('First result')).toBeInTheDocument()
  })
})
