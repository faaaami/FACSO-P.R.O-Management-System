import { AlertCircle, ListChecks, RotateCcw } from 'lucide-react'
import {
  TASK_STATUS_KEYS,
  taskStatusLabel,
  taskStatusTone,
} from './taskDisplay'

/**
 * Six assigned-only status figures plus the authoritative unfiltered total.
 *
 * BACKING QUERY: GET /api/v1/admin/staff/workload, via the existing
 * useAdminTaskStatusBreakdown hook. One request, shared with the Admin Dashboard
 * under the ['admin','dashboard','task-status-breakdown'] cache key, so visiting
 * both pages does not refetch it.
 *
 * ------------------------------------------------------------------
 * WHY THESE SIX NUMBERS ARE LABELLED "ASSIGNED" AND NOT "TOTAL".
 * ------------------------------------------------------------------
 * The workload endpoint aggregates per ASSIGNED staff member: every row is a
 * user with the Agent role and a count of the tasks assigned to them. A task with
 * no assignee therefore appears in NO row and is invisible in the sum. Adding
 * these six figures together gives the number of ASSIGNED tasks, not the number
 * of tasks — which is a genuinely different and much less useful number, because
 * a large unassigned backlog is exactly what an administrator opens this page to
 * see.
 *
 * So the cards state the scope in words ("assigned"), and the unfiltered task
 * total from GET /admin/tasks is shown beside them as the only true total on the
 * page. The two are deliberately not presented as one number, and the note
 * beneath the grid spells out the gap.
 *
 * WHAT WAS CONSIDERED AND REJECTED: deriving true per-status totals by firing the
 * task list six times with pageSize=1 and a status filter. That would have
 * produced global figures, at the cost of six extra paginated requests on every
 * page load, six more cache entries to invalidate, and — because a status-filtered
 * totalCount is still a filtered count — a set of numbers that would silently go
 * stale the moment an assignment landed. It is also not needed: the one honest
 * global figure readers need is the total, and that is already in the list
 * response.
 *
 * A FAILED REQUEST NEVER READS AS ZERO. Loading, error and empty each have their
 * own presentation, and a card whose value is unknown shows an explicit dash
 * rather than a fabricated 0 — the rule DashboardKpiCard already exists to
 * enforce, applied here to six figures at once.
 */

/** Card border/dot accents per tone. Colour only; every card carries its label. */
const TONE_ACCENT = {
  success: { dot: 'bg-[#0F9D74]', border: 'hover:border-[#0F9D74]/40' },
  warning: { dot: 'bg-[#D97706]', border: 'hover:border-[#D97706]/35' },
  danger: { dot: 'bg-[#DC2626]', border: 'hover:border-[#DC2626]/35' },
  neutral: { dot: 'bg-[#9CA3AF]', border: '' },
}

function StatusTile({ status, value, unavailable, loading, onRetry }) {
  const label = taskStatusLabel(status) ?? String(status)
  const tone = taskStatusTone(status)
  const accent = TONE_ACCENT[tone] ?? TONE_ACCENT.neutral

  return (
    <div
      className={`rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 ${accent.border}`}
    >
      <div className="flex items-center gap-2">
        <span className={`h-2 w-2 shrink-0 rounded-full ${accent.dot}`} aria-hidden="true" />
        <p className="min-w-0 truncate text-xs font-semibold text-[#6B7280]" title={label}>
          {label}
        </p>
      </div>

      {loading ? (
        <div className="mt-2.5" aria-hidden="true">
          <div className="h-7 w-12 animate-pulse rounded-[6px] bg-[#F7F8FA]" />
        </div>
      ) : unavailable ? (
        <div className="mt-2">
          <p className="text-2xl font-bold leading-none tracking-tight text-[#9CA3AF]">
            &mdash;
          </p>
          <p className="mt-1.5 text-xs text-[#6B7280]">Unavailable</p>
        </div>
      ) : (
        <p className="mt-2.5 text-2xl font-bold leading-none tracking-tight tabular-nums text-[#16181D]">
          {value.toLocaleString('en-US')}
          <span className="sr-only"> assigned tasks</span>
        </p>
      )}

      {/* The scope word lives on every card, not only in the footnote, so a single
          screenshot or a single screen-reader stop cannot be misread as a global
          per-status total. */}
      <p className="mt-1.5 text-[11px] text-[#9CA3AF]">
        {loading ? 'Loading…' : unavailable ? 'Not loaded' : 'Assigned'}
      </p>

      {unavailable && onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-2 inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2 py-1 text-[11px] font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
        >
          <RotateCcw size={11} strokeWidth={2} aria-hidden="true" />
          Retry
          <span className="sr-only"> loading the assigned task counts</span>
        </button>
      )}
    </div>
  )
}

function TotalTile({ totalCount, available, isFetching, isError, onRetry }) {
  return (
    <div className="shrink-0 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] px-3.5 py-2">
      <p className="text-[11px] font-semibold uppercase tracking-wider text-[#6B7280]">
        All tasks
      </p>
      {available ? (
        <>
          <p className="mt-0.5 text-xl font-bold leading-none tracking-tight tabular-nums text-[#16181D]">
            {totalCount.toLocaleString('en-US')}
            <span className="sr-only"> renewal tasks in total, assigned and unassigned</span>
          </p>
          <p className="mt-1 text-[11px] text-[#9CA3AF]">
            {isFetching ? 'Updating…' : 'Unfiltered'}
          </p>
        </>
      ) : (
        <>
          <p className="mt-0.5 text-xl font-bold leading-none tracking-tight text-[#9CA3AF]">
            &mdash;
            <span className="sr-only"> total task count unavailable</span>
          </p>
          <p className="mt-1 text-[11px] text-[#9CA3AF]">
            {isError ? 'Not loaded' : 'Loading…'}
          </p>
        </>
      )}
      {isError && onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-1.5 inline-flex cursor-pointer items-center gap-1 rounded-[8px] border border-[#E2E4E9] bg-white px-2 py-0.5 text-[11px] font-semibold text-[#16181D] transition duration-150 hover:bg-white focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
        >
          <RotateCcw size={10} strokeWidth={2} aria-hidden="true" />
          Retry
          <span className="sr-only"> loading the total task count</span>
        </button>
      )}
    </div>
  )
}

function AdminTaskSummaryCards({
  byStatus,
  isLoading,
  isError,
  refresh,
  totalCount,
  tasksTotalAvailable,
  isTotalFetching = false,
  isTotalError = false,
  onRetryTotal,
}) {
  const unavailable = isError || byStatus == null

  return (
    <section aria-labelledby="admin-task-summary-heading" className="mb-5">
      <div className="mb-3 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <h2
            id="admin-task-summary-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Assigned tasks by status
          </h2>
          <p className="mt-0.5 text-xs text-[#6B7280]">
            Six figures from the per-agent workload endpoint. Unassigned tasks are
            outside this breakdown.
          </p>
        </div>

        {/* The one true global figure on the page, kept visually separate from the
            assigned-only grid above so the two are never added together by eye. It
            comes from its own UNFILTERED query, so it keeps saying "all" while a
            filter narrows the list below. */}
        <TotalTile
          totalCount={totalCount}
          available={tasksTotalAvailable}
          isFetching={isTotalFetching}
          isError={isTotalError}
          onRetry={onRetryTotal}
        />
      </div>

      <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-3 lg:grid-cols-6">
        {TASK_STATUS_KEYS.map((status) => (
          <StatusTile
            key={status}
            status={status}
            value={byStatus?.[status] ?? 0}
            unavailable={unavailable}
            loading={isLoading}
            onRetry={refresh}
          />
        ))}
      </div>

      {isError && (
        <p
          role="alert"
          className="mt-3 flex items-start gap-2 rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-2.5 text-xs text-[#B91C1C]"
        >
          <AlertCircle size={14} strokeWidth={2} aria-hidden="true" className="mt-px shrink-0" />
          <span>
            The per-agent workload request failed, so these six figures are unknown
            rather than zero. The task list below is unaffected.
          </span>
        </p>
      )}

      {!isError && !isLoading && (
        <p className="mt-3 flex items-start gap-2 text-xs text-[#6B7280]">
          <ListChecks size={14} strokeWidth={1.75} aria-hidden="true" className="mt-px shrink-0" />
          <span>
            The six tiles count only tasks that have an assignee, because the
            workload endpoint is keyed on assigned Agent. Unassigned tasks are
            counted only in &ldquo;All tasks&rdquo; on the right, and appear in the
            list below.
          </span>
        </p>
      )}
    </section>
  )
}

export default AdminTaskSummaryCards
