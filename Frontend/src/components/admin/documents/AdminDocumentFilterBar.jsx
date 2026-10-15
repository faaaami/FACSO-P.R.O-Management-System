import { RotateCw } from 'lucide-react'

/**
 * Registry filters — exactly the four parameters the endpoint really binds.
 *
 * BACKING QUERY: GET /api/v1/admin/documents/expiring
 * Real parameters: days (1..365), page, pageSize, includeExpired. Nothing else.
 *
 * WHAT IS DELIBERATELY ABSENT, and why each one is a backend gap rather than a UI
 * choice:
 *
 *   - SEARCH. The action binds no `search` parameter and the SQL has no text
 *     predicate on any column. A field here could not filter anything.
 *   - DOCUMENT TYPE. `type` is returned on every row but is not a bound parameter,
 *     so it cannot be filtered server-side. It is displayed, never filtered.
 *   - STATUS. Not a bound parameter — and worse, it would be actively misleading
 *     here, because the registry's status label is computed on a hardcoded 90-day
 *     threshold that is decoupled from the `days` filter. At days = 90 the entire
 *     result set is "Expiring Soon", and for any window <= 90 the "Active" bucket
 *     is unreachable. A status filter built on that would offer a permanently
 *     empty option and imply the selected window controls the status.
 *   - CLIENT / ENTITY / EMPLOYEE. A company-scoped variant of this query exists in
 *     the repository but is wired only to the Client module; no Admin route exposes
 *     it. Nothing to filter on.
 *   - SORTING. The registry is hardcoded to `ORDER BY expiry_date ASC`. No column
 *     is sortable, so no header may imply that it is.
 *   - INCLUDE DELETED. The query hard-filters `is_deleted = false` and binds no such
 *     flag.
 *
 * THE WINDOW IS NOT AN UPPER BOUND ON OVERDUE HISTORY. `days` moves only the upper
 * edge of the window; switching it to 60 or 90 with "include expired" switched off
 * narrows nothing, it only looks further ahead. See the include-expired note below
 * for the unbounded case.
 */
const WINDOW_OPTIONS = [
  { value: 30, label: '30 days' },
  { value: 60, label: '60 days' },
  { value: 90, label: '90 days' },
]

function AdminDocumentFilterBar({ days, includeExpired, onDaysChange, onIncludeExpiredChange, isFetching = false }) {
  return (
    <div className="mb-4 flex flex-col gap-3 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5 lg:flex-row lg:items-start lg:justify-between lg:gap-6">
      <fieldset className="min-w-0">
        <legend className="sr-only">Expiry window</legend>
        <p
          id="admin-doc-window-label"
          className="mb-2 text-xs font-semibold text-[#6B7280]"
        >
          Expiry window
        </p>
        <div
          className="flex flex-wrap items-center gap-2"
          role="radiogroup"
          aria-labelledby="admin-doc-window-label"
        >
          {WINDOW_OPTIONS.map((option) => {
            const inputId = `admin-doc-window-${option.value}`
            const isSelected = days === option.value

            return (
              <div key={option.value} className="relative">
                <input
                  type="radio"
                  id={inputId}
                  name="admin-doc-expiry-window"
                  value={option.value}
                  checked={isSelected}
                  onChange={() => onDaysChange(option.value)}
                  className="peer sr-only"
                />
                <label
                  htmlFor={inputId}
                  className={`inline-flex cursor-pointer items-center rounded-[8px] border px-2.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none peer-focus-visible:ring-2 peer-focus-visible:ring-[rgba(15,157,116,0.15)] ${
                    isSelected
                      ? 'border-[#0F9D74]/30 bg-[rgba(15,157,116,0.10)] text-[#0B7A5A]'
                      : 'border-[#E2E4E9] bg-white text-[#6B7280] hover:border-[#0F9D74]/25 hover:text-[#16181D]'
                  }`}
                >
                  {option.label}
                </label>
              </div>
            )
          })}
        </div>
      </fieldset>

      <div className="flex min-w-0 flex-col gap-2.5 lg:max-w-[26rem] lg:items-end">
        <div className="flex items-center gap-2.5">
          <input
            type="checkbox"
            id="admin-doc-include-expired"
            checked={includeExpired}
            onChange={(event) => onIncludeExpiredChange(event.target.checked)}
            className="h-4 w-4 shrink-0 cursor-pointer rounded-[4px] border-[#E2E4E9] text-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          />
          <label
            htmlFor="admin-doc-include-expired"
            className="cursor-pointer text-xs font-semibold text-[#16181D]"
          >
            Also show all overdue (no time limit)
          </label>
        </div>

        {/*
          The label above is worded honestly rather than as a plain "Include
          expired" because the flag does something a reader would not expect:
          includeExpired=true drops the LOWER bound of the window entirely, so the
          result becomes every already-expired document ever recorded, plus the
          next N days. There is no "expired within N days" mode, and 30/60/90
          return the SAME overdue rows — only the forward-looking part grows. The
          count can therefore jump sharply the moment this is switched on.
        */}
        <p className="min-w-0 flex-1 text-xs text-[#6B7280] lg:text-right">
          {includeExpired
            ? 'Overdue documents are not limited by the window — every expired document ever recorded is included, plus the next ' +
              `${days} days.`
            : 'Only documents expiring within the next ' +
              `${days} days are shown. No text search, type filter or sorting is available: the registry endpoint supports none of them.`}
        </p>
      </div>

      {isFetching && (
        <p className="flex items-center gap-1.5 text-xs text-[#6B7280] lg:hidden" role="status">
          <RotateCw size={13} strokeWidth={1.75} aria-hidden="true" />
          Refreshing…
        </p>
      )}
    </div>
  )
}

export default AdminDocumentFilterBar
