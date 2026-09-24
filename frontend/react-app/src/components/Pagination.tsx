import { Button } from './Ui'

/** Previous/next controls for a PagedResult. Renders nothing when there is nothing to page through. */
export function Pagination({
  page,
  totalPages,
  totalCount,
  pageSize,
  itemCount,
  onPageChange,
  disabled,
}: {
  page: number
  totalPages: number
  totalCount: number
  pageSize: number
  /** Rows actually on this page, so the range is right on a short last page. */
  itemCount: number
  onPageChange: (page: number) => void
  disabled?: boolean
}) {
  if (totalCount === 0) return null
  const first = (page - 1) * pageSize + 1
  const last = Math.min(first + Math.max(itemCount, 1) - 1, totalCount)

  return (
    <nav className="pagination" aria-label="Pagination">
      <span className="muted-text">Showing {first}-{last} of {totalCount}</span>
      <div className="pagination-controls">
        <Button variant="secondary" onClick={() => onPageChange(page - 1)} disabled={disabled || page <= 1}>Previous</Button>
        <span aria-live="polite">Page {page} of {Math.max(totalPages, 1)}</span>
        <Button variant="secondary" onClick={() => onPageChange(page + 1)} disabled={disabled || page >= totalPages}>Next</Button>
      </div>
    </nav>
  )
}
