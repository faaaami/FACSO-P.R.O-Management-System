import { useState } from 'react'
import { FileClock } from 'lucide-react'
import SectionCard from '../../client/SectionCard'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import Pagination from '../../client/billing/Pagination'
import AdminEmployeeExpiringTable from './AdminEmployeeExpiringTable'
import { useAdminExpiringEmployees } from '../../../hooks/admin/useAdminEmployees'
import { extractApiErrorMessage } from '../../../utils/apiError'

const PAGE_SIZE = 20
const WINDOW_DAYS = 30

/**
 * Employee documents approaching expiry, across all companies and entities.
 *
 * BACKING QUERY: GET /api/v1/admin/employees/expiring
 * Query: days, page, pageSize, includeExpired=false
 *
 * A real paginated list — this route returns page, pageSize and totalCount, so
 * the pager below is backed by genuine metadata. That is unlike the per-employee
 * documents route, which is unpaginated and must never get a pager.
 *
 * WHY IT IS HERE AND NOT IN THE EMPLOYEE DRAWER. The rows are per DOCUMENT and
 * span employees, so a drawer scoped to one employee would hide every other
 * employee's expiring document behind opening each one in turn. The cross-employee
 * question needs a cross-employee list.
 *
 * WHY NOT THE DASHBOARD'S expiry-alerts/employees. That route returns the
 * document alert DTO, which has no employee identity, so a row there cannot say
 * whose document it is. This one carries fullName and employeeId on every row.
 *
 * includeExpired is left false, so the list is forward-looking: a document is
 * reported while it is still in hand, not after it has lapsed. A genuine
 * "overdue" view is a different question and is not silently folded in.
 *
 * The window is fixed at 30 days rather than exposed as a control. It is a
 * read-only oversight list, and a free day-count input would add a filter whose
 * only value is to re-query a count the operator can already reason about from
 * the expiry dates printed in the rows.
 *
 * THE COUNT IS SUPPRESSED WHLOADING AND ON ERROR, matching the employee list
 * above it: totalCount falls back to 0 in both cases, so a badge built on it
 * would report a confident zero for a request that never succeeded.
 */
function AdminEmployeeExpiringSection() {
  const [page, setPage] = useState(1)

  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminExpiringEmployees({ days: WINDOW_DAYS, page, pageSize: PAGE_SIZE })

  const showCountBadge = !isLoading && !isError

  function renderContent() {
    if (isLoading) return <LoadingState label="Loading expiring documents…" />

    if (isError) {
      return (
        <ErrorState
          message={extractApiErrorMessage(
            error,
            'Could not load expiring employee documents.',
          )}
          onRetry={() => refresh()}
        />
      )
    }

    if (items.length === 0) {
      return (
        <EmptyState
          message="No employee documents expiring soon."
          description={`Nothing on an employee's documents expires within the next ${WINDOW_DAYS} days. Documents already past their expiry date are not included here.`}
          icon={FileClock}
        />
      )
    }

    return <AdminEmployeeExpiringTable items={items} />
  }

  return (
    <SectionCard
      title="Expiring employee documents"
      icon={FileClock}
      subtitle={`Employee-held documents expiring within ${WINDOW_DAYS} days. One row per document, so an employee with two documents appears twice.`}
      badge={
        showCountBadge ? (
          <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
            {totalCount.toLocaleString('en-US')}
            <span className="sr-only"> documents</span>
          </span>
        ) : null
      }
    >
      {isFetching && !isLoading && (
        <p className="mb-3 text-xs text-[#6B7280]" role="status">
          Refreshing expiring documents…
        </p>
      )}

      {renderContent()}

      {showCountBadge && totalCount > PAGE_SIZE && (
        <Pagination
          page={page}
          pageSize={PAGE_SIZE}
          totalCount={totalCount}
          itemLabel="document"
          itemLabelPlural="documents"
          onPageChange={setPage}
        />
      )}
    </SectionCard>
  )
}

export default AdminEmployeeExpiringSection
