import { ExternalLink, Pencil, SearchCheck, X } from 'lucide-react'
import type { CropFindingSuggestion, EvidenceStatus, ReferenceDiscoveryResponse, ReferenceDraftItem, ReviewDecision } from '../cropFinding'
import { Button, Notice } from './Ui'
import { StatusPill } from './StatusPill'

const statusTone: Record<EvidenceStatus, 'good' | 'warn' | 'bad' | 'info'> = {
  Supported: 'good',
  'Partially Supported': 'warn',
  Unsupported: 'bad',
  Conflict: 'warn',
  'Manual Review Required': 'info',
}

function EvidenceStatusPill({ status }: { status: EvidenceStatus }) {
  return <StatusPill label={status} tone={statusTone[status]} />
}

function Provenance({ item }: { item: { provenance: CropFindingSuggestion['provenance']; warnings: string[] } }) {
  return <>
    {item.provenance.map((source, index) => <div className="crop-finding-provenance" key={`${source.sourceId}-${index}`}>
      <div className="crop-finding-provenance-heading">
        <strong>{source.organizationName || source.sourceName}</strong>
        <StatusPill label={source.sourceClassification} tone={source.sourceClassification === 'Sri Lankan' ? 'good' : 'warn'} />
      </div>
      {source.evidenceText ? <q>{source.evidenceText}</q> : <span>No machine-readable evidence excerpt was returned.</span>}
      <div className="crop-finding-source-meta">
        {source.pageNumber ? <span>PDF page {source.pageNumber}</span> : null}
        {source.section ? <span>Section: {source.section}</span> : null}
        <a href={source.originalUrl} target="_blank" rel="noreferrer">Open original source <ExternalLink size={13} /></a>
      </div>
    </div>)}
    {item.warnings.length ? <ul className="crop-finding-warnings">{item.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul> : null}
  </>
}

export function FindingSummary({ analysis, recommendations, warnings }: { analysis: string[]; recommendations: string[]; warnings: string[] }) {
  if (!analysis.length && !recommendations.length && !warnings.length) return null
  return <div className="crop-finding-summary">
    {analysis.length ? <section><span className="crop-finding-label">AI Analysis</span><ul>{analysis.map((item) => <li key={item}>{item}</li>)}</ul></section> : null}
    {recommendations.length ? <section><span className="crop-finding-label">AI Recommendation</span><ul>{recommendations.map((item) => <li key={item}>{item}</li>)}</ul></section> : null}
    {warnings.length ? <Notice tone="warning"><ul>{warnings.map((item) => <li key={item}>{item}</li>)}</ul></Notice> : null}
  </div>
}

export function SuggestionReview({
  suggestions,
  decisions,
  noun,
  onAccept,
  onEdit,
  onReject,
}: {
  suggestions: CropFindingSuggestion[]
  decisions: Record<string, ReviewDecision>
  noun: 'crop' | 'variety'
  onAccept: (suggestion: CropFindingSuggestion) => void
  onEdit: (suggestion: CropFindingSuggestion) => void
  onReject: (suggestion: CropFindingSuggestion) => void
}) {
  if (!suggestions.length) return <Notice>No evidence-backed {noun} suggestions were found.</Notice>
  return <div className="crop-finding-review-list">
    {suggestions.map((suggestion) => {
      const decision = decisions[suggestion.id] ?? 'pending'
      const blocked = suggestion.alreadyExists || ['Unsupported', 'Manual Review Required', 'Conflict'].includes(suggestion.evidenceStatus)
      return <article className="crop-finding-review-card" key={suggestion.id}>
        <header>
          <div><span className="crop-finding-label">AI Suggestion</span><h4>{suggestion.name}</h4></div>
          <div className="crop-finding-pills"><EvidenceStatusPill status={suggestion.evidenceStatus} />{suggestion.alreadyExists ? <StatusPill label="Already Exists" tone="info" /> : null}{decision !== 'pending' ? <StatusPill label={decision === 'edited' ? 'Editing in form' : decision} tone={decision === 'rejected' ? 'bad' : 'info'} /> : null}</div>
        </header>
        {suggestion.description ? <p>{suggestion.description}</p> : null}
        <p className="crop-finding-explanation">{suggestion.explanation}</p>
        <Provenance item={suggestion} />
        <footer className="crop-finding-actions">
          <Button variant="secondary" icon={<SearchCheck size={15} />} disabled={blocked} onClick={() => onAccept(suggestion)}>Accept</Button>
          <Button variant="ghost" icon={<Pencil size={15} />} disabled={blocked} onClick={() => onEdit(suggestion)}>Edit in form</Button>
          <Button variant="ghost" icon={<X size={15} />} onClick={() => onReject(suggestion)}>Reject</Button>
        </footer>
      </article>
    })}
  </div>
}

function fieldLabel(field: ReferenceDraftItem['field']) {
  return ({ sourceName: 'Source name', sourceUrl: 'Source URL', sourceVersion: 'Source version', region: 'Region / applicability', growthStage: 'Growth stage', minimumDays: 'Minimum days', maximumDays: 'Maximum days', evidenceNotes: 'Evidence notes', structuredRule: 'Structured rule' })[field]
}

export function ReferenceDiscoveryReview({
  response,
  primarySourceId,
  decisions,
  onSelectPrimary,
  onAccept,
  onEdit,
  onReject,
}: {
  response: ReferenceDiscoveryResponse
  primarySourceId: string
  decisions: Record<string, ReviewDecision>
  onSelectPrimary: (sourceId: string) => void
  onAccept: (sourceId: string, item: ReferenceDraftItem) => void
  onEdit: (sourceId: string, item: ReferenceDraftItem) => void
  onReject: (item: ReferenceDraftItem) => void
}) {
  return <div className="crop-finding-reference-review">
    <FindingSummary analysis={response.analysis} recommendations={response.recommendations} warnings={response.warnings} />
    {response.unsupportedFields.length ? <Notice tone="warning">Unsupported fields: {response.unsupportedFields.join(', ')}. Leave these manual unless the original source supports them.</Notice> : null}
    {!response.sourceDrafts.length ? <Notice>No approved readable sources were returned. Retry or enter evidence manually.</Notice> : null}
    {response.sourceDrafts.map((draft) => {
      const selected = primarySourceId === draft.source.sourceId
      return <section className={`crop-finding-source-card${selected ? ' is-primary' : ''}`} key={draft.source.sourceId}>
        <header>
          <div>
            <span className="crop-finding-label">{selected ? 'Primary source' : 'Discovered source'}</span>
            <h4>{draft.source.title}</h4>
            <p>{draft.source.organizationName} · {draft.source.sourceCategory}</p>
          </div>
          <div className="crop-finding-pills">
            <StatusPill label={draft.source.sourceClassification} tone={draft.source.sourceClassification === 'Sri Lankan' ? 'good' : 'warn'} />
            {draft.source.existingReference ? <StatusPill label="Existing Reference" tone="info" /> : null}
            {draft.source.manualReviewRequired ? <StatusPill label="Manual Review Required" tone="warn" /> : null}
          </div>
        </header>
        <div className="crop-finding-source-choice">
          <a href={draft.source.originalUrl} target="_blank" rel="noreferrer">Open original source <ExternalLink size={13} /></a>
          <Button variant={selected ? 'secondary' : 'ghost'} disabled={draft.source.retrievalStatus !== 'Retrieved'} onClick={() => onSelectPrimary(draft.source.sourceId)}>{selected ? 'Primary source selected' : 'Select primary source'}</Button>
        </div>
        <p className="crop-finding-explanation">{draft.source.acceptanceReason}</p>
        {draft.source.warnings.length ? <ul className="crop-finding-warnings">{draft.source.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul> : null}
        {draft.items.map((item) => {
          const decision = decisions[item.id] ?? 'pending'
          const hardBlocked = ['Unsupported', 'Manual Review Required'].includes(item.evidenceStatus)
          const wrongSource = !selected
          return <article className="crop-finding-evidence-item" key={item.id}>
            <header><strong>{fieldLabel(item.field)}</strong><div className="crop-finding-pills"><EvidenceStatusPill status={item.evidenceStatus} />{item.conflictGroupId ? <StatusPill label="Source conflict" tone="warn" /> : null}{decision !== 'pending' ? <StatusPill label={decision} tone={decision === 'rejected' ? 'bad' : 'info'} /> : null}</div></header>
            <code>{item.displayValue || 'No suggested value'}</code>
            <p>{item.explanation}</p>
            <Provenance item={item} />
            <footer className="crop-finding-actions">
              <Button variant="secondary" disabled={hardBlocked || wrongSource} onClick={() => onAccept(draft.source.sourceId, item)}>{item.evidenceStatus === 'Conflict' ? 'Choose this claim' : 'Accept'}</Button>
              <Button variant="ghost" icon={<Pencil size={15} />} disabled={hardBlocked || wrongSource} onClick={() => onEdit(draft.source.sourceId, item)}>Edit in form</Button>
              <Button variant="ghost" icon={<X size={15} />} onClick={() => onReject(item)}>Reject</Button>
            </footer>
          </article>
        })}
        <FindingSummary analysis={draft.analysis} recommendations={draft.recommendations} warnings={[]} />
      </section>
    })}
  </div>
}
