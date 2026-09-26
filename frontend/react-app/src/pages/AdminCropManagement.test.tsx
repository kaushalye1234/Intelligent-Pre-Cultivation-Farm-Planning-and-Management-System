import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AdminCropManagement } from './AdminCropManagement'

const paged = <T,>(items: T[]) => ({ data: { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 } })

afterEach(() => vi.restoreAllMocks())

describe('Admin crop management', () => {
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
})
