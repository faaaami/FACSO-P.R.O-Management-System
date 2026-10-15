import { useState } from 'react'
import { Inbox, RefreshCw, ShieldCheck } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import AdminServiceRequestKpi from '../../components/admin/serviceRequests/AdminServiceRequestKpi'
import AdminServiceRequestTable from '../../components/admin/serviceRequests/AdminServiceRequestTable'
import AdminServiceRequestDetailDrawer from '../../components/admin/serviceRequests/AdminServiceRequestDetailDrawer'
import { useAdminServiceRequests } from '../../hooks/admin/useAdminServiceRequests'
import { useAdminServiceRequestSubjects } from '../../hooks/admin/useAdminServiceRequestSubjects'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal — Service Requests.
 *
 * BACKING QUERIES
 *   GET /api/v1/service-requests?page&pageSize   (the list; the only Admin request
 *                                                 query this page needs)
 *   GET /api/v1/admin/clients, /api/v1/admin/employees
 *                                                 (via useAdminServiceRequestSubjects,
 *                                                  which reuses the maps the other
 *                                                  Admin modules already hold in
 *                                                  cache, for the GUID-only company
 *                                                  and employee columns)
 *   GET /api/v1/service-requests/{id}
 *   GET /api/v1/audit-log?entityType=ServiceRequest&entityId=…
 *                                                 (drawer, Activity tab only)
 *   GET /api/v1/admin/clients/{clientId}/entities, GET /api/v1/admin/documents/{id}
 *                                                 (drawer, Subject tab only)
 *
 * BACKING MUTATIONS
 *   PATCH /api/v1/service-requests/{id}/convert  (convert to a renewal task)
 *   PATCH /api/v1/service-requests/{id}/reject   (reject, with a required reason)
 *
 * WHAT THIS PAGE CAN DO: list, page, inspect one request in full, and DECIDE it —
 * convert it to a renewal task or reject it with a reason. Both decisions are
 * exposed through AdminServiceRequestDetailDrawer, which renders
 * AdminServiceRequestDecisionDialog and drives useAdminServiceRequestMutations.
 * This header previously described the page as strictly read-only, which has not
 * been true since the decision dialog was wired in.
 *
 * THERE IS NO FILTER BAR, AND ITS ABSENCE IS THE POINT. The endpoint accepts only
 * `page` and `pageSize` — no status, type, company, date or text filter — so any
 * filter control built here would be a control that does not narrow anything. The
 * Renewal Tasks filter bar is not reusable for the same reason, and the two
 * endpoints are not interchangeable. Offering a filter that silently does nothing is
 * worse than offering none.
 *
 * WHY THERE IS NO STATUS BREAKDOWN, unlike the Dashboard's task tiles. The task
 * tiles come from GET /admin/staff/workload, which aggregates by status server-side.
 * No equivalent exists for requests: GET /api/v1/service-requests cannot be filtered
 * by status, DashboardSummaryDto has no request field at all, and there is no
 * aggregate endpoint. Per-status counts would have to come from paging every request
 * in the system and tallying client-side — an unbounded walk presented with a
 * server's authority. So the page shows one number, totalCount, which is real.
 *
 * COUNT IS NEVER FABRICATED. The single figure is withheld while loading and after a
 * failure, because `totalCount` falls back to 0 in both states and a card built on it
 * would be a confident wrong number at the two moments that matter most. The list
 * badge follows the same rule.
 *
 * THE SUBJECT RESOLVERS COME FROM ONE PAGE-LEVEL HOOK CALL, not one per row: a table
 * renders many rows and cannot call a hook inside a loop. This page calls the hook
 * with no record and detail lookups off, which yields only the resolvers that need
 * the shared company and employee maps — both already cached by the other Admin
 * modules, so no extra request is made. The Drawer calls the same hook again with
 * its own record and detail lookups on, and only while the Subject tab is open.
 */
const PAGE_SIZE = 20

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

function AdminServiceRequestList() {
  const [page, setPage] = useState(1)
  const [selectedRequestId, setSelectedRequestId] = useState(null)

  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminServiceRequests({ page, pageSize: PAGE_SIZE })

  // Company and employee names for the GUID-only columns. Called with no record, so
  // it resolves nothing on its own behalf and adds no request beyond the shared maps.
  const { resolveCompany, resolveEmployee } = useAdminServiceRequestSubjects()

  const showCountBadge = !isLoading && !isError

  let content
  if (isLoading) {
    content = <LoadingState label="Loading service requests…" />
  } else if (isError) {
    // A failed list is an error with a retry. It is never an empty list and never a
    // total of zero, because either would read as a real measurement.
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load service requests.')}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={Inbox}
        message="No service requests yet."
        description="Requests appear here as soon as a client company submits one. This list is unfiltered, so an empty result means none exist — not that they were hidden by a filter."
      />
    )
  } else {
    content = (
      <AdminServiceRequestTable
        items={items}
        resolveCompany={resolveCompany}
        resolveEmployee={resolveEmployee}
        onOpenDetails={setSelectedRequestId}
      />
    )
  }

  return (
    <>
      <AdminServiceRequestKpi
        totalCount={totalCount}
        isLoading={isLoading}
        isError={isError}
        isFetching={isFetching && !isLoading}
      />

      <SectionCard
        title="Service requests"
        icon={Inbox}
        subtitle="Every request submitted by a client company, newest first."
        badge={
          showCountBadge ? (
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
              {totalCount.toLocaleString('en-US')}
              <span className="sr-only"> service requests</span>
            </span>
          ) : null
        }
        action={
          <button
            type="button"
            onClick={() => refresh()}
            disabled={isFetching}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <RefreshCw
              size={13}
              strokeWidth={2}
              className={isFetching ? 'animate-spin' : ''}
              aria-hidden="true"
            />
            Refresh
            <span className="sr-only"> the service request list</span>
          </button>
        }
      >
        {content}

        {showCountBadge && totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="request"
            itemLabelPlural="requests"
            onPageChange={setPage}
          />
        )}
      </SectionCard>

      {selectedRequestId && (
        <AdminServiceRequestDetailDrawer
          requestId={selectedRequestId}
          onClose={() => setSelectedRequestId(null)}
        />
      )}
    </>
  )
}

function AdminServiceRequestsPage() {
  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Service Requests"
        subtitle="Every request submitted by a client company. Inspect the request, its subject and its audit trail."
        action={ADMIN_PORTAL_BADGE}
      />
      <AdminServiceRequestList />
    </div>
  )
}

export default AdminServiceRequestsPage
