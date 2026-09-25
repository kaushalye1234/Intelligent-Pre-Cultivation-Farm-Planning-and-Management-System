import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { ResourcesPage } from './ResourcesPage'

const paged = (items: unknown[]) => ({ data: { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 } })

afterEach(() => {
  vi.restoreAllMocks()
})

function mockResourceApi() {
  return vi.spyOn(api, 'get').mockImplementation(async (url, config) => {
    if (url === '/resources/reservations') {
      return paged([{ id: 'res-1', inventoryStockId: 'stock-1', requestedByUserId: 'user-1', quantity: 4, status: 1, purpose: 'Rice sowing' }]) as never
    }
    if (url === '/resources/stocks') {
      return paged([{ id: 'stock-1', resourceId: 'resource-1', quantityOnHand: 10, reservedQuantity: 4, availableQuantity: 6, lowStockThreshold: 2 }]) as never
    }
    if (url === '/resources') {
      return paged([{ id: 'resource-1', resourceCategoryId: 'cat-1', name: 'Paddy Seed', unit: 'kg', isActive: true }]) as never
    }
    if (url === '/weather/forecast') {
      return {
        data: {
          location: (config?.params as { location?: string } | undefined)?.location,
          isAvailable: true,
          message: 'Forecast from OpenWeatherMap.',
          days: [{ date: '2026-09-15', minTemperatureC: 23, maxTemperatureC: 31, rainMm: 12.5, maxWindSpeedMs: 5, description: 'moderate rain' }],
        },
      } as never
    }
    return paged([]) as never
  })
}

describe('ResourcesPage', () => {
  it('loads saved reservations from the API', async () => {
    const get = mockResourceApi()

    render(<MemoryRouter><ResourcesPage /></MemoryRouter>)
    await screen.findByText('Paddy Seed')
    await userEvent.click(screen.getByRole('tab', { name: /reservations/i }))

    expect(await screen.findByText('Rice sowing')).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('/resources/reservations', expect.anything())
  })

  it('shows the weather forecast for a location', async () => {
    const get = mockResourceApi()

    render(<MemoryRouter><ResourcesPage /></MemoryRouter>)
    await screen.findByText('Paddy Seed')
    await userEvent.click(screen.getByRole('tab', { name: /weather/i }))
    await userEvent.type(screen.getByLabelText(/weather location/i), 'Kurunegala')
    await userEvent.click(screen.getByRole('button', { name: /get forecast/i }))

    expect(await screen.findByText('moderate rain')).toBeInTheDocument()
    await waitFor(() => expect(get).toHaveBeenCalledWith('/weather/forecast', { params: { location: 'Kurunegala' } }))
  })
})
