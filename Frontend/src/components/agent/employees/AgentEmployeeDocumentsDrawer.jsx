import { Users } from 'lucide-react'
import Drawer from '../../client/documents/Drawer'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import StatusPill from '../../client/StatusPill'
import { useAgentEmployeeDocuments } from '../../../hooks/agent/useAgentEmployee'
import {
  documentTypeLabel,
  documentStatusLabel,
  documentStatusTone,
  formatDate,
} from '../documents/documentDisplay'

/**
 * Read-only list of an employee's documents. Selecting a document hands its id
 * back to the caller (which typically swaps to the document detail drawer).
 */
function AgentEmployeeDocumentsDrawer({ employeeId, employeeName, onClose, onSelectDocument }) {
  const { items, isLoading, isError, refresh } = useAgentEmployeeDocuments(employeeId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading employee documents…" />
  } else if (isError) {
    content = (
      <ErrorState message="Could not load this employee's documents." onRetry={() => refresh()} />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={Users}
        message="No documents on file."
        description="Documents registered for this employee will appear here."
      />
    )
  } else {
    content = (
      <ul className="divide-y divide-[#E2E4E9]">
        {items.map((document) => (
          <li key={document.id} className="flex items-start justify-between gap-3 py-3">
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold text-[#16181D]">
                {documentTypeLabel(document.type)}
              </p>
              <p className="mt-0.5 truncate font-mono text-xs text-[#6B7280]">
                {document.documentNumber || '—'}
              </p>
              <p className="mt-0.5 text-xs text-[#6B7280]">
                {document.expiryDate ? `Expires ${formatDate(document.expiryDate)}` : 'No expiry date'}
              </p>
            </div>
            <div className="flex shrink-0 flex-col items-end gap-2">
              <StatusPill
                label={documentStatusLabel(document.status)}
                tone={documentStatusTone(document.status)}
              />
              {onSelectDocument && (
                <button
                  type="button"
                  onClick={() => onSelectDocument(document.id)}
                  className="rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
                >
                  Open
                  <span className="sr-only">
                    {' '}
                    {documentTypeLabel(document.type)} {document.documentNumber}
                  </span>
                </button>
              )}
            </div>
          </li>
        ))}
      </ul>
    )
  }

  return (
    <Drawer
      title="Employee Documents"
      icon={Users}
      subtitle={employeeName ?? 'Registered documents for this employee'}
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default AgentEmployeeDocumentsDrawer
