import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Package, Plus, Search, Warehouse } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, MetricCard, Modal, Notice, PageHeader, Tabs, Toolbar } from '../components/Ui'
import { formatNumber } from '../format'
import { reservationStatus } from '../labels'
import type { InventoryStock, PagedResult, Reservation, ResourceCategory, ResourceItem, Supplier } from '../types'

type ResourceTab = 'inventory' | 'resources' | 'categories' | 'suppliers' | 'reservations'
type ResourceModal = 'category' | 'supplier' | 'resource' | 'stock' | 'reserve' | null

type ConfirmAction = {
  title: string
  message: string
  label: string
  variant?: 'primary' | 'danger'
  action: () => Promise<void>
  success: string
} | null

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

export function ResourcesPage() {
  const [categories, setCategories] = useState<ResourceCategory[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [resources, setResources] = useState<ResourceItem[]>([])
  const [stocks, setStocks] = useState<InventoryStock[]>([])
  const [reservations, setReservations] = useState<Reservation[]>([])
  const [activeTab, setActiveTab] = useState<ResourceTab>('inventory')
  const [activeModal, setActiveModal] = useState<ResourceModal>(null)
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null)
  const [search, setSearch] = useState('')
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [categoryForm, setCategoryForm] = useState({ name: '', description: '' })
  const [supplierForm, setSupplierForm] = useState({ name: '', contactEmail: '', phone: '' })
  const [resourceForm, setResourceForm] = useState({ resourceCategoryId: '', supplierId: '', name: '', unit: '' })
  const [stockForm, setStockForm] = useState({ resourceId: '', quantityOnHand: '', lowStockThreshold: '' })
  const [reservationForm, setReservationForm] = useState({ inventoryStockId: '', quantity: '', purpose: '' })

  const categoryOptions = categories.map((category) => ({ value: category.id, label: category.name }))
  const supplierOptions = suppliers.map((supplier) => ({ value: supplier.id, label: supplier.name }))
  const resourceOptions = resources.map((resource) => ({ value: resource.id, label: resource.name }))
  const stockOptions = stocks.map((stock) => ({ value: stock.id, label: `${resources.find((item) => item.id === stock.resourceId)?.name ?? stock.resourceId.slice(0, 8)} (${formatNumber(stock.availableQuantity)} available)` }))
  const resourceNameById = useMemo(() => new Map(resources.map((resource) => [resource.id, resource.name])), [resources])
  const categoryNameById = useMemo(() => new Map(categories.map((category) => [category.id, category.name])), [categories])
  const supplierNameById = useMemo(() => new Map(suppliers.map((supplier) => [supplier.id, supplier.name])), [suppliers])

  async function loadData(nextSearch = search, nextLowStockOnly = lowStockOnly) {
    setIsLoading(true)
    setError('')
    try {
      const [categoryResult, supplierResult, resourceResult, stockResult] = await Promise.all([
        api.get<PagedResult<ResourceCategory>>('/resources/categories', { params: { search: nextSearch, sortBy: 'name' } }),
        api.get<PagedResult<Supplier>>('/resources/suppliers', { params: { search: nextSearch, sortBy: 'name' } }),
        api.get<PagedResult<ResourceItem>>('/resources', { params: { search: nextSearch, sortBy: 'name' } }),
        api.get<PagedResult<InventoryStock>>('/resources/stocks', { params: { search: nextSearch, sortBy: 'createdAt', sortDirection: 'desc', lowStockOnly: nextLowStockOnly || undefined } }),
      ])
      setCategories(categoryResult.data.items)
      setSuppliers(supplierResult.data.items)
      setResources(resourceResult.data.items)
      setStocks(stockResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData('', false)
  }, [])

  function closeModal() {
    setActiveModal(null)
    setActionError('')
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

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setActionError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      closeModal()
      setConfirmAction(null)
      await loadData()
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function createCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/resources/categories', categoryForm)
      setCategoryForm({ name: '', description: '' })
    }, 'Category created successfully.')
  }

  async function createSupplier(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/resources/suppliers', supplierForm)
      setSupplierForm({ name: '', contactEmail: '', phone: '' })
    }, 'Supplier created successfully.')
  }

  async function createResource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/resources', { ...resourceForm, supplierId: resourceForm.supplierId || null, isActive: true })
      setResourceForm({ resourceCategoryId: '', supplierId: '', name: '', unit: '' })
    }, 'Resource created successfully.')
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
      const response = await api.post<Reservation>('/resources/reservations', {
        inventoryStockId: reservationForm.inventoryStockId,
        quantity: Number(reservationForm.quantity),
        purpose: reservationForm.purpose,
      })
      setReservations((current) => [response.data, ...current])
      setReservationForm({ inventoryStockId: '', quantity: '', purpose: '' })
    }, 'Resource reserved successfully.')
  }

  async function updateReservation(id: string, action: 'release' | 'cancel') {
    const response = await api.post<Reservation>(`/resources/reservations/${id}/${action}`)
    setReservations((current) => current.map((reservation) => reservation.id === id ? response.data : reservation))
  }

  const tabs = [
    { id: 'inventory', label: 'Inventory', count: stocks.length },
    { id: 'resources', label: 'Resources', count: resources.length },
    { id: 'categories', label: 'Categories', count: categories.length },
    { id: 'suppliers', label: 'Suppliers', count: suppliers.length },
    { id: 'reservations', label: 'Reservations', count: reservations.length },
  ]

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Resource Operations"
        title="Resources"
        description="Track agricultural resource availability, suppliers, stock and reservations."
        actions={<Button icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('resource')}>Add Resource</Button>}
      >
        <Toolbar>
          <form className="search-box" onSubmit={(event) => { event.preventDefault(); void loadData(search, lowStockOnly) }}>
            <Search size={16} aria-hidden="true" />
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search resources" aria-label="Search resources" />
            <label className="toolbar-check">
              <input type="checkbox" checked={lowStockOnly} onChange={(event) => { setLowStockOnly(event.target.checked); void loadData(search, event.target.checked) }} />
              <span>Low stock only</span>
            </label>
            <Button variant="secondary" type="submit">Search</Button>
          </form>
        </Toolbar>
      </PageHeader>

      <Tabs tabs={tabs} activeTab={activeTab} onChange={(tab) => setActiveTab(tab as ResourceTab)} ariaLabel="Resource sections" />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      {isLoading ? <LoadingState /> : (
        <>
          {activeTab === 'inventory' ? (
            <section className="work-section">
              <div className="metric-grid compact-metrics">
                <MetricCard label="Stock Records" value={stocks.length} icon={<Warehouse size={20} aria-hidden="true" />} />
                <MetricCard label="Low Stock" value={stocks.filter((stock) => stock.availableQuantity <= stock.lowStockThreshold).length} tone="warn" />
                <MetricCard label="Out of Stock" value={stocks.filter((stock) => stock.availableQuantity <= 0).length} tone="bad" />
              </div>
              <DataTable
                rows={stocks}
                emptyTitle="No stock records"
                emptyMessage="Add a resource and set stock before creating reservations."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Resource', render: (row) => resourceNameById.get(row.resourceId) ?? row.resourceId.slice(0, 8) },
                  { header: 'On Hand', render: (row) => formatNumber(row.quantityOnHand) },
                  { header: 'Reserved', render: (row) => formatNumber(row.reservedQuantity) },
                  { header: 'Available', render: (row) => formatNumber(row.availableQuantity) },
                  { header: 'Condition', render: (row) => <StatusPill label={stockLabel(row)} tone={stockTone(row)} /> },
                  { header: 'Actions', className: 'actions-cell', render: (row) => (
                    <div className="row-actions">
                      <Button variant="ghost" onClick={() => openStock(row.resourceId)}>Adjust Stock</Button>
                      <Button variant="ghost" onClick={() => openReserve(row.id)} disabled={row.availableQuantity <= 0}>Reserve</Button>
                    </div>
                  ) },
                ]}
              />
            </section>
          ) : null}

          {activeTab === 'resources' ? (
            <section className="work-section">
              <div className="section-title section-title-actions"><h2>Resources</h2><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('resource')}>Add Resource</Button></div>
              <DataTable rows={resources} emptyTitle="No resources" emptyMessage="Create resources before setting stock." getRowKey={(row) => row.id} columns={[
                { header: 'Resource', render: (row) => row.name },
                { header: 'Category', render: (row) => categoryNameById.get(row.resourceCategoryId) ?? row.resourceCategoryId.slice(0, 8) },
                { header: 'Supplier', render: (row) => row.supplierId ? supplierNameById.get(row.supplierId) ?? row.supplierId.slice(0, 8) : 'Not assigned' },
                { header: 'Unit', render: (row) => row.unit },
                { header: 'Status', render: (row) => <StatusPill label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'good' : 'bad'} /> },
                { header: 'Actions', className: 'actions-cell', render: (row) => <Button variant="ghost" onClick={() => openStock(row.id)}>Set Stock</Button> },
              ]} />
            </section>
          ) : null}

          {activeTab === 'categories' ? (
            <section className="work-section">
              <div className="section-title section-title-actions"><h2>Categories</h2><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('category')}>Add Category</Button></div>
              <DataTable rows={categories} emptyTitle="No categories" emptyMessage="Create resource categories to organize inventory." getRowKey={(row) => row.id} columns={[
                { header: 'Category', render: (row) => row.name },
                { header: 'Description', render: (row) => row.description || 'Not provided' },
              ]} />
            </section>
          ) : null}

          {activeTab === 'suppliers' ? (
            <section className="work-section">
              <div className="section-title section-title-actions"><h2>Suppliers</h2><Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('supplier')}>Add Supplier</Button></div>
              <DataTable rows={suppliers} emptyTitle="No suppliers" emptyMessage="Add suppliers when resource sourcing information is available." getRowKey={(row) => row.id} columns={[
                { header: 'Supplier', render: (row) => row.name },
                { header: 'Email', render: (row) => row.contactEmail || 'Not provided' },
                { header: 'Phone', render: (row) => row.phone || 'Not provided' },
              ]} />
            </section>
          ) : null}

          {activeTab === 'reservations' ? (
            <section className="work-section">
              <div className="section-title section-title-actions">
                <div>
                  <h2>Reservations</h2>
                  <p className="muted-text">The backend supports reserve, release and cancel actions, but does not expose a persisted reservation list endpoint.</p>
                </div>
                <Button variant="secondary" icon={<Package size={16} aria-hidden="true" />} onClick={() => openReserve()}>Reserve Stock</Button>
              </div>
              <DataTable rows={reservations} emptyTitle="No session reservations" emptyMessage="Reservations created in this browser session will appear here." getRowKey={(row) => row.id} columns={[
                { header: 'Resource', render: (row) => resourceNameById.get(stocks.find((stock) => stock.id === row.inventoryStockId)?.resourceId ?? '') ?? row.inventoryStockId.slice(0, 8) },
                { header: 'Purpose', render: (row) => row.purpose },
                { header: 'Quantity', render: (row) => formatNumber(row.quantity) },
                { header: 'Status', render: (row) => <StatusPill label={reservationStatus[row.status] ?? String(row.status)} tone={row.status === 1 ? 'info' : row.status === 2 ? 'good' : 'bad'} /> },
                { header: 'Actions', className: 'actions-cell', render: (row) => row.status === 1 ? (
                  <div className="row-actions">
                    <Button variant="ghost" onClick={() => setConfirmAction({ title: 'Release reservation?', message: 'This will return the reserved quantity through the existing backend release action.', label: 'Release', action: async () => updateReservation(row.id, 'release'), success: 'Reservation released successfully.' })}>Release</Button>
                    <Button variant="ghost" onClick={() => setConfirmAction({ title: 'Cancel reservation?', message: 'This will cancel the reservation and return the quantity to available stock.', label: 'Cancel Reservation', variant: 'danger', action: async () => updateReservation(row.id, 'cancel'), success: 'Reservation cancelled successfully.' })}>Cancel</Button>
                  </div>
                ) : <span className="muted-text">Finalized</span> },
              ]} />
            </section>
          ) : null}
        </>
      )}

      <Modal open={activeModal === 'category'} title="Add Category" description="Create a resource category for inventory organization." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="category-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Category'}</Button></>}>
        <form id="category-form" className="form-grid" onSubmit={(event) => void createCategory(event)}>
          <TextInput label="Name" value={categoryForm.name} required onChange={(value) => setCategoryForm({ ...categoryForm, name: value })} />
          <TextAreaInput label="Description" value={categoryForm.description} onChange={(value) => setCategoryForm({ ...categoryForm, description: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'supplier'} title="Add Supplier" description="Create a supplier record using the existing resources API." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="supplier-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Supplier'}</Button></>}>
        <form id="supplier-form" className="form-grid" onSubmit={(event) => void createSupplier(event)}>
          <TextInput label="Name" value={supplierForm.name} required onChange={(value) => setSupplierForm({ ...supplierForm, name: value })} />
          <TextInput label="Contact email" type="email" value={supplierForm.contactEmail} required onChange={(value) => setSupplierForm({ ...supplierForm, contactEmail: value })} />
          <TextInput label="Phone" value={supplierForm.phone} required onChange={(value) => setSupplierForm({ ...supplierForm, phone: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'resource'} title="Add Resource" description="Add an agricultural resource to the catalog." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="resource-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Resource'}</Button></>}>
        <form id="resource-form" className="form-grid" onSubmit={(event) => void createResource(event)}>
          <SelectInput label="Category" value={resourceForm.resourceCategoryId} required options={categoryOptions} onChange={(value) => setResourceForm({ ...resourceForm, resourceCategoryId: value })} />
          <SelectInput label="Supplier" value={resourceForm.supplierId} options={supplierOptions} onChange={(value) => setResourceForm({ ...resourceForm, supplierId: value })} />
          <TextInput label="Name" value={resourceForm.name} required onChange={(value) => setResourceForm({ ...resourceForm, name: value })} />
          <TextInput label="Unit" value={resourceForm.unit} required placeholder="kg, L, units" onChange={(value) => setResourceForm({ ...resourceForm, unit: value })} />
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

