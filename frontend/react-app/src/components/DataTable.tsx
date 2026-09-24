import { EmptyState } from './States'

export type Column<T> = {
  header: string
  render: (row: T) => React.ReactNode
  className?: string
  /** Backend SortBy field. When set (and the table has onSort) the header becomes a sort button. */
  sortKey?: string
}

export type SortState = {
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export function DataTable<T>({
  columns,
  rows,
  emptyMessage,
  emptyTitle,
  getRowKey,
  sort,
  onSort,
}: {
  columns: Column<T>[]
  rows: T[]
  emptyMessage: string
  emptyTitle?: string
  getRowKey?: (row: T, index: number) => string
  sort?: SortState
  onSort?: (sortKey: string) => void
}) {
  if (rows.length === 0) {
    return <EmptyState title={emptyTitle} message={emptyMessage} />
  }

  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            {columns.map((column) => {
              const sortable = Boolean(onSort && column.sortKey)
              const active = sortable && sort?.sortBy === column.sortKey
              return (
                <th
                  key={column.header}
                  className={column.className}
                  aria-sort={active ? (sort?.sortDirection === 'desc' ? 'descending' : 'ascending') : undefined}
                >
                  {sortable ? (
                    <button type="button" className="sort-button" onClick={() => onSort?.(column.sortKey as string)}>
                      {column.header}
                      <span aria-hidden="true">{active ? (sort?.sortDirection === 'desc' ? ' ▼' : ' ▲') : ''}</span>
                    </button>
                  ) : column.header}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={getRowKey ? getRowKey(row, index) : index}>
              {columns.map((column) => (
                <td key={column.header} className={column.className}>{column.render(row)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
