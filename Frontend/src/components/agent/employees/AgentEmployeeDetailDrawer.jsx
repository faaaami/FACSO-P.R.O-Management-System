import { Building2, FileText, Users } from 'lucide-react'
import Drawer from '../../client/documents/Drawer'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import StatusPill from '../../client/StatusPill'
import { useAgentEmployee } from '../../../hooks/agent/useAgentEmployee'
import { formatDate } from '../documents/documentDisplay'

function DetailRow({ label, value, mono }) {
  return (
    <div className="flex items-start justify-between gap-3 py-2">
      <span className="shrink-0 text-xs text-[#6B7280]">{label}</span>
      <span
        className={`min-w-0 break-words text-right text-xs font-medium text-[#16181D] ${mono ? 'font-mono' : ''}`}
      >
        {value ?? <span className="text-[#9CA3AF]">N/A</span>}
      </span>
    </div>
  )
}

/**
 * Read-only employee detail drawer for the Agent portal. When `onViewDocuments`
 * is provided, the caller can drill into that employee's documents.
 */
function AgentEmployeeDetailDrawer({ employeeId, onClose, onViewDocuments }) {
  const { data: employee, isLoading, isError, refresh } = useAgentEmployee(employeeId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading employee details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load this employee. They may have been removed or are no longer in your portfolio."
        onRetry={() => refresh()}
      />
    )
  } else if (!employee) {
    content = <p className="text-sm text-[#6B7280]">Employee is not available.</p>
  } else {
    content = (
      <div className="space-y-5">
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <StatusPill
            label={employee.isActive ? 'Active' : 'Inactive'}
            tone={employee.isActive ? 'success' : 'neutral'}
          />
          {onViewDocuments && (
            <button
              type="button"
              onClick={() => onViewDocuments(employee.id)}
              className="inline-flex w-fit items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
            >
              <FileText size={13} strokeWidth={1.75} aria-hidden="true" />
              View documents
            </button>
          )}
        </div>

        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#6B7280]">
              <Building2 size={15} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-[11px] font-medium uppercase tracking-wider text-[#6B7280]">
                Company
              </p>
              <p className="mt-0.5 truncate text-sm font-semibold text-[#16181D]">
                {employee.entityName ?? '—'}
              </p>
            </div>
          </div>
        </div>

        <div className="divide-y divide-[#E2E4E9]">
          <DetailRow label="Full name" value={employee.fullName} />
          <DetailRow label="Passport number" value={employee.passportNumber} mono />
          <DetailRow label="Nationality" value={employee.nationality} />
          <DetailRow label="Job title" value={employee.jobTitle} />
          <DetailRow label="Date of birth" value={formatDate(employee.dateOfBirth)} />
          <DetailRow label="Hire date" value={formatDate(employee.hireDate)} />
          <DetailRow label="Employee ref" value={String(employee.id).slice(0, 8)} mono />
        </div>
      </div>
    )
  }

  return (
    <Drawer
      title="Employee Details"
      icon={Users}
      subtitle={employee?.fullName ?? 'Employee'}
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default AgentEmployeeDetailDrawer
