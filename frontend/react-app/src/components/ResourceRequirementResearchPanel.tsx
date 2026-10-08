import { useEffect, useMemo, useState } from 'react'
import { ExternalLink, Loader2, Search, ShieldCheck, X } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import type { ResourceRequirementRecommendation, ResourceRequirementResearchResponse, VerifiedResourceRequirement } from '../resourceRequirementResearch'
import type { CropType, CropVariety, PagedResult, ResourceItem } from '../types'
import { SelectInput, TextInput } from './FormControls'
import { StatusPill } from './StatusPill'
import { Button, Notice } from './Ui'

const STATUS_LABELS: Record<ResourceRequirementResearchResponse['status'], { label: string; tone: 'warn' | 'bad' | 'neutral' }> = {
  PendingVerification: { label: 'Pending verification', tone: 'warn' },
  ConflictingSources: { label: 'Conflicting sources', tone: 'bad' },
  NoVerifiedRecommendationFound: { label: 'No verified recommendation found', tone: 'neutral' },
  EvidenceValidationFailed: { label: 'Evidence could not be verified', tone: 'bad' },
}

async function activeResources(): Promise<ResourceItem[]> {
  const first = await api.get<PagedResult<ResourceItem>>('/resources', { params: { page: 1, pageSize: 100 } })
  const pages = await Promise.all(Array.from({ length: Math.max(0, first.data.totalPages - 1) }, (_, index) =>
    api.get<PagedResult<ResourceItem>>('/resources', { params: { page: index + 2, pageSize: 100 } })))
  return [first.data, ...pages.map((page) => page.data)].flatMap((page) => page.items).filter((resource) => resource.isActive)
}

function safeUrl(value: string) {
  return /^https?:\/\//i.test(value) ? value : undefined
}

/**
 * Member 3 Admin-only Resource Requirement Research. Research returns an unverified, sourced draft; only
 * "Save draft for officer review" creates an inactive reference; agents require subsequent officer verification.
 */
export function ResourceRequirementResearchPanel({ crops, varieties, onSaved }: {
  crops: CropType[]
  varieties: CropVariety[]
  onSaved?: () => void
}) {
  const [resources, setResources] = useState<ResourceItem[]>([])
  const [form, setForm] = useState({ cropTypeId: '', cropVarietyId: '', resourceId: '', region: '' })
  const [result, setResult] = useState<ResourceRequirementResearchResponse | null>(null)
  const [confirmed, setConfirmed] = useState<Record<string, boolean>>({})
  const [busy, setBusy] = useState<'research' | 'save' | null>(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  useEffect(() => {
    activeResources().then(setResources).catch((cause) => setError(getErrorMessage(cause)))
  }, [])

  const cropOptions = useMemo(() => crops.filter((crop) => crop.isActive).map((crop) => ({ value: crop.id, label: crop.name })), [crops])
  const varietyOptions = useMemo(() => varieties
    .filter((variety) => variety.isActive && variety.cropTypeId === form.cropTypeId)
    .map((variety) => ({ value: variety.id, label: variety.name })), [varieties, form.cropTypeId])
  const resourceOptions = useMemo(() => resources.map((resource) => ({ value: resource.id, label: `${resource.name} (${resource.unit})` })), [resources])

  function update(next: Partial<typeof form>) {
    setForm((current) => ({ ...current, ...next }))
    setResult(null)
    setConfirmed({})
  }

  async function research() {
    setBusy('research')
    setError('')
    setMessage('')
    setResult(null)
    setConfirmed({})
    try {
      const response = await api.post<ResourceRequirementResearchResponse>('/resources/requirement-research', {
        cropTypeId: form.cropTypeId,
        resourceId: form.resourceId,
        cropVarietyId: form.cropVarietyId || null,
        region: form.region.trim() || null,
      })
      setResult(response.data)
    } catch (cause) {
      setError(`${getErrorMessage(cause)} Nothing was saved.`)
    } finally {
      setBusy(null)
    }
  }

  function reject() {
    setResult(null)
    setConfirmed({})
    setError('')
    setMessage('Research result rejected. Nothing was saved.')
  }

  async function saveDraft(draft: ResourceRequirementResearchResponse, recommendation: ResourceRequirementRecommendation) {
    setBusy('save')
    setError('')
    try {
      const source = recommendation.source
      const response = await api.post<VerifiedResourceRequirement>('/resources/requirement-research/draft', {
        cropTypeId: draft.cropTypeId,
        resourceId: draft.resourceId,
        cropVarietyId: draft.cropVarietyId || null,
        region: draft.region || null,
        quantityPerArea: recommendation.quantityPerArea,
        resourceUnit: recommendation.resourceUnit,
        areaUnit: recommendation.areaUnit,
        sourceName: `${source.organizationName} – ${source.sourceName}`.slice(0, 180),
        sourceUrl: source.finalUrl,
        evidence: recommendation.components.map((component) => component.evidenceText).join(' … ').slice(0, 1200),
        researchRequestId: draft.requestId,
      })
      const saved = response.data
      setResult(null)
      setConfirmed({})
      setMessage(`Inactive draft saved for officer review: ${saved.resourceName} ${saved.quantityPerArea} ${saved.resourceUnit}/${saved.areaUnit} for ${draft.cropName}.`)
      onSaved?.()
    } catch (cause) {
      setError(`${getErrorMessage(cause)} No rule was saved.`)
    } finally {
      setBusy(null)
    }
  }

  return (
    <section className="crop-finding-panel" aria-label="Resource requirement research">
      <div className="crop-finding-panel-heading">
        <div>
          <span className="crop-finding-label">AI Research Draft · Needs Admin Verification</span>
          <h3>Resource requirement research</h3>
          <p>Research how much of one inventory resource a crop needs, from approved agricultural sources only. Nothing is saved until you choose Save draft for officer review.</p>
        </div>
      </div>
      <div className="form-grid">
        <SelectInput label="Research crop" value={form.cropTypeId} options={cropOptions} required onChange={(cropTypeId) => update({ cropTypeId, cropVarietyId: '' })} />
        <SelectInput label="Variety (optional)" value={form.cropVarietyId} options={varietyOptions} onChange={(cropVarietyId) => update({ cropVarietyId })} />
        <SelectInput label="Resource" value={form.resourceId} options={resourceOptions} required onChange={(resourceId) => update({ resourceId })} />
        <TextInput label="Region (optional)" value={form.region} onChange={(region) => update({ region })} />
      </div>
      <div className="crop-reference-submit">
        <Button variant="secondary" icon={busy === 'research' ? <Loader2 className="spin" size={16} /> : <Search size={16} />} disabled={!form.cropTypeId || !form.resourceId || busy !== null} onClick={() => void research()}>
          {busy === 'research' ? 'Researching…' : 'Research requirement'}
        </Button>
      </div>
      {error ? <Notice tone="error">{error}</Notice> : null}
      {message ? <Notice tone="success">{message}</Notice> : null}
      {result ? (
        <div className="crop-reference-stack" aria-label="Research result">
          <div className="crop-reference-item">
            <div className="crop-reference-item-heading">
              <strong>{result.cropName}{result.varietyName ? ` · ${result.varietyName}` : ''} · {result.resourceName}</strong>
              <StatusPill label={STATUS_LABELS[result.status].label} tone={STATUS_LABELS[result.status].tone} />
            </div>
            {result.status === 'ConflictingSources' ? <Notice tone="warning">Approved sources disagree. No value is preselected; verify only the one you have checked.</Notice> : null}
            {result.status === 'NoVerifiedRecommendationFound' ? <Notice tone="info">No approved source stated a usable rate for this crop and resource. No value was suggested.</Notice> : null}
            {result.status === 'EvidenceValidationFailed' ? <Notice tone="warning">Proposed rates were rejected because their evidence could not be confirmed in the source text. No value was suggested.</Notice> : null}
            {result.warnings.length ? <ul>{result.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul> : null}
            {result.rejectedClaims.length ? <details><summary>Rejected claims ({result.rejectedClaims.length})</summary><ul>{result.rejectedClaims.map((claim) => <li key={claim}>{claim}</li>)}</ul></details> : null}
          </div>
          {result.recommendations.map((recommendation) => {
            const url = safeUrl(recommendation.source.finalUrl)
            const canSave = recommendation.unitMatchesInventory && result.status !== 'NoVerifiedRecommendationFound' && result.status !== 'EvidenceValidationFailed'
            return (
              <article className="crop-reference-item" key={recommendation.id} aria-label={`Recommendation ${recommendation.quantityPerArea} ${recommendation.resourceUnit} per ${recommendation.areaUnit}`}>
                <div className="crop-reference-item-heading">
                  <strong>Suggested requirement: {recommendation.quantityPerArea} {recommendation.resourceUnit} / {recommendation.areaUnit}</strong>
                  <StatusPill label={recommendation.evidenceStatus} tone={recommendation.evidenceStatus === 'Supported' ? 'info' : 'warn'} />
                </div>
                <p>Basis: {recommendation.basis}</p>
                <p>
                  Source: {recommendation.source.organizationName} – {recommendation.source.sourceName}
                  {url ? <> · <a href={url} target="_blank" rel="noopener noreferrer">Open source <ExternalLink size={13} /></a></> : null}
                </p>
                {recommendation.cropContext ? <p>Crop context: “{recommendation.cropContext}”</p> : null}
                <ul aria-label="Evidence">
                  {recommendation.components.map((component, index) => <li key={`${component.label}-${index}`}>{component.label}: {component.quantity} — “{component.evidenceText}”</li>)}
                </ul>
                {recommendation.warnings.length ? <ul>{recommendation.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul> : null}
                {canSave ? (
                  <>
                    <label className="crop-finding-confirm">
                      <input type="checkbox" checked={Boolean(confirmed[recommendation.id])} onChange={(event) => setConfirmed((current) => ({ ...current, [recommendation.id]: event.target.checked }))} />
                      {' '}I checked this value against the cited source.
                    </label>
                    <div className="crop-reference-submit">
                      <Button icon={busy === 'save' ? <Loader2 className="spin" size={16} /> : <ShieldCheck size={16} />} disabled={!confirmed[recommendation.id] || busy !== null} onClick={() => void saveDraft(result, recommendation)}>
                        Save draft for officer review
                      </Button>
                    </div>
                  </>
                ) : <Notice tone="info">This value cannot be saved: it does not use the inventory unit {result.resourceUnit} or has unsupported evidence.</Notice>}
              </article>
            )
          })}
          <div className="crop-reference-submit">
            <Button variant="ghost" icon={<X size={16} />} disabled={busy !== null} onClick={reject}>Reject</Button>
          </div>
        </div>
      ) : null}
    </section>
  )
}
