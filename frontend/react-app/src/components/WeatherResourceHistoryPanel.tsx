import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { AlertTriangle, Eye, FileClock, RefreshCw, Search } from 'lucide-react'
import { api } from '../api/client'
import { formatDate, formatDateTime } from '../format'
import type { PagedResult, WeatherResourceHistoryDetail, WeatherResourceHistoryItem } from '../types'
import { DataTable } from './DataTable'
import type { Column } from './DataTable'
import { Pagination } from './Pagination'
import { LoadingState } from './States'
import { StatusPill } from './StatusPill'
import { Button, Notice, Toolbar } from './Ui'
import { Detail, WeatherResourceAnalysisView } from './WeatherResourceAnalysisView'
import { humanize, requirementTone, riskTone, weatherResourceHistoryUrl as historyUrl } from '../weatherResourceAnalysis'

const pageSize = 10

/**
 * Resource Officer history of Weather/Resource analyses that were already run. Each row is one crop plan workflow;
 * View loads the exact AI result stored for it. Read-only: nothing here re-runs, edits or approves a plan.
 * reloadKey lets the dashboard refresh the list after the queue runs a new analysis.
 */
export function WeatherResourceHistoryPanel({ reloadKey = 0 }: { reloadKey?: number }) {
  const [page, setPage] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [localReloadKey, setLocalReloadKey] = useState(0)
  const [history, setHistory] = useState<PagedResult<WeatherResourceHistoryItem> | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)

  const [selected, setSelected] = useState<WeatherResourceHistoryItem | null>(null)
  const [detail, setDetail] = useState<WeatherResourceHistoryDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState(false)
  const detailRequest = useRef<AbortController | null>(null)

  useEffect(() => () => detailRequest.current?.abort(), [])

  // Loading/error are reset by the handlers that change page, search or the local reload key.
  useEffect(() => {
    const controller = new AbortController()
    api
      .get<PagedResult<WeatherResourceHistoryItem>>(historyUrl, {
        params: { page, pageSize, search: search || undefined },
        signal: controller.signal,
      })
      .then((response) => {
        if (controller.signal.aborted) return
        setHistory(response.data)
        setLoadError(false)
        setLoading(false)
      })
      .catch(() => {
        if (controller.signal.aborted) return
        setLoadError(true)
        setLoading(false)
      })
    return () => controller.abort()
  }, [page, search, localReloadKey, reloadKey])

  function load(change: () => void) {
    setLoading(true)
    setLoadError(false)
    change()
  }

  function applySearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    load(() => {
      setPage(1)
      setSearch(searchInput.trim())
    })
  }

  function view(item: WeatherResourceHistoryItem) {
    detailRequest.current?.abort()
    const controller = new AbortController()
    detailRequest.current = controller
    setSelected(item)
    setDetail(null)
    setDetailError(false)
    setDetailLoading(true)
    api
      .get<WeatherResourceHistoryDetail>(`${historyUrl}/${item.workflowId}`, { signal: controller.signal })
      .then((response) => {
        if (!controller.signal.aborted) setDetail(response.data)
      })
      .catch(() => {
        if (!controller.signal.aborted) setDetailError(true)
      })
      .finally(() => {
        if (!controller.signal.aborted) setDetailLoading(false)
      })
  }

  const items = Array.isArray(history?.items) ? history.items : []
  const totalCount = history?.totalCount ?? 0

  const columns: Column<WeatherResourceHistoryItem>[] = [
    { header: 'Analysed', render: (row) => formatDateTime(row.analyzedAt) },
    {
      header: 'Crop plan',
      render: (row) => (
        <div className="work-queue-cell">
          <strong>{row.farmName || 'Unknown farm'}</strong>
          <span className="muted-text">
            {row.fieldName || 'No field recorded'} · {row.cropName || 'Unknown crop'}{row.cropVarietyName ? ` · ${row.cropVarietyName}` : ''}
          </span>
          {row.objective ? <span className="muted-text">{row.objective}</span> : null}
        </div>
      ),
    },
    {
      header: 'AI weather risk',
      render: (row) => (
        <div className="work-queue-cell">
          <StatusPill label={row.weatherRisk || 'Unknown'} tone={riskTone(row.weatherRisk)} />
          {row.headline ? <span className="muted-text history-headline">{row.headline}</span> : null}
        </div>
      ),
    },
    {
      header: 'Resources',
      render: (row) => <StatusPill label={humanize(row.requirementStatus) || 'Unknown'} tone={requirementTone(row.requirementStatus)} />,
    },
    {
      header: 'Result',
      render: (row) => row.status === 'Analyzed'
        ? <StatusPill label="Completed" tone="good" />
        : <StatusPill label="Did not complete" tone="bad" />,
    },
    { header: 'Run by', render: (row) => row.runByName || 'Not recorded' },
    {
      header: 'Action',
      render: (row) => (
        <Button variant="secondary" icon={<Eye size={16} aria-hidden="true" />} aria-label={`View analysis for ${row.farmName || 'crop plan'}`} onClick={() => view(row)}>
          View
        </Button>
      ),
    },
  ]

  return (
    <section className="work-section dashboard-section" aria-labelledby="weather-resource-history-title">
      <div className="section-title">
        <div>
          <div className="work-queue-title">
            <FileClock size={18} aria-hidden="true" />
            <h2 id="weather-resource-history-title">Weather/Resource Analysis History</h2>
          </div>
          <p>Crop plans that have already been analysed, each saved with the AI weather and resource result produced for it.</p>
        </div>
        <Button
          variant="secondary"
          icon={<RefreshCw size={16} aria-hidden="true" />}
          onClick={() => load(() => setLocalReloadKey((key) => key + 1))}
          disabled={loading}
          aria-label="Refresh history"
        >
          Refresh
        </Button>
      </div>

      <Toolbar>
        <form className="search-box" onSubmit={applySearch}>
          <span className="search-field">
            <Search size={16} aria-hidden="true" />
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Search farm, field, crop or objective"
              aria-label="Search analysis history"
            />
          </span>
          <Button variant="secondary" type="submit" aria-label="Search history">Search</Button>
        </form>
      </Toolbar>

      {loadError ? (
        <div className="state-box state-box-error" role="alert">
          <AlertTriangle size={20} aria-hidden="true" />
          <span>Unable to load the analysis history.</span>
          <Button variant="secondary" onClick={() => load(() => setLocalReloadKey((key) => key + 1))}>Retry</Button>
        </div>
      ) : loading && !history ? (
        <LoadingState label="Loading analysis history" />
      ) : (
        <>
          <p className="muted-text">{totalCount} saved analys{totalCount === 1 ? 'is' : 'es'}</p>
          <DataTable
            columns={columns}
            rows={items}
            getRowKey={(row) => row.workflowId}
            emptyTitle="No saved analyses yet"
            emptyMessage="Run a Weather/Resource Analysis from the queue and it will be saved here with its AI result."
          />
          <Pagination
            page={history?.page ?? page}
            totalPages={history?.totalPages ?? 0}
            totalCount={totalCount}
            pageSize={history?.pageSize ?? pageSize}
            itemCount={items.length}
            onPageChange={(next) => load(() => setPage(next))}
            disabled={loading}
          />
        </>
      )}

      {selected ? (
        <section className="work-queue-review" aria-label={`Saved analysis for ${selected.farmName || 'crop plan'}`}>
          <div className="work-queue-review-heading">
            <h3>Saved plan and AI analysis</h3>
            <span className="muted-text">
              {selected.farmName || 'Unknown farm'} · {selected.fieldName || 'No field recorded'} · {selected.cropName || 'Unknown crop'}
            </span>
          </div>
          <dl className="work-queue-details">
            <Detail label="Objective" value={selected.objective} />
            <Detail label="Preferred window" value={`${formatDate(selected.preferredStartDate)} - ${formatDate(selected.preferredEndDate)}`} />
            <Detail label="Analysed" value={formatDateTime(selected.analyzedAt)} />
            <Detail label="Run by" value={selected.runByName} />
          </dl>

          {detailLoading ? <LoadingState label="Loading saved analysis" /> : null}
          {detailError ? <Notice tone="error">Unable to load the saved analysis for this crop plan.</Notice> : null}
          {detail ? (
            <>
              {detail.result.status === 'SafeFailure' ? (
                <Notice tone="warning">This analysis did not complete safely, so no weather or resource assessment was produced. The crop plan needed human review.</Notice>
              ) : null}
              <WeatherResourceAnalysisView result={detail.result} />
            </>
          ) : null}
        </section>
      ) : null}
    </section>
  )
}
