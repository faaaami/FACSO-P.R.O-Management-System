import { ChevronLeft, ChevronRight } from 'lucide-react'

/**
 * `itemLabelPlural` exists for irregular plurals ("company" -> "companies").
 * When it is omitted the component falls back to appending "s", which is what
 * every existing caller already relies on, so adding it changes no current
 * behaviour. Without it a label like "company" rendered as "companys".
 */
function Pagination({ page, pageSize, totalCount, itemLabel, itemLabelPlural, onPageChange }) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)
  const singular = itemLabel ?? 'item'
  const noun = totalCount === 1 ? singular : (itemLabelPlural ?? `${singular}s`)

  return (
    <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <p aria-live="polite" className="text-xs text-[#9CA3AF]">
        Showing {from}–{to} of {totalCount} {noun}
      </p>
      <div className="flex items-center gap-2">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          aria-label="Go to previous page"
          className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
        >
          <ChevronLeft size={14} strokeWidth={2} aria-hidden="true" />
          Previous
        </button>
        <span
          aria-live="polite"
          className="rounded-[6px] bg-[#F7F8FA] px-2.5 py-1 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]"
        >
          Page {page} of {totalPages}
        </span>
        <button
          type="button"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          aria-label="Go to next page"
          className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
        >
          Next
          <ChevronRight size={14} strokeWidth={2} aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}

export default Pagination