import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, Users } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import Pagination from '../../client/billing/Pagination'
import AdminClientSection, { AdminClientRecordCard, AdminClientRecordList } from './AdminClientSection'
import { useAdminEmployees } from '../../../hooks/admin/useAdminEmployees'
import {
  MISSING_VALUE,
  displayText,
  formatDate,
  presentText,
} from './clientDisplay'

const PAGE_SIZE = 10

/**
 * Employees attached to the selected company, embedded in the drawer.
 * Backing query GET /api/v1/admin/employees?clientId&page&pageSize
 *
 * `clientId` IS A REAL SERVER-SIDE FILTER here, which is what makes embedding
 * honest rather than decorative — the repository applies it to both the count and
 * the data query. The alternative, paging the global employee list and filtering
 * it in the browser, would show a partial list with a count belonging to
 * everyone, so it is not used.
 *
 * entityName comes from the backend, so an employee's owning entity is shown by
 * name without any extra lookup. There is no employee name map in this module:
 * every name here is already inline in the DTO.
 *
 * There is no employee search. The endpoint accepts no text search parameter, so
 * offering one would be a control that silently does nothing.
 */
function AdminClientEmployeesSection({ clientId, companyName }) {
  const [page, setPage] = useState(1)

  const { items, totalCount, isLoading, isError, error, refresh } = useAdminEmployees({
    clientId,
    page,
    pageSize: PAGE_SIZE,
  })

  useEffect(() => {
    setPage(1)
  }, [clientId])

  return (
    <AdminClientSection
      title="Employees"
      description="Employees recorded against this company's legal entities."
      loading={isLoading}
      loadingLabel="Loading employees…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load the employees for this company."
      isEmpty={!isLoading && !isError && items.length === 0}
      emptyMessage="No employees on file."
      emptyDescription="Employees added against this company's entities will appear here."
      emptyIcon={Users}
    >
      <div className="flex flex-col gap-4">
        <AdminClientRecordList items={items}>
          {(employee) => (
            <AdminClientRecordCard
              key={employee?.id}
              title={presentText(employee?.fullName) ?? 'Unnamed employee'}
              subtitle={presentText(employee?.entityName) ?? undefined}
              trailing={
                <StatusPill
                  label={employee?.isActive ? 'Active' : 'Inactive'}
                  tone={employee?.isActive ? 'success' : 'neutral'}
                />
              }
              meta={[
                { label: 'Job title', value: displayText(employee?.jobTitle) },
                { label: 'Nationality', value: displayText(employee?.nationality) },
                { label: 'Hire date', value: formatDate(employee?.hireDate) },
                {
                  label: 'Passport',
                  value: presentText(employee?.passportNumber) ?? MISSING_VALUE,
                },
              ]}
            />
          )}
        </AdminClientRecordList>

        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="employee"
            onPageChange={setPage}
          />
        )}

        {/* The full Employees module is a separate destination. The deep link
            carries clientId because the Admin Employees page is a placeholder
            that does not yet read URL params, so the filter is expected but not
            yet applied there — the section above is the reliable view. */}
        <Link
          to={`/employees?clientId=${encodeURIComponent(clientId)}`}
          className="inline-flex items-center gap-1.5 self-start rounded-[8px] px-2 py-1 text-xs font-semibold text-[#0F9D74] transition duration-150 hover:bg-[rgba(15,157,116,0.08)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
        >
          View all in Employees
          <ArrowRight size={13} strokeWidth={2} aria-hidden="true" />
          <span className="sr-only">
            {' '}
            for {companyName ?? 'this company'}
          </span>
        </Link>
      </div>
    </AdminClientSection>
  )
}

export default AdminClientEmployeesSection
