import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { SelectInput, TextAreaInput } from '../components/FormControls'
import { ErrorState, LoadingState } from '../components/States'
import { Button, Notice, PageHeader } from '../components/Ui'
import { formatDateTime } from '../format'
import type { CropReferenceProfileDetails, WorkflowEvidenceResolution } from '../types'
import { safeSourceUrl } from './schedulingProposal'

const stateLabels: Record<number, string> = { 1: 'Draft', 2: 'Verified', 3: 'Legacy review required' }
const roleLabels: Record<string, string> = { AgriculturalOfficer: 'Agricultural Officer', Admin: 'Admin',
  FieldOfficer: 'Field Officer', ResourceOfficer: 'Resource Officer', Farmer: 'Farmer' }

export function WorkflowEvidenceResolutionPage() {
  const { id } = useParams()
  const { user } = useAuth()
  const [resolution, setResolution] = useState<WorkflowEvidenceResolution | null>(null)
  const [selectedId, setSelectedId] = useState('')
  const [details, setDetails] = useState<CropReferenceProfileDetails | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [waterRegime, setWaterRegime] = useState('')
  const [observation, setObservation] = useState('')
  const [confirmed, setConfirmed] = useState(false)
  const [createdSuccessor, setCreatedSuccessor] = useState('')
  const [detailsRefresh, setDetailsRefresh] = useState(0)
  const detailsRequest = useRef(0)
  const replacementKeys = useRef(new Map<string, string>())

  const load = useCallback(async () => {
    if (!id) return
    setLoading(true)
    try {
      const { data } = await api.get<WorkflowEvidenceResolution>('/task-approval/workflows/' + id + '/evidence-resolution')
      setResolution(data)
      setDetailsRefresh((current) => current + 1)
      setSelectedId((current) => data.profiles.some((profile) => profile.id === current) ? current
        : data.profiles.some((profile) => profile.id === data.pinnedProfileId) ? data.pinnedProfileId!
          : data.profiles[0]?.id ?? '')
      setError('')
    } catch (cause) {
      setError(getErrorMessage(cause))
    } finally {
      setLoading(false)
    }
  }, [id])
  useEffect(() => { void load() }, [load])

  const loadDetails = useCallback(async () => {
    const requestNumber = ++detailsRequest.current
    if (!selectedId) return
    setDetails(null)
    try {
      const { data } = await api.get<CropReferenceProfileDetails>('/crop-planning/crop-reference-profiles/' + selectedId)
      if (requestNumber === detailsRequest.current) setDetails(data)
    } catch (cause) {
      if (requestNumber === detailsRequest.current) setError(getErrorMessage(cause))
    }
  }, [selectedId])
  useEffect(() => { void loadDetails() }, [loadDetails, detailsRefresh])

  function selectProfile(next: string) {
    detailsRequest.current++
    setDetails(null)
    setSelectedId(next)
    setConfirmed(false)
    setWaterRegime('')
    setObservation('')
    setMessage('')
  }

  async function verify() {
    if (!details || !resolution?.fieldId) return
    setBusy(true)
    setError('')
    try {
      const { data } = await api.post<CropReferenceProfileDetails>('/crop-planning/crop-reference-profiles/' + details.id + '/verify', {
        fieldId: resolution.fieldId, waterRegime: Number(waterRegime), observation: observation.trim(),
        expectedDraftVersion: details.draftVersion, confirmed,
      })
      await load()
      setDetails(data)
      setConfirmed(false)
      setMessage('Reference verified and activated. Admin can now start a replacement workflow.')
    } catch (cause) {
      setError(getErrorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  async function replace() {
    if (!id || !resolution || !selectedId) return
    setBusy(true)
    setError('')
    const payloadKey = id + ':' + selectedId
    let key = replacementKeys.current.get(payloadKey)
    if (!key) {
      key = crypto.randomUUID()
      replacementKeys.current.set(payloadKey, key)
    }
    try {
      const { data } = await api.post<{ workflowId: string }>('/crop-plans/' + resolution.cropPlanRequestId + '/replace-blocked-workflow', {
        blockedWorkflowId: id, verifiedProfileId: selectedId, idempotencyKey: key,
      })
      setCreatedSuccessor(data.workflowId)
      await load()
      setMessage('Replacement workflow started. Continue the Field Officer and Resource Officer handoffs before approval.')
    } catch (cause) {
      setError(getErrorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  if (loading && !resolution) return <LoadingState />
  if (!resolution) return <ErrorState message={error || 'Workflow evidence is unavailable.'} />
  const successor = createdSuccessor || resolution.successorWorkflowId
  const sourceUrl = safeSourceUrl(details?.sourceUrl ?? undefined)
  const complete = Boolean(details?.stages.length && details.rules.some((rule) => rule.ruleType === 'ResourceRequirement') && sourceUrl)
  const canVerify = user?.role === 4 && details?.verificationState === 1 && !successor
  const canReplace = user?.role === 5 && resolution.canStartReplacement && !successor
  return <section className="page-stack">
    <PageHeader eyebrow="Member 4" title="Resolve workflow evidence"
      description={[resolution.cropName, resolution.varietyName, resolution.fieldName || 'Field missing', resolution.region || 'Region missing'].filter(Boolean).join(' · ')}
      actions={<Button variant="secondary" disabled={loading || busy} onClick={() => void load()}>Refresh evidence</Button>} />
    <Link to={'/task-approval/workflows/' + id}>Back to workflow review</Link>
    {error ? <ErrorState message={error} /> : null}
    {message ? <Notice tone="success">{message}</Notice> : null}
    <section className="work-section">
      <h2>Blocking evidence</h2>
      {resolution.blockingReasons.length ? resolution.blockingReasons.map((reason, index) =>
        <Notice tone="warning" key={index}>{reason}</Notice>) : <p>No blocking reasons were recorded.</p>}
      <p>Planning window: {resolution.preferredStartDate} to {resolution.preferredEndDate}</p>
      <p>Pinned reference: {resolution.pinnedProfileId || 'No reference was pinned'}</p>
      <p>Next responsible role: {roleLabels[resolution.nextResponsibleRole] ?? resolution.nextResponsibleRole}</p>
      {successor ? <Link to={'/task-approval/workflows/' + successor}>Open replacement workflow</Link> : null}
    </section>
    <section className="work-section">
      <h2>Crop reference review</h2>
      <SelectInput label="Reference version" value={selectedId} onChange={selectProfile}
        disabled={busy} options={resolution.profiles.map((profile) => ({
          value: profile.id, label: [profile.sourceName, profile.sourceVersion, stateLabels[profile.verificationState ?? 3]].join(' · '),
        }))} />
      {!resolution.profiles.length ? <Notice tone="warning">Prepare a sourced draft in crop reference management.</Notice> : null}
      <Link to="/crop-planning">Prepare or edit reference drafts</Link>
      {details ? <>
        <p>{stateLabels[details.verificationState ?? 3]} · Draft version {details.draftVersion ?? 1}
          {details.verifiedAt ? ' · Verified ' + formatDateTime(details.verifiedAt) : ''}</p>
        {details.verifiedByUserId ? <p>Verified by officer: {details.verifiedByUserId}</p> : null}
        {sourceUrl ? <a href={sourceUrl} target="_blank" rel="noopener noreferrer">{details.sourceName}</a> : <p>Source URL is missing.</p>}
        <h3>Growth stages</h3>
        <Notice tone="info">Stage days are successive durations. Check a sourced stage timetable; maturity age alone does not establish the stage schedule.</Notice>
        {details.stages.map((stage) => <article key={stage.id}><p>{stage.sequence}. {stage.stageName}: {stage.typicalMinDays ?? '?'}–{stage.typicalMaxDays ?? '?'} days</p>
          {safeSourceUrl(stage.sourceUrl ?? undefined) ? <a href={safeSourceUrl(stage.sourceUrl ?? undefined)!} target="_blank" rel="noopener noreferrer">{stage.stageName} source</a> : null}</article>)}
        <h3>Resource and scheduling rules</h3>
        {details.rules.map((rule) => <article key={rule.id}><p>{rule.ruleType}: {rule.ruleKey} · {describeRule(rule.structuredValueJson)}</p>
          {safeSourceUrl(rule.sourceUrl ?? undefined) ? <a href={safeSourceUrl(rule.sourceUrl ?? undefined)!} target="_blank" rel="noopener noreferrer">{rule.ruleKey} source</a> : null}</article>)}
        {!complete ? <Notice tone="warning">Final approval requires both growth stages and resource rules with cited sources.</Notice> : null}
        {canVerify ? <div className="form-grid">
          <SelectInput label="Field water regime" value={waterRegime} onChange={setWaterRegime} required
            options={[{ value: '1', label: 'Irrigated' }, { value: '2', label: 'Rainfed' }]} disabled={busy} />
          <TextAreaInput label="Officer observation" value={observation} onChange={setObservation} required disabled={busy} />
          <label className="field-control field-control-wide"><span>
            <input type="checkbox" checked={confirmed} disabled={busy} onChange={(event) => setConfirmed(event.target.checked)} />
            {' '}I confirm the field regime and have checked the sources, successive stage durations and applicable resource values.
          </span></label>
          <Button disabled={busy || !complete || !resolution.fieldId || !waterRegime || !observation.trim() || !confirmed}
            onClick={() => void verify()}>Verify and activate</Button>
        </div> : null}
      </> : null}
      {canReplace ? <Button disabled={busy || !resolution.compatibleVerifiedProfileIds.includes(selectedId)}
        onClick={() => void replace()}>Start replacement workflow</Button> : null}
      {!successor && !resolution.canStartReplacement && user?.role === 5 ?
        <Notice tone="info">A compatible officer-verified profile and a future planning window are required before replacement.</Notice> : null}
    </section>
  </section>
}


function describeRule(value: string): string {
  try {
    const rule = JSON.parse(value) as Record<string, unknown>
    if (typeof rule.quantityPerArea === 'number') return [rule.quantityPerArea, rule.resourceUnit, 'per', rule.areaUnit].join(' ')
    if (typeof rule.dayOffsetFromPlanting === 'number') return 'Day ' + rule.dayOffsetFromPlanting
      + ', ' + String(rule.startTimeUtc) + ' UTC, ' + String(rule.durationMinutes) + ' minutes'
    return 'Review the full rule in reference management.'
  } catch {
    return 'Invalid rule. Correct the draft before verification.'
  }
}
