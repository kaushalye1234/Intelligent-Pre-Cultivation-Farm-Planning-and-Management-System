import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api, getErrorMessage } from '../api/client'
import { SelectInput, TextAreaInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { StatusPill } from '../components/StatusPill'
import { Button, Notice } from '../components/Ui'
import { formatDate } from '../format'
import type { CropReferenceProfile, CropType, CropVariety, PagedResult } from '../types'

type StageForm = { stageName: string; sequence: number; typicalMinDays: string; typicalMaxDays: string; notes: string }
type RuleForm = { ruleType: string; ruleKey: string; structuredValueJson: string }
const emptyStage = (): StageForm => ({ stageName: '', sequence: 1, typicalMinDays: '', typicalMaxDays: '', notes: '' })
const emptyRule = (): RuleForm => ({ ruleType: '', ruleKey: '', structuredValueJson: '' })

async function allItems<T>(path: string): Promise<T[]> {
  const first = await api.get<PagedResult<T>>(path, { params: { page: 1, pageSize: 100 } })
  const pages = await Promise.all(Array.from({ length: first.data.totalPages - 1 }, (_, index) =>
    api.get<PagedResult<T>>(path, { params: { page: index + 2, pageSize: 100 } })))
  return [first.data, ...pages.map((page) => page.data)].flatMap((page) => page.items)
}

export function AdminCropManagement() {
  const [crops, setCrops] = useState<CropType[]>([])
  const [varieties, setVarieties] = useState<CropVariety[]>([])
  const [profiles, setProfiles] = useState<CropReferenceProfile[]>([])
  const [cropForm, setCropForm] = useState({ id: '', name: '', description: '', isActive: true })
  const [varietyForm, setVarietyForm] = useState({ id: '', cropTypeId: '', name: '', isActive: true })
  const [referenceForm, setReferenceForm] = useState({ cropTypeId: '', cropVarietyId: '', region: '', sourceName: '', sourceUrl: '', sourceVersion: '', verifiedAt: '' })
  const [stages, setStages] = useState<StageForm[]>([emptyStage()])
  const [rules, setRules] = useState<RuleForm[]>([])
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [busy, setBusy] = useState(false)

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

  useEffect(() => { void load().catch((cause) => setError(getErrorMessage(cause))) }, [])

  async function run(action: () => Promise<void>, message: string) {
    setBusy(true)
    setError('')
    setSuccess('')
    try {
      await action()
      await load()
      setSuccess(message)
    } catch (cause) {
      setError(getErrorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  async function saveCrop(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await run(async () => {
      const body = { name: cropForm.name.trim(), description: cropForm.description.trim() || null, isActive: cropForm.isActive }
      if (cropForm.id) await api.put(`/crop-planning/crop-types/${cropForm.id}`, body)
      else await api.post('/crop-planning/crop-types', body)
      setCropForm({ id: '', name: '', description: '', isActive: true })
    }, cropForm.id ? 'Crop updated.' : 'Crop created.')
  }

  async function saveVariety(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await run(async () => {
      const body = { cropTypeId: varietyForm.cropTypeId, name: varietyForm.name.trim(), isActive: varietyForm.isActive }
      if (varietyForm.id) await api.put(`/crop-planning/crop-varieties/${varietyForm.id}`, body)
      else await api.post('/crop-planning/crop-varieties', body)
      setVarietyForm({ id: '', cropTypeId: '', name: '', isActive: true })
    }, varietyForm.id ? 'Variety updated.' : 'Variety created.')
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
      setReferenceForm({ cropTypeId: '', cropVarietyId: '', region: '', sourceName: '', sourceUrl: '', sourceVersion: '', verifiedAt: '' })
      setStages([emptyStage()])
      setRules([])
    }, 'Verified reference version created.')
  }

  const cropOptions = crops.filter((crop) => crop.isActive).map((crop) => ({ value: crop.id, label: crop.name }))
  const varietyOptions = varieties.filter((variety) => variety.isActive && variety.cropTypeId === referenceForm.cropTypeId)
    .map((variety) => ({ value: variety.id, label: variety.name }))
  const cropName = (id: string) => crops.find((crop) => crop.id === id)?.name ?? id.slice(0, 8)

  return (
    <div className="page-stack">
      <div className="section-title"><h2>Crop master and verified references</h2><p>Only active crops and varieties appear in the farmer catalog. Reference versions retain their source and verification date.</p></div>
      {error ? <Notice tone="error">{error}</Notice> : null}
      {success ? <Notice tone="success">{success}</Notice> : null}

      <section className="work-section">
        <div className="section-title"><h3>Crops</h3></div>
        <DataTable rows={crops} emptyTitle="No crops" emptyMessage="Add a crop to start the master catalog." getRowKey={(crop) => crop.id} columns={[
          { header: 'Crop', render: (crop) => crop.name },
          { header: 'Reference notes', render: (crop) => crop.description || '—' },
          { header: 'State', render: (crop) => <StatusPill label={crop.isActive ? 'Active' : 'Inactive'} tone={crop.isActive ? 'good' : 'bad'} /> },
          { header: 'Manage', render: (crop) => <Button variant="secondary" onClick={() => setCropForm({ id: crop.id, name: crop.name, description: crop.description ?? '', isActive: crop.isActive })}>Edit</Button> },
        ]} />
        <form className="form-grid" onSubmit={(event) => void saveCrop(event)}>
          <TextInput label="Crop name" value={cropForm.name} required onChange={(name) => setCropForm({ ...cropForm, name })} />
          <TextAreaInput label="General reference notes" value={cropForm.description} onChange={(description) => setCropForm({ ...cropForm, description })} />
          <label className="field-control"><span>Active for farmers</span><input type="checkbox" checked={cropForm.isActive} onChange={(event) => setCropForm({ ...cropForm, isActive: event.target.checked })} /></label>
          <div className="field-control-wide"><Button type="submit" disabled={busy}>{cropForm.id ? 'Save crop' : 'Add crop'}</Button></div>
        </form>
      </section>

      <section className="work-section">
        <div className="section-title"><h3>Varieties</h3></div>
        <DataTable rows={varieties} emptyTitle="No varieties" emptyMessage="Add a variety under an active crop." getRowKey={(variety) => variety.id} columns={[
          { header: 'Variety', render: (variety) => variety.name },
          { header: 'Crop', render: (variety) => cropName(variety.cropTypeId) },
          { header: 'State', render: (variety) => <StatusPill label={variety.isActive ? 'Active' : 'Inactive'} tone={variety.isActive ? 'good' : 'bad'} /> },
          { header: 'Manage', render: (variety) => <Button variant="secondary" onClick={() => setVarietyForm({ id: variety.id, cropTypeId: variety.cropTypeId, name: variety.name, isActive: variety.isActive })}>Edit</Button> },
        ]} />
        <form className="form-grid" onSubmit={(event) => void saveVariety(event)}>
          <SelectInput label="Crop" value={varietyForm.cropTypeId} options={cropOptions} required disabled={Boolean(varietyForm.id)} onChange={(cropTypeId) => setVarietyForm({ ...varietyForm, cropTypeId })} />
          <TextInput label="Variety name" value={varietyForm.name} required onChange={(name) => setVarietyForm({ ...varietyForm, name })} />
          <label className="field-control"><span>Active for farmers</span><input type="checkbox" checked={varietyForm.isActive} onChange={(event) => setVarietyForm({ ...varietyForm, isActive: event.target.checked })} /></label>
          <div className="field-control-wide"><Button type="submit" disabled={busy}>{varietyForm.id ? 'Save variety' : 'Add variety'}</Button></div>
        </form>
      </section>

      <section className="work-section">
        <div className="section-title"><h3>Verified reference versions</h3><p>Create a new sourced version when crop facts change. Deactivate an old version to stop its use by the coordinator.</p></div>
        <DataTable rows={profiles} emptyTitle="No verified references" emptyMessage="The coordinator will request human review until verified evidence is added." getRowKey={(profile) => profile.id} columns={[
          { header: 'Crop / variety', render: (profile) => `${cropName(profile.cropTypeId)}${profile.varietyName ? ` · ${profile.varietyName}` : ''}` },
          { header: 'Source', render: (profile) => `${profile.sourceName} · ${profile.sourceVersion}` },
          { header: 'Verified', render: (profile) => formatDate(profile.verifiedAt) },
          { header: 'Evidence', render: (profile) => `${profile.stageCount} stages · ${profile.ruleCount} rules` },
          { header: 'State', render: (profile) => <StatusPill label={profile.isActive ? 'Active' : 'Inactive'} tone={profile.isActive ? 'good' : 'bad'} /> },
          { header: 'Manage', render: (profile) => <Button variant="secondary" disabled={busy} onClick={() => void run(async () => { await api.put(`/crop-planning/crop-reference-profiles/${profile.id}/active`, !profile.isActive, { headers: { 'Content-Type': 'application/json' } }) }, 'Reference state updated.')}>{profile.isActive ? 'Deactivate' : 'Activate'}</Button> },
        ]} />
        <form className="form-grid" onSubmit={(event) => void saveReference(event)}>
          <SelectInput label="Crop" value={referenceForm.cropTypeId} options={cropOptions} required onChange={(cropTypeId) => setReferenceForm({ ...referenceForm, cropTypeId, cropVarietyId: '' })} />
          <SelectInput label="Variety (optional)" value={referenceForm.cropVarietyId} options={varietyOptions} onChange={(cropVarietyId) => setReferenceForm({ ...referenceForm, cropVarietyId })} />
          <TextInput label="Region" value={referenceForm.region} onChange={(region) => setReferenceForm({ ...referenceForm, region })} />
          <TextInput label="Source name" value={referenceForm.sourceName} required onChange={(sourceName) => setReferenceForm({ ...referenceForm, sourceName })} />
          <TextInput label="Source URL" value={referenceForm.sourceUrl} type="url" onChange={(sourceUrl) => setReferenceForm({ ...referenceForm, sourceUrl })} />
          <TextInput label="Source version" value={referenceForm.sourceVersion} required onChange={(sourceVersion) => setReferenceForm({ ...referenceForm, sourceVersion })} />
          <TextInput label="Verified at" value={referenceForm.verifiedAt} type="datetime-local" required onChange={(verifiedAt) => setReferenceForm({ ...referenceForm, verifiedAt })} />
          <div className="field-control-wide"><h4>Growth stages</h4></div>
          {stages.map((stage, index) => <div className="form-grid field-control-wide" key={index}>
            <TextInput label={`Stage ${index + 1} name`} value={stage.stageName} onChange={(stageName) => setStages(stages.map((item, position) => position === index ? { ...item, stageName } : item))} />
            <TextInput label="Minimum days" value={stage.typicalMinDays} type="number" min="0" onChange={(typicalMinDays) => setStages(stages.map((item, position) => position === index ? { ...item, typicalMinDays } : item))} />
            <TextInput label="Maximum days" value={stage.typicalMaxDays} type="number" min="0" onChange={(typicalMaxDays) => setStages(stages.map((item, position) => position === index ? { ...item, typicalMaxDays } : item))} />
            <TextInput label="Evidence notes" value={stage.notes} onChange={(notes) => setStages(stages.map((item, position) => position === index ? { ...item, notes } : item))} />
            <Button variant="secondary" type="button" onClick={() => setStages(stages.filter((_, position) => position !== index))}>Remove stage</Button>
          </div>)}
          <div className="field-control-wide"><Button variant="secondary" type="button" onClick={() => setStages([...stages, { ...emptyStage(), sequence: stages.length + 1 }])}>Add stage</Button></div>
          <div className="field-control-wide"><h4>Structured rules</h4></div>
          {rules.map((rule, index) => <div className="form-grid field-control-wide" key={index}>
            <TextInput label="Rule type" value={rule.ruleType} onChange={(ruleType) => setRules(rules.map((item, position) => position === index ? { ...item, ruleType } : item))} />
            <TextInput label="Rule key" value={rule.ruleKey} onChange={(ruleKey) => setRules(rules.map((item, position) => position === index ? { ...item, ruleKey } : item))} />
            <TextAreaInput label="Verified value (JSON)" value={rule.structuredValueJson} onChange={(structuredValueJson) => setRules(rules.map((item, position) => position === index ? { ...item, structuredValueJson } : item))} />
            <Button variant="secondary" type="button" onClick={() => setRules(rules.filter((_, position) => position !== index))}>Remove rule</Button>
          </div>)}
          <div className="field-control-wide"><Button variant="secondary" type="button" onClick={() => setRules([...rules, emptyRule()])}>Add rule</Button></div>
          <div className="field-control-wide"><Button type="submit" disabled={busy}>Create verified version</Button></div>
        </form>
      </section>
    </div>
  )
}
