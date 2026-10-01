import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { BookOpenCheck, Loader2, Pencil, Plus, Power, Search, Sparkles, Sprout, Trash2 } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { FindingSummary, ReferenceDiscoveryReview, SuggestionReview } from '../components/CropFindingReview'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { StatusPill } from '../components/StatusPill'
import { Button, Modal, Notice, Tabs } from '../components/Ui'
import { formatDate } from '../format'
import type { CropFindingSuggestion, CropSuggestionsResponse, ReferenceDiscoveryResponse, ReferenceDraftItem, ReviewDecision, VarietySuggestionsResponse } from '../cropFinding'
import type { CropReferenceProfile, CropType, CropVariety, PagedResult } from '../types'
import './AdminCropManagement.css'

type AdminTab = 'crops' | 'varieties' | 'references'
type StatusFilter = 'all' | 'active' | 'inactive'
type DeleteTarget = { kind: 'crop' | 'variety'; id: string; name: string } | null
type StageForm = { stageName: string; sequence: number; typicalMinDays: string; typicalMaxDays: string; notes: string }
type RuleForm = { ruleType: string; ruleKey: string; structuredValueJson: string }
type FindingAction = 'crops' | 'varieties' | 'references' | null
type PendingPartial =
  | { kind: 'crop'; id: string; edit: boolean }
  | { kind: 'variety'; id: string; edit: boolean }
  | { kind: 'reference'; sourceId: string; id: string; edit: boolean }
  | null
type ReferenceSnapshot = {
  form: ReturnType<typeof emptyReferenceForm>
  stages: StageForm[]
  rules: RuleForm[]
}

const emptyCropForm = () => ({ id: '', name: '', description: '', isActive: true })
const emptyVarietyForm = () => ({ id: '', cropTypeId: '', name: '', isActive: true })
const emptyReferenceForm = () => ({ cropTypeId: '', cropVarietyId: '', region: '', sourceName: '', sourceUrl: '', sourceVersion: '', verifiedAt: '' })
const emptyStage = (): StageForm => ({ stageName: '', sequence: 1, typicalMinDays: '', typicalMaxDays: '', notes: '' })
const emptyRule = (): RuleForm => ({ ruleType: '', ruleKey: '', structuredValueJson: '' })

async function allItems<T>(path: string): Promise<T[]> {
  const first = await api.get<PagedResult<T>>(path, { params: { page: 1, pageSize: 100 } })
  const pages = await Promise.all(Array.from({ length: first.data.totalPages - 1 }, (_, index) =>
    api.get<PagedResult<T>>(path, { params: { page: index + 2, pageSize: 100 } })))
  return [first.data, ...pages.map((page) => page.data)].flatMap((page) => page.items)
}

function matchesStatus(isActive: boolean, filter: StatusFilter) {
  return filter === 'all' || (filter === 'active' ? isActive : !isActive)
}

function objectValue(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null
}

function textValue(value: unknown): string {
  return typeof value === 'string' ? value : value == null ? '' : String(value)
}

function numericValue(...values: unknown[]): string {
  const value = values.find((candidate) => typeof candidate === 'number' && Number.isFinite(candidate))
  return value === undefined ? '' : String(value)
}

export function AdminCropManagement() {
  const [activeTab, setActiveTab] = useState<AdminTab>('crops')
  const [crops, setCrops] = useState<CropType[]>([])
  const [varieties, setVarieties] = useState<CropVariety[]>([])
  const [profiles, setProfiles] = useState<CropReferenceProfile[]>([])
  const [cropForm, setCropForm] = useState(emptyCropForm)
  const [varietyForm, setVarietyForm] = useState(emptyVarietyForm)
  const [referenceForm, setReferenceForm] = useState(emptyReferenceForm)
  const [stages, setStages] = useState<StageForm[]>([emptyStage()])
  const [rules, setRules] = useState<RuleForm[]>([])
  const [cropSearch, setCropSearch] = useState('')
  const [cropStatus, setCropStatus] = useState<StatusFilter>('all')
  const [varietySearch, setVarietySearch] = useState('')
  const [varietyStatus, setVarietyStatus] = useState<StatusFilter>('all')
  const [cropDialogOpen, setCropDialogOpen] = useState(false)
  const [varietyDialogOpen, setVarietyDialogOpen] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget>(null)
  const [error, setError] = useState('')
  const [dialogError, setDialogError] = useState('')
  const [success, setSuccess] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [findingAction, setFindingAction] = useState<FindingAction>(null)
  const [findingErrors, setFindingErrors] = useState<Record<Exclude<FindingAction, null>, string>>({ crops: '', varieties: '', references: '' })
  const [cropSuggestions, setCropSuggestions] = useState<CropSuggestionsResponse | null>(null)
  const [varietySuggestions, setVarietySuggestions] = useState<VarietySuggestionsResponse | null>(null)
  const [referenceDiscovery, setReferenceDiscovery] = useState<ReferenceDiscoveryResponse | null>(null)
  const [varietyFindingCropId, setVarietyFindingCropId] = useState('')
  const [cropReview, setCropReview] = useState<Record<string, ReviewDecision>>({})
  const [varietyReview, setVarietyReview] = useState<Record<string, ReviewDecision>>({})
  const [referenceReview, setReferenceReview] = useState<Record<string, ReviewDecision>>({})
  const [primarySourceId, setPrimarySourceId] = useState('')
  const [referenceSnapshot, setReferenceSnapshot] = useState<ReferenceSnapshot | null>(null)
  const [pendingPartial, setPendingPartial] = useState<PendingPartial>(null)

  async function load() {
    const [nextCrops, nextVarieties, nextProfiles] = await Promise.all([
      allItems<CropType>('/crop-planning/crop-types?includeInactive=true'),
      allItems<CropVariety>('/crop-planning/crop-varieties?includeInactive=true'),
      allItems<CropReferenceProfile>('/crop-planning/crop-reference-profiles'),
    ])
    setCrops(nextCrops)
    setVarieties(nextVarieties)
    setProfiles(nextProfiles)
  }

  useEffect(() => {
    async function initialize() {
      try {
        await load()
      } catch (cause) {
        setError(getErrorMessage(cause))
      } finally {
        setLoading(false)
      }
    }

    void initialize()
  }, [])

  async function run(action: () => Promise<void>, message: string, errorTarget: 'page' | 'dialog' = 'page') {
    setBusy(true)
    setError('')
    setDialogError('')
    setSuccess('')
    try {
      await action()
      await load()
      setSuccess(message)
      return true
    } catch (cause) {
      const message = getErrorMessage(cause)
      if (errorTarget === 'dialog') setDialogError(message)
      else setError(message)
      return false
    } finally {
      setBusy(false)
    }
  }

  function openAddCrop() {
    setCropForm(emptyCropForm())
    setDialogError('')
    setCropDialogOpen(true)
  }

  function openEditCrop(crop: CropType) {
    setCropForm({ id: crop.id, name: crop.name, description: crop.description ?? '', isActive: crop.isActive })
    setDialogError('')
    setCropDialogOpen(true)
  }

  function openAddVariety() {
    setVarietyForm(emptyVarietyForm())
    setDialogError('')
    setVarietyDialogOpen(true)
  }

  function openEditVariety(variety: CropVariety) {
    setVarietyForm({ id: variety.id, cropTypeId: variety.cropTypeId, name: variety.name, isActive: variety.isActive })
    setDialogError('')
    setVarietyDialogOpen(true)
  }

  async function saveCrop(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!cropForm.name.trim()) {
      setDialogError('Crop name is required.')
      return
    }
    const editing = Boolean(cropForm.id)
    const saved = await run(async () => {
      const body = { name: cropForm.name.trim(), description: cropForm.description.trim() || null, isActive: cropForm.isActive }
      if (editing) await api.put(`/crop-planning/crop-types/${cropForm.id}`, body)
      else await api.post('/crop-planning/crop-types', body)
    }, editing ? 'Crop updated.' : 'Crop created.', 'dialog')
    if (saved) {
      setCropDialogOpen(false)
      setCropForm(emptyCropForm())
      if (!editing) {
        setCropSuggestions(null)
        setCropReview({})
      }
    }
  }

  async function saveVariety(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!varietyForm.cropTypeId || !varietyForm.name.trim()) {
      setDialogError('Crop and variety name are required.')
      return
    }
    const editing = Boolean(varietyForm.id)
    const saved = await run(async () => {
      const body = { cropTypeId: varietyForm.cropTypeId, name: varietyForm.name.trim(), isActive: varietyForm.isActive }
      if (editing) await api.put(`/crop-planning/crop-varieties/${varietyForm.id}`, body)
      else await api.post('/crop-planning/crop-varieties', body)
    }, editing ? 'Variety updated.' : 'Variety created.', 'dialog')
    if (saved) {
      setVarietyDialogOpen(false)
      setVarietyForm(emptyVarietyForm())
      if (!editing) {
        setVarietySuggestions(null)
        setVarietyReview({})
      }
    }
  }

  async function setCropActive(crop: CropType) {
    const isActive = !crop.isActive
    await run(async () => {
      await api.put(`/crop-planning/crop-types/${crop.id}`, {
        name: crop.name,
        description: crop.description ?? null,
        isActive,
      })
    }, `Crop ${isActive ? 'reactivated' : 'deactivated'}.`)
  }

  async function setVarietyActive(variety: CropVariety) {
    const isActive = !variety.isActive
    await run(async () => {
      await api.put(`/crop-planning/crop-varieties/${variety.id}`, {
        cropTypeId: variety.cropTypeId,
        name: variety.name,
        isActive,
      })
    }, `Variety ${isActive ? 'reactivated' : 'deactivated'}.`)
  }

  function openDelete(target: Exclude<DeleteTarget, null>) {
    setDialogError('')
    setDeleteTarget(target)
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    const label = deleteTarget.kind === 'crop' ? 'Crop' : 'Variety'
    const path = deleteTarget.kind === 'crop' ? 'crop-types' : 'crop-varieties'
    const deleted = await run(async () => {
      await api.delete(`/crop-planning/${path}/${deleteTarget.id}`)
    }, `${label} deleted.`, 'dialog')
    if (deleted) setDeleteTarget(null)
  }

  async function findCrops() {
    setFindingAction('crops')
    setFindingErrors((current) => ({ ...current, crops: '' }))
    try {
      const response = await api.post<CropSuggestionsResponse>('/crop-finding/suggest-crops', { maxSuggestions: 8 })
      setCropSuggestions(response.data)
      setCropReview({})
    } catch (cause) {
      setFindingErrors((current) => ({ ...current, crops: getErrorMessage(cause) }))
    } finally {
      setFindingAction(null)
    }
  }

  async function findVarieties() {
    if (!varietyFindingCropId) return
    setFindingAction('varieties')
    setFindingErrors((current) => ({ ...current, varieties: '' }))
    try {
      const response = await api.post<VarietySuggestionsResponse>('/crop-finding/suggest-varieties', {
        cropTypeId: varietyFindingCropId,
        maxSuggestions: 8,
      })
      setVarietySuggestions(response.data)
      setVarietyReview({})
    } catch (cause) {
      setFindingErrors((current) => ({ ...current, varieties: getErrorMessage(cause) }))
    } finally {
      setFindingAction(null)
    }
  }

  async function findReferences() {
    if (!referenceForm.cropTypeId) return
    setFindingAction('references')
    setFindingErrors((current) => ({ ...current, references: '' }))
    try {
      const response = await api.post<ReferenceDiscoveryResponse>('/crop-finding/discover-references', {
        cropTypeId: referenceForm.cropTypeId,
        cropVarietyId: referenceForm.cropVarietyId || null,
        region: referenceForm.region.trim() || null,
      })
      setReferenceDiscovery(response.data)
      setReferenceReview({})
      setPrimarySourceId('')
      setReferenceSnapshot(null)
    } catch (cause) {
      setFindingErrors((current) => ({ ...current, references: getErrorMessage(cause) }))
    } finally {
      setFindingAction(null)
    }
  }

  function prefillCropSuggestion(suggestion: CropFindingSuggestion, edit: boolean) {
    setCropReview((current) => ({ ...current, [suggestion.id]: edit ? 'edited' : 'accepted' }))
    setCropForm({ id: '', name: suggestion.name, description: suggestion.description ?? '', isActive: true })
    setDialogError('')
    setCropDialogOpen(true)
  }

  function prefillVarietySuggestion(suggestion: CropFindingSuggestion, edit: boolean) {
    setVarietyReview((current) => ({ ...current, [suggestion.id]: edit ? 'edited' : 'accepted' }))
    setVarietyForm({ id: '', cropTypeId: varietyFindingCropId, name: suggestion.name, isActive: true })
    setDialogError('')
    setVarietyDialogOpen(true)
  }

  function requestSuggestionUse(kind: 'crop' | 'variety', suggestion: CropFindingSuggestion, edit: boolean) {
    if (suggestion.evidenceStatus === 'Partially Supported') {
      setPendingPartial({ kind, id: suggestion.id, edit })
      return
    }
    if (kind === 'crop') prefillCropSuggestion(suggestion, edit)
    else prefillVarietySuggestion(suggestion, edit)
  }

  function selectPrimarySource(sourceId: string) {
    if (sourceId !== primarySourceId && referenceSnapshot) {
      setReferenceForm(referenceSnapshot.form)
      setStages(referenceSnapshot.stages)
      setRules(referenceSnapshot.rules)
      setReferenceReview({})
      setReferenceSnapshot(null)
    }
    setPrimarySourceId(sourceId)
    setFindingErrors((current) => ({ ...current, references: '' }))
  }

  function applyReferenceItem(sourceId: string, item: ReferenceDraftItem, edit: boolean) {
    if (sourceId !== primarySourceId) return
    if (!referenceSnapshot) setReferenceSnapshot({ form: { ...referenceForm }, stages: stages.map((stage) => ({ ...stage })), rules: rules.map((rule) => ({ ...rule })) })
    const value = objectValue(item.suggestedValue)
    let applied = true
    if (item.field === 'sourceName') setReferenceForm((current) => ({ ...current, sourceName: textValue(item.suggestedValue) }))
    else if (item.field === 'sourceUrl') setReferenceForm((current) => ({ ...current, sourceUrl: textValue(item.suggestedValue) }))
    else if (item.field === 'sourceVersion') setReferenceForm((current) => ({ ...current, sourceVersion: textValue(item.suggestedValue) }))
    else if (item.field === 'region') setReferenceForm((current) => ({ ...current, region: textValue(item.suggestedValue) }))
    else if (item.field === 'growthStage') {
      const stageName = textValue(value?.stageName ?? value?.name ?? item.suggestedValue).trim()
      if (!stageName) applied = false
      else {
        const stage: StageForm = {
          stageName,
          sequence: typeof value?.sequence === 'number' ? value.sequence : stages.filter((entry) => entry.stageName.trim()).length + 1,
          typicalMinDays: numericValue(value?.typicalMinDays, value?.minimumDays, value?.minDays),
          typicalMaxDays: numericValue(value?.typicalMaxDays, value?.maximumDays, value?.maxDays),
          notes: textValue(value?.notes),
        }
        setStages((current) => current.length === 1 && !current[0].stageName.trim() ? [stage] : [...current, stage])
      }
    } else if (item.field === 'minimumDays' || item.field === 'maximumDays') {
      const stageName = textValue(value?.stageName ?? value?.name).trim()
      const days = numericValue(value?.days, value?.value, item.suggestedValue)
      if (!stageName || !days) applied = false
      else setStages((current) => {
        const index = current.findIndex((stage) => stage.stageName.localeCompare(stageName, undefined, { sensitivity: 'base' }) === 0)
        const key = item.field === 'minimumDays' ? 'typicalMinDays' : 'typicalMaxDays'
        if (index < 0) return [...current.filter((stage) => stage.stageName.trim()), { ...emptyStage(), stageName, sequence: current.filter((stage) => stage.stageName.trim()).length + 1, [key]: days }]
        return current.map((stage, position) => position === index ? { ...stage, [key]: days } : stage)
      })
    } else if (item.field === 'evidenceNotes') {
      const stageName = textValue(value?.stageName ?? value?.name).trim()
      const notes = textValue(value?.notes ?? value?.note ?? item.suggestedValue).trim()
      if (!stageName || !notes) applied = false
      else setStages((current) => current.map((stage) => stage.stageName.localeCompare(stageName, undefined, { sensitivity: 'base' }) === 0 ? { ...stage, notes } : stage))
    } else if (item.field === 'structuredRule') {
      const ruleType = textValue(value?.ruleType).trim()
      const ruleKey = textValue(value?.ruleKey).trim()
      const structuredValueJson = typeof value?.structuredValueJson === 'string'
        ? value.structuredValueJson
        : JSON.stringify(value?.structuredValue ?? value?.value ?? {})
      if (!ruleType || !ruleKey) applied = false
      else setRules((current) => [...current, { ruleType, ruleKey, structuredValueJson }])
    }

    if (!applied) {
      setFindingErrors((current) => ({ ...current, references: `The ${item.field} suggestion could not be mapped safely. Enter it manually after reviewing the source.` }))
      return
    }
    setReferenceReview((current) => ({ ...current, [item.id]: edit ? 'edited' : 'accepted' }))
  }

  function requestReferenceUse(sourceId: string, item: ReferenceDraftItem, edit: boolean) {
    if (item.evidenceStatus === 'Partially Supported') {
      setPendingPartial({ kind: 'reference', sourceId, id: item.id, edit })
      return
    }
    applyReferenceItem(sourceId, item, edit)
  }

  function confirmPartialUse() {
    if (!pendingPartial) return
    if (pendingPartial.kind === 'crop') {
      const suggestion = cropSuggestions?.suggestions.find((item) => item.id === pendingPartial.id)
      if (suggestion) prefillCropSuggestion(suggestion, pendingPartial.edit)
    } else if (pendingPartial.kind === 'variety') {
      const suggestion = varietySuggestions?.suggestions.find((item) => item.id === pendingPartial.id)
      if (suggestion) prefillVarietySuggestion(suggestion, pendingPartial.edit)
    } else {
      const item = referenceDiscovery?.sourceDrafts.flatMap((draft) => draft.items).find((candidate) => candidate.id === pendingPartial.id)
      if (item) applyReferenceItem(pendingPartial.sourceId, item, pendingPartial.edit)
    }
    setPendingPartial(null)
  }

  async function saveReference(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const includedStages = stages.filter((stage) => stage.stageName.trim())
    const includedRules = rules.filter((rule) => rule.ruleType.trim() || rule.ruleKey.trim() || rule.structuredValueJson.trim())
    if (!includedStages.length && !includedRules.length) {
      setError('Add at least one verified stage or rule.')
      return
    }
    await run(async () => {
      await api.post('/crop-planning/crop-reference-profiles', {
        cropTypeId: referenceForm.cropTypeId,
        cropVarietyId: referenceForm.cropVarietyId || null,
        region: referenceForm.region.trim() || null,
        sourceName: referenceForm.sourceName.trim(),
        sourceUrl: referenceForm.sourceUrl.trim() || null,
        sourceVersion: referenceForm.sourceVersion.trim(),
        verifiedAt: new Date(referenceForm.verifiedAt).toISOString(),
        stages: includedStages.map((stage) => ({
          stageName: stage.stageName.trim(), sequence: stage.sequence,
          typicalMinDays: stage.typicalMinDays ? Number(stage.typicalMinDays) : null,
          typicalMaxDays: stage.typicalMaxDays ? Number(stage.typicalMaxDays) : null,
          notes: stage.notes.trim() || null,
        })),
        rules: includedRules.map((rule) => ({ ruleType: rule.ruleType.trim(), ruleKey: rule.ruleKey.trim(), structuredValueJson: rule.structuredValueJson.trim() })),
      })
      setReferenceForm(emptyReferenceForm())
      setStages([emptyStage()])
      setRules([])
      setReferenceDiscovery(null)
      setReferenceReview({})
      setPrimarySourceId('')
      setReferenceSnapshot(null)
    }, 'Verified reference version created.')
  }

  const activeCropOptions = useMemo(() => crops.filter((crop) => crop.isActive).map((crop) => ({ value: crop.id, label: crop.name })), [crops])
  const varietyCropOptions = useMemo(() => (varietyForm.id ? crops : crops.filter((crop) => crop.isActive)).map((crop) => ({ value: crop.id, label: crop.name })), [crops, varietyForm.id])
  const referenceVarietyOptions = useMemo(() => varieties.filter((variety) => variety.isActive && variety.cropTypeId === referenceForm.cropTypeId).map((variety) => ({ value: variety.id, label: variety.name })), [referenceForm.cropTypeId, varieties])
  const cropNames = useMemo(() => new Map(crops.map((crop) => [crop.id, crop.name])), [crops])
  const cropName = (id: string) => cropNames.get(id) ?? id.slice(0, 8)

  const filteredCrops = useMemo(() => {
    const query = cropSearch.trim().toLowerCase()
    return crops.filter((crop) => matchesStatus(crop.isActive, cropStatus)
      && (!query || crop.name.toLowerCase().includes(query) || crop.description?.toLowerCase().includes(query)))
  }, [cropSearch, cropStatus, crops])

  const filteredVarieties = useMemo(() => {
    const query = varietySearch.trim().toLowerCase()
    return varieties.filter((variety) => matchesStatus(variety.isActive, varietyStatus)
      && (!query || variety.name.toLowerCase().includes(query) || (cropNames.get(variety.cropTypeId) ?? '').toLowerCase().includes(query)))
  }, [cropNames, varieties, varietySearch, varietyStatus])

  const tabs = [
    { id: 'crops', label: 'Crops', count: crops.length, icon: <Sprout size={17} /> },
    { id: 'varieties', label: 'Varieties', count: varieties.length, icon: <Sprout size={17} /> },
    { id: 'references', label: 'Verified References', count: profiles.length, icon: <BookOpenCheck size={17} /> },
  ]

  return (
    <div className="page-stack crop-admin-page">
      <div className="crop-admin-heading">
        <div>
          <span className="crop-admin-eyebrow">Crop catalog</span>
          <h2>Crop master and verified references</h2>
          <p>Manage the catalog farmers can select and keep sourced planning evidence current.</p>
        </div>
      </div>

      {error ? <Notice tone="error">{error}</Notice> : null}
      {success ? <Notice tone="success">{success}</Notice> : null}
      <Tabs tabs={tabs} activeTab={activeTab} onChange={(tab) => setActiveTab(tab as AdminTab)} ariaLabel="Crop master sections" />

      {activeTab === 'crops' ? (
        <section className="work-section crop-admin-panel" aria-label="Crops management">
          <div className="crop-admin-panel-header">
            <div><h3>Crops</h3><p>Active crops are available in the farmer catalog.</p></div>
            <Button icon={<Plus size={17} />} onClick={openAddCrop}>Add crop</Button>
          </div>
          <section className="crop-finding-panel" aria-label="AI Sri Lankan crop suggestions">
            <div className="crop-finding-panel-heading">
              <div><span className="crop-finding-label">Needs Admin Review</span><h3>AI Sri Lankan crop suggestions</h3><p>Find evidence-backed candidates. Nothing is created until you submit the existing Add crop form.</p></div>
              <Button variant="secondary" icon={findingAction === 'crops' ? <Loader2 className="spin" size={16} /> : <Sparkles size={16} />} disabled={findingAction !== null} onClick={() => void findCrops()}>{findingAction === 'crops' ? 'Finding crops…' : 'Suggest Sri Lankan Crops'}</Button>
            </div>
            {findingErrors.crops ? <Notice tone="error">{findingErrors.crops} Retry without losing the current catalog form.</Notice> : null}
            {cropSuggestions ? <>
              <FindingSummary analysis={cropSuggestions.analysis} recommendations={cropSuggestions.recommendations} warnings={cropSuggestions.warnings} />
              <SuggestionReview suggestions={cropSuggestions.suggestions} decisions={cropReview} noun="crop" onAccept={(item) => requestSuggestionUse('crop', item, false)} onEdit={(item) => requestSuggestionUse('crop', item, true)} onReject={(item) => setCropReview((current) => ({ ...current, [item.id]: 'rejected' }))} />
            </> : null}
          </section>
          <ManagementToolbar searchLabel="Search crops" search={cropSearch} onSearch={setCropSearch} placeholder="Search crops or notes" status={cropStatus} onStatus={setCropStatus} />
          {loading ? <p className="crop-admin-loading">Loading crops…</p> : (
            <DataTable rows={filteredCrops} emptyTitle="No crops found" emptyMessage="Try another search or status filter." getRowKey={(crop) => crop.id} columns={[
              { header: 'Crop', render: (crop) => <PrimaryCell title={crop.name} detail={crop.description || 'No reference notes'} /> },
              { header: 'State', render: (crop) => <StatusPill label={crop.isActive ? 'Active' : 'Inactive'} tone={crop.isActive ? 'good' : 'bad'} /> },
              { header: 'Actions', className: 'crop-admin-actions-column', render: (crop) => <div className="crop-admin-actions"><Button variant="secondary" icon={<Pencil size={15} />} disabled={busy} onClick={() => openEditCrop(crop)}>Edit</Button><Button variant="ghost" icon={<Power size={15} />} disabled={busy} onClick={() => void setCropActive(crop)}>{crop.isActive ? 'Deactivate' : 'Reactivate'}</Button><Button variant="danger" icon={<Trash2 size={15} />} disabled={busy} onClick={() => openDelete({ kind: 'crop', id: crop.id, name: crop.name })}>Delete</Button></div> },
            ]} />
          )}
        </section>
      ) : null}

      {activeTab === 'varieties' ? (
        <section className="work-section crop-admin-panel" aria-label="Varieties management">
          <div className="crop-admin-panel-header">
            <div><h3>Varieties</h3><p>Organize selectable varieties beneath an active crop.</p></div>
            <Button icon={<Plus size={17} />} onClick={openAddVariety} disabled={!activeCropOptions.length}>Add variety</Button>
          </div>
          <section className="crop-finding-panel" aria-label="AI Sri Lankan variety suggestions">
            <div className="crop-finding-panel-heading">
              <div><span className="crop-finding-label">Needs Admin Review</span><h3>Find Sri Lankan varieties</h3><p>Select an existing crop, then review source-backed variety suggestions before using the Add variety form.</p></div>
              <Button variant="secondary" icon={findingAction === 'varieties' ? <Loader2 className="spin" size={16} /> : <Sparkles size={16} />} disabled={!varietyFindingCropId || findingAction !== null} onClick={() => void findVarieties()}>{findingAction === 'varieties' ? 'Finding varieties…' : 'Find Sri Lankan Varieties'}</Button>
            </div>
            <div className="crop-finding-controls"><SelectInput label="Existing crop" value={varietyFindingCropId} options={activeCropOptions} disabled={findingAction === 'varieties'} onChange={(cropTypeId) => { setVarietyFindingCropId(cropTypeId); setVarietySuggestions(null); setVarietyReview({}) }} /></div>
            {findingErrors.varieties ? <Notice tone="error">{findingErrors.varieties} Retry without losing the current variety form.</Notice> : null}
            {varietySuggestions ? <>
              <FindingSummary analysis={varietySuggestions.analysis} recommendations={varietySuggestions.recommendations} warnings={varietySuggestions.warnings} />
              <SuggestionReview suggestions={varietySuggestions.suggestions} decisions={varietyReview} noun="variety" onAccept={(item) => requestSuggestionUse('variety', item, false)} onEdit={(item) => requestSuggestionUse('variety', item, true)} onReject={(item) => setVarietyReview((current) => ({ ...current, [item.id]: 'rejected' }))} />
            </> : null}
          </section>
          <ManagementToolbar searchLabel="Search varieties" search={varietySearch} onSearch={setVarietySearch} placeholder="Search varieties or crops" status={varietyStatus} onStatus={setVarietyStatus} />
          {loading ? <p className="crop-admin-loading">Loading varieties…</p> : (
            <DataTable rows={filteredVarieties} emptyTitle="No varieties found" emptyMessage="Try another search or status filter." getRowKey={(variety) => variety.id} columns={[
              { header: 'Variety', render: (variety) => <PrimaryCell title={variety.name} detail={cropName(variety.cropTypeId)} /> },
              { header: 'State', render: (variety) => <StatusPill label={variety.isActive ? 'Active' : 'Inactive'} tone={variety.isActive ? 'good' : 'bad'} /> },
              { header: 'Actions', className: 'crop-admin-actions-column', render: (variety) => <div className="crop-admin-actions"><Button variant="secondary" icon={<Pencil size={15} />} disabled={busy} onClick={() => openEditVariety(variety)}>Edit</Button><Button variant="ghost" icon={<Power size={15} />} disabled={busy} onClick={() => void setVarietyActive(variety)}>{variety.isActive ? 'Deactivate' : 'Reactivate'}</Button><Button variant="danger" icon={<Trash2 size={15} />} disabled={busy} onClick={() => openDelete({ kind: 'variety', id: variety.id, name: variety.name })}>Delete</Button></div> },
            ]} />
          )}
        </section>
      ) : null}

      {activeTab === 'references' ? (
        <section className="work-section crop-admin-panel" aria-label="Verified references management">
          <div className="crop-admin-panel-header"><div><h3>Verified references</h3><p>Create sourced versions without changing prior evidence.</p></div></div>
          <DataTable rows={profiles} emptyTitle="No verified references" emptyMessage="The coordinator will request human review until verified evidence is added." getRowKey={(profile) => profile.id} columns={[
            { header: 'Crop / variety', render: (profile) => <PrimaryCell title={cropName(profile.cropTypeId)} detail={profile.varietyName || 'All varieties'} /> },
            { header: 'Source', render: (profile) => `${profile.sourceName} · ${profile.sourceVersion}` },
            { header: 'Verified', render: (profile) => formatDate(profile.verifiedAt) },
            { header: 'Evidence', render: (profile) => `${profile.stageCount} stages · ${profile.ruleCount} rules` },
            { header: 'State', render: (profile) => <StatusPill label={profile.isActive ? 'Active' : 'Inactive'} tone={profile.isActive ? 'good' : 'bad'} /> },
            { header: 'Actions', render: (profile) => <Button variant="secondary" disabled={busy} onClick={() => void run(async () => { await api.put(`/crop-planning/crop-reference-profiles/${profile.id}/active`, !profile.isActive, { headers: { 'Content-Type': 'application/json' } }) }, 'Reference state updated.')}>{profile.isActive ? 'Deactivate' : 'Activate'}</Button> },
          ]} />
          <form className="crop-reference-form" onSubmit={(event) => void saveReference(event)}>
            <div className="crop-reference-form-heading"><h3>Create verified version</h3><p>Add a traceable source and at least one growth stage or structured rule.</p></div>
            <section className="crop-finding-panel" aria-label="AI reference discovery">
              <div className="crop-finding-panel-heading">
                <div><span className="crop-finding-label">AI Generated Draft · Needs Admin Review</span><h3>Find references with AI</h3><p>Choose the crop below first. Review one primary source at a time; accepted items only prefill this existing form.</p></div>
                <Button variant="secondary" icon={findingAction === 'references' ? <Loader2 className="spin" size={16} /> : <Sparkles size={16} />} disabled={!referenceForm.cropTypeId || findingAction !== null} onClick={() => void findReferences()}>{findingAction === 'references' ? 'Finding references…' : 'Find References with AI'}</Button>
              </div>
              {findingErrors.references ? <Notice tone="error">{findingErrors.references} Current form values and completed review decisions were preserved.</Notice> : null}
              {referenceDiscovery ? <ReferenceDiscoveryReview response={referenceDiscovery} primarySourceId={primarySourceId} decisions={referenceReview} onSelectPrimary={selectPrimarySource} onAccept={(sourceId, item) => requestReferenceUse(sourceId, item, false)} onEdit={(sourceId, item) => requestReferenceUse(sourceId, item, true)} onReject={(item) => setReferenceReview((current) => ({ ...current, [item.id]: 'rejected' }))} /> : null}
            </section>
            <fieldset className="crop-reference-section">
              <legend>Source details</legend>
              <div className="form-grid">
                <SelectInput label="Crop" value={referenceForm.cropTypeId} options={activeCropOptions} required onChange={(cropTypeId) => setReferenceForm({ ...referenceForm, cropTypeId, cropVarietyId: '' })} />
                <SelectInput label="Variety (optional)" value={referenceForm.cropVarietyId} options={referenceVarietyOptions} onChange={(cropVarietyId) => setReferenceForm({ ...referenceForm, cropVarietyId })} />
                <TextInput label="Region" value={referenceForm.region} onChange={(region) => setReferenceForm({ ...referenceForm, region })} />
                <TextInput label="Source name" value={referenceForm.sourceName} required onChange={(sourceName) => setReferenceForm({ ...referenceForm, sourceName })} />
                <TextInput label="Source URL" value={referenceForm.sourceUrl} type="url" onChange={(sourceUrl) => setReferenceForm({ ...referenceForm, sourceUrl })} />
                <TextInput label="Source version" value={referenceForm.sourceVersion} required onChange={(sourceVersion) => setReferenceForm({ ...referenceForm, sourceVersion })} />
                <TextInput label="Verified at" value={referenceForm.verifiedAt} type="datetime-local" required onChange={(verifiedAt) => setReferenceForm({ ...referenceForm, verifiedAt })} />
              </div>
            </fieldset>
            <fieldset className="crop-reference-section">
              <legend>Growth stages</legend>
              <div className="crop-reference-stack">
                {stages.map((stage, index) => <div className="crop-reference-item" key={index}>
                  <div className="crop-reference-item-heading"><strong>Stage {index + 1}</strong><Button variant="ghost" type="button" onClick={() => setStages(stages.filter((_, position) => position !== index))}>Remove</Button></div>
                  <div className="form-grid">
                    <TextInput label="Stage name" value={stage.stageName} onChange={(stageName) => setStages(stages.map((item, position) => position === index ? { ...item, stageName } : item))} />
                    <TextInput label="Minimum days" value={stage.typicalMinDays} type="number" min="0" onChange={(typicalMinDays) => setStages(stages.map((item, position) => position === index ? { ...item, typicalMinDays } : item))} />
                    <TextInput label="Maximum days" value={stage.typicalMaxDays} type="number" min="0" onChange={(typicalMaxDays) => setStages(stages.map((item, position) => position === index ? { ...item, typicalMaxDays } : item))} />
                    <TextInput label="Evidence notes" value={stage.notes} onChange={(notes) => setStages(stages.map((item, position) => position === index ? { ...item, notes } : item))} />
                  </div>
                </div>)}
              </div>
              <Button variant="secondary" type="button" icon={<Plus size={15} />} onClick={() => setStages([...stages, { ...emptyStage(), sequence: stages.length + 1 }])}>Add stage</Button>
            </fieldset>
            <fieldset className="crop-reference-section">
              <legend>Structured rules</legend>
              <div className="crop-reference-stack">
                {rules.map((rule, index) => <div className="crop-reference-item" key={index}>
                  <div className="crop-reference-item-heading"><strong>Rule {index + 1}</strong><Button variant="ghost" type="button" onClick={() => setRules(rules.filter((_, position) => position !== index))}>Remove</Button></div>
                  <div className="form-grid">
                    <TextInput label="Rule type" value={rule.ruleType} onChange={(ruleType) => setRules(rules.map((item, position) => position === index ? { ...item, ruleType } : item))} />
                    <TextInput label="Rule key" value={rule.ruleKey} onChange={(ruleKey) => setRules(rules.map((item, position) => position === index ? { ...item, ruleKey } : item))} />
                    <TextAreaInput label="Verified value (JSON)" value={rule.structuredValueJson} rows={3} onChange={(structuredValueJson) => setRules(rules.map((item, position) => position === index ? { ...item, structuredValueJson } : item))} />
                  </div>
                </div>)}
              </div>
              <Button variant="secondary" type="button" icon={<Plus size={15} />} onClick={() => setRules([...rules, emptyRule()])}>Add rule</Button>
            </fieldset>
            <div className="crop-reference-submit"><Button type="submit" disabled={busy}>Create verified version</Button></div>
          </form>
        </section>
      ) : null}

      <CropDialog open={cropDialogOpen} form={cropForm} error={dialogError} busy={busy} onChange={setCropForm} onClose={() => setCropDialogOpen(false)} onSubmit={saveCrop} />
      <VarietyDialog open={varietyDialogOpen} form={varietyForm} cropOptions={varietyCropOptions} error={dialogError} busy={busy} onChange={setVarietyForm} onClose={() => setVarietyDialogOpen(false)} onSubmit={saveVariety} />
      <DeleteDialog target={deleteTarget} error={dialogError} busy={busy} onCancel={() => setDeleteTarget(null)} onConfirm={confirmDelete} />
      <Modal open={Boolean(pendingPartial)} title="Use partially supported value?" description="The source does not fully support the exact crop, variety, region, or value." onClose={() => setPendingPartial(null)} footer={<><Button variant="secondary" onClick={() => setPendingPartial(null)}>Cancel</Button><Button onClick={confirmPartialUse}>Use with warning</Button></>}>
        <Notice tone="warning">Check the original evidence before continuing. The warning remains in this review session, and the value is not verified until you submit the existing Admin form.</Notice>
      </Modal>
    </div>
  )
}

function ManagementToolbar({ searchLabel, search, onSearch, placeholder, status, onStatus }: { searchLabel: string; search: string; onSearch: (value: string) => void; placeholder: string; status: StatusFilter; onStatus: (value: StatusFilter) => void }) {
  return <div className="crop-admin-toolbar">
    <label className="crop-admin-search"><span className="sr-only">{searchLabel}</span><Search size={17} aria-hidden="true" /><input value={search} onChange={(event) => onSearch(event.target.value)} placeholder={placeholder} /></label>
    <label className="crop-admin-filter"><span>Status</span><select value={status} onChange={(event) => onStatus(event.target.value as StatusFilter)}><option value="all">All</option><option value="active">Active</option><option value="inactive">Inactive</option></select></label>
  </div>
}

function PrimaryCell({ title, detail }: { title: string; detail: string }) {
  return <div className="crop-admin-primary-cell"><strong>{title}</strong><span>{detail}</span></div>
}

function CropDialog({ open, form, error, busy, onChange, onClose, onSubmit }: { open: boolean; form: ReturnType<typeof emptyCropForm>; error: string; busy: boolean; onChange: (value: ReturnType<typeof emptyCropForm>) => void; onClose: () => void; onSubmit: (event: FormEvent<HTMLFormElement>) => void }) {
  return <Modal open={open} title={form.id ? 'Edit crop' : 'Add crop'} description={form.id ? 'Update the catalog name, notes, or farmer visibility.' : 'Create a crop for the planning catalog.'} onClose={onClose} footer={<><Button variant="secondary" onClick={onClose} disabled={busy}>Cancel</Button><Button type="submit" form="crop-admin-crop-form" disabled={busy}>{busy ? 'Saving…' : form.id ? 'Save changes' : 'Add crop'}</Button></>}>
    {error ? <Notice tone="error">{error}</Notice> : null}
    <form id="crop-admin-crop-form" className="form-grid" onSubmit={onSubmit}>
      <TextInput label="Crop name" value={form.name} required onChange={(name) => onChange({ ...form, name })} />
      <TextAreaInput label="Notes" value={form.description} rows={3} onChange={(description) => onChange({ ...form, description })} />
      <CompactSwitch label="Active for farmers" description="Show this crop in the farmer catalog." checked={form.isActive} onChange={(isActive) => onChange({ ...form, isActive })} />
    </form>
  </Modal>
}

function VarietyDialog({ open, form, cropOptions, error, busy, onChange, onClose, onSubmit }: { open: boolean; form: ReturnType<typeof emptyVarietyForm>; cropOptions: { value: string; label: string }[]; error: string; busy: boolean; onChange: (value: ReturnType<typeof emptyVarietyForm>) => void; onClose: () => void; onSubmit: (event: FormEvent<HTMLFormElement>) => void }) {
  return <Modal open={open} title={form.id ? 'Edit variety' : 'Add variety'} description={form.id ? 'Update the variety name or farmer visibility.' : 'Add a variety beneath an active crop.'} onClose={onClose} footer={<><Button variant="secondary" onClick={onClose} disabled={busy}>Cancel</Button><Button type="submit" form="crop-admin-variety-form" disabled={busy}>{busy ? 'Saving…' : form.id ? 'Save changes' : 'Add variety'}</Button></>}>
    {error ? <Notice tone="error">{error}</Notice> : null}
    <form id="crop-admin-variety-form" className="form-grid" onSubmit={onSubmit}>
      <SelectInput label="Crop" value={form.cropTypeId} options={cropOptions} required disabled={Boolean(form.id)} onChange={(cropTypeId) => onChange({ ...form, cropTypeId })} />
      <TextInput label="Variety name" value={form.name} required onChange={(name) => onChange({ ...form, name })} />
      <CompactSwitch label="Active for farmers" description="Show this variety when its crop is selected." checked={form.isActive} onChange={(isActive) => onChange({ ...form, isActive })} />
    </form>
  </Modal>
}

function CompactSwitch({ label, description, checked, onChange }: { label: string; description: string; checked: boolean; onChange: (checked: boolean) => void }) {
  return <label className="crop-admin-switch field-control-wide"><span><strong>{label}</strong><small>{description}</small></span><input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} /><i aria-hidden="true" /></label>
}

function DeleteDialog({ target, error, busy, onCancel, onConfirm }: { target: DeleteTarget; error: string; busy: boolean; onCancel: () => void; onConfirm: () => void | Promise<void> }) {
  const label = target?.kind === 'crop' ? 'crop' : 'variety'
  return <Modal
    open={Boolean(target)}
    title={`Delete ${label}?`}
    description="This action is allowed only when no planning or reference history depends on the item."
    onClose={onCancel}
    footer={<><Button variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button><Button variant="danger" onClick={() => void onConfirm()} disabled={busy}>{busy ? 'Deleting…' : `Delete ${label}`}</Button></>}
  >
    {error ? <Notice tone="error">{error}</Notice> : null}
    <div className="crop-admin-delete-message">
      <p>Delete <strong>{target?.name}</strong> from the admin catalog?</p>
      <p>If it is already in use, deletion will be blocked to protect historical data. Use <strong>Deactivate</strong> instead.</p>
    </div>
  </Modal>
}
