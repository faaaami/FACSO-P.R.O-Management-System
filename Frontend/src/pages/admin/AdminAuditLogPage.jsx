import { useState } from 'react'
import { History, ShieldCheck } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import AdminAuditLogFilterBar from '../../components/admin/audit/AdminAuditLogFilterBar'
import AdminAuditLogList from '../../components/admin/audit/AdminAuditLogList'
import { toUtcDayEnd, toUtcDayStart, OTHER_ENTITY_TYPE } from '../../components/admin/audit/auditLogDisplay'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import { useAdminAuditLog } from '../../hooks/admin/useAdminAuditLog'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal audit log.
 *
 * BACKING QUERY
 *   GET /api/v1/audit-log?entityType&entityId&userId&from&to&page&pageSize
 *
 * `[Authorize(Roles = "Admin")]`, and it is the only verb on the controller, so
 * this page is entirely read-only: there is no mutation, no toast, no optimistic
 * state and no confirm dialog anywhere below.
 *
 * ONLY THE SEVEN VERIFIED PARAMETERS ARE EVER SENT. The repository filters
 * `entity_type = @EntityType` (exact, case-sensitive), `entity_id`, `user_id`,
 * `created_at >= @From` and `created_at <= @To`. There is no sort parameter —
 * the query is hard-ordered `created_at DESC` — and no action filter, no text
 * search, no export and no single-entry detail endpoint. No control is offered
 * for any of them, because a control that cannot change the request is worse
 * than an absent one.
 *
 * IN PARTICULAR THERE IS NO SEARCH BOX. Filtering the 20 rows currently on screen
 * and calling the result a search would silently miss every matching event on
 * pages 2 and beyond, in a log whose whole purpose is to be a complete record.
 *
 * WHY THE DATES ARE CONVERTED HERE RATHER THAN IN THE FILTER BAR. Committed
 * filters stay in the raw `YYYY-MM-DD` shape the date inputs understand, so the
 * filter bar can re-sync its draft from them without a value it cannot render.
 * The conversion to the wire format happens once, at the query boundary:
 * `toUtcDayStart` for the inclusive lower bound and `toUtcDayEnd` for the
 * inclusive UPPER bound, so that choosing From 28 Sep and To 28 Sep returns that
 * entire calendar day. Sending the raw date as `to` would bind to midnight and
 * silently discard the rest of the day.
 *
 * Blank filters are OMITTED from the query object rather than sent as empty
 * strings, so an untouched field is genuinely absent from the URL and the
 * repository's `string.IsNullOrEmpty` / `HasValue` guards take their "no filter"
 * branch.
 *
 * pageSize is fixed at 20, matching every other Admin list. The hook still
 * carries it in the query key because that is the existing hook's shape.
 *
 * WHY THE COUNT BADGE IS SUPPRESSED WHILE LOADING AND AFTER A FAILURE. Both
 * totalCount and items fall back to zero in those two states, so a badge built
 * on them would print a confident "0" in precisely the situations where the
 * number means least and the reader most needs to know it is unknown.
 *
 * The two empty states are deliberately different. "No audit events recorded
 * yet" describes a system that has produced nothing; "No audit events match
 * these filters" describes a populated system narrowed by the filters in front
 * of the reader. Collapsing them would let a mistyped GUID read as though the
 * audit log were empty.
 */
const PAGE_SIZE = 20

const EMPTY_FILTERS = {
  entityType: '',
  entityTypeOther: '',
  entityId: '',
  userId: '',
  from: '',
  to: '',
}

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

function AdminAuditLogPage() {
  const [filters, setFilters] = useState(EMPTY_FILTERS)
  const [page, setPage] = useState(1)

  // The filter bar commits the "Other…" choice as a sentinel PLUS the override
  // so the control can round-trip back into a populated draft. The sentinel is a
  // UI-only marker and MUST be resolved away before it reaches the request —
  // sending the literal "__other__" as entity_type would match no rows and look
  // like a bug rather than an error. The backend only ever sees the override.
  const wireEntityType =
    filters.entityType === OTHER_ENTITY_TYPE ? filters.entityTypeOther : filters.entityType

  const { items, totalCount, isLoading, isFetching, isError, error, refresh } =
    useAdminAuditLog({
      entityType: wireEntityType || undefined,
      entityId: filters.entityId || undefined,
      userId: filters.userId || undefined,
      from: toUtcDayStart(filters.from) ?? undefined,
      to: toUtcDayEnd(filters.to) ?? undefined,
      page,
      pageSize: PAGE_SIZE,
    })

  const hasActiveFilters = Object.values(filters).some(
    (value) => typeof value === 'string' && value.trim(),
  )
  const showCountBadge = !isLoading && !isError

  function handleApply(nextFilters) {
    setFilters(nextFilters)
    // A new filter set is a different result set, so the current page number is
    // meaningless against it and can point past its end.
    setPage(1)
  }

  function handleReset() {
    setFilters({ ...EMPTY_FILTERS })
    setPage(1)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading audit log…" />
  } else if (isError) {
    // A failed request is an error with a retry. It is never rendered as an
    // empty log and never as a count of zero, because both would read as real
    // measurements about the audit trail.
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load the audit log.')}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    content = hasActiveFilters ? (
      <EmptyState
        icon={History}
        message="No audit events match these filters."
        description="The audit log may well contain events — these filters just do not match any of them. Clear them to see the full record."
      />
    ) : (
      <EmptyState
        icon={History}
        message="No audit events recorded yet."
        description="Audit events appear here as activity is recorded across the platform."
      />
    )
  } else {
    content = <AdminAuditLogList items={items} />
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Audit Log"
        subtitle="System-wide record of recorded activity. Filters are exact-match; events are shown newest first."
        action={ADMIN_PORTAL_BADGE}
      />

      <SectionCard
        title="Audit Log"
        icon={History}
        subtitle="Recorded activity, newest first"
        badge={
          showCountBadge ? (
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
              {totalCount.toLocaleString('en-US')}
              <span className="sr-only"> audit events</span>
            </span>
          ) : null
        }
      >
        <AdminAuditLogFilterBar
          filters={filters}
          onApply={handleApply}
          onReset={handleReset}
          isFetching={isFetching}
        />

        {content}

        {showCountBadge && totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="audit event"
            itemLabelPlural="audit events"
            onPageChange={setPage}
          />
        )}
      </SectionCard>
    </div>
  )
}

export default AdminAuditLogPage
