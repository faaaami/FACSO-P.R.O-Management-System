import { Briefcase, CalendarDays, Globe, IdCard, Users } from 'lucide-react'
import Drawer from '../documents/Drawer'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import InfoRow from '../company/InfoRow'
import { useClientEmployeeDetails } from '../../../hooks/client/useClientEmployeeDetails'
import { formatDate, formatDateTime } from '../billing/format'

function employeeInitials(fullName) {
  return (fullName || '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}

function employeeStatus(employee) {
  if (employee.isDeleted) {
    return <StatusPill label="Deleted" tone="danger" />
  }
  if (employee.isActive) {
    return <StatusPill label="Active" tone="success" />
  }
  return <StatusPill label="Inactive" tone="neutral" />
}

function EmployeeDetailDrawer({ employeeId, onClose }) {
  const { data: employee, isLoading, isError, refetch } = useClientEmployeeDetails(employeeId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading employee details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load employee details. It may have been removed."
        onRetry={() => refetch()}
      />
    )
  } else if (!employee) {
    content = (
      <EmptyState
        icon={Users}
        message="Employee is not available."
        description="The requested employee could not be retrieved."
      />
    )
  } else {
    content = (
      <div className="space-y-5">
        {/* Employee identity */}
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#1C1F26] text-xs font-semibold text-white">
              {employeeInitials(employee.fullName)}
            </div>
            <div className="min-w-0">
              <p className="text-sm font-semibold text-[#16181D]">{employee.fullName}</p>
              <p className="mt-0.5 text-xs text-[#6B7280]">{employee.entityName}</p>
            </div>
          </div>
          <div className="self-start">{employeeStatus(employee)}</div>
        </div>

        {/* Profile fields */}
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider mb-3">
            Profile
          </span>
          <div className="space-y-3">
            <InfoRow label="Full Name" value={employee.fullName} />
            <InfoRow label="Entity" value={employee.entityName} />
            <InfoRow
              label="Passport Number"
              value={employee.passportNumber}
              icon={IdCard}
              mono
            />
            <InfoRow label="Nationality" value={employee.nationality} icon={Globe} />
            <InfoRow label="Job Title" value={employee.jobTitle} icon={Briefcase} />
            <InfoRow label="Hire Date" value={formatDate(employee.hireDate)} icon={CalendarDays} />
            {employee.dateOfBirth && (
              <InfoRow
                label="Date of Birth"
                value={formatDate(employee.dateOfBirth)}
                icon={CalendarDays}
              />
            )}
          </div>
        </div>

        {/* Timestamps */}
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider mb-3">
            Record
          </span>
          <div className="space-y-3">
            <InfoRow label="Created" value={formatDateTime(employee.createdAt) ?? 'N/A'} />
            <InfoRow label="Last Updated" value={formatDateTime(employee.updatedAt) ?? 'N/A'} />
          </div>
        </div>
      </div>
    )
  }

  return (
    <Drawer
      title="Employee Details"
      icon={Users}
      subtitle={employee?.entityName}
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default EmployeeDetailDrawer