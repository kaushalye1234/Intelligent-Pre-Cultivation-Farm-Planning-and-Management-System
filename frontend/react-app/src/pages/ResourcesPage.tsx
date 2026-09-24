import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { CloudSun, History, Package, Plus, Search, Warehouse } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import type { SortState } from '../components/DataTable'
import { Pagination } from '../components/Pagination'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, MetricCard, Modal, Notice, PageHeader, Tabs, Toolbar } from '../components/Ui'
import { formatDate, formatDateTime, formatNumber } from '../format'
import { reservationStatus, stockTransactionType } from '../labels'
import type { InventoryStock, PagedResult, Reservation, ResourceCategory, ResourceItem, StockTransaction, Supplier, WeatherForecast } from '../types'
import './ResourcesPage.css'

type ResourceTab = 'inventory' | 'resources' | 'categories' | 'suppliers' | 'reservations' | 'history' | 'weather'
type PagedTab = 'inventory' | 'resources' | 'categories' | 'suppliers' | 'reservations'
type ResourceModal = 'category' | 'supplier' | 'resource' | 'stock' | 'reserve' | null

type ConfirmAction = {
  title: string
  message: string
  label: string
  variant?: 'primary' | 'danger'
  action: () => Promise<void>
  success: string
} | null

type TabView = SortState & { page: number }

const PAGE_SIZE = 10
const LOOKUP_SIZE = 100

const DEFAULT_VIEWS: Record<PagedTab, TabView> = {
  inventory: { page: 1, sortBy: 'resourceName', sortDirection: 'asc' },
  resources: { page: 1, sortBy: 'name', sortDirection: 'asc' },
  categories: { page: 1, sortBy: 'name', sortDirection: 'asc' },
  suppliers: { page: 1, sortBy: 'name', sortDirection: 'asc' },
  reservations: { page: 1, sortBy: 'createdAt', sortDirection: 'desc' },
}

const TAB_ENDPOINTS: Record<PagedTab, string> = {
  inventory: '/resources/stocks',
  resources: '/resources',
  categories: '/resources/categories',
  suppliers: '/resources/suppliers',
  reservations: '/resources/reservations',
}

function isPagedTab(tab: ResourceTab): tab is PagedTab {
  return tab !== 'weather' && tab !== 'history'
}

function stockTone(stock: InventoryStock) {
  if (stock.availableQuantity <= 0) return 'bad'
  if (stock.availableQuantity <= stock.lowStockThreshold) return 'warn'
  return 'good'
}

function stockLabel(stock: InventoryStock) {
  if (stock.availableQuantity <= 0) return 'Out of Stock'
  if (stock.availableQuantity <= stock.lowStockThreshold) return 'Low Stock'
  return 'Available'
}

const emptyCategoryForm = { name: '', description: '' }
const emptySupplierForm = { name: '', contactEmail: '', phone: '' }
const emptyResourceForm = { resourceCategoryId: '', supplierId: '', name: '', unit: '', isActive: 'true' }

export function ResourcesPage() {
  // Lookup lists feed dropdowns and name columns. They are separate from the paged table data.
  const [categories, setCategories] = useState<ResourceCategory[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [resources, setResources] = useState<ResourceItem[]>([])
  const [stocks, setStocks] = useState<InventoryStock[]>([])
  const [lowStockTotal, setLowStockTotal] = useState(0)

  const [activeTab, setActiveTab] = useState<ResourceTab>('inventory')
  const [views, setViews] = useState<Record<PagedTab, TabView>>(DEFAULT_VIEWS)
  const [results, setResults] = useState<Partial<Record<PagedTab, PagedResult<unknown>>>>({})
  const [reloadKey, setReloadKey] = useState(0)

  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const [categoryFilter, setCategoryFilter] = useState('')
  const [supplierFilter, setSupplierFilter] = useState('')

  const [activeModal, setActiveModal] = useState<ResourceModal>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [categoryForm, setCategoryForm] = useState(emptyCategoryForm)
  const [supplierForm, setSupplierForm] = useState(emptySupplierForm)
  const [resourceForm, setResourceForm] = useState(emptyResourceForm)
  const [stockForm, setStockForm] = useState({ resourceId: '', quantityOnHand: '', lowStockThreshold: '' })
  const [reservationForm, setReservationForm] = useState({ inventoryStockId: '', quantity: '', purpose: '' })

  const [historyStockId, setHistoryStockId] = useState('')
  const [history, setHistory] = useState<StockTransaction[]>([])
  const [historyError, setHistoryError] = useState('')
  const [isHistoryLoading, setIsHistoryLoading] = useState(false)

  const [weatherLocation, setWeatherLocation] = useState('')
  const [forecast, setForecast] = useState<WeatherForecast | null>(null)
  const [weatherError, setWeatherError] = useState('')
  const [isWeatherLoading, setIsWeatherLoading] = useState(false)

  const categoryOptions = categories.map((category) => ({ value: category.id, label: category.name }))
  const supplierOptions = suppliers.map((supplier) => ({ value: supplier.id, label: supplier.name }))
  const resourceOptions = resources.map((resource) => ({ value: resource.id, label: resource.name }))
  const resourceNameById = useMemo(() => new Map(resources.map((resource) => [resource.id, resource.name])), [resources])
  const categoryNameById = useMemo(() => new Map(categories.map((category) => [category.id, category.name])), [categories])
  const supplierNameById = useMemo(() => new Map(suppliers.map((supplier) => [supplier.id, supplier.name])), [suppliers])
  const stockName = useCallback((stock: InventoryStock) => stock.resourceName || resourceNameById.get(stock.resourceId) || stock.resourceId.slice(0, 8), [resourceNameById])
  const stockOptions = stocks.map((stock) => ({ value: stock.id, label: `${stockName(stock)} (${formatNumber(stock.availableQuantity)} available)` }))

  const view = isPagedTab(activeTab) ? views[activeTab] : null
  const current = isPagedTab(activeTab) ? results[activeTab] : undefined
  const hasFilters = Boolean(appliedSearch || categoryFilter || supplierFilter || lowStockOnly)

  // Dropdown/name lookups, refreshed after every successful change.
  useEffect(() => {
    let cancelled = false
    async function loadLookups() {
      try {
        const [categoryResult, supplierResult, resourceResult, stockResult, lowResult] = await Promise.all([
          api.get<PagedResult<ResourceCategory>>('/resources/categories', { params: { pageSize: LOOKUP_SIZE, sortBy: 'name' } }),
          api.get<PagedResult<Supplier>>('/resources/suppliers', { params: { pageSize: LOOKUP_SIZE, sortBy: 'name' } }),
          api.get<PagedResult<ResourceItem>>('/resources', { params: { pageSize: LOOKUP_SIZE, sortBy: 'name' } }),
          api.get<PagedResult<InventoryStock>>('/resources/stocks', { params: { pageSize: LOOKUP_SIZE, sortBy: 'resourceName' } }),
          api.get<PagedResult<InventoryStock>>('/resources/stocks', { params: { pageSize: 1, lowStockOnly: true } }),
        ])
        if (cancelled) return
        setCategories(categoryResult.data.items)
        setSuppliers(supplierResult.data.items)
        setResources(resourceResult.data.items)
        setStocks(stockResult.data.items)
        setLowStockTotal(lowResult.data.totalCount)
      } catch (err) {
        if (!cancelled) setError(getErrorMessage(err))
      }
    }
    void loadLookups()
    return () => { cancelled = true }
  }, [reloadKey])

  // The table for the active tab. Page, sort, search and filters are all sent together, so changing
  // the page keeps the rest and changing the rest returns to page 1 (see updateView).
  const activeView = view
  useEffect(() => {
    if (!isPagedTab(activeTab) || !activeView) return
    const tab = activeTab
    let cancelled = false
    async function loadTab() {
      setIsLoading(true)
      setError('')
      try {
        const params: Record<string, unknown> = {
          page: activeView?.page,
          pageSize: PAGE_SIZE,
          search: appliedSearch || undefined,
        }
        if (tab !== 'reservations') {
          params.sortBy = activeView?.sortBy
          params.sortDirection = activeView?.sortDirection
        }
        if (tab === 'inventory') params.lowStockOnly = lowStockOnly || undefined
        if (tab === 'resources') {
          params.categoryId = categoryFilter || undefined
          params.supplierId = supplierFilter || undefined
        }
        const response = await api.get<PagedResult<unknown>>(TAB_ENDPOINTS[tab], { params })
        if (cancelled) return
        setResults((existing) => ({ ...existing, [tab]: response.data }))
        // Deleting the last row of the last page leaves an empty page: step back to a page that exists.
        if (response.data.items.length === 0 && response.data.totalCount > 0 && response.data.totalPages > 0 && activeView && activeView.page > response.data.totalPages) {
          setViews((existing) => ({ ...existing, [tab]: { ...existing[tab], page: response.data.totalPages } }))
        }
      } catch (err) {
        if (!cancelled) setError(getErrorMessage(err))
      } finally {
        if (!cancelled) setIsLoading(false)
      }
    }
    void loadTab()
    return () => { cancelled = true }
  }, [activeTab, activeView, appliedSearch, lowStockOnly, categoryFilter, supplierFilter, reloadKey])

  useEffect(() => {
    // History is only rendered while a stock is selected, so there is nothing to clear here.
    if (!historyStockId) return
    let cancelled = false
    async function loadHistory() {
      setIsHistoryLoading(true)
      setHistoryError('')
      try {
        const response = await api.get<StockTransaction[]>(`/resources/stocks/${historyStockId}/history`)
        if (!cancelled) setHistory([...response.data].reverse())
      } catch (err) {
        if (!cancelled) {
          setHistory([])
          setHistoryError(getErrorMessage(err))
        }
      } finally {
        if (!cancelled) setIsHistoryLoading(false)
      }
    }
    void loadHistory()
    return () => { cancelled = true }
  }, [historyStockId, reloadKey])

  function updateView(tab: PagedTab, change: Partial<TabView>, resetPage = true) {
    setViews((existing) => ({ ...existing, [tab]: { ...existing[tab], ...change, ...(resetPage ? { page: 1 } : {}) } }))
  }

  function changePage(page: number) {
    if (isPagedTab(activeTab)) updateView(activeTab, { page }, false)
  }

  function sortBy(sortKey: string) {
    if (!isPagedTab(activeTab)) return
    const existing = views[activeTab]
    updateView(activeTab, { sortBy: sortKey, sortDirection: existing.sortBy === sortKey && existing.sortDirection === 'asc' ? 'desc' : 'asc' })
  }

  function applySearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setAppliedSearch(search.trim())
    setViews((existing) => Object.fromEntries(Object.entries(existing).map(([tab, tabView]) => [tab, { ...tabView, page: 1 }])) as Record<PagedTab, TabView>)
  }

  function changeFilter(apply: () => void) {
    apply()
    setViews((existing) => ({ ...existing, inventory: { ...existing.inventory, page: 1 }, resources: { ...existing.resources, page: 1 } }))
  }

  function clearFilters() {
    setSearch('')
    setAppliedSearch('')
    setLowStockOnly(false)
    setCategoryFilter('')
    setSupplierFilter('')
    setViews((existing) => Object.fromEntries(Object.entries(existing).map(([tab, tabView]) => [tab, { ...tabView, page: 1 }])) as Record<PagedTab, TabView>)
  }

  function switchTab(tab: ResourceTab) {
    setSuccess('')
    setActionError('')
    setActiveTab(tab)
  }

  function closeModal() {
    setActiveModal(null)
    setEditingId(null)
    setActionError('')
  }

  function openCategory(category?: ResourceCategory) {
    setEditingId(category?.id ?? null)
    setCategoryForm(category ? { name: category.name, description: category.description ?? '' } : emptyCategoryForm)
    setActiveModal('category')
  }

  function openSupplier(supplier?: Supplier) {
    setEditingId(supplier?.id ?? null)
    setSupplierForm(supplier ? { name: supplier.name, contactEmail: supplier.contactEmail, phone: supplier.phone } : emptySupplierForm)
    setActiveModal('supplier')
  }

  function openResource(resource?: ResourceItem) {
    setEditingId(resource?.id ?? null)
    setResourceForm(resource
      ? { resourceCategoryId: resource.resourceCategoryId, supplierId: resource.supplierId ?? '', name: resource.name, unit: resource.unit, isActive: String(resource.isActive) }
      : emptyResourceForm)
    setActiveModal('resource')
  }

  function openStock(resourceId?: string) {
    const existing = stocks.find((stock) => stock.resourceId === resourceId)
    setStockForm({
      resourceId: resourceId ?? '',
      quantityOnHand: existing ? String(existing.quantityOnHand) : '',
      lowStockThreshold: existing ? String(existing.lowStockThreshold) : '',
    })
    setActiveModal('stock')
  }

  function openReserve(stockId?: string) {
    setReservationForm({ inventoryStockId: stockId ?? '', quantity: '', purpose: '' })
    setActiveModal('reserve')
  }

  function openHistory(stockId: string) {
    setHistoryStockId(stockId)
    switchTab('history')
  }

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setActionError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      closeModal()
      setConfirmAction(null)
      setReloadKey((key) => key + 1)
    } catch (err) {
      setActionError(getErrorMessage(err))
      // A failed delete/release has no form to show the error in, so close the dialog and show it on the page.
      setConfirmAction(null)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function saveCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const id = editingId
    await runAction(async () => {
      if (id) await api.put(`/resources/categories/${id}`, categoryForm)
      else await api.post('/resources/categories', categoryForm)
      setCategoryForm(emptyCategoryForm)
    }, id ? 'Category updated successfully.' : 'Category created successfully.')
  }

  async function saveSupplier(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const id = editingId
    await runAction(async () => {
      if (id) await api.put(`/resources/suppliers/${id}`, supplierForm)
      else await api.post('/resources/suppliers', supplierForm)
      setSupplierForm(emptySupplierForm)
    }, id ? 'Supplier updated successfully.' : 'Supplier created successfully.')
  }

  async function saveResource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const id = editingId
    await runAction(async () => {
      const body = { ...resourceForm, supplierId: resourceForm.supplierId || null, isActive: resourceForm.isActive === 'true' }
      if (id) await api.put(`/resources/${id}`, body)
      else await api.post('/resources', body)
      setResourceForm(emptyResourceForm)
    }, id ? 'Resource updated successfully.' : 'Resource created successfully.')
  }

  async function upsertStock(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/resources/stocks', {
        resourceId: stockForm.resourceId,
        quantityOnHand: Number(stockForm.quantityOnHand),
        lowStockThreshold: Number(stockForm.lowStockThreshold),
      })
      setStockForm({ resourceId: '', quantityOnHand: '', lowStockThreshold: '' })
    }, 'Stock updated successfully.')
  }

  async function reserve(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post<Reservation>('/resources/reservations', {
        inventoryStockId: reservationForm.inventoryStockId,
        quantity: Number(reservationForm.quantity),
        purpose: reservationForm.purpose,
      })
      setReservationForm({ inventoryStockId: '', quantity: '', purpose: '' })
    }, 'Resource reserved successfully.')
  }

  async function updateReservation(id: string, action: 'release' | 'cancel') {
    await api.post<Reservation>(`/resources/reservations/${id}/${action}`)
  }

  function confirmDelete(what: string, name: string, path: string, extra = '') {
    setActionError('')
    setSuccess('')
    setConfirmAction({
      title: `Delete ${what}?`,
      message: `"${name}" will be removed from the ${what.toLowerCase()} list. ${extra}`.trim(),
      label: `Delete ${what}`,
      variant: 'danger',
      action: async () => { await api.delete(path) },
      success: `${what} deleted successfully.`,
    })
  }

  async function loadForecast(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsWeatherLoading(true)
    setWeatherError('')
    try {
      const response = await api.get<WeatherForecast>('/weather/forecast', { params: { location: weatherLocation } })
      setForecast(response.data)
    } catch (err) {
      setForecast(null)
      setWeatherError(getErrorMessage(err))
    } finally {
      setIsWeatherLoading(false)
    }
  }

  const stockRows = (results.inventory?.items ?? []) as InventoryStock[]
  const resourceRows = (results.resources?.items ?? []) as ResourceItem[]
  const categoryRows = (results.categories?.items ?? []) as ResourceCategory[]
  const supplierRows = (results.suppliers?.items ?? []) as Supplier[]
  const reservationRows = (results.reservations?.items ?? []) as Reservation[]
  const sort = view ? { sortBy: view.sortBy, sortDirection: view.sortDirection } : undefined
  const selectedHistoryStock = stocks.find((stock) => stock.id === historyStockId)
  const showSearchToolbar = isPagedTab(activeTab)

  const tabs = [
    { id: 'inventory', label: 'Inventory', count: results.inventory?.totalCount },
    { id: 'resources', label: 'Resources', count: results.resources?.totalCount },
    { id: 'categories', label: 'Categories', count: results.categories?.totalCount },
    { id: 'suppliers', label: 'Suppliers', count: results.suppliers?.totalCount },
    { id: 'reservations', label: 'Reservations', count: results.reservations?.totalCount },
    { id: 'history', label: 'Stock History' },
    { id: 'weather', label: 'Weather' },
  ]

  const pagination = current && view ? (
    <Pagination page={current.page} totalPages={current.totalPages} totalCount={current.totalCount} pageSize={current.pageSize} itemCount={current.items.length} onPageChange={changePage} disabled={isLoading} />
  ) : null
  const emptyFilterHint = hasFilters ? 'No records match the current search or filters.' : undefined

  return (
    <section className="page-stack resource-hub">
      <PageHeader
        eyebrow="Resource Operations"
        title="Resources"
        description="Track agricultural resource availability, suppliers, stock and reservations."
        actions={<Button icon={<Plus size={16} aria-hidden="true" />} onClick={() => openResource()}>Add Resource</Button>}
      >
        {showSearchToolbar ? (
          <Toolbar>
            <form className="search-box" onSubmit={applySearch}>
              <Search size={16} aria-hidden="true" />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search resources" aria-label="Search resources" />
              {activeTab === 'inventory' ? (
                <label className="toolbar-check">
                  <input type="checkbox" checked={lowStockOnly} onChange={(event) => changeFilter(() => setLowStockOnly(event.target.checked))} />
                  <span>Low stock only</span>
                </label>
              ) : null}
              {activeTab === 'resources' ? (
                <>
                  <select aria-label="Filter by category" value={categoryFilter} onChange={(event) => changeFilter(() => setCategoryFilter(event.target.value))}>
                    <option value="">All categories</option>
                    {categoryOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <select aria-label="Filter by supplier" value={supplierFilter} onChange={(event) => changeFilter(() => setSupplierFilter(event.target.value))}>
                    <option value="">All suppliers</option>
                    {supplierOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                </>
              ) : null}
              <Button variant="secondary" type="submit">Search</Button>
              {hasFilters ? <Button variant="ghost" onClick={clearFilters}>Clear filters</Button> : null}
            </form>
          </Toolbar>
        ) : null}
      </PageHeader>

      <Tabs tabs={tabs} activeTab={activeTab} onChange={(tab) => switchTab(tab as ResourceTab)} ariaLabel="Resource sections" />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {actionError && !activeModal ? <Notice tone="error">{actionError}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      {isPagedTab(activeTab) && isLoading ? <LoadingState /> : null}

      {!isLoading && activeTab === 'inventory' ? (
        <section className="work-section">
          <div className="metric-grid compact-metrics">
            <MetricCard label="Stock Records" value={results.inventory?.totalCount ?? 0} icon={<Warehouse size={20} aria-hidden="true" />} />
            <MetricCard label="Low Stock" value={lowStockTotal} tone="warn" />
            <MetricCard label="Out of Stock (this page)" value={stockRows.filter((stock) => stock.availableQuantity <= 0).length} tone="bad" />
          </div>
          <DataTable
            rows={stockRows}
            emptyTitle={hasFilters ? 'No matching stock' : 'No stock records'}
            emptyMessage={emptyFilterHint ?? 'Add a resource and set stock before creating reservations.'}
            getRowKey={(row) => row.id}
            sort={sort}
            onSort={sortBy}
            columns={[
              { header: 'Resource', sortKey: 'resourceName', render: (row) => stockName(row) },
              { header: 'On Hand', sortKey: 'quantityOnHand', render: (row) => formatNumber(row.quantityOnHand) },
              { header: 'Reserved', sortKey: 'reservedQuantity', render: (row) => formatNumber(row.reservedQuantity) },
              { header: 'Available', sortKey: 'availableQuantity', render: (row) => formatNumber(row.availableQuantity) },
              { header: 'Condition', render: (row) => <StatusPill label={stockLabel(row)} tone={stockTone(row)} /> },
              { header: 'Actions', className: 'actions-cell', render: (row) => (
                <div className="row-actions">
                  <Button variant="ghost" onClick={() => openStock(row.resourceId)}>Adjust Stock</Button>
                  <Button variant="ghost" onClick={() => openReserve(row.id)} disabled={row.availableQuantity <= 0}>Reserve</Button>
                  <Button variant="ghost" onClick={() => openHistory(row.id)}>History</Button>
                </div>
              ) },
            ]}
          />
          {pagination}
        </section>
      ) : null}

      {!isLoading && activeTab === 'resources' ? (
        <section className="work-section">
          <div className="section-title section-title-actions"><h2>Resources</h2><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => openResource()}>Add Resource</Button></div>
          <DataTable
            rows={resourceRows}
            emptyTitle={hasFilters ? 'No matching resources' : 'No resources'}
            emptyMessage={emptyFilterHint ?? 'Create resources before setting stock.'}
            getRowKey={(row) => row.id}
            sort={sort}
            onSort={sortBy}
            columns={[
              { header: 'Resource', sortKey: 'name', render: (row) => row.name },
              { header: 'Category', sortKey: 'category', render: (row) => categoryNameById.get(row.resourceCategoryId) ?? row.resourceCategoryId.slice(0, 8) },
              { header: 'Supplier', render: (row) => row.supplierId ? supplierNameById.get(row.supplierId) ?? row.supplierId.slice(0, 8) : 'Not assigned' },
              { header: 'Unit', sortKey: 'unit', render: (row) => row.unit },
              { header: 'Status', sortKey: 'isActive', render: (row) => <StatusPill label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'good' : 'bad'} /> },
              { header: 'Actions', className: 'actions-cell', render: (row) => (
                <div className="row-actions">
                  <Button variant="ghost" onClick={() => openStock(row.id)}>Set Stock</Button>
                  <Button variant="ghost" onClick={() => openResource(row)}>Edit</Button>
                  <Button variant="ghost" onClick={() => confirmDelete('Resource', row.name, `/resources/${row.id}`, 'Its stock record is removed too. Resources with active reservations cannot be deleted.')}>Delete</Button>
                </div>
              ) },
            ]}
          />
          {pagination}
        </section>
      ) : null}

      {!isLoading && activeTab === 'categories' ? (
        <section className="work-section">
          <div className="section-title section-title-actions"><h2>Categories</h2><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => openCategory()}>Add Category</Button></div>
          <DataTable
            rows={categoryRows}
            emptyTitle={hasFilters ? 'No matching categories' : 'No categories'}
            emptyMessage={emptyFilterHint ?? 'Create resource categories to organize inventory.'}
            getRowKey={(row) => row.id}
            sort={sort}
            onSort={sortBy}
            columns={[
              { header: 'Category', sortKey: 'name', render: (row) => row.name },
              { header: 'Description', render: (row) => row.description || 'Not provided' },
              { header: 'Actions', className: 'actions-cell', render: (row) => (
                <div className="row-actions">
                  <Button variant="ghost" onClick={() => openCategory(row)}>Edit</Button>
                  <Button variant="ghost" onClick={() => confirmDelete('Category', row.name, `/resources/categories/${row.id}`, 'Categories used by resources cannot be deleted.')}>Delete</Button>
                </div>
              ) },
            ]}
          />
          {pagination}
        </section>
      ) : null}

      {!isLoading && activeTab === 'suppliers' ? (
        <section className="work-section">
          <div className="section-title section-title-actions"><h2>Suppliers</h2><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => openSupplier()}>Add Supplier</Button></div>
          <DataTable
            rows={supplierRows}
            emptyTitle={hasFilters ? 'No matching suppliers' : 'No suppliers'}
            emptyMessage={emptyFilterHint ?? 'Add suppliers when resource sourcing information is available.'}
            getRowKey={(row) => row.id}
            sort={sort}
            onSort={sortBy}
            columns={[
              { header: 'Supplier', sortKey: 'name', render: (row) => row.name },
              { header: 'Email', sortKey: 'email', render: (row) => row.contactEmail || 'Not provided' },
              { header: 'Phone', sortKey: 'phone', render: (row) => row.phone || 'Not provided' },
              { header: 'Actions', className: 'actions-cell', render: (row) => (
                <div className="row-actions">
                  <Button variant="ghost" onClick={() => openSupplier(row)}>Edit</Button>
                  <Button variant="ghost" onClick={() => confirmDelete('Supplier', row.name, `/resources/suppliers/${row.id}`, 'Suppliers used by resources cannot be deleted.')}>Delete</Button>
                </div>
              ) },
            ]}
          />
          {pagination}
        </section>
      ) : null}

      {!isLoading && activeTab === 'reservations' ? (
        <section className="work-section">
          <div className="section-title section-title-actions">
            <div>
              <h2>Reservations</h2>
              <p className="muted-text">Reserved quantities are held until the reservation is released or cancelled.</p>
            </div>
            <Button variant="secondary" icon={<Package size={16} aria-hidden="true" />} onClick={() => openReserve()}>Reserve Stock</Button>
          </div>
          <DataTable
            rows={reservationRows}
            emptyTitle={hasFilters ? 'No matching reservations' : 'No reservations'}
            emptyMessage={emptyFilterHint ?? 'Reserve stock to hold it for a planned activity.'}
            getRowKey={(row) => row.id}
            columns={[
              { header: 'Resource', render: (row) => row.resourceName || resourceNameById.get(stocks.find((stock) => stock.id === row.inventoryStockId)?.resourceId ?? '') || row.inventoryStockId.slice(0, 8) },
              { header: 'Purpose', render: (row) => row.purpose },
              { header: 'Quantity', render: (row) => formatNumber(row.quantity) },
              { header: 'Status', render: (row) => <StatusPill label={reservationStatus[row.status] ?? String(row.status)} tone={row.status === 1 ? 'info' : row.status === 2 ? 'good' : 'bad'} /> },
              { header: 'Actions', className: 'actions-cell', render: (row) => row.status === 1 ? (
                <div className="row-actions">
                  <Button variant="ghost" onClick={() => setConfirmAction({ title: 'Release reservation?', message: 'This will return the reserved quantity through the existing backend release action.', label: 'Release', action: async () => updateReservation(row.id, 'release'), success: 'Reservation released successfully.' })}>Release</Button>
                  <Button variant="ghost" onClick={() => setConfirmAction({ title: 'Cancel reservation?', message: 'This will cancel the reservation and return the quantity to available stock.', label: 'Cancel Reservation', variant: 'danger', action: async () => updateReservation(row.id, 'cancel'), success: 'Reservation cancelled successfully.' })}>Cancel</Button>
                </div>
              ) : <span className="muted-text">Finalized</span> },
            ]}
          />
          {pagination}
        </section>
      ) : null}

      {activeTab === 'history' ? (
        <section className="work-section">
          <div className="section-title">
            <div>
              <h2>Stock History</h2>
              <p className="muted-text">Every change to on-hand or reserved stock is recorded here, newest first.</p>
            </div>
          </div>
          <div className="search-box">
            <History size={16} aria-hidden="true" />
            <select aria-label="Stock to view" value={historyStockId} onChange={(event) => setHistoryStockId(event.target.value)}>
              <option value="">Select a resource</option>
              {stockOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </div>
          {!historyStockId ? <EmptyState title="Choose a resource" message="Select a resource above to see its stock transactions." /> : null}
          {historyStockId && isHistoryLoading ? <LoadingState label="Loading stock history" /> : null}
          {historyStockId && historyError ? <ErrorState message={historyError} /> : null}
          {historyStockId && !isHistoryLoading && !historyError ? (
            <DataTable
              rows={history}
              emptyTitle="No transactions"
              emptyMessage="No stock changes have been recorded for this resource yet."
              getRowKey={(row) => row.id}
              columns={[
                { header: 'Date and time', render: (row) => formatDateTime(row.createdAt) },
                { header: 'Type', render: (row) => <StatusPill label={stockTransactionType[row.type] ?? String(row.type)} tone={row.type === 1 || row.type === 4 ? 'good' : row.type === 2 ? 'bad' : 'info'} /> },
                { header: 'Quantity', render: (row) => `${formatNumber(row.quantity)}${selectedHistoryStock?.unit ? ` ${selectedHistoryStock.unit}` : ''}` },
                { header: 'Reference / reason', render: (row) => row.note || 'Not provided' },
              ]}
            />
          ) : null}
        </section>
      ) : null}

      {activeTab === 'weather' ? (
        <section className="work-section">
          <div className="section-title">
            <div>
              <h2>Weather Forecast</h2>
              <p className="muted-text">Five-day forecast for a farm location, used when checking whether a planting window is suitable.</p>
            </div>
          </div>
          <form className="search-box" onSubmit={(event) => void loadForecast(event)}>
            <CloudSun size={16} aria-hidden="true" />
            <input value={weatherLocation} onChange={(event) => setWeatherLocation(event.target.value)} placeholder="Farm location, e.g. Kurunegala" aria-label="Weather location" required />
            <Button variant="secondary" type="submit" disabled={isWeatherLoading}>{isWeatherLoading ? 'Loading...' : 'Get Forecast'}</Button>
          </form>
          {weatherError ? <ErrorState message={weatherError} /> : null}
          {forecast && !forecast.isAvailable ? <Notice tone="warning">{forecast.message}</Notice> : null}
          {forecast?.isAvailable ? (
            <DataTable rows={forecast.days} emptyTitle="No forecast" emptyMessage="No forecast days were returned." getRowKey={(row) => row.date} columns={[
              { header: 'Date', render: (row) => formatDate(row.date) },
              { header: 'Conditions', render: (row) => row.description },
              { header: 'Temperature (C)', render: (row) => `${formatNumber(row.minTemperatureC)} - ${formatNumber(row.maxTemperatureC)}` },
              { header: 'Rain (mm)', render: (row) => formatNumber(row.rainMm) },
              { header: 'Wind (m/s)', render: (row) => formatNumber(row.maxWindSpeedMs) },
            ]} />
          ) : null}
        </section>
      ) : null}

      <Modal open={activeModal === 'category'} title={editingId ? 'Edit Category' : 'Add Category'} description={editingId ? 'Update the category name or description.' : 'Create a resource category for inventory organization.'} onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="category-form" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : editingId ? 'Save Changes' : 'Create Category'}</Button></>}>
        <form id="category-form" className="form-grid" onSubmit={(event) => void saveCategory(event)}>
          <TextInput label="Name" value={categoryForm.name} required onChange={(value) => setCategoryForm({ ...categoryForm, name: value })} />
          <TextAreaInput label="Description" value={categoryForm.description} onChange={(value) => setCategoryForm({ ...categoryForm, description: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'supplier'} title={editingId ? 'Edit Supplier' : 'Add Supplier'} description={editingId ? 'Update the supplier details.' : 'Create a supplier record using the existing resources API.'} onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="supplier-form" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : editingId ? 'Save Changes' : 'Create Supplier'}</Button></>}>
        <form id="supplier-form" className="form-grid" onSubmit={(event) => void saveSupplier(event)}>
          <TextInput label="Name" value={supplierForm.name} required onChange={(value) => setSupplierForm({ ...supplierForm, name: value })} />
          <TextInput label="Contact email" type="email" value={supplierForm.contactEmail} required onChange={(value) => setSupplierForm({ ...supplierForm, contactEmail: value })} />
          <TextInput label="Phone" value={supplierForm.phone} required onChange={(value) => setSupplierForm({ ...supplierForm, phone: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'resource'} title={editingId ? 'Edit Resource' : 'Add Resource'} description={editingId ? 'Update the resource details.' : 'Add an agricultural resource to the catalog.'} onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="resource-form" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : editingId ? 'Save Changes' : 'Create Resource'}</Button></>}>
        <form id="resource-form" className="form-grid" onSubmit={(event) => void saveResource(event)}>
          <SelectInput label="Category" value={resourceForm.resourceCategoryId} required options={categoryOptions} onChange={(value) => setResourceForm({ ...resourceForm, resourceCategoryId: value })} />
          <SelectInput label="Supplier" value={resourceForm.supplierId} options={supplierOptions} onChange={(value) => setResourceForm({ ...resourceForm, supplierId: value })} />
          <TextInput label="Name" value={resourceForm.name} required onChange={(value) => setResourceForm({ ...resourceForm, name: value })} />
          <TextInput label="Unit" value={resourceForm.unit} required placeholder="kg, L, units" onChange={(value) => setResourceForm({ ...resourceForm, unit: value })} />
          {editingId ? <SelectInput label="Status" value={resourceForm.isActive} required options={[{ value: 'true', label: 'Active' }, { value: 'false', label: 'Inactive' }]} onChange={(value) => setResourceForm({ ...resourceForm, isActive: value })} /> : null}
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'stock'} title="Adjust Stock" description="Set on-hand quantity and low-stock threshold for a resource." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="stock-form" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : 'Save Stock'}</Button></>}>
        <form id="stock-form" className="form-grid" onSubmit={(event) => void upsertStock(event)}>
          <SelectInput label="Resource" value={stockForm.resourceId} required options={resourceOptions} onChange={(value) => setStockForm({ ...stockForm, resourceId: value })} />
          <TextInput label="On hand" value={stockForm.quantityOnHand} type="number" min="0" step="0.01" required onChange={(value) => setStockForm({ ...stockForm, quantityOnHand: value })} />
          <TextInput label="Low threshold" value={stockForm.lowStockThreshold} type="number" min="0" step="0.01" required onChange={(value) => setStockForm({ ...stockForm, lowStockThreshold: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'reserve'} title="Reserve Stock" description="Reserve available inventory for an operational purpose." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="reservation-form" disabled={isSubmitting}>{isSubmitting ? 'Reserving...' : 'Reserve Stock'}</Button></>}>
        <form id="reservation-form" className="form-grid" onSubmit={(event) => void reserve(event)}>
          <SelectInput label="Stock" value={reservationForm.inventoryStockId} required options={stockOptions} onChange={(value) => setReservationForm({ ...reservationForm, inventoryStockId: value })} />
          <TextInput label="Quantity" value={reservationForm.quantity} type="number" min="0" step="0.01" required onChange={(value) => setReservationForm({ ...reservationForm, quantity: value })} />
          <TextAreaInput label="Purpose" value={reservationForm.purpose} required onChange={(value) => setReservationForm({ ...reservationForm, purpose: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <ConfirmDialog
        open={Boolean(confirmAction)}
        title={confirmAction?.title ?? ''}
        message={confirmAction?.message ?? ''}
        confirmLabel={confirmAction?.label ?? 'Confirm'}
        variant={confirmAction?.variant}
        isSubmitting={isSubmitting}
        onCancel={() => setConfirmAction(null)}
        onConfirm={() => confirmAction ? runAction(confirmAction.action, confirmAction.success) : undefined}
      />
    </section>
  )
}
