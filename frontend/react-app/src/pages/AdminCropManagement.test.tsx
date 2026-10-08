import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AdminCropManagement } from './AdminCropManagement'

const paged = <T,>(items: T[]) => ({ data: { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 } })

afterEach(() => vi.restoreAllMocks())

describe('Admin crop management', () => {
  it('gives reference managers only the verified reference workflow and active catalog choices', async () => {
    const profile = {
      id: 'reference-1',
      cropTypeId: 'crop-1',
      varietyName: null,
      region: 'Kurunegala',
      sourceName: 'Department of Agriculture field guide',
      sourceUrl: 'https://example.test/rice-guide',
      sourceVersion: '2026 edition',
      verifiedAt: '2026-10-05T08:30:00Z',
      isActive: false,
      stageCount: 1,
      ruleCount: 0,
    }
    const get = vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: '', isActive: true }]))
      if (url.includes('/crop-varieties')) return Promise.resolve(paged([{ id: 'variety-1', cropTypeId: 'crop-1', name: 'Bg 352', isActive: true }]))
      if (url.includes('/crop-reference-profiles')) return Promise.resolve(paged([profile]))
      return Promise.resolve(paged([]))
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} })

    render(<AdminCropManagement referenceOnly />)

    expect(await screen.findByRole('heading', { name: 'Verified references' })).toBeInTheDocument()
    expect(screen.queryByRole('tab', { name: /Crops/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('tab', { name: /Varieties/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add crop' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add variety' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Find References with AI' })).not.toBeInTheDocument()
    expect(get.mock.calls.some(([url]) => String(url).includes('includeInactive=true'))).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Activate' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith(
      '/crop-planning/crop-reference-profiles/reference-1/active',
      true,
      { headers: { 'Content-Type': 'application/json' } },
    ))

    fireEvent.change(screen.getByRole('combobox', { name: /^Crop/ }), { target: { value: 'crop-1' } })
    fireEvent.change(screen.getByRole('textbox', { name: /^Source name/ }), { target: { value: 'Verified rice guide' } })
    fireEvent.change(screen.getByRole('textbox', { name: /^Source version/ }), { target: { value: 'v1' } })
    fireEvent.change(screen.getByLabelText(/^Verified at/), { target: { value: '2026-10-05T10:30' } })
    fireEvent.change(screen.getByRole('textbox', { name: /^Stage name/ }), { target: { value: 'Land preparation' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create verified version' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(
      '/crop-planning/crop-reference-profiles',
      expect.objectContaining({
        cropTypeId: 'crop-1',
        sourceName: 'Verified rice guide',
        sourceVersion: 'v1',
        stages: [expect.objectContaining({ stageName: 'Land preparation', sequence: 1 })],
      }),
    ))
  })

  it('uses tabs, filters crops, and creates catalog items in dialogs', async () => {
    const get = vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/crop-types')) return Promise.resolve(paged([
        { id: 'crop-1', name: 'Rice', description: 'Wet-zone crop', isActive: true },
        { id: 'crop-2', name: 'Old maize', description: '', isActive: false },
      ]))
      return Promise.resolve(paged([]))
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })

    render(<AdminCropManagement />)
    expect((await screen.findAllByText('Rice')).length).toBeGreaterThan(0)
    expect(get.mock.calls.some(([url]) => String(url).includes('includeInactive=true'))).toBe(true)

    fireEvent.change(screen.getByRole('textbox', { name: 'Search crops' }), { target: { value: 'maize' } })
    expect(screen.queryByText('Wet-zone crop')).not.toBeInTheDocument()
    expect(screen.getByText('Old maize')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Add crop' }))
    const cropDialog = screen.getByRole('dialog', { name: 'Add crop' })
    fireEvent.change(within(cropDialog).getByRole('textbox', { name: /^Crop name/ }), { target: { value: 'Beans' } })
    fireEvent.click(within(cropDialog).getByRole('button', { name: 'Add crop' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-planning/crop-types', {
      name: 'Beans', description: null, isActive: true,
    }))

    fireEvent.click(screen.getByRole('tab', { name: /Varieties/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Add variety' }))
    const varietyDialog = screen.getByRole('dialog', { name: 'Add variety' })
    fireEvent.change(within(varietyDialog).getByRole('combobox', { name: /^Crop/ }), { target: { value: 'crop-1' } })
    fireEvent.change(within(varietyDialog).getByRole('textbox', { name: /^Variety name/ }), { target: { value: 'Bg 352' } })
    fireEvent.click(within(varietyDialog).getByRole('button', { name: 'Add variety' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-planning/crop-varieties', {
      cropTypeId: 'crop-1', name: 'Bg 352', isActive: true,
    }))
  })

  it('edits crops and changes active state directly from the table', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: 'Original notes', isActive: true }]))
      return Promise.resolve(paged([]))
    })
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} })

    render(<AdminCropManagement />)
    await screen.findByText('Original notes')

    fireEvent.click(screen.getByRole('button', { name: 'Deactivate' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/crop-planning/crop-types/crop-1', {
      name: 'Rice', description: 'Original notes', isActive: false,
    }))

    fireEvent.click(screen.getByRole('button', { name: 'Edit' }))
    const dialog = screen.getByRole('dialog', { name: 'Edit crop' })
    fireEvent.change(within(dialog).getByRole('textbox', { name: /^Crop name/ }), { target: { value: 'Paddy rice' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(put).toHaveBeenCalledWith('/crop-planning/crop-types/crop-1', {
      name: 'Paddy rice', description: 'Original notes', isActive: true,
    }))
  })

  it('keeps an edit dialog open and shows a save error in context', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: '', isActive: true }]))
      return Promise.resolve(paged([]))
    })
    vi.spyOn(api, 'put').mockRejectedValue(new Error('Crop type already exists.'))

    render(<AdminCropManagement />)
    await screen.findByText('Rice')
    fireEvent.click(screen.getByRole('button', { name: 'Edit' }))
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Edit crop' })).getByRole('button', { name: 'Save changes' }))

    expect(await within(screen.getByRole('dialog', { name: 'Edit crop' })).findByRole('alert')).toHaveTextContent('Crop type already exists.')
  })

  it('confirms deletes and explains when historical use blocks deletion', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: '', isActive: true }]))
      if (url.includes('/crop-varieties')) return Promise.resolve(paged([{ id: 'variety-1', cropTypeId: 'crop-1', name: 'Bg 352', isActive: true }]))
      return Promise.resolve(paged([]))
    })
    const remove = vi.spyOn(api, 'delete')
      .mockResolvedValueOnce({ data: {} })
      .mockRejectedValueOnce(new Error('This variety is already in use. Deactivate it instead.'))

    render(<AdminCropManagement />)
    await screen.findByText('Rice')
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))
    const cropDelete = screen.getByRole('dialog', { name: 'Delete crop?' })
    expect(cropDelete).toHaveTextContent('Use Deactivate instead')
    fireEvent.click(within(cropDelete).getByRole('button', { name: 'Delete crop' }))
    await waitFor(() => expect(remove).toHaveBeenCalledWith('/crop-planning/crop-types/crop-1'))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Delete crop?' })).not.toBeInTheDocument())

    fireEvent.click(screen.getByRole('tab', { name: /Varieties/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))
    const varietyDelete = screen.getByRole('dialog', { name: 'Delete variety?' })
    fireEvent.click(within(varietyDelete).getByRole('button', { name: 'Delete variety' }))

    expect(await within(varietyDelete).findByRole('alert')).toHaveTextContent('Deactivate it instead.')
    expect(remove).toHaveBeenCalledWith('/crop-planning/crop-varieties/variety-1')
  })

  it('opens persisted verified reference details without changing data', async () => {
    const profile = {
      id: 'reference-1',
      cropTypeId: 'crop-1',
      varietyName: 'MICH HY2',
      region: 'Sri Lanka',
      sourceName: 'Sri Lanka Department of Agriculture e-Repository',
      sourceUrl: 'https://dl-doa.nsf.gov.lk/reference/chili',
      sourceVersion: '2018 edition',
      verifiedAt: '2026-10-02T08:30:00Z',
      isActive: true,
      stageCount: 1,
      ruleCount: 1,
    }
    const details = {
      ...profile,
      cropName: 'Chili',
      stages: [{
        id: 'stage-1',
        stageName: 'Vegetative growth',
        sequence: 1,
        typicalMinDays: 18,
        typicalMaxDays: 32,
        notes: 'Duration stated in the persisted source evidence.',
      }],
      rules: [{
        id: 'rule-1',
        ruleType: 'Season',
        ruleKey: 'planting-window',
        structuredValueJson: '{"season":"Maha"}',
      }],
    }
    const get = vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url === '/crop-planning/crop-reference-profiles/reference-1') return Promise.resolve({ data: details })
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Chili', description: '', isActive: true }]))
      if (url.includes('/crop-varieties')) return Promise.resolve(paged([{ id: 'variety-1', cropTypeId: 'crop-1', name: 'MICH HY2', isActive: true }]))
      if (url.includes('/crop-reference-profiles')) return Promise.resolve(paged([profile]))
      return Promise.resolve(paged([]))
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} })

    render(<AdminCropManagement />)
    await screen.findByText('Chili')
    fireEvent.click(screen.getByRole('tab', { name: /Verified References/ }))

    const sourceNameInput = screen.getByRole('textbox', { name: /^Source name/ })
    expect(sourceNameInput).toHaveValue('')
    const viewDetails = screen.getByRole('button', { name: 'View details' })
    expect(viewDetails).toBeVisible()
    fireEvent.click(viewDetails)

    const dialog = await screen.findByRole('dialog', { name: 'Chili reference' })
    expect(get).toHaveBeenCalledWith('/crop-planning/crop-reference-profiles/reference-1')
    expect(dialog).toHaveTextContent('MICH HY2')
    expect(dialog).toHaveTextContent('Sri Lanka')
    expect(dialog).toHaveTextContent('2018 edition')
    expect(dialog).toHaveTextContent('Active')
    expect(dialog).toHaveTextContent('Vegetative growth')
    expect(dialog).toHaveTextContent('18')
    expect(dialog).toHaveTextContent('32')
    expect(dialog).toHaveTextContent('Duration stated in the persisted source evidence.')
    expect(dialog).toHaveTextContent('Season')
    expect(dialog).toHaveTextContent('planting-window')
    expect(dialog).toHaveTextContent('"season": "Maha"')
    expect(within(dialog).getByRole('link', { name: /Open original source/ })).toHaveAttribute('href', profile.sourceUrl)

    fireEvent.click(within(dialog).getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog', { name: 'Chili reference' })).not.toBeInTheDocument()
    expect(sourceNameInput).toHaveValue('')
    expect(post).not.toHaveBeenCalled()
    expect(put).not.toHaveBeenCalled()
  })

  it('warns that a rules-only reference cannot support final approval', async () => {
    const profile = {
      id: 'rules-only', cropTypeId: 'rice', varietyName: null, region: null,
      sourceName: 'Officer reviewed source', sourceUrl: null, sourceVersion: 'v1',
      verifiedAt: '2026-10-05T08:30:00Z', isActive: true, stageCount: 0, ruleCount: 1,
    }
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.endsWith('/rules-only')) return Promise.resolve({ data: {
        ...profile, cropName: 'Rice', stages: [], rules: [{
          id: 'rule-1', ruleType: 'ResourceRequirement', ruleKey: 'urea',
          structuredValueJson: '{}',
        }],
      } })
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'rice', name: 'Rice', description: '', isActive: true }]))
      if (url.includes('/crop-reference-profiles')) return Promise.resolve(paged([profile]))
      return Promise.resolve(paged([]))
    })

    render(<AdminCropManagement />)
    fireEvent.click(await screen.findByRole('tab', { name: /Verified References/ }))
    expect(screen.getByText(/Cannot support final approval/)).toBeVisible()
    fireEvent.click(await screen.findByRole('button', { name: 'View details' }))

    expect(await screen.findByText(/rules-only reference has no growth stages/i)).toBeVisible()
  })
})
