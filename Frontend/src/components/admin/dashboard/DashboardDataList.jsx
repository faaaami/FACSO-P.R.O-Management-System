import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * Responsive data list shared by the four Phase 1 Admin Dashboard widgets.
 *
 * One component serves both breakpoints because a dashboard list has two
 * different jobs: on a wide screen it is a scannable table, on a phone it is a
 * stack of self-describing records. Duplicating the markup per widget is how the
 * two versions drift apart, so the columns are described once as data and both
 * renderings are derived from that description.
 *
 * `columns` is an array of:
 *   { key, header, render(row), align?, width?, primary? }
 * `primary: true` marks the column that becomes the heading of a mobile card
 * (at most one per list). Every other column becomes a label/value pair, so a
 * 390px viewport never needs horizontal scrolling to read a row.
 *
 * The table carries a visually hidden <caption> and <th scope="col"> so the
 * column meanings are available to assistive technology, not just to sighted
 * users scanning the header row.
 */
function DashboardDataList({
  caption,
  columns,
  rows,
  loading = false,
  loadingLabel = 'Loading…',
  error = null,
  onRetry,
  errorMessage = 'Could not load this section.',
  isEmpty = false,
  emptyMessage = 'Nothing to show yet.',
  emptyDescription,
  emptyIcon,
}) {
  if (loading) return <LoadingState label={loadingLabel} />
  if (error) {
    return <ErrorState message={extractApiErrorMessage(error, errorMessage)} onRetry={onRetry} />
  }
  if (isEmpty) {
    return <EmptyState message={emptyMessage} description={emptyDescription} icon={emptyIcon} />
  }

  const list = Array.isArray(rows) ? rows : []
  const primaryColumn = columns.find((column) => column.primary) ?? columns[0]
  const secondaryColumns = columns.filter((column) => column !== primaryColumn)

  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full border-collapse text-left text-sm">
          <caption className="sr-only">{caption}</caption>
          <thead>
            <tr className="border-b border-[#E2E4E9]">
              {columns.map((column) => (
                <th
                  key={column.key}
                  scope="col"
                  style={column.width ? { width: column.width } : undefined}
                  className={`whitespace-nowrap px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] first:pl-0 last:pr-0 ${
                    column.align === 'right' ? 'text-right' : 'text-left'
                  }`}
                >
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {list.map((row, rowIndex) => (
              <tr
                key={row?.id ?? rowIndex}
                className="border-b border-[#E2E4E9] last:border-b-0"
              >
                {columns.map((column) => (
                  <td
                    key={column.key}
                    className={`px-3 py-3 align-middle text-[#16181D] first:pl-0 last:pr-0 ${
                      column.align === 'right' ? 'text-right' : 'text-left'
                    } ${column.cellClassName ?? ''}`}
                  >
                    {column.render(row, rowIndex)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {list.map((row, rowIndex) => (
          <li
            key={row?.id ?? rowIndex}
            className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
          >
            <p className="text-sm font-semibold text-[#16181D]">
              {primaryColumn.render(row, rowIndex)}
            </p>
            <dl className="mt-2.5 flex flex-col gap-1.5">
              {secondaryColumns.map((column) => (
                <div key={column.key} className="flex items-start justify-between gap-3">
                  <dt className="text-xs text-[#6B7280]">{column.header}</dt>
                  <dd className="text-right text-xs font-medium text-[#16181D]">
                    {column.render(row, rowIndex)}
                  </dd>
                </div>
              ))}
            </dl>
          </li>
        ))}
      </ul>
    </>
  )
}

export default DashboardDataList
