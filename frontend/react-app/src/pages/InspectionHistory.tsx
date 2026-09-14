import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, getErrorMessage } from '../api/client'
import { DataTable } from '../components/DataTable'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { PageHeader } from '../components/Ui'
import { formatDateTime } from '../format'
import type { Inspection, InspectionHistoryEvent, PagedResult } from '../types'

export function InspectionHistory() {
  const { id } = useParams()
  const [events, setEvents] = useState<InspectionHistoryEvent[]>([])
  const [inspections, setInspections] = useState<Inspection[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const loadHistory = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      if (id) {
        const response = await api.get<InspectionHistoryEvent[]>(`/inspections/${id}/history`)
        setEvents(response.data)
        return
      }

      const response = await api.get<PagedResult<Inspection>>('/inspections', { params: { sortBy: 'completedAt', sortDirection: 'desc', pageSize: 50 } })
      setInspections(response.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }, [id])

  useEffect(() => {
    void loadHistory()
  }, [loadHistory])

  if (isLoading) return <LoadingState />
  if (error) return <ErrorState message={error} />

  return (
    <section className="page-stack">
      <PageHeader eyebrow="Field Operations" title={id ? 'Inspection History' : 'Inspection History Index'} description={id ? 'Chronological events for the selected inspection.' : 'Open an inspection to review its event timeline.'} />
      {id ? (
        <section className="work-section">
          <DataTable
            rows={events}
            emptyMessage="No history events were returned."
            getRowKey={(row, index) => row.relatedId ?? `${row.eventType}-${index}`}
            columns={[
              { header: 'Time', render: (row) => formatDateTime(row.occurredAt) },
              { header: 'Event', render: (row) => row.eventType },
              { header: 'Summary', render: (row) => row.summary },
            ]}
          />
        </section>
      ) : inspections.length ? (
        <section className="work-section">
          <DataTable
            rows={inspections}
            emptyMessage="No inspections found."
            getRowKey={(row) => row.id}
            columns={[
              { header: 'Scheduled', render: (row) => formatDateTime(row.scheduledAt) },
              { header: 'Summary', render: (row) => row.summary },
              { header: 'Open', className: 'actions-cell', render: (row) => <Link className="ui-button ui-button-ghost" to={`/inspections/${row.id}/history`}>Timeline</Link> },
            ]}
          />
        </section>
      ) : <EmptyState title="No inspections" message="Inspection timelines will appear after records are created." />}
    </section>
  )
}
