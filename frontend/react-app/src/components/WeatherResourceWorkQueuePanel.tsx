import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import axios from 'axios'
import { AlertTriangle, CloudSun, PlayCircle, RefreshCw, Search } from 'lucide-react'
import { api } from '../api/client'
import { formatDate, formatDateTime } from '../format'
import type { Member3Handoff, PagedResult, WeatherResourceHistoryDetail, WeatherResourceResult, WeatherResourceRunResult, WeatherResourceWorkItem } from '../types'
import { DataTable } from './DataTable'
import type { Column } from './DataTable'
import { Pagination } from './Pagination'
import { LoadingState } from './States'
import { StatusPill } from './StatusPill'
import { Button, Notice, Toolbar } from './Ui'
import { Detail, TextList, WeatherResourceAnalysisView } from './WeatherResourceAnalysisView'
import { humanize, weatherResourceHistoryUrl } from '../weatherResourceAnalysis'

const queueUrl = '/crop-plans/weather-resource-work-queue'
const pageSize = 10
// AgentStepStatus.Running
const runningStepStatus = 2

type RunError = 'conflict' | 'failed'

/**
 * Resource Officer work queue for crop plans waiting at WeatherResourceAgent. Review reads only the existing safe
 * Member 3 handoff; Run calls the existing Member 3 endpoint, which re-validates the workflow on the server.
 * After a run the stored AI result is loaded from the history, and onAnalysisSaved lets the dashboard refresh it.
 */
export function WeatherResourceWorkQueuePanel({ onAnalysisSaved }: { onAnalysisSaved?: () => void } = {}) {
  const [page, setPage] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [queue, setQueue] = useState<PagedResult<WeatherResourceWorkItem> | null>(null)
  const [queueLoading, setQueueLoading] = useState(true)
  const [queueError, setQueueError] = useState(false)

  const [selected, setSelected] = useState<WeatherResourceWorkItem | null>(null)
  const [handoff, setHandoff] = useState<Member3Handoff | null>(null)
  const [handoffLoading, setHandoffLoading] = useState(false)
  const [handoffError, setHandoffError] = useState(false)
  const handoffRequest = useRef<AbortController | null>(null)

  const [running, setRunning] = useState(false)
  const runningRef = useRef(false)
  const [runResult, setRunResult] = useState<WeatherResourceRunResult | null>(null)
  const [runDetail, setRunDetail] = useState<WeatherResourceResult | null>(null)
  const [runError, setRunError] = useState<RunError | null>(null)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      handoffRequest.current?.abort()
    }
  }, [])

  // Loading/error are reset by the handlers that change page, search or reloadKey (see loadQueue).
  useEffect(() => {
    const controller = new AbortController()
    api
      .get<PagedResult<WeatherResourceWorkItem>>(queueUrl, {
        params: { page, pageSize, search: search || undefined, sortBy: 'readyAt', sortDirection: 'asc' },
        signal: controller.signal,
      })
      .then((response) => {
        if (controller.signal.aborted) return
        const data = response.data
        // A refresh can empty the last page (the analysed plan left the queue); step back to a page with rows.
        if (page > 1 && (data?.items?.length ?? 0) === 0 && (data?.totalCount ?? 0) > 0) {
          setPage(Math.max(1, data.totalPages))
          return
        }
        setQueue(data)
        setQueueLoading(false)
      })
      .catch(() => {
        if (controller.signal.aborted) return
        setQueueError(true)
        setQueueLoading(false)
      })
    return () => controller.abort()
  }, [page, search, reloadKey])

  function loadQueue(change: () => void) {
    setQueueLoading(true)
    setQueueError(false)
    change()
  }

  function reloadQueue() {
    loadQueue(() => setReloadKey((key) => key + 1))
  }

  function changePage(next: number) {
    loadQueue(() => setPage(next))
  }

  function applySearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    loadQueue(() => {
      setPage(1)
      setSearch(searchInput.trim())
    })
  }

  function review(item: WeatherResourceWorkItem) {
    if (runningRef.current) return
    handoffRequest.current?.abort()
    const controller = new AbortController()
    handoffRequest.current = controller
    setSelected(item)
    setHandoff(null)
    setHandoffError(false)
    setHandoffLoading(true)
    setRunResult(null)
    setRunDetail(null)
    setRunError(null)
    api
      .get<Member3Handoff>(`/crop-plans/${item.cropPlanRequestId}/member-3-handoff`, { signal: controller.signal })
      .then((response) => {
        if (!controller.signal.aborted) setHandoff(response.data)
      })
      .catch(() => {
        if (!controller.signal.aborted) setHandoffError(true)
      })
      .finally(() => {
        if (!controller.signal.aborted) setHandoffLoading(false)
      })
  }

  async function run() {
    if (!selected || runningRef.current) return
    runningRef.current = true
    setRunning(true)
    setRunError(null)
    try {
      // No body: the server derives workflow, evidence and next step from its own state.
      const response = await api.post<WeatherResourceRunResult>(`/crop-plans/${selected.cropPlanRequestId}/run-weather-resource-analysis`)
      if (!mounted.current) return
      setRunResult(response.data)
      reloadQueue()
      onAnalysisSaved?.()
      if (response.data?.workflowId) void loadRunDetail(response.data.workflowId)
    } catch (error) {
      if (!mounted.current) return
      if (axios.isAxiosError(error) && error.response?.status === 409) {
        setRunError('conflict')
        reloadQueue()
      } else {
        setRunError('failed')
      }
    } finally {
      runningRef.current = false
      if (mounted.current) setRunning(false)
    }
  }

  // The full stored result (weather explanation, factors, actions) comes from the saved history entry. If it cannot be
  // read, the compact run summary stays on screen.
  async function loadRunDetail(workflowId: string) {
    try {
      const response = await api.get<WeatherResourceHistoryDetail>(`${weatherResourceHistoryUrl}/${workflowId}`)
      if (mounted.current && response.data?.result) setRunDetail(response.data.result)
    } catch {
      // Keep the compact summary.
    }
  }

  const items = Array.isArray(queue?.items) ? queue.items : []
  const totalCount = queue?.totalCount ?? 0

  const columns: Column<WeatherResourceWorkItem>[] = [
    {
      header: 'Farm',
      render: (row) => (
        <div className="work-queue-cell">
          <strong>{row.farmName || 'Unknown farm'}</strong>
          {row.farmLocation ? <span className="muted-text">{row.farmLocation}</span> : null}
        </div>
      ),
    },
    {
      header: 'Field and crop',
      render: (row) => (
        <div className="work-queue-cell">
          <span>{row.fieldName || 'No field recorded'}</span>
          <span className="muted-text">{row.cropName || 'Unknown crop'}{row.cropVarietyName ? ` · ${row.cropVarietyName}` : ''}</span>
        </div>
      ),
    },
    {
      header: 'Objective',
      render: (row) => <span>{row.objective || 'No objective recorded'}</span>,
    },
    {
      header: 'Preferred window',
      render: (row) => `${formatDate(row.preferredStartDate)} - ${formatDate(row.preferredEndDate)}`,
    },
    {
      header: 'Ready since',
      render: (row) => formatDateTime(row.readyAt),
    },
    {
      header: 'Status',
      render: (row) => row.stepStatus === runningStepStatus
        ? <StatusPill label="Analysis in progress" tone="info" />
        : <StatusPill label="Waiting" tone="warn" />,
    },
    {
      header: 'Action',
      render: (row) => (
        <Button variant="secondary" aria-label={`Review ${row.farmName || 'crop plan'}`} disabled={running} onClick={() => review(row)}>
          Review
        </Button>
      ),
    },
  ]

  const selectedIsRunning = selected?.stepStatus === runningStepStatus
  const canShowRun = Boolean(selected && handoff && !runResult)

  return (
    <section className="work-section dashboard-section" aria-labelledby="weather-resource-queue-title">
      <div className="section-title">
        <div>
          <div className="work-queue-title">
            <CloudSun size={18} aria-hidden="true" />
            <h2 id="weather-resource-queue-title">Weather/Resource Analysis Queue</h2>
          </div>
          <p>Crop plans whose field analysis is complete and that are waiting for Weather/Resource Analysis.</p>
        </div>
        <Button variant="secondary" icon={<RefreshCw size={16} aria-hidden="true" />} onClick={reloadQueue} disabled={queueLoading}>
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
              aria-label="Search Weather/Resource work"
            />
          </span>
          <Button variant="secondary" type="submit">Search</Button>
        </form>
      </Toolbar>

      {queueError ? (
        <div className="state-box state-box-error" role="alert">
          <AlertTriangle size={20} aria-hidden="true" />
          <span>Unable to load Weather/Resource work.</span>
          <Button variant="secondary" onClick={reloadQueue}>Retry</Button>
        </div>
      ) : queueLoading && !queue ? (
        <LoadingState label="Loading Weather/Resource work" />
      ) : (
        <>
          <p className="muted-text">{totalCount} crop plan{totalCount === 1 ? '' : 's'} waiting for Weather/Resource Analysis</p>
          <DataTable
            columns={columns}
            rows={items}
            getRowKey={(row) => row.workflowId}
            emptyTitle="Queue is clear"
            emptyMessage="No crop plans are waiting for Weather/Resource Analysis."
          />
          <Pagination
            page={queue?.page ?? page}
            totalPages={queue?.totalPages ?? 0}
            totalCount={totalCount}
            pageSize={queue?.pageSize ?? pageSize}
            itemCount={items.length}
            onPageChange={changePage}
            disabled={queueLoading}
          />
        </>
      )}

      {selected ? (
        <section className="work-queue-review" aria-label={`Handoff review for ${selected.farmName || 'crop plan'}`}>
          <div className="work-queue-review-heading">
            <h3>Field analysis handoff</h3>
            <span className="muted-text">
              {selected.farmName || 'Unknown farm'} · {selected.fieldName || 'No field recorded'} · {selected.cropName || 'Unknown crop'}
            </span>
          </div>

          {handoffLoading ? <LoadingState label="Loading field analysis handoff" /> : null}
          {handoffError ? (
            <Notice tone="error">Unable to load the field analysis handoff. It may no longer be waiting for Weather/Resource Analysis.</Notice>
          ) : null}
          {handoff ? <HandoffSummary handoff={handoff} /> : null}

          {selectedIsRunning && !runResult ? (
            <Notice tone="info">Weather/Resource Analysis is already in progress for this crop plan.</Notice>
          ) : null}
          {canShowRun ? (
            <div className="work-queue-actions">
              <Button
                icon={<PlayCircle size={16} aria-hidden="true" />}
                onClick={() => void run()}
                disabled={running || selectedIsRunning || runError === 'conflict'}
              >
                {running ? 'Running analysis...' : 'Run Weather/Resource Analysis'}
              </Button>
            </div>
          ) : null}

          {runError === 'conflict' ? (
            <Notice tone="warning">This crop plan is already running or has moved on. The queue has been refreshed.</Notice>
          ) : null}
          {runError === 'failed' ? (
            <Notice tone="error">Weather/Resource Analysis could not be started. Please try again.</Notice>
          ) : null}
          {runResult ? <RunResultSummary result={runResult} detail={runDetail?.workflowId === runResult.workflowId ? runDetail : null} /> : null}
        </section>
      ) : null}
    </section>
  )
}

function HandoffSummary({ handoff }: { handoff: Member3Handoff }) {
  const risks = Array.isArray(handoff.identifiedRisks) ? handoff.identifiedRisks : []
  return (
    <>
      <p>{handoff.fieldAnalysisSummary || 'No field analysis summary was provided.'}</p>
      <dl className="work-queue-details">
        <Detail label="Location" value={handoff.fieldLocationContext} />
        <Detail label="Preferred window" value={`${formatDate(handoff.preferredStartDate)} - ${formatDate(handoff.preferredEndDate)}`} />
        <Detail label="Priority" value={humanize(handoff.priority)} />
        <Detail label="Field suitability" value={humanize(handoff.fieldSuitability)} />
        <Detail label="Planting readiness" value={humanize(handoff.plantingReadiness)} />
        <Detail label="Human review" value={handoff.requiresHumanReview ? 'Required' : 'Not required'} />
        <Detail label="Soil" value={handoff.soilAssessment} />
        <Detail label="Water" value={handoff.waterAssessment} />
        <Detail label="Drainage" value={handoff.drainageAssessment} />
        <Detail label="Identified risks" value={risks.length > 0 ? risks.map(humanize).join(', ') : 'None identified'} />
      </dl>
      <TextList title="Field preparation requirements" items={handoff.fieldPreparationRequirements} />
      <TextList title="Recommended pre-planting actions" items={handoff.recommendedPrePlantingActions} />
      <TextList title="Field analysis warnings" items={handoff.warnings} />
    </>
  )
}

function RunResultSummary({ result, detail }: { result: WeatherResourceRunResult; detail: WeatherResourceResult | null }) {
  const analysed = result.status === 'Analyzed'
  return (
    <div className="work-queue-result">
      {analysed ? (
        <Notice tone="success">Weather/Resource Analysis completed. Scheduling Validation is next; the plan has not been approved. It is saved in the analysis history.</Notice>
      ) : (
        <Notice tone="warning">Weather/Resource Analysis could not complete safely. The crop plan needs human review before it can continue.</Notice>
      )}
      {detail && analysed ? (
        <WeatherResourceAnalysisView result={detail} />
      ) : (
        <>
          <dl className="work-queue-details">
            <Detail label="Weather risk" value={humanize(result.weatherRisk)} />
            <Detail label="Resource requirements" value={humanize(result.requirementStatus)} />
            <Detail label="Human review" value={result.requiresHumanReview ? 'Required' : 'Not required'} />
          </dl>
          <TextList title="Analysis warnings" items={result.warnings} />
        </>
      )}
    </div>
  )
}