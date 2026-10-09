import { Bot, Clock } from 'lucide-react'
import { formatDate, formatNumber } from '../format'
import type { WeatherResourceRequirement, WeatherResourceResult, WeatherRiskAction, WeatherRiskFactor } from '../types'
import { DataTable } from './DataTable'
import type { Column } from './DataTable'
import { StatusPill } from './StatusPill'
import { humanize, requirementTone, riskTone } from '../weatherResourceAnalysis'

/**
 * Read-only view of one stored Weather/Resource AI result: the explained weather risk (why, contributing factors,
 * impact, farmer actions), then the verified resource requirements and planning recommendations. It never edits,
 * approves or reserves anything.
 */
export function WeatherResourceAnalysisView({ result }: { result: WeatherResourceResult }) {
  const assessment = result.weatherRiskAssessment ?? null
  const risk = result.weatherRisk ?? 'Unknown'
  const factors = Array.isArray(assessment?.contributingFactors) ? assessment.contributingFactors : []
  const actions = Array.isArray(assessment?.recommendedActions) ? assessment.recommendedActions : []
  const requirements = Array.isArray(result.resourceRequirements) ? result.resourceRequirements : []
  const ruleRequirements = requirements.filter((item) => item.ruleId)
  const requirementNotes = requirements.filter((item) => !item.ruleId && item.reason).map((item) => item.reason as string)

  return (
    <div className="weather-analysis">
      <section className={`weather-risk-card weather-risk-${risk.toLowerCase()}`} aria-label="Weather risk explanation">
        <div className="weather-risk-card-header">
          <StatusPill label={`${risk} weather risk`} tone={riskTone(risk)} />
          {assessment ? (
            <span className="weather-risk-source">
              <Bot size={14} aria-hidden="true" />
              {assessment.generatedBy === 'OpenAI' ? 'AI explanation (OpenAI)' : 'Rule-based explanation'}
            </span>
          ) : null}
        </div>
        {assessment ? (
          <>
            <h4>{assessment.headline}</h4>
            <p>{assessment.explanation}</p>
          </>
        ) : (
          <>
            <p>{result.weatherSummary || 'No weather summary was recorded.'}</p>
            <p className="muted-text">This analysis was saved before detailed weather explanations were available.</p>
          </>
        )}
      </section>

      {factors.length > 0 ? (
        <div className="work-queue-list">
          <h4>Why the risk is {risk}</h4>
          <DataTable columns={factorColumns} rows={factors} getRowKey={(row) => row.metric} emptyMessage="No forecast measures were recorded." />
        </div>
      ) : null}

      {assessment ? (
        <div className="weather-analysis-columns">
          <TextList title="Potential impact" items={assessment.potentialImpacts} />
          {actions.length > 0 ? (
            <div className="work-queue-list">
              <h4>What the farmer should do</h4>
              <ol className="weather-actions">
                {actions.map((item, index) => <ActionItem key={`${index}-${item.action}`} item={item} />)}
              </ol>
            </div>
          ) : null}
        </div>
      ) : null}

      {assessment?.monitoringAdvice ? (
        <p className="weather-analysis-note"><strong>Monitoring:</strong> {assessment.monitoringAdvice}</p>
      ) : null}
      {assessment && result.weatherSummary ? <p className="muted-text">{result.weatherSummary}</p> : null}

      <dl className="work-queue-details">
        <Detail label="Resource requirements" value={humanize(result.requirementStatus)} />
        <Detail label="Human review" value={result.requiresHumanReview ? 'Required' : 'Not required'} />
        {result.reason ? <Detail label="Assessment" value={result.reason} /> : null}
      </dl>

      {ruleRequirements.length > 0 ? (
        <div className="work-queue-list">
          <h4>Verified resource requirements</h4>
          <DataTable columns={requirementColumns} rows={ruleRequirements} getRowKey={(row, index) => row.ruleId ?? String(index)} emptyMessage="No verified requirements were assessed." />
        </div>
      ) : null}
      <TextList title="Resource requirement notes" items={requirementNotes} />
      <TextList title="Planning recommendations" items={result.recommendations} />
      <TextList title="Analysis warnings" items={result.warnings} />
    </div>
  )
}

const factorColumns: Column<WeatherRiskFactor>[] = [
  { header: 'Measure', render: (row) => row.label },
  {
    header: 'Forecast',
    render: (row) => `${quantity(row.value, row.unit)}${row.observedOn ? ` on ${formatDate(row.observedOn)}` : ' in total'}`,
  },
  { header: 'Medium from', render: (row) => quantity(row.mediumThreshold, row.unit) },
  { header: 'High from', render: (row) => quantity(row.highThreshold, row.unit) },
  { header: 'Level', render: (row) => <StatusPill label={row.level} tone={riskTone(row.level)} /> },
]

const requirementColumns: Column<WeatherResourceRequirement>[] = [
  {
    header: 'Resource',
    render: (row) => (
      <div className="work-queue-cell">
        <strong>{row.resourceName}</strong>
        {row.basis ? <span className="muted-text">{row.basis}</span> : null}
      </div>
    ),
  },
  { header: 'Required', render: (row) => optionalQuantity(row.requiredQuantity, row.unit) },
  { header: 'Available', render: (row) => optionalQuantity(row.availableQuantity, row.unit) },
  { header: 'Shortage', render: (row) => optionalQuantity(row.shortageQuantity, row.unit) },
  { header: 'Status', render: (row) => <StatusPill label={humanize(row.requirementStatus)} tone={requirementTone(row.requirementStatus)} /> },
]

function ActionItem({ item }: { item: WeatherRiskAction }) {
  return (
    <li>
      <div className="weather-action-heading">
        <StatusPill label={`${item.priority} priority`} tone={riskTone(item.priority)} />
        <span className="muted-text">
          <Clock size={14} aria-hidden="true" /> {item.timing}
        </span>
      </div>
      <span>{item.action}</span>
    </li>
  )
}

export function Detail({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value || 'Not recorded'}</dd>
    </div>
  )
}

export function TextList({ title, items }: { title: string; items?: string[] | null }) {
  const values = Array.isArray(items) ? items.filter((item) => typeof item === 'string' && item.trim() !== '') : []
  if (values.length === 0) return null
  return (
    <div className="work-queue-list">
      <h4>{title}</h4>
      <ul>
        {values.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}
      </ul>
    </div>
  )
}

function quantity(value: number, unit?: string | null) {
  return `${formatNumber(value, { maximumFractionDigits: 3 })}${unit ? ` ${unit}` : ''}`
}

function optionalQuantity(value: number | null, unit?: string | null) {
  return typeof value === 'number' ? quantity(value, unit) : 'Unknown'
}
