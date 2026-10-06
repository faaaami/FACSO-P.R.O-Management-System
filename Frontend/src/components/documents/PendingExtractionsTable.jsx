import { FileText, ScanLine } from 'lucide-react'
import StatusPill from '../client/StatusPill'
import { documentTypeLabel } from '../agent/documents/documentDisplay'
import { formatFileSize } from '../agent/documents/documentDisplay'
import { formatDateTime } from '../agent/documents/documentDisplay'
import { formatConfidence } from './documentExtractionForm'

/**
 * The extraction review queue, one row per draft. SHARED BY AGENT AND ADMIN.
 *
 * Purely presentational: the list query, the status filter and the pager all
 * live in DocumentExtractionsQueue, and the two portals' list items have the
 * same shape.
 *
 * EVERY VALUE IN A ROW IS A SUGGESTION. The column headings say so, and nothing
 * here is styled as if it were recorded — a row shows what extraction proposed,
 * and the review drawer is where any of it is confirmed, changed or discarded.
 *
 * THERE IS NO FILE LINK IN A ROW. A draft has no browser-retrievable URL: the
 * bytes come from the review drawer, which calls the authorized extraction file
 * endpoint. Rendering the stored reference here would both leak internal layout
 * and produce a dead control.
 *
 * `createdBy` is deliberately not shown. The DTO carries it as a bare GUID and
 * there is no name for it on this payload; printing an id where a person is
 * meant would be a fabrication. Upload time is shown instead, which is real.
 */
function extractionStatusTone(status) {
  if (status === 'Pending') return 'warning'
  if (status === 'Confirmed') return 'success'
  if (status === 'Expired') return 'neutral'
  return 'neutral'
}

function PendingExtractionsTable({ items, onReview }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[46rem] border-collapse text-left">
        <caption className="sr-only">
          Document extraction drafts awaiting review
        </caption>
        <thead>
          <tr className="border-b border-[#E2E4E9]">
            <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
              File
            </th>
            <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
              Suggested type
            </th>
            <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
              Document #
            </th>
            <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
              Confidence
            </th>
            <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
              Uploaded
            </th>
            <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
              Status
            </th>
            <th scope="col" className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-[#E2E4E9]">
          {items.map((item) => {
            const confidence = formatConfidence(item.confidence)
            return (
              <tr key={item.id} className="transition duration-150 hover:bg-[#F8F9FB]">
                <td className="px-3 py-3">
                  <div className="flex items-start gap-2.5">
                    <FileText
                      size={15}
                      strokeWidth={1.75}
                      className="mt-0.5 shrink-0 text-[#6B7280]"
                      aria-hidden="true"
                    />
                    <div className="min-w-0">
                      <p className="break-words text-sm font-medium text-[#16181D]">{item.fileName}</p>
                      <p className="mt-0.5 text-xs text-[#9CA3AF]">
                        {formatFileSize(item.fileSize) ?? item.contentType}
                      </p>
                    </div>
                  </div>
                </td>
                <td className="px-3 py-3 text-xs text-[#16181D]">
                  {item.suggestedType ? documentTypeLabel(item.suggestedType) : <span className="text-[#9CA3AF]">Not detected</span>}
                </td>
                <td className="px-3 py-3 text-xs text-[#16181D]">
                  {item.suggestedDocumentNumber || <span className="text-[#9CA3AF]">Not detected</span>}
                </td>
                <td className="px-3 py-3 text-xs tabular-nums text-[#6B7280]">{confidence ?? '—'}</td>
                <td className="px-3 py-3 text-xs text-[#6B7280]">{formatDateTime(item.createdAt) ?? '—'}</td>
                <td className="px-3 py-3">
                  <StatusPill label={item.status} tone={extractionStatusTone(item.status)} />
                </td>
                <td className="px-3 py-3 text-right">
                  <button
                    type="button"
                    onClick={() => onReview(item.id)}
                    className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
                  >
                    <ScanLine size={13} strokeWidth={1.75} aria-hidden="true" />
                    {item.status === 'Pending' ? 'Review' : 'View'}
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

export default PendingExtractionsTable
