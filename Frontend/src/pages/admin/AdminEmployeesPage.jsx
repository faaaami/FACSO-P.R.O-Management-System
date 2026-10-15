import { useEffect, useState } from 'react'
import { ShieldCheck, Users } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import AdminEmployeeFilterBar from '../../components/admin/employees/AdminEmployeeFilterBar'
import AdminEmployeesTable from '../../components/admin/employees/AdminEmployeesTable'
import AdminEmployeeDetailDrawer from '../../components/admin/employees/AdminEmployeeDetailDrawer'
import AdminEmployeeExpiringSection from '../../components/admin/employees/AdminEmployeeExpiringSection'
import { useAdminEmployees } from '../../hooks/admin/useAdminEmployees'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal employee directory — Phase 2, strictly read-only.
 *
 * BACKING QUERY: GET /api/v1/admin/employees?isActive&page&pageSize
 *
 * Scope notes that shape this page, each one checked against the controller
 * action rather than assumed:
 *
 *  - No create, edit, activate, deactivate or delete control. The backend does
 *    expose all of those routes, but Phase 2 is read-only so not even a disabled
 *    placeholder is added for visual completeness. DELETE in particular is
 *    excluded on purpose: it is a soft delete with no restore endpoint, the list
 *    hard-filters deleted rows, and the documents route 404s on a deleted parent,
 *    so a delete button here would be a one-way action with no way back.
 *
 *  - No search box. The endpoint binds clientId, entityId, isActive, page and
 *    pageSize — and no `search` parameter. The SQL contains no text predicate on
 *    any column, so there is nothing to search. This is the one place the
 *    directory is materially weaker than the Admin Clients list, and it is a
 *    backend gap rather than a UI omission. Offering a search field that silently
 *    matched nothing would be worse than admitting its absence, which is why the
 *    filter bar states it in as many words.
 *
 *  - No company or legal-entity filter. clientId and entityId ARE real
 *    server-side filters with filter-correct counts, but scoping this module by
 *    company is out of scope for this phase.
 *
 *  - No sort control. The query hardcodes ORDER BY e.created_at DESC, e.id ASC,
 *    so the newest record is first and no column can be made sortable. The card
 *    subtitle says "newest first" so the order is stated rather than implied.
 *
 *  - No "include deleted" toggle. The list hard-filters e.is_deleted = false and
 *    binds no includeDeleted; only the detail route accepts it.
 *
 *  - `?clientId=` IS DELIBERATELY NOT READ. The Admin Clients drawer links to
 *    /employees?clientId=<id>, and this page does not consume that parameter, so
 *    following that link lands on the unfiltered global directory. Consuming it
 *    is a later decision, and the Clients Phase 2 files are not touched here to
 *    accommodate it either way.
 *
 *  - `itemLabelPlural="employees"` avoids "employeeys" from the shared pager.
 */
const PAGE_SIZE = 20

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

/** Maps the filter bar's tri-state onto the endpoint's nullable isActive. */
function toIsActiveParam(status) {
  if (status === 'true') return true
  if (status === 'false') return false
  return undefined
}

function AdminEmployeesDirectory() {
  const [status, setStatus] = useState('all')
  const [page, setPage] = useState(1)
  const [selectedEmployeeId, setSelectedEmployeeId] = useState(null)

  const isActive = toIsActiveParam(status)

  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminEmployees({ isActive, page, pageSize: PAGE_SIZE })

  // A new filter means the current page number is meaningless: page 4 of the
  // previous result set is rarely page 1 of the new one, and can be past its end.
  useEffect(() => {
    setPage(1)
  }, [status])

  function handleStatusChange(nextStatus) {
    setStatus(nextStatus)
  }

  function handlePageChange(nextPage) {
    setPage(nextPage)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading employees…" />
  } else if (isError) {
    // A failed list is an error with a retry. It is never an empty list and never
    // a total of zero, because either would read as a real measurement.
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load employees.')}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    const filtered = status !== 'all'
    content = (
      <EmptyState
        icon={Users}
        message={
          filtered ? 'No employees with this status.' : 'No employees yet.'
        }
        description={
          filtered
            ? 'No employee on file is currently active or inactive as selected. Try “All”.'
            : 'Employees will appear here once they are recorded against a client legal entity.'
        }
      />
    )
  } else {
    content = (
      <AdminEmployeesTable
        items={items}
        onOpenDetails={setSelectedEmployeeId}
      />
    )
  }

  // The count is only trustworthy once the request succeeded. While loading it
  // would be stale or zero, and on error it would be a lie, so the badge is
  // suppressed in both cases rather than rendered as "Unavailable" beside a zero.
  const showCountBadge = !isLoading && !isError

  return (
    <>
      <SectionCard
        title="Employees"
        icon={Users}
        subtitle="Every employee across all client companies and legal entities, newest records first."
        badge={
          showCountBadge ? (
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
              {totalCount.toLocaleString('en-US')}
              <span className="sr-only"> employees</span>
            </span>
          ) : null
        }
      >
        <AdminEmployeeFilterBar
          status={status}
          onStatusChange={handleStatusChange}
          isFetching={isFetching && !isLoading}
        />

        {content}

        {showCountBadge && totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="employee"
            itemLabelPlural="employees"
            onPageChange={handlePageChange}
          />
        )}
      </SectionCard>

      {selectedEmployeeId && (
        <AdminEmployeeDetailDrawer
          employeeId={selectedEmployeeId}
          onClose={() => setSelectedEmployeeId(null)}
        />
      )}
    </>
  )
}

function AdminEmployeesPage() {
  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Employees"
        subtitle="Staff recorded against client legal entities, with their documents and change history. Admin is not restricted to assigned companies."
        action={ADMIN_PORTAL_BADGE}
      />
      <AdminEmployeesDirectory />

      {/*
        A second card on the same page, not a tab. The directory above and this
        list answer different questions — "who is on file" versus "what lapses
        soon" — and either should be usable without a mode switch. Each runs its
        own query, so a failure in one leaves the other readable.
      */}
      <AdminEmployeeExpiringSection />
    </div>
  )
}

export default AdminEmployeesPage
