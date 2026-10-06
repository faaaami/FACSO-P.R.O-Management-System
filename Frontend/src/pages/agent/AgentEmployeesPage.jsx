import { useState } from 'react'
import { Briefcase, Building2, Calendar, Users } from 'lucide-react'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import StatusPill from '../../components/client/StatusPill'
import Pagination from '../../components/client/billing/Pagination'
import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import AgentEmployeeDetailDrawer from '../../components/agent/employees/AgentEmployeeDetailDrawer'
import AgentEmployeeDocumentsDrawer from '../../components/agent/employees/AgentEmployeeDocumentsDrawer'
import { formatDate } from '../../components/agent/documents/documentDisplay'
import { useAgentEmployees } from '../../hooks/agent/useAgentEmployees'
import { extractApiErrorMessage } from '../../utils/apiError'

const PAGE_SIZE = 20

const PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" aria-hidden="true" />
    Agent Portal
  </span>
)

function AgentEmployeesHeader() {
  const today = new Date().toLocaleDateString('en-GB', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
    year: 'numeric',
  })

  return (
    <AgentPageHeader
      title="Employees"
      badge={PORTAL_BADGE}
      description="Review registered personnel across the companies in your portfolio."
      meta={<p className="text-xs text-[#9CA3AF]">{today}</p>}
    />
  )
}

function EmployeesDirectoryCard() {
  const [page, setPage] = useState(1)
  const [selectedEmployeeId, setSelectedEmployeeId] = useState(null)
  const [documentsEmployee, setDocumentsEmployee] = useState(null)
  const { items, totalCount, isLoading, isError, error, refresh } = useAgentEmployees({
    page,
    pageSize: PAGE_SIZE,
  })

  let content
  if (isLoading) {
    content = <LoadingState label="Loading employees…" />
  } else if (isError) {
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load employees.')}
        onRetry={refresh}
      />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={Users}
        message="No employees yet."
        description="Personnel registered under the companies in your portfolio will appear here."
      />
    )
  } else {
    content = (
      <ul className="divide-y divide-[#E2E4E9]">
        {items.map((employee) => (
          <li
            key={employee.id}
            className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between"
          >
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <Users size={15} className="shrink-0 text-[#6B7280]" aria-hidden="true" />
                <p className="truncate text-sm font-semibold text-[#16181D]">
                  {employee.fullName}
                </p>
              </div>
              <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-[#6B7280]">
                {employee.entityName && (
                  <span className="inline-flex max-w-[220px] items-center gap-1 truncate">
                    <Building2 size={12} aria-hidden="true" />
                    {employee.entityName}
                  </span>
                )}
                {employee.jobTitle && (
                  <span className="inline-flex items-center gap-1">
                    <Briefcase size={12} aria-hidden="true" />
                    {employee.jobTitle}
                  </span>
                )}
                {employee.hireDate && (
                  <span className="inline-flex items-center gap-1">
                    <Calendar size={12} aria-hidden="true" />
                    Since {formatDate(employee.hireDate)}
                  </span>
                )}
              </div>
            </div>
            <div className="flex shrink-0 flex-wrap items-center gap-2 self-start sm:self-center">
              <StatusPill
                label={employee.isActive ? 'Active' : 'Inactive'}
                tone={employee.isActive ? 'success' : 'neutral'}
              />
              <button
                type="button"
                onClick={() => setSelectedEmployeeId(employee.id)}
                className="rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
              >
                Details
                <span className="sr-only"> for {employee.fullName}</span>
              </button>
              <button
                type="button"
                onClick={() =>
                  setDocumentsEmployee({ id: employee.id, name: employee.fullName })
                }
                className="rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
              >
                Documents
                <span className="sr-only"> for {employee.fullName}</span>
              </button>
            </div>
          </li>
        ))}
      </ul>
    )
  }

  return (
    <>
      <SectionCard
        title="Employee Directory"
        icon={Users}
        subtitle="Personnel registered under companies assigned to you"
        badge={
          <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280]">
            {totalCount}
          </span>
        }
      >
        {content}
        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="employee"
            onPageChange={setPage}
          />
        )}
      </SectionCard>

      {selectedEmployeeId && (
        <AgentEmployeeDetailDrawer
          employeeId={selectedEmployeeId}
          onClose={() => setSelectedEmployeeId(null)}
          onViewDocuments={(id) => {
            setSelectedEmployeeId(null)
            setDocumentsEmployee({ id, name: null })
          }}
        />
      )}

      {documentsEmployee && (
        <AgentEmployeeDocumentsDrawer
          employeeId={documentsEmployee.id}
          employeeName={documentsEmployee.name}
          onClose={() => setDocumentsEmployee(null)}
        />
      )}
    </>
  )
}

function AgentEmployeesPage() {
  return (
    <div className="space-y-6">
      <AgentEmployeesHeader />
      <EmployeesDirectoryCard />
    </div>
  )
}

export default AgentEmployeesPage
