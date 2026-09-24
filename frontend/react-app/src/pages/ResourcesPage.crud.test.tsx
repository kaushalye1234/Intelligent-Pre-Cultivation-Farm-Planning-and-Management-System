import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { ResourcesPage } from './ResourcesPage'

const category = { id: 'cat-1', name: 'Seeds', description: 'Planting material' }
const supplier = { id: 'sup-1', name: 'AgroCo', contactEmail: 'sales@agroco.test', phone: '0112223333' }
const resource = { id: 'resource-1', resourceCategoryId: 'cat-1', supplierId: 'sup-1', name: 'Paddy Seed', unit: 'kg', isActive: true }
const stock = { id: 'stock-1', resourceId: 'resource-1', quantityOnHand: 10, reservedQuantity: 4, availableQuantity: 6, lowStockThreshold: 2, resourceName: 'Paddy Seed', unit: 'kg' }

function paged(items: unknown[], overrides: Partial<{ page: number; totalPages: number; totalCount: number }> = {}) {
  return { data: { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1, ...overrides } }
}

function mockGet(handlers: Record<string, (params: Record<string, unknown>) => unknown> = {}) {
  return vi.spyOn(api, 'get').mockImplementation(async (url, config) => {
    const params = (config?.params ?? {}) as Record<string, unknown>
    const handler = handlers[url]
    if (handler) return handler(params) as never
    if (url === '/resources/categories') return paged([category]) as never
    if (url === '/resources/suppliers') return paged([supplier]) as never
    if (url === '/resources/stocks') return paged([stock]) as never
    if (url === '/resources') return paged([resource]) as never
    return paged([]) as never
  })
}

function renderPage() {
  return render(<MemoryRouter><ResourcesPage /></MemoryRouter>)
}

async function openTab(name: RegExp) {
  await userEvent.click(screen.getByRole('tab', { name }))
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('ResourcesPage edit and delete', () => {
  it('edits a resource with PUT and refreshes', async () => {
    const get = mockGet()
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} } as never)
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^resources/i)
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))

    const dialog = screen.getByRole('dialog')
    const name = within(dialog).getByLabelText(/^name/i)
    await userEvent.clear(name)
    await userEvent.type(name, 'Paddy Seed B')
    const callsBefore = get.mock.calls.length
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save Changes' }))

    await waitFor(() => expect(put).toHaveBeenCalledWith('/resources/resource-1', expect.objectContaining({ name: 'Paddy Seed B', resourceCategoryId: 'cat-1', supplierId: 'sup-1', isActive: true })))
    expect(await screen.findByText('Resource updated successfully.')).toBeInTheDocument()
    await waitFor(() => expect(get.mock.calls.length).toBeGreaterThan(callsBefore))
  })

  it('asks for confirmation before deleting a resource and does not delete on cancel', async () => {
    mockGet()
    const del = vi.spyOn(api, 'delete').mockResolvedValue({ data: {} } as never)
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^resources/i)
    await userEvent.click(await screen.findByRole('button', { name: 'Delete' }))

    expect(screen.getByRole('dialog')).toHaveTextContent('Delete Resource?')
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }))
    expect(del).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete Resource' }))
    await waitFor(() => expect(del).toHaveBeenCalledWith('/resources/resource-1'))
    expect(await screen.findByText('Resource deleted successfully.')).toBeInTheDocument()
  })

  it('shows the API message when a delete is refused', async () => {
    mockGet()
    vi.spyOn(api, 'delete').mockRejectedValue({ isAxiosError: true, message: 'x', response: { data: { error: { message: 'This resource has active reservations.' } } } })
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^resources/i)
    await userEvent.click(await screen.findByRole('button', { name: 'Delete' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete Resource' }))

    expect(await screen.findByText('This resource has active reservations.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('edits and deletes a category', async () => {
    mockGet()
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} } as never)
    const del = vi.spyOn(api, 'delete').mockResolvedValue({ data: {} } as never)
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^categories/i)
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByLabelText(/^name/i)).toHaveValue('Seeds')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save Changes' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/resources/categories/cat-1', { name: 'Seeds', description: 'Planting material' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Delete' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete Category' }))
    await waitFor(() => expect(del).toHaveBeenCalledWith('/resources/categories/cat-1'))
  })

  it('edits and deletes a supplier', async () => {
    mockGet()
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} } as never)
    const del = vi.spyOn(api, 'delete').mockResolvedValue({ data: {} } as never)
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^suppliers/i)
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByLabelText(/contact email/i)).toHaveValue('sales@agroco.test')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save Changes' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/resources/suppliers/sup-1', { name: 'AgroCo', contactEmail: 'sales@agroco.test', phone: '0112223333' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Delete' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete Supplier' }))
    await waitFor(() => expect(del).toHaveBeenCalledWith('/resources/suppliers/sup-1'))
  })
})

describe('ResourcesPage filters, sorting and pagination', () => {
  it('sends category and supplier filters with the search text and clears them together', async () => {
    const get = mockGet()
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^resources/i)
    await screen.findByLabelText('Filter by category')

    await userEvent.type(screen.getByLabelText('Search resources'), 'paddy')
    await userEvent.selectOptions(screen.getByLabelText('Filter by category'), 'cat-1')
    await userEvent.selectOptions(screen.getByLabelText('Filter by supplier'), 'sup-1')
    await userEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() => expect(get).toHaveBeenCalledWith('/resources', { params: expect.objectContaining({ search: 'paddy', categoryId: 'cat-1', supplierId: 'sup-1', page: 1 }) }))

    await userEvent.click(screen.getByRole('button', { name: 'Clear filters' }))
    await waitFor(() => {
      const last = [...get.mock.calls].reverse().find(([url, config]) => url === '/resources' && (config?.params as Record<string, unknown>)?.pageSize === 10)
      expect(last?.[1]?.params).toEqual(expect.objectContaining({ search: undefined, categoryId: undefined, supplierId: undefined, page: 1 }))
    })
    expect(screen.getByLabelText('Filter by category')).toHaveValue('')
    expect(screen.getByLabelText('Search resources')).toHaveValue('')
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument()
  })

  it('toggles the sort direction when a column header is clicked', async () => {
    const get = mockGet()
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^resources/i)
    const header = await screen.findByRole('button', { name: /^Unit/ })

    await userEvent.click(header)
    await waitFor(() => expect(get).toHaveBeenCalledWith('/resources', { params: expect.objectContaining({ sortBy: 'unit', sortDirection: 'asc' }) }))
    await userEvent.click(await screen.findByRole('button', { name: /^Unit/ }))
    await waitFor(() => expect(get).toHaveBeenCalledWith('/resources', { params: expect.objectContaining({ sortBy: 'unit', sortDirection: 'desc' }) }))
    expect(screen.getByRole('columnheader', { name: /Unit/ })).toHaveAttribute('aria-sort', 'descending')
  })

  it('pages through results while keeping search and sort, with a page indicator', async () => {
    const get = mockGet({
      '/resources': (params) => paged([{ ...resource, id: `resource-${params.page}`, name: `Resource page ${params.page}` }], { page: Number(params.page), totalPages: 3, totalCount: 25 }),
    })
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^resources/i)
    await screen.findByText('Resource page 1')
    expect(screen.getByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByText('Showing 1-1 of 25')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Search resources'), 'seed')
    await userEvent.click(screen.getByRole('button', { name: 'Search' }))
    await userEvent.click(await screen.findByRole('button', { name: /^Unit/ }))
    await screen.findByText('Resource page 1')
    await userEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Resource page 2')).toBeInTheDocument()
    expect(screen.getByText('Page 2 of 3')).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('/resources', { params: expect.objectContaining({ page: 2, pageSize: 10, search: 'seed', sortBy: 'unit', sortDirection: 'asc' }) })

    await userEvent.click(screen.getByRole('button', { name: 'Next' }))
    await screen.findByText('Resource page 3')
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Previous' }))
    expect(await screen.findByText('Resource page 2')).toBeInTheDocument()
  })

  it('shows a loading state while a page loads and an empty state for no results', async () => {
    let release: (value: unknown) => void = () => undefined
    mockGet({
      '/resources': (params) => params.page === 2
        ? new Promise((resolve) => { release = resolve })
        : params.search === 'zzz'
          ? paged([])
          : paged([resource], { totalPages: 2, totalCount: 12 }),
    })
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/^resources/i)
    await screen.findByRole('button', { name: 'Next' })
    await userEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(await screen.findByText(/loading/i)).toBeInTheDocument()
    release(paged([resource], { page: 2, totalPages: 2, totalCount: 12 }))
    await screen.findByText('Page 2 of 2')

    await userEvent.type(screen.getByLabelText('Search resources'), 'zzz')
    await userEvent.click(screen.getByRole('button', { name: 'Search' }))
    expect(await screen.findByText('No matching resources')).toBeInTheDocument()
    expect(screen.getByText('No records match the current search or filters.')).toBeInTheDocument()
  })
})

describe('ResourcesPage stock history', () => {
  const transactions = [
    { id: 't1', inventoryStockId: 'stock-1', type: 1, quantity: 10, note: 'Stock level updated.', createdAt: '2026-09-01T08:00:00Z' },
    { id: 't2', inventoryStockId: 'stock-1', type: 3, quantity: 4, note: 'Rice sowing', createdAt: '2026-09-02T09:30:00Z' },
  ]

  it('lists transactions for the selected stock, newest first', async () => {
    const get = mockGet({ '/resources/stocks/stock-1/history': () => ({ data: transactions }) })
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/stock history/i)
    expect(screen.getByText('Choose a resource')).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Stock to view'), 'stock-1')

    await screen.findByText('Rice sowing')
    expect(get).toHaveBeenCalledWith('/resources/stocks/stock-1/history')
    const rows = screen.getAllByRole('row').slice(1)
    expect(rows[0]).toHaveTextContent('Reserved')
    expect(rows[0]).toHaveTextContent('4 kg')
    expect(rows[1]).toHaveTextContent('Stock added')
    expect(rows[1]).toHaveTextContent('Stock level updated.')
  })

  it('opens from an inventory row and shows the empty state', async () => {
    mockGet({ '/resources/stocks/stock-1/history': () => ({ data: [] }) })
    renderPage()
    await screen.findByText('Paddy Seed')
    await userEvent.click(await screen.findByRole('button', { name: 'History' }))

    expect(await screen.findByText('No transactions')).toBeInTheDocument()
    expect(screen.getByLabelText('Stock to view')).toHaveValue('stock-1')
  })

  it('shows an error state when the history cannot be loaded', async () => {
    mockGet({ '/resources/stocks/stock-1/history': () => { throw new Error('History unavailable') } })
    renderPage()
    await screen.findByText('Paddy Seed')
    await openTab(/stock history/i)
    await userEvent.selectOptions(screen.getByLabelText('Stock to view'), 'stock-1')

    expect(await screen.findByText('History unavailable')).toBeInTheDocument()
  })
})
