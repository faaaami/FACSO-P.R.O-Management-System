import { useAgentDocumentDependencies } from '../../../hooks/agent/useAgentDocument'
import { documentTypeLabel, formatDate } from './documentDisplay'

/**
 * Read-only forward dependencies for a document (documents this one depends
 * on). The backend does not expose the reverse direction ("what depends on
 * this document"), so this list is intentionally one-way.
 */
function AgentDocumentDependencies({ documentId }) {
  const { items, isLoading, isError } = useAgentDocumentDependencies(documentId)

  if (isLoading) {
    return <p className="text-xs text-[#9CA3AF]">Loading dependencies…</p>
  }

  if (isError) {
    return <p className="text-xs text-[#9CA3AF]">Dependencies could not be loaded.</p>
  }

  if (items.length === 0) {
    return <p className="text-xs text-[#9CA3AF]">No linked documents.</p>
  }

  return (
    <ul className="divide-y divide-[#E2E4E9]">
      {items.map((dependency) => (
        <li key={dependency.id} className="flex items-center justify-between gap-3 py-2">
          <div className="min-w-0">
            <p className="truncate text-xs font-semibold text-[#16181D]">
              {documentTypeLabel(dependency.documentType)}
            </p>
            {dependency.documentNumber && (
              <p className="truncate font-mono text-xs text-[#6B7280]">
                {dependency.documentNumber}
              </p>
            )}
          </div>
          <span className="shrink-0 text-xs text-[#6B7280]">
            {formatDate(dependency.expiryDate) ?? '—'}
          </span>
        </li>
      ))}
    </ul>
  )
}

export default AgentDocumentDependencies
