import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AdminCropManagement } from './AdminCropManagement'

const paged = <T,>(items: T[]) => ({ data: { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 } })

afterEach(() => vi.restoreAllMocks())

describe('Admin crop management', () => {
  it('loads inactive master data for Admin and creates a variety under its crop', async () => {
    const get = vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.includes('/crop-types')) return Promise.resolve(paged([{ id: 'crop-1', name: 'Rice', description: '', isActive: true }]))
      return Promise.resolve(paged([]))
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })

    render(<AdminCropManagement />)
    expect((await screen.findAllByText('Rice')).length).toBeGreaterThan(0)
    expect(get.mock.calls.some(([url]) => String(url).includes('includeInactive=true'))).toBe(true)

    fireEvent.change(screen.getAllByRole('combobox', { name: /^Crop/ })[0], { target: { value: 'crop-1' } })
    fireEvent.change(screen.getByRole('textbox', { name: /^Variety name/ }), { target: { value: 'Bg 352' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add variety' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-planning/crop-varieties', {
      cropTypeId: 'crop-1', name: 'Bg 352', isActive: true,
    }))
  })
})
