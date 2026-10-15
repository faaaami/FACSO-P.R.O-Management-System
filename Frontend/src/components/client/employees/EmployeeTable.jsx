import { Users } from 'lucide-react'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import Pagination from '../billing/Pagination'
import { formatDate } from '../billing/format'

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

function Avatar({ fullName }) {
  return (
    <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[#1C1F26] text-[11px] font-semibold text-white">
      {employeeInitials(fullName)}
    </div>
  )
}

function RowActions({ onDetails, onDocuments }) {
  return (
    <div className="flex shrink-0 items-center justify-end gap-2">
      <button
        type="button"
        onClick={onDetails}
        className="inline-flex items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-1.5 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
      >
        Details
      </button>
      <button
        type="button"
        onClick={onDocuments}
        className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
      >
        View Documents
      </button>
    </div>
  )
}

function MobileCard({ employee, onDetails, onDocuments }) {
  return (
    <li className="flex flex-col gap-3 rounded-[10px] bg-white border border-[#E2E4E9] p-4 sm:hidden">
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2.5">
          <Avatar fullName={employee.fullName} />
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-[#16181D]">{employee.fullName}</p>
            <p className="mt-0.5 truncate text-xs text-[#6B7280]">{employee.entityName}</p>
          </div>
        </div>
        {employeeStatus(employee)}
      </div>

      <div className="space-y-1.5 text-xs">
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Job Title</span>
          <span className="font-medium text-[#16181D]">{employee.jobTitle ?? 'N/A'}</span>
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Nationality</span>
          <span className="font-medium text-[#16181D]">{employee.nationality ?? 'N/A'}</span>
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Hire Date</span>
          <span className="font-medium text-[#16181D]">{formatDate(employee.hireDate) ?? 'N/A'}</span>
        </div>
      </div>

      <div className="flex gap-2 self-end">
        <RowActions onDetails={onDetails} onDocuments={onDocuments} />
      </div>
    </li>
  )
}

function EmployeeTable({ data, isLoading, isError, onRetry, page, pageSize, onPageChange, onDetails, onDocuments }) {
  let content

  if (isLoading) {
    content = <LoadingState label="Loading employees…" />
  } else if (isError) {
    content = <ErrorState message="Could not load employees." onRetry={onRetry} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={Users}
        message="No employees found."
        description="Employees registered for your company will appear here."
      />
    )
  } else {
    content = (
      <>
        {/* Mobile card list */}
        <ul className="space-y-3 sm:hidden">
          {data.items.map((item) => (
            <MobileCard
              key={item.id}
              employee={item}
              onDetails={() => onDetails(item.id)}
              onDocuments={() => onDocuments(item.id)}
            />
          ))}
        </ul>

        {/* Desktop table */}
        <div className="hidden sm:block overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Employee
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Entity
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Job Title
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Nationality
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Hire Date
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Status
                </th>
                <th className="bg-gray-50 text-right text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E2E4E9]">
              {data.items.map((item) => (
                <tr key={item.id} className="hover:bg-gray-50/60 transition-colors">
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <div className="flex items-center gap-2.5">
                      <Avatar fullName={item.fullName} />
                      <span className="font-medium">{item.fullName}</span>
                    </div>
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">{item.entityName}</td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">{item.jobTitle ?? <span className="text-[#9CA3AF]">N/A</span>}</td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">{item.nationality ?? <span className="text-[#9CA3AF]">N/A</span>}</td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">{formatDate(item.hireDate) ?? <span className="text-[#9CA3AF]">N/A</span>}</td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">{employeeStatus(item)}</td>
                  <td className="px-4 py-3.5 text-right">
                    <RowActions
                      onDetails={() => onDetails(item.id)}
                      onDocuments={() => onDocuments(item.id)}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={data.totalCount}
          itemLabel="employee"
          onPageChange={onPageChange}
        />
      </>
    )
  }

  return (
    <section aria-label="Employee directory" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-start justify-between gap-4 mb-5">
        <div className="min-w-0 flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <Users size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">Employee Directory</h2>
            <p className="mt-0.5 text-xs text-[#6B7280]">Registered personnel and their compliance files</p>
          </div>
        </div>
        {data?.totalCount != null && (
          <span className="shrink-0 rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
            {data.totalCount}
          </span>
        )}
      </div>

      {content}
    </section>
  )
}

export default EmployeeTable