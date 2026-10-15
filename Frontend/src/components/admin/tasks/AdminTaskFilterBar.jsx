import { RotateCw, X } from 'lucide-react'
import { TASK_STATUS_KEYS, taskStatusLabel } from './taskDisplay'

/**
 * Task filters — exactly the three the endpoint really binds.
 *
 * BACKING QUERY: GET /api/v1/admin/tasks
 * Real parameters: status (RenewalTaskStatus?), assignedStaffId (Guid?),
 * clientCompanyId (Guid?), page, pageSize. Nothing else.
 *
 * WHAT IS DELIBERATELY ABSENT, and why each is a backend gap rather than a UI
 * choice:
 *
 *   - SEARCH. The action binds no `search` parameter and the SQL has no text
 *     predicate on any column, so a search box here could not filter anything.
 *   - DATE FILTERS. createdAt, blockedSince and completedAt are returned but no
 *     date parameter is bound, so no date control may be offered.
 *   - EMPLOYEE / LEGAL ENTITY. A renewal task has no employeeId and no
 *     entityId in any DTO — only clientCompanyId. Filtering by employee would
 *     require a field the contract does not carry.
 *   - DOCUMENT TYPE OR DOCUMENT. `documentId` is a bare Guid on the row and the
 *     detail's `document` block is not available in a list filter, so neither is
 *     filterable here. The type is displayed in the table, never filtered.
 *   - SORTING. The repository orders by `created_at DESC, id ASC` and exposes no
 *     sort parameter, so no column header may imply it is sortable.
 *   - INCLUDE DELETED / INCLUDE UNASSIGNED. The query hard-filters
 *     `is_deleted = false` and binds no such flag. Unassigned tasks are included
 *     by default and are NOT a filter — they are simply the tasks with a null
 *     assignedStaffId.
 *
 * THE STAFF FILTER AND THE ASSIGNEE PICKER USE DIFFERENT SOURCES ON PURPOSE.
 * This filter is fed by useAdminEntityMaps, which requests
 * `includeInactive: true` deliberately: a task assigned to a deactivated Agent
 * must stay findable, and the Admin staff list hides inactive rows by default.
 * The assignee picker in AdminTaskAssignPanel uses `includeInactive: false`
 * instead, because the backend's assignment guard requires an ACTIVE Agent. The
 * two lists are therefore not the same set, and neither one is wrong.
 *
 * Both option lists come from the entity maps, which page through the Admin
 * client/staff lists once and cap at 1,000 rows. Past that cap an option is
 * absent from this dropdown; the row itself still renders with an explicit
 * unresolved short id rather than a raw GUID, so nothing becomes unreachable —
 * it simply cannot be selected as a filter value. The endpoint exposes no
 * text-search or id-lookup parameter that could enumerate past the cap.
 *
 * FILTER CHANGES RESET PAGINATION. Handled by the page, which drops back to
 * page 1 whenever any of the three values changes, because page 4 of one result
 * set is rarely page 1 of the next and can be past its end.
 *
 * INITIAL LOAD vs REFETCH are different states and are shown differently:
 * `isFetching` is true again on every background refetch (a filter change, a
 * window focus, an invalidation after an assignment) while `isLoading` is true
 * only for the first load. Only the latter warrants a full-page spinner.
 */

const selectClass =
  'w-full cursor-pointer rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-medium text-[#16181D] transition duration-150 hover:border-[#0F9D74]/25 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]'

function AdminTaskFilterBar({
  status,
  assignedStaffId,
  clientCompanyId,
  staffOptions,
  companyOptions,
  onStatusChange,
  onAssignedStaffChange,
  onClientCompanyChange,
  onClearFilters,
  isFetching = false,
}) {
  const hasActiveFilters = Boolean(status || assignedStaffId || clientCompanyId)

  return (
    <div className="mb-4 flex flex-col gap-3.5 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <div className="min-w-0">
          <label
            htmlFor="admin-task-filter-status"
            className="mb-1.5 block text-xs font-semibold text-[#6B7280]"
          >
            Status
          </label>
          <select
            id="admin-task-filter-status"
            value={status ?? ''}
            onChange={(event) => onStatusChange(event.target.value || null)}
            className={selectClass}
          >
            <option value="">All statuses</option>
            {TASK_STATUS_KEYS.map((key) => (
              <option key={key} value={key}>
                {taskStatusLabel(key)}
              </option>
            ))}
          </select>
        </div>

        <div className="min-w-0">
          <label
            htmlFor="admin-task-filter-staff"
            className="mb-1.5 block text-xs font-semibold text-[#6B7280]"
          >
            Assigned Agent
          </label>
          <select
            id="admin-task-filter-staff"
            value={assignedStaffId ?? ''}
            onChange={(event) => onAssignedStaffChange(event.target.value || null)}
            className={selectClass}
          >
            <option value="">Any assignee</option>
            {/*
              An explicit "Unassigned" option is NOT invented as a query value.
              The endpoint has no such parameter. Unassigned tasks are reachable
              by leaving this on "Any assignee" and reading the table, where an
              unassigned row says so explicitly.
            */}
            {staffOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="min-w-0">
          <label
            htmlFor="admin-task-filter-company"
            className="mb-1.5 block text-xs font-semibold text-[#6B7280]"
          >
            Client company
          </label>
          <select
            id="admin-task-filter-company"
            value={clientCompanyId ?? ''}
            onChange={(event) => onClientCompanyChange(event.target.value || null)}
            className={selectClass}
          >
            <option value="">Any company</option>
            {companyOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex flex-col gap-2.5 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 flex-wrap items-center gap-2.5">
          {hasActiveFilters && (
            <button
              type="button"
              onClick={onClearFilters}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-white focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              <X size={13} strokeWidth={2} aria-hidden="true" />
              Clear filters
            </button>
          )}

          {/* Refresh indication, kept separate from the initial-loading state so a
              background refetch never blanks the table it is updating. */}
          {isFetching && (
            <p
              role="status"
              className="flex items-center gap-1.5 text-xs font-medium text-[#6B7280]"
            >
              <RotateCw
                size={13}
                strokeWidth={1.75}
                className="animate-spin"
                aria-hidden="true"
              />
              Refreshing&hellip;
            </p>
          )}
        </div>

        <p className="min-w-0 text-xs text-[#6B7280] sm:text-right">
          No text search, date, employee or sorting control is offered: the task
          list endpoint binds none of them.
        </p>
      </div>
    </div>
  )
}

export default AdminTaskFilterBar
