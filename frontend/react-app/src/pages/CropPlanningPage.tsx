import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Plus, Search, Sprout } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, MetricCard, Modal, Notice, PageHeader, Tabs, Toolbar } from '../components/Ui'
import { formatArea, formatDate, formatMoney } from '../format'
import { cropPlanStatus } from '../labels'
import type { CropPlan, CropType, Farm, Field, PagedResult } from '../types'

type CropTab = 'overview' | 'farms' | 'fields' | 'cropTypes' | 'requests'
type CropModal = 'farm' | 'field' | 'plan' | null

function getCropPlanTone(status: number) {
  if (status === 4) return 'good'
  if (status === 5 || status === 6) return 'bad'
  if (status === 3) return 'info'
  return 'warn'
}

export function CropPlanningPage() {
  const [farms, setFarms] = useState<Farm[]>([])
  const [fields, setFields] = useState<Field[]>([])
  const [cropTypes, setCropTypes] = useState<CropType[]>([])
  const [requests, setRequests] = useState<CropPlan[]>([])
  const [activeTab, setActiveTab] = useState<CropTab>('overview')
  const [activeModal, setActiveModal] = useState<CropModal>(null)
  const [search, setSearch] = useState('')
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [farmForm, setFarmForm] = useState({ name: '', location: '', totalArea: '' })
  const [fieldForm, setFieldForm] = useState({ farmId: '', name: '', area: '', soilType: '' })
  const [planForm, setPlanForm] = useState({ farmId: '', fieldId: '', cropTypeId: '', preferredStartDate: '', preferredEndDate: '', budget: '', objective: '' })

  const farmOptions = farms.map((farm) => ({ value: farm.id, label: farm.name }))
  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const cropTypeOptions = cropTypes.map((cropType) => ({ value: cropType.id, label: cropType.name }))
  const farmNameById = useMemo(() => new Map(farms.map((farm) => [farm.id, farm.name])), [farms])
  const cropNameById = useMemo(() => new Map(cropTypes.map((crop) => [crop.id, crop.name])), [cropTypes])
  const fieldNameById = useMemo(() => new Map(fields.map((field) => [field.id, field.name])), [fields])

  async function loadData(nextSearch = search) {
    setIsLoading(true)
    setError('')
    try {
      const [farmResult, fieldResult, cropTypeResult, requestResult] = await Promise.all([
        api.get<PagedResult<Farm>>('/crop-planning/farms', { params: { search: nextSearch, sortBy: 'name' } }),
        api.get<PagedResult<Field>>('/crop-planning/fields', { params: { sortBy: 'name' } }),
        api.get<PagedResult<CropType>>('/crop-planning/crop-types', { params: { sortBy: 'name' } }),
        api.get<PagedResult<CropPlan>>('/crop-planning/requests', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
      ])
      setFarms(farmResult.data.items)
      setFields(fieldResult.data.items)
      setCropTypes(cropTypeResult.data.items)
      setRequests(requestResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData('')
  }, [])

  function closeModal() {
    setActiveModal(null)
    setActionError('')
  }

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setActionError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      closeModal()
      await loadData()
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function createFarm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/crop-planning/farms', { ...farmForm, totalArea: Number(farmForm.totalArea), ownerUserId: null })
      setFarmForm({ name: '', location: '', totalArea: '' })
    }, 'Farm created successfully.')
  }

  async function createField(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/crop-planning/fields', { ...fieldForm, area: Number(fieldForm.area), isActive: true })
      setFieldForm({ farmId: '', name: '', area: '', soilType: '' })
    }, 'Field created successfully.')
  }

  async function createPreliminary(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAction(async () => {
      await api.post('/crop-planning/requests/preliminary', {
        farmId: planForm.farmId,
        fieldId: planForm.fieldId || null,
        cropTypeId: planForm.cropTypeId,
        preferredStartDate: planForm.preferredStartDate,
        preferredEndDate: planForm.preferredEndDate,
        budget: Number(planForm.budget),
        objective: planForm.objective,
      })
      setPlanForm({ farmId: '', fieldId: '', cropTypeId: '', preferredStartDate: '', preferredEndDate: '', budget: '', objective: '' })
    }, 'Planning request created successfully.')
  }

  const tabs = [
    { id: 'overview', label: 'Overview' },
    { id: 'farms', label: 'Farms', count: farms.length },
    { id: 'fields', label: 'Fields', count: fields.length },
    { id: 'cropTypes', label: 'Crop Types', count: cropTypes.length },
    { id: 'requests', label: 'Plan Requests', count: requests.length },
  ]

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Crop Planning"
        title="Crop Planning"
        description="Manage farms, fields, crop types and planning requests."
        actions={
          <>
            <Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('farm')}>Add Farm</Button>
            <Button variant="secondary" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setActiveModal('field')}>Add Field</Button>
            <Button icon={<Sprout size={16} aria-hidden="true" />} onClick={() => setActiveModal('plan')}>Create Planning Request</Button>
          </>
        }
      >
        <Toolbar>
          <form className="search-box" onSubmit={(event) => { event.preventDefault(); void loadData(search) }}>
            <Search size={16} aria-hidden="true" />
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search farms" aria-label="Search farms" />
            <Button variant="secondary" type="submit">Search</Button>
          </form>
        </Toolbar>
      </PageHeader>

      <Tabs tabs={tabs} activeTab={activeTab} onChange={(tab) => setActiveTab(tab as CropTab)} ariaLabel="Crop planning sections" />
      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}

      {isLoading ? <LoadingState /> : (
        <>
          {activeTab === 'overview' ? (
            <div className="metric-grid">
              <MetricCard label="Farms" value={farms.length} description="Farm records returned by the API." icon={<Sprout size={20} aria-hidden="true" />} />
              <MetricCard label="Fields" value={fields.length} description="Fields connected to registered farms." />
              <MetricCard label="Crop Types" value={cropTypes.length} description="Available crop types for planning requests." />
              <MetricCard label="Plan Requests" value={requests.length} description="Planning requests from the backend." tone={requests.length > 0 ? 'warn' : 'neutral'} />
            </div>
          ) : null}

          {activeTab === 'farms' ? (
            <section className="work-section">
              <div className="section-title"><h2>Farms</h2></div>
              <DataTable
                rows={farms}
                emptyTitle="No farms found"
                emptyMessage="Create a farm record before adding fields or planning requests."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Farm Name', render: (row) => row.name },
                  { header: 'Location', render: (row) => row.location },
                  { header: 'Area', render: (row) => formatArea(row.totalArea) },
                  { header: 'Created', render: (row) => formatDate(row.createdAt) },
                ]}
              />
            </section>
          ) : null}

          {activeTab === 'fields' ? (
            <section className="work-section">
              <div className="section-title"><h2>Fields</h2></div>
              <DataTable
                rows={fields}
                emptyTitle="No fields found"
                emptyMessage="Add fields to a farm so inspections and planning requests can reference them."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Field', render: (row) => row.name },
                  { header: 'Farm', render: (row) => farmNameById.get(row.farmId) ?? row.farmId.slice(0, 8) },
                  { header: 'Area', render: (row) => formatArea(row.area) },
                  { header: 'Soil Type', render: (row) => row.soilType },
                  { header: 'Status', render: (row) => <StatusPill label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'good' : 'bad'} /> },
                ]}
              />
            </section>
          ) : null}

          {activeTab === 'cropTypes' ? (
            <section className="work-section">
              <div className="section-title"><h2>Crop Types</h2></div>
              <DataTable
                rows={cropTypes}
                emptyTitle="No crop types found"
                emptyMessage="Crop type records are managed through the existing API seed/admin flow."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Crop Type', render: (row) => row.name },
                  { header: 'Description', render: (row) => row.description || 'Not provided' },
                  { header: 'Status', render: (row) => <StatusPill label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'good' : 'bad'} /> },
                ]}
              />
            </section>
          ) : null}

          {activeTab === 'requests' ? (
            <section className="work-section">
              <div className="section-title"><h2>Plan Requests</h2></div>
              <DataTable
                rows={requests}
                emptyTitle="No crop plan requests"
                emptyMessage="Create the first planning request when a farm is ready for seasonal review."
                getRowKey={(row) => row.id}
                columns={[
                  { header: 'Farm', render: (row) => farmNameById.get(row.farmId) ?? row.farmId.slice(0, 8) },
                  { header: 'Field', render: (row) => row.fieldId ? fieldNameById.get(row.fieldId) ?? row.fieldId.slice(0, 8) : 'Not selected' },
                  { header: 'Crop', render: (row) => cropNameById.get(row.cropTypeId) ?? row.cropTypeId.slice(0, 8) },
                  { header: 'Window', render: (row) => `${formatDate(row.preferredStartDate)} to ${formatDate(row.preferredEndDate)}` },
                  { header: 'Budget', render: (row) => formatMoney(row.budget) },
                  { header: 'Status', render: (row) => <StatusPill label={cropPlanStatus[row.status] ?? String(row.status)} tone={getCropPlanTone(row.status)} /> },
                ]}
              />
            </section>
          ) : null}
        </>
      )}

      <Modal open={activeModal === 'farm'} title="Add Farm" description="Create a farm record for future field and planning workflows." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="farm-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Farm'}</Button></>}>
        <form id="farm-form" className="form-grid" onSubmit={(event) => void createFarm(event)}>
          <TextInput label="Name" value={farmForm.name} placeholder="Farm name" required onChange={(value) => setFarmForm({ ...farmForm, name: value })} />
          <TextInput label="Location" value={farmForm.location} placeholder="Farm location" required onChange={(value) => setFarmForm({ ...farmForm, location: value })} />
          <TextInput label="Total area" value={farmForm.totalArea} type="number" min="0" step="0.01" placeholder="2.5" required onChange={(value) => setFarmForm({ ...farmForm, totalArea: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'field'} title="Add Field" description="Add a field under an existing farm." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="field-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Field'}</Button></>}>
        <form id="field-form" className="form-grid" onSubmit={(event) => void createField(event)}>
          <SelectInput label="Farm" value={fieldForm.farmId} required options={farmOptions} onChange={(value) => setFieldForm({ ...fieldForm, farmId: value })} />
          <TextInput label="Name" value={fieldForm.name} placeholder="Field name" required onChange={(value) => setFieldForm({ ...fieldForm, name: value })} />
          <TextInput label="Area" value={fieldForm.area} type="number" min="0" step="0.01" placeholder="1.25" required onChange={(value) => setFieldForm({ ...fieldForm, area: value })} />
          <TextInput label="Soil type" value={fieldForm.soilType} placeholder="Loam" required onChange={(value) => setFieldForm({ ...fieldForm, soilType: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={activeModal === 'plan'} title="Create Planning Request" description="Submit a preliminary crop planning request through the existing API." onClose={closeModal} footer={<><Button variant="secondary" onClick={closeModal} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="plan-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create Request'}</Button></>}>
        <form id="plan-form" className="form-grid" onSubmit={(event) => void createPreliminary(event)}>
          <SelectInput label="Farm" value={planForm.farmId} required options={farmOptions} onChange={(value) => setPlanForm({ ...planForm, farmId: value })} />
          <SelectInput label="Field" value={planForm.fieldId} options={fieldOptions} onChange={(value) => setPlanForm({ ...planForm, fieldId: value })} />
          <SelectInput label="Crop type" value={planForm.cropTypeId} required options={cropTypeOptions} onChange={(value) => setPlanForm({ ...planForm, cropTypeId: value })} />
          <TextInput label="Start date" type="date" value={planForm.preferredStartDate} required onChange={(value) => setPlanForm({ ...planForm, preferredStartDate: value })} />
          <TextInput label="End date" type="date" value={planForm.preferredEndDate} required onChange={(value) => setPlanForm({ ...planForm, preferredEndDate: value })} />
          <TextInput label="Budget" type="number" min="0" step="1" value={planForm.budget} placeholder="30000" required onChange={(value) => setPlanForm({ ...planForm, budget: value })} />
          <TextAreaInput label="Objective" value={planForm.objective} placeholder="Describe the planning objective" required onChange={(value) => setPlanForm({ ...planForm, objective: value })} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>
    </section>
  )
}
