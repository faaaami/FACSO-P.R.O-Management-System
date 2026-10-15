import { useEffect, useMemo, useState } from 'react'
import { ListChecks, RefreshCw, ShieldCheck } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import AdminTaskFilterBar from '../../components/admin/tasks/AdminTaskFilterBar'
import AdminTaskSummaryCards from '../../components/admin/tasks/AdminTaskSummaryCards'
import AdminTasksTable from '../../components/admin/tasks/AdminTasksTable'
import AdminTaskDetailDrawer from '../../components/admin/tasks/AdminTaskDetailDrawer'
import { useAdminTasks } from '../../hooks/admin/useAdminTasks'
import { useAdminTaskStatusBreakdown } from '../../hooks/admin/useAdminDashboard'
import { useAdminEntityMaps } from '../../hooks/admin/useAdminEntityMaps'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal — Renewal Tasks. PHASE 2: READ-ONLY PLUS ASSIGNMENT.
 *
 * BACKING QUERIES
 *   GET /api/v1/admin/tasks?status&assignedStaffId&clientCompanyId&page&pageSize
 *   GET /api/v1/admin/staff/workload   (via useAdminTaskStatusBreakdown, shared
 *                                      with the Dashboard under one cache key)
 *   GET /api/v1/admin/clients, /api/v1/admin/staff (via useAdminEntityMaps, for
 *                                      resolving the GUID-only company/assignee
 *                                      columns and building the filter options)
 *
 * WHAT THIS PAGE CAN DO: list, filter, page, inspect, and assign a task to an
 * Agent. That is the whole surface.
 *
 * WHAT THIS PAGE DELIBERATELY CANNOT DO, and why each absence is a decision
 * rather than an unfinished feature:
 *
 *   - NO STATUS CHANGE. An Admin may skip forward but never backward, and once a
 *     task is Blocked every further status call is rejected because Blocked is
 *     absent from the handler's chain. A status control would therefore offer
 *     moves that fail, and would need a client-side state machine to avoid
 *     offering them — which is precisely the duplication this module refuses.
 *     Returning a Blocked task to the chain is what UNBLOCK below is for.
 *   - NO BLOCK. Blocking is the Agent's action on their own task
 *     (PATCH /agent/tasks/{id}/block), so an Admin control here would duplicate a
 *     control the Agent module already owns and the Admin is not responsible for.
 *     Its counterpart IS offered: an Admin-only UNBLOCK, in the task drawer, for
 *     the decision that reversing a block is the Admin's own.
 *   - NO CANCEL. Cancel is a one-way soft delete and the list hard-filters
 *     deleted rows, so a cancelled task would vanish with no way back. Unblock is
 *     not a cancel: it clears the blocked markers and restores the task to the
 *     chain, leaving the task and its history intact.
 *   - NO ADD / EDIT / DELETE STEP. Steps are append-only; only POST exists.
 *   - NO INVOICE TAB. A service-fee invoice is created on reaching the `Updated`
 *     state, which requires ServiceFeeAmount > 0 — a value no Admin endpoint
 *     sets. The workflow is unreachable, so it is not offered. (The fee itself is
 *     set from the drawer, via AdminTaskServiceFeePanel, which does make this
 *     reachable; the invoice tab remains out of scope for this page.)
 *
 * THE TWO NUMBERS ON THIS PAGE MEAN DIFFERENT THINGS, and they are kept apart on
 * purpose. The six status tiles are ASSIGNED-ONLY, because
 * GET /admin/staff/workload aggregates by assigned Agent and an unassigned task
 * appears in no row. "All tasks" is an UNFILTERED `totalCount`, read from a
 * separate unfiltered list query so that it keeps meaning "all" while a filter is
 * active — the only true global figure available. Adding the tiles together and
 * calling that the task total would understate a backlog by exactly the number of
 * unassigned tasks, which is the number an administrator most wants to see. The
 * table badge above the list is a third number and IS filter-aware, so it reads
 * "N matching" whenever a filter is on.
 *
 * COUNTS ARE NEVER FABRICATED. Every figure is withheld while loading or after a
 * failure, because `totalCount` falls back to 0 in those states and a badge built
 * on it would be a confident wrong number at the two moments that matter most.
 *
 * FILTER CHANGES RESET TO PAGE 1, because page 4 of one result set is rarely
 * page 1 of the next and can be past its end.
 */
const PAGE_SIZE = 20

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

function AdminTaskList() {
  const [status, setStatus] = useState(null)
  const [assignedStaffId, setAssignedStaffId] = useState(null)
  const [clientCompanyId, setClientCompanyId] = useState(null)
  const [page, setPage] = useState(1)
  const [selectedTaskId, setSelectedTaskId] = useState(null)

  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminTasks({ status, assignedStaffId, clientCompanyId, page, pageSize: PAGE_SIZE })

  // THE UNFILTERED TOTAL, as a separate query.
  //
  // "All tasks" must mean every task in the system, so it cannot be read off the
  // list query above: the moment a filter is active that `totalCount` is a
  // filtered count, and printing it under the label "All tasks" would state a
  // falsehood in the one place on the page that claims to be the true total.
  //
  // The nulls are deliberate. The list query's key carries `status: null` etc.
  // when nothing is filtered, and this call uses the same nulls at page 1, so
  // TanStack collapses the two into ONE cache entry and ONE request in the
  // unfiltered case. Only an active filter (or a page number above 1) makes it a
  // genuinely separate entry — which is exactly when a separate unfiltered
  // number is required. The extra round trip is therefore paid only when the
  // distinction is actually visible to the reader.
  const {
    totalCount: allTasksTotal,
    isLoading: isAllTasksLoading,
    isFetching: isAllTasksFetching,
    isError: isAllTasksError,
    refresh: refreshAllTasks,
  } = useAdminTasks({
    status: null,
    assignedStaffId: null,
    clientCompanyId: null,
    page: 1,
    pageSize: PAGE_SIZE,
  })

  // One request, already cached for the Dashboard under
  // ['admin','dashboard','task-status-breakdown'].
  const {
    byStatus,
    isLoading: isBreakdownLoading,
    isError: isBreakdownError,
    refresh: refreshBreakdown,
  } = useAdminTaskStatusBreakdown()

  // Company and Agent names for the GUID-only columns, and the option lists for
  // the two entity filters. Already fetched, cached and capped by that hook.
  const { resolveClient, resolveStaff, clientNames, staffNames } = useAdminEntityMaps()

  // A new filter makes the current page number meaningless.
  useEffect(() => {
    setPage(1)
  }, [status, assignedStaffId, clientCompanyId])

  const companyOptions = useMemo(
    () =>
      [...clientNames.entries()]
        .map(([value, label]) => ({ value, label }))
        .sort((a, b) => a.label.localeCompare(b.label)),
    [clientNames],
  )

  const staffOptions = useMemo(
    () =>
      [...staffNames.entries()]
        .map(([value, label]) => ({ value, label }))
        .sort((a, b) => a.label.localeCompare(b.label)),
    [staffNames],
  )

  function handleClearFilters() {
    setStatus(null)
    setAssignedStaffId(null)
    setClientCompanyId(null)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading renewal tasks…" />
  } else if (isError) {
    // A failed list is an error with a retry. It is never an empty list and never
    // a total of zero, because either would read as a real measurement.
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load renewal tasks.')}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={ListChecks}
        message="No renewal tasks match these filters."
        description="Clear the filters to see every task. Unassigned tasks are included in the unfiltered list — the endpoint has no filter for them."
      />
    )
  } else {
    content = (
      <AdminTasksTable
        items={items}
        resolveClient={resolveClient}
        resolveStaff={resolveStaff}
        onOpenDetails={setSelectedTaskId}
      />
    )
  }

  // Withheld while loading and on error, for the reason in the header comment.
  const showCountBadge = !isLoading && !isError
  const hasActiveFilters = Boolean(status || assignedStaffId || clientCompanyId)

  return (
    <>
      <AdminTaskSummaryCards
        byStatus={byStatus}
        isLoading={isBreakdownLoading}
        isError={isBreakdownError}
        refresh={refreshBreakdown}
        totalCount={allTasksTotal}
        tasksTotalAvailable={!isAllTasksLoading && !isAllTasksError}
        isTotalFetching={isAllTasksFetching && !isAllTasksLoading}
        isTotalError={isAllTasksError}
        onRetryTotal={refreshAllTasks}
      />

      <SectionCard
        title="Renewal tasks"
        icon={ListChecks}
        subtitle="Every renewal task across all client companies, newest first. Read-only, with Agent assignment."
        badge={
          showCountBadge ? (
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
              {hasActiveFilters
                ? `${totalCount.toLocaleString('en-US')} matching`
                : totalCount.toLocaleString('en-US')}
              <span className="sr-only">
                {hasActiveFilters ? ' renewal tasks matching the filters' : ' renewal tasks'}
              </span>
            </span>
          ) : null
        }
        action={
          <button
            type="button"
            onClick={() => {
              refresh()
              refreshAllTasks()
            }}
            disabled={isFetching || isAllTasksFetching}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <RefreshCw
              size={13}
              strokeWidth={2}
              className={isFetching ? 'animate-spin' : ''}
              aria-hidden="true"
            />
            Refresh
            <span className="sr-only"> the renewal task list</span>
          </button>
        }
      >
        <AdminTaskFilterBar
          status={status}
          assignedStaffId={assignedStaffId}
          clientCompanyId={clientCompanyId}
          staffOptions={staffOptions}
          companyOptions={companyOptions}
          onStatusChange={setStatus}
          onAssignedStaffChange={setAssignedStaffId}
          onClientCompanyChange={setClientCompanyId}
          onClearFilters={handleClearFilters}
          isFetching={isFetching && !isLoading}
        />

        {content}

        {showCountBadge && totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="task"
            itemLabelPlural="tasks"
            onPageChange={setPage}
          />
        )}
      </SectionCard>

      {selectedTaskId && (
        <AdminTaskDetailDrawer
          taskId={selectedTaskId}
          onClose={() => setSelectedTaskId(null)}
        />
      )}
    </>
  )
}

function AdminTasksPage() {
  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Renewal Tasks"
        subtitle="Every renewal task across all client companies. Inspect status, steps and history, and assign a task to an Agent."
        action={ADMIN_PORTAL_BADGE}
      />
      <AdminTaskList />
    </div>
  )
}

export default AdminTasksPage
