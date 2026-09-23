import { format } from 'date-fns'
import { Eye, FolderOpen, Users } from 'lucide-react'
import Drawer from './Drawer'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import { useClientEmployeeDocuments } from '../../../hooks/client/useClientEmployeeDocuments'
import { DOCUMENT_TYPES, DOCUMENT_STATUS, enumLabel } from '../enumLabels'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function documentStatusTone(status) {
  if (status === 'Active') return 'success'
  if (status === 'Overdue') return 'danger'
  return 'warning'
}

function EmployeeDocumentsDrawer({ employeeId, onClose, onSelectDocument }) {
  const { data, isLoading, isError, refetch } = useClientEmployeeDocuments(employeeId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading employee documents…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load this employee's documents."
        onRetry={() => refetch()}
      />
    )
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={FolderOpen}
        message="No documents on this employee's file."
        description="Registered documents for this employee will appear here."
      />
    )
  } else {
    content = (
      <>
        <ul className="divide-y divide-[#E2E4E9] border-t border-b border-[#E2E4E9]">
          {data.items.map((doc) => (
            <li
              key={doc.id}
              className="flex flex-col gap-2.5 py-3 sm:flex-row sm:items-center sm:justify-between first:pt-3 last:pb-3"
            >
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-semibold text-[#16181D]">
                    {enumLabel(DOCUMENT_TYPES, doc.type) ?? 'Official Document'}
                  </span>
                  {doc.documentNumber && (
                    <span className="rounded-[4px] bg-[#F7F8FA] px-1.5 py-0.5 text-[11px] font-mono font-medium text-[#6B7280] border border-[#E2E4E9]">
                      {doc.documentNumber}
                    </span>
                  )}
                </div>
                <p className="mt-0.5 text-xs text-[#6B7280]">
                  {doc.expiryDate ? `Expires on ${formatDate(doc.expiryDate)}` : 'No expiry date'}
                </p>
              </div>
              <div className="flex shrink-0 items-center gap-2 self-start sm:self-center">
                <StatusPill
                  label={enumLabel(DOCUMENT_STATUS, doc.status) ?? 'Pending'}
                  tone={documentStatusTone(doc.status)}
                />
                <button
                  type="button"
                  onClick={() => onSelectDocument(doc.id)}
                  className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
                >
                  <Eye size={13} strokeWidth={2} aria-hidden="true" />
                  View
                </button>
              </div>
            </li>
          ))}
        </ul>

        <p className="mt-3 text-right text-xs text-[#9CA3AF]">
          {data.totalCount} document{data.totalCount === 1 ? '' : 's'} on record
        </p>
      </>
    )
  }

  return (
    <Drawer
      title="Employee Documents"
      icon={Users}
      subtitle="Registered documents for this employee"
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default EmployeeDocumentsDrawer