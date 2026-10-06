import { FileText } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import {
  documentTypeLabel,
  documentStatusLabel,
  documentStatusTone,
  formatDate,
} from './documentDisplay'

const thClass =
  'px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-[#6B7280]'

/**
 * Agent-specific document table. Rows are keyboard- and screen-reader
 * accessible: each row exposes a single "View details" button rather than a
 * clickable row, so focus order and activation are predictable.
 */
function AgentDocumentTable({ items, onSelect, resolveOwner }) {
  return (
    <div className="-mx-1 overflow-x-auto">
      <table className="w-full min-w-[720px] border-collapse">
        <caption className="sr-only">
          Documents attached to the renewal tasks assigned to you. Activate a row’s
          View details button to open the document.
        </caption>
        <thead>
          <tr className="border-b border-[#E2E4E9]">
            <th scope="col" className={thClass}>
              Document
            </th>
            <th scope="col" className={thClass}>
              Owner
            </th>
            <th scope="col" className={thClass}>
              Issued
            </th>
            <th scope="col" className={thClass}>
              Expiry
            </th>
            <th scope="col" className={thClass}>
              Status
            </th>
            <th scope="col" className={`${thClass} text-right`}>
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-[#E2E4E9]">
          {items.map((document) => {
            const owner = resolveOwner(document)
            return (
              <tr key={document.id} className="transition duration-150 hover:bg-[#F7F8FA]">
                <td className="px-4 py-3">
                  <div className="flex min-w-0 items-center gap-2">
                    <FileText size={15} className="shrink-0 text-[#6B7280]" aria-hidden="true" />
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-[#16181D]">
                        {documentTypeLabel(document.type)}
                      </p>
                      {document.documentNumber && (
                        <p className="truncate font-mono text-xs text-[#6B7280]">
                          {document.documentNumber}
                        </p>
                      )}
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3">
                  {owner ? (
                    <span
                      className="text-sm text-[#16181D]"
                      title={owner.resolved ? undefined : owner.fallback}
                    >
                      {owner.name ?? owner.fallback}
                    </span>
                  ) : (
                    <span className="text-sm text-[#9CA3AF]">Unassigned</span>
                  )}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-sm text-[#6B7280]">
                  {formatDate(document.issueDate) ?? '—'}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-sm text-[#6B7280]">
                  {formatDate(document.expiryDate) ?? '—'}
                </td>
                <td className="px-4 py-3">
                  <StatusPill
                    label={documentStatusLabel(document.status)}
                    tone={documentStatusTone(document.status)}
                  />
                </td>
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    onClick={() => onSelect(document.id)}
                    className="inline-flex items-center rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
                  >
                    View details
                    <span className="sr-only">
                      {' '}
                      for {documentTypeLabel(document.type)}
                      {document.documentNumber ? ` ${document.documentNumber}` : ''}
                    </span>
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

export default AgentDocumentTable
