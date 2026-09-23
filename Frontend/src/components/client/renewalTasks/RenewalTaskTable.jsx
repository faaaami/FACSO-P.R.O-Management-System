import { format } from 'date-fns'
import { AlertTriangle, ChevronLeft, ChevronRight, Eye, RefreshCw } from 'lucide-react'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import { RENEWAL_TASK_STATUS, enumLabel } from '../enumLabels'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function taskStatusTone(status) {
  if (status === 'Blocked') return 'danger'
  if (status === 'Approved' || status === 'Updated') return 'success'
  return 'warning'
}

function BlockedPreview({ blockedReason }) {
  if (!blockedReason) return <span className="text-[#9CA3AF]">–</span>
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-[#DC2626]">
      <AlertTriangle size={12} strokeWidth={2} aria-hidden="true" />
      <span className="block max-w-[14rem] truncate">{blockedReason}</span>
    </span>
  )
}

function RowAction({ onSelect }) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className="inline-flex items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-1.5 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
    >
      <Eye size={13} strokeWidth={2} aria-hidden="true" />
      Details
    </button>
  )
}

function MobileRow({ item, onSelect }) {
  return (
    <li className="flex flex-col gap-3 rounded-[10px] bg-white border border-[#E2E4E9] p-4 sm:hidden">
      <div className="flex items-center justify-between gap-3">
        <StatusPill
          label={enumLabel(RENEWAL_TASK_STATUS, item.status) ?? 'Pending'}
          tone={taskStatusTone(item.status)}
        />
        <span className="text-xs font-medium text-[#9CA3AF]">#{item.id.slice(0, 8)}</span>
      </div>
      <div className="space-y-1.5 text-xs">
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Started</span>
          <span className="font-medium text-[#16181D]">{formatDate(item.createdAt) ?? 'In queue'}</span>
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Completed</span>
          <span className="font-medium text-[#16181D]">{formatDate(item.completedAt) ?? '–'}</span>
        </div>
        {item.status === 'Blocked' && (
          <div className="flex items-start justify-between gap-3">
            <span className="shrink-0 text-[#6B7280]">Blocked</span>
            <BlockedPreview blockedReason={item.blockedReason} />
          </div>
        )}
      </div>
      <div className="self-end">
        <RowAction onSelect={onSelect} />
      </div>
    </li>
  )
}

function Pagination({ page, pageSize, totalCount, onPageChange }) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)

  return (
    <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-xs text-[#9CA3AF]">
        Showing {from}–{to} of {totalCount} task{totalCount === 1 ? '' : 's'}
      </p>
      <div className="flex items-center gap-2">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
        >
          <ChevronLeft size={14} strokeWidth={2} aria-hidden="true" />
          Previous
        </button>
        <span className="rounded-[6px] bg-[#F7F8FA] px-2.5 py-1 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
          Page {page} of {totalPages}
        </span>
        <button
          type="button"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
        >
          Next
          <ChevronRight size={14} strokeWidth={2} aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}

function RenewalTaskTable({ data, isLoading, isError, onRetry, page, pageSize, onPageChange, onSelect }) {
  let content

  if (isLoading) {
    content = <LoadingState label="Loading renewal tasks…" />
  } else if (isError) {
    content = <ErrorState message="Could not load renewal tasks." onRetry={onRetry} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={RefreshCw}
        message="No renewal tasks found."
        description="No renewal tasks match the current filter settings. Adjust or clear the filters to see the full trail."
      />
    )
  } else {
    content = (
      <>
        {/* Mobile card list */}
        <ul className="space-y-3 sm:hidden">
          {data.items.map((item) => (
            <MobileRow key={item.id} item={item} onSelect={() => onSelect(item.id)} />
          ))}
        </ul>

        {/* Desktop table */}
        <div className="hidden sm:block overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Status
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Blocked
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Started
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Completed
                </th>
                <th className="bg-gray-50 text-right text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E2E4E9]">
              {data.items.map((item) => (
                <tr key={item.id} className="hover:bg-gray-50/60 transition-colors">
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <StatusPill
                      label={enumLabel(RENEWAL_TASK_STATUS, item.status) ?? 'Pending'}
                      tone={taskStatusTone(item.status)}
                    />
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <BlockedPreview blockedReason={item.blockedReason} />
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    {formatDate(item.createdAt) ?? <span className="text-[#9CA3AF]">In queue</span>}
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    {formatDate(item.completedAt) ?? <span className="text-[#9CA3AF]">–</span>}
                  </td>
                  <td className="px-4 py-3.5 text-right">
                    <RowAction onSelect={() => onSelect(item.id)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={data.totalCount}
          onPageChange={onPageChange}
        />
      </>
    )
  }

  return (
    <section aria-label="Renewal tasks list" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-start justify-between gap-4 mb-5">
        <div className="min-w-0 flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <RefreshCw size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">Renewal Tasks</h2>
            <p className="mt-0.5 text-xs text-[#6B7280]">Documents in the renewal workflow and their processing trail</p>
          </div>
        </div>
        {data?.totalCount != null && (
          <span className="shrink-0 rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
            {data.totalCount}
          </span>
        )}
      </div>

      {content}
    </section>
  )
}

export default RenewalTaskTable