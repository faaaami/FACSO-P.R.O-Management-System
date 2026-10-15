import { format } from 'date-fns'
import { Building2, ChevronLeft, ChevronRight, ClipboardList, Eye, Users } from 'lucide-react'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import { SERVICE_REQUEST_STATUS, SERVICE_REQUEST_TYPES, enumLabel } from '../enumLabels'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function requestStatusTone(status) {
  if (status === 'Converted') return 'success'
  if (status === 'Rejected') return 'danger'
  return 'warning'
}

function RequestTypeLabel({ type }) {
  return (
    <span className="rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-medium text-[#6B7280] border border-[#E2E4E9]">
      {enumLabel(SERVICE_REQUEST_TYPES, type) ?? 'Service Request'}
    </span>
  )
}

function SubjectLabel({ item }) {
  if (item.employeeId) {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-[#6B7280]">
        <Users size={12} strokeWidth={1.75} aria-hidden="true" />
        Employee
      </span>
    )
  }
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-[#6B7280]">
      <Building2 size={12} strokeWidth={1.75} aria-hidden="true" />
      Entity
    </span>
  )
}

function NotesPreview({ description }) {
  if (!description) return <span className="text-[#9CA3AF]">No notes</span>
  return <span className="block max-w-[16rem] truncate">{description}</span>
}

/**
 * The status pill, plus the rejection reason when there is one.
 *
 * The reason is the part a client actually needs from a rejected row, and a list
 * whose only signal is the word "Rejected" makes them open every one of them to
 * find out why. It is placed under the pill in the status cell rather than in its
 * own column, so the reason appears only where there is room for it — one line,
 * truncated — and the full text stays available in the element's `title` and in the
 * details drawer.
 *
 * A rejected request with no recorded reason renders the honest fallback rather
 * than an empty line. This can only be a request decided before a reason was
 * required, but the list must not imply there is nothing to see.
 */
function StatusCell({ item }) {
  const isRejected = item.status === 'Rejected'

  return (
    <div className="flex flex-col items-start gap-1.5">
      <StatusPill
        label={enumLabel(SERVICE_REQUEST_STATUS, item.status) ?? 'Submitted'}
        tone={requestStatusTone(item.status)}
      />
      {isRejected &&
        (item.rejectionReason ? (
          <span
            className="block max-w-[16rem] truncate text-[11px] text-[#9A1C1C]"
            title={item.rejectionReason}
          >
            {item.rejectionReason}
          </span>
        ) : (
          <span className="block text-[11px] text-[#9CA3AF]">No reason recorded</span>
        ))}
    </div>
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
        <RequestTypeLabel type={item.type} />
        <StatusPill
          label={enumLabel(SERVICE_REQUEST_STATUS, item.status) ?? 'Submitted'}
          tone={requestStatusTone(item.status)}
        />
      </div>
      <div className="space-y-1.5 text-xs">
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Subject</span>
          <SubjectLabel item={item} />
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Created</span>
          <span className="font-medium text-[#16181D]">
            {formatDate(item.createdAt) ?? 'N/A'}
          </span>
        </div>
        <div className="flex items-start justify-between gap-3">
          <span className="shrink-0 text-[#6B7280]">Notes</span>
          <NotesPreview description={item.description} />
        </div>
        {/*
          The reason is shown on the card rather than left behind the status pill,
          because a mobile card has the vertical room the table's status cell does
          not and a rejected request is the one a client most needs to read.
        */}
        {item.status === 'Rejected' && (
          <div className="flex items-start justify-between gap-3">
            <span className="shrink-0 text-[#6B7280]">Reason</span>
            {item.rejectionReason ? (
              <span className="text-right text-[#9A1C1C] break-words">{item.rejectionReason}</span>
            ) : (
              <span className="text-right text-[#9CA3AF]">No reason recorded</span>
            )}
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
        Showing {from}–{to} of {totalCount} request{totalCount === 1 ? '' : 's'}
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

function ServiceRequestTable({ data, isLoading, isError, onRetry, page, pageSize, onPageChange, onSelect }) {
  let content

  if (isLoading) {
    content = <LoadingState label="Loading service requests…" />
  } else if (isError) {
    content = <ErrorState message="Could not load service requests." onRetry={onRetry} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={ClipboardList}
        message="No service requests found."
        description="No service requests match the current filter settings. Adjust or clear the filters, or submit a new request."
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
                  Type
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Subject
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Notes
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Created
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Status
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
                    <RequestTypeLabel type={item.type} />
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <SubjectLabel item={item} />
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <NotesPreview description={item.description} />
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    {formatDate(item.createdAt) ?? <span className="text-[#9CA3AF]">N/A</span>}
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <StatusCell item={item} />
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
    <section aria-label="Service requests list" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-start justify-between gap-4 mb-5">
        <div className="min-w-0 flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <ClipboardList size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">Service Requests</h2>
            <p className="mt-0.5 text-xs text-[#6B7280]">Submitted requests and their processing trail</p>
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

export default ServiceRequestTable