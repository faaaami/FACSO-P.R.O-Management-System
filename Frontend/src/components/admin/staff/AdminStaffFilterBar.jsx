import { Search, X } from 'lucide-react'

/**
 * Filter control for the Admin staff list.
 *
 * TWO CONTROLS, AND THE SECOND ONE IS HONEST ABOUT WHAT IT CANNOT DO.
 *
 * "Include inactive" is a real server-side parameter. GET /api/v1/admin/staff
 * binds includeInactive, page and pageSize, and the repository applies the
 * is_active predicate to the same query that produces totalCount — so the
 * reported total always matches the rows shown. That is exactly the property a
 * client-side-only control cannot offer.
 *
 * THE SEARCH BOX IS CLIENT-SIDE AND CURRENT-PAGE ONLY. The list endpoint has no
 * `search` parameter and its SQL contains no text predicate on any column, so
 * there is nothing on the server to filter by. This box therefore matches
 * fullName and email across the rows of the loaded page and nothing else.
 *
 * That limitation is stated in three places on purpose — the input's hint text,
 * the persistent note beside the control, and the empty-state copy — because a
 * search field that silently misses a matching agent on page 3 is worse than no
 * field at all. It reports what it actually did.
 *
 * WHY NO ROLE FILTER. Every staff handler filters `role = Agent`, so the dataset
 * is single-valued. A role filter would offer a choice that cannot change the
 * result.
 *
 * WHY NO SORT CONTROL. The query hard-orders by `created_at DESC, id ASC`, so
 * no column can be made sortable and no header may look clickable.
 *
 * The input is a labelled search field with a real clear button rather than a
 * bare box, so it is reachable and resettable by keyboard alone.
 */
function AdminStaffFilterBar({
  search,
  onSearchChange,
  includeInactive,
  onIncludeInactiveChange,
  isFetching,
  matchCount,
  pageRowCount,
}) {
  const inputId = 'adminStaffSearch'
  const inactiveId = 'adminStaffIncludeInactive'
  const hasSearch = search.trim().length > 0

  return (
    <div className="mb-5 flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-3">
        <div className="min-w-0 flex-1 sm:max-w-xs">
          <label htmlFor={inputId} className="mb-1.5 block text-xs font-medium text-[#6B7280]">
            Filter this page
          </label>
          <div className="relative">
            <Search
              size={15}
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]"
              aria-hidden="true"
            />
            <input
              id={inputId}
              type="search"
              value={search}
              onChange={(event) => onSearchChange(event.target.value)}
              placeholder="Name or email on this page"
              aria-describedby={`${inputId}-hint`}
              className="block w-full rounded-[10px] border border-[#E2E4E9] bg-white py-2 pl-9 pr-9 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            />
            {hasSearch && (
              <button
                type="button"
                onClick={() => onSearchChange('')}
                aria-label="Clear the page filter"
                className="absolute inset-y-0 right-0 flex cursor-pointer items-center pr-3 text-[#9CA3AF] transition-colors hover:text-[#16181D] focus:outline-none focus:text-[#0F9D74]"
              >
                <X size={15} aria-hidden="true" />
              </button>
            )}
          </div>
        </div>

        <div className="flex items-center gap-2.5 pt-5 sm:pt-0">
          <input
            id={inactiveId}
            type="checkbox"
            checked={includeInactive}
            onChange={(event) => onIncludeInactiveChange(event.target.checked)}
            className="h-4 w-4 shrink-0 cursor-pointer rounded-[4px] border-[#E2E4E9] text-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          />
          <label
            htmlFor={inactiveId}
            className="cursor-pointer text-sm text-[#16181D] select-none"
          >
            Include inactive
          </label>
        </div>

        {isFetching && (
          <p className="text-xs text-[#6B7280] sm:pt-6" role="status">
            Refreshing…
          </p>
        )}
      </div>

      {/*
        The live match count is the one place the filter reports its own reach.
        It is only rendered once the request has succeeded, because the numbers it
        would otherwise print are 0-while-loading and 0-on-error — and a filter
        that claims "0 of 0" after a failure is reporting a measurement that was
        never taken.
      */}
      {hasSearch && !isFetching && (
        <p id={`${inputId}-hint`} className="text-xs text-[#6B7280]">
          Showing {matchCount} of {pageRowCount} loaded on this page.
        </p>
      )}
      {!hasSearch && (
        <p id={`${inputId}-hint`} className="text-xs text-[#6B7280]">
          The staff list has no server-side search, so this box filters the current
          page only. Agents on other pages are not searched.
        </p>
      )}
    </div>
  )
}

export default AdminStaffFilterBar
