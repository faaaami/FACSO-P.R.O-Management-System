import { useCallback } from 'react'
import { format } from 'date-fns'
import { Building2, FileText, FolderOpen, Users } from 'lucide-react'
import Drawer from './Drawer'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import DocumentFilePanel from '../../documents/DocumentFilePanel'
import { getClientDocumentFile } from '../../../api/client/documents'
import { useClientDocument } from '../../../hooks/client/useClientDocument'
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

function MetaRow({ label, value }) {
  return (
    <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
      <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">{label}</span>
      <p className="mt-1 text-sm font-semibold text-[#16181D] break-words">{value}</p>
    </div>
  )
}

/**
 * Read-only document detail drawer for the client portal.
 *
 * D1 ADDS AUTHORIZED FILE VIEWING, AND REMOVES THE STORAGE PATH.
 *
 * This drawer used to render the attached file as a button that called
 * `window.open(document.fileUrl)`, falling back to showing the fileUrl as the
 * file's name when fileName was missing. That value is a relative storage key
 * describing where the file sits inside the server — not a retrievable URL — so
 * the tab it opened was a 404, and the fallback printed internal layout on a
 * customer-facing screen.
 *
 * The bytes now come from GET /client/documents/{id}/file, which is scoped to the
 * signed-in client company with the same authorization as the document detail
 * endpoint above it. DocumentFilePanel turns them into a short-lived object URL
 * and revokes it when the preview is closed or the drawer unmounts.
 *
 * The client stays READ-ONLY. There is no upload, no extraction review and no
 * confirmation here: registering a document is an Agent or Admin action, and a
 * client can only view what already exists for their own company.
 */
function DocumentDetailDrawer({ documentId, onClose, onOpenEmployeeDocuments }) {
  const {
    data: document,
    isLoading,
    isError,
    refetch,
  } = useClientDocument(documentId)

  const loadFile = useCallback(() => getClientDocumentFile(documentId), [documentId])

  let content

  if (isLoading) {
    content = <LoadingState label="Loading document details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load this document. It may have been removed."
        onRetry={() => refetch()}
      />
    )
  } else if (!document) {
    content = (
      <EmptyState
        icon={FileText}
        message="Document is not available."
        description="The requested document could not be retrieved."
      />
    )
  } else {
    // Presence check only. The value itself is never displayed or navigated to.
    const hasFile = Boolean(document.fileUrl)
    const employeeId = document.employeeId

    content = (
      <div className="space-y-5">
        {/* Document identity */}
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              {enumLabel(DOCUMENT_TYPES, document.type) ?? 'Official Document'}
            </span>
            <StatusPill
              label={enumLabel(DOCUMENT_STATUS, document.status) ?? 'Pending'}
              tone={documentStatusTone(document.status)}
            />
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">Document #</span>
            {document.documentNumber ? (
              <span className="rounded-[4px] bg-white px-1.5 py-0.5 text-[11px] font-mono font-medium text-[#6B7280] border border-[#E2E4E9]">
                {document.documentNumber}
              </span>
            ) : (
              <span className="text-xs text-[#9CA3AF]">No number</span>
            )}
          </div>
        </div>

        {/* Ownership */}
        {employeeId && (
          <div className="flex items-center justify-between gap-3 rounded-[8px] border border-[#E2E4E9] bg-white p-3">
            <div className="flex items-center gap-2.5">
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#6B7280]">
                <Users size={15} strokeWidth={1.75} aria-hidden="true" />
              </div>
              <div>
                <p className="text-sm font-semibold text-[#16181D]">Employee document</p>
                <p className="text-xs text-[#6B7280]">Belongs to an employee profile</p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => onOpenEmployeeDocuments(employeeId)}
              className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
            >
              <FolderOpen size={13} strokeWidth={2} aria-hidden="true" />
              Employee documents
            </button>
          </div>
        )}

        {/* Key metadata */}
        <div className="grid grid-cols-2 gap-3">
          <MetaRow label="Issue Date" value={formatDate(document.issueDate) ?? 'N/A'} />
          <MetaRow label="Expiry Date" value={formatDate(document.expiryDate) ?? 'No expiry'} />
        </div>

        {/* File meta (when present) */}
        {hasFile && (
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
            <span className="font-medium text-[#6B7280]">Attached file:</span>
            <p className="mt-1 text-[#16181D] break-words">
              {document.fileName ?? 'File attached'}
            </p>
            <div className="mt-1.5 flex flex-wrap gap-x-3 gap-y-1 text-[#6B7280]">
              {document.contentType && <span>{document.contentType}</span>}
              {document.fileSize != null && (
                <span>
                  {document.fileSize > 0
                    ? `${Math.max(1, Math.round(document.fileSize / 1024))} KB`
                    : 'Unknown size'}
                </span>
              )}
            </div>
          </div>
        )}

        {/* Details object */}
        {document.details && Object.keys(document.details).length > 0 && (
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
            <span className="font-medium text-[#6B7280]">Additional details:</span>
            <dl className="mt-2 grid grid-cols-1 gap-x-4 gap-y-1.5 sm:grid-cols-2">
              {Object.entries(document.details).map(([key, value]) => (
                <div key={key} className="min-w-0">
                  <dt className="text-[#9CA3AF] capitalize">{key}</dt>
                  <dd className="mt-0.5 text-[#16181D] break-words">
                    {typeof value === 'object' && value !== null
                      ? JSON.stringify(value)
                      : String(value ?? 'N/A')}
                  </dd>
                </div>
              ))}
            </dl>
          </div>
        )}

        {/* Timestamps */}
        <div className="grid grid-cols-2 gap-3">
          <MetaRow label="Added" value={formatDate(document.createdAt) ?? 'N/A'} />
          <MetaRow label="Last Updated" value={formatDate(document.updatedAt) ?? 'N/A'} />
        </div>

        {/* Authorized file access. The panel fetches the bytes on demand, so a
            document that was only opened for its metadata costs no transfer. */}
        {hasFile ? (
          <DocumentFilePanel
            load={loadFile}
            identity={['client-document', documentId]}
            fileName={document.fileName}
            contentType={document.contentType}
            label="View file"
          />
        ) : (
          <p className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3 text-xs text-[#6B7280]">
            No file is attached to this document.
          </p>
        )}
      </div>
    )
  }

  return (
    <Drawer
      title="Document Details"
      icon={Building2}
      subtitle="Registered document on the compliance file"
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default DocumentDetailDrawer