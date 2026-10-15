import { History } from 'lucide-react'
import { useAgentDocumentVersions } from '../../../hooks/agent/useAgentDocument'
import { formatDateTime, formatFileSize } from './documentDisplay'

/** Read-only version history for a document in the Agent portfolio. */
function AgentDocumentVersions({ documentId }) {
  const { items, totalCount, isLoading, isError } = useAgentDocumentVersions(documentId)

  if (isLoading) {
    return <p className="text-xs text-[#9CA3AF]">Loading versions…</p>
  }

  if (isError) {
    return <p className="text-xs text-[#9CA3AF]">Versions could not be loaded.</p>
  }

  if (items.length === 0) {
    return <p className="text-xs text-[#9CA3AF]">No other versions recorded.</p>
  }

  return (
    <div>
      <p className="mb-1.5 text-xs text-[#6B7280]">
        {totalCount} {totalCount === 1 ? 'version' : 'versions'}
      </p>
      <ul className="divide-y divide-[#E2E4E9]">
        {items.map((version) => (
          <li key={version.id} className="flex items-start justify-between gap-3 py-2">
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-xs font-semibold text-[#16181D]">
                <History size={12} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
                Version {version.versionNumber}
              </p>
              {version.fileName && (
                <p className="mt-0.5 truncate text-xs text-[#6B7280]">{version.fileName}</p>
              )}
            </div>
            <div className="shrink-0 text-right">
              <p className="text-xs text-[#6B7280]">{formatDateTime(version.createdAt) ?? '—'}</p>
              {typeof version.fileSize === 'number' && (
                <p className="text-xs text-[#9CA3AF]">{formatFileSize(version.fileSize)}</p>
              )}
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}

export default AgentDocumentVersions
