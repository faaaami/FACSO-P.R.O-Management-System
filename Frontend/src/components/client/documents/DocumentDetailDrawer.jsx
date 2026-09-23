import { format } from 'date-fns'
import {
  AlertTriangle,
  Building2,
  Eye,
  FileText,
  FolderOpen,
  Users,
} from 'lucide-react'
import Drawer from './Drawer'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import { useClientDocument } from '../../../hooks/client/useClientDocument'
import { useClientDocumentScan } from '../../../hooks/client/useClientDocumentScan'
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

function DocumentDetailDrawer({
  documentId,
  onClose,
  onOpenEmployeeDocuments,
  isScanRequested,
  onRequestScan,
}) {
  const {
    data: document,
    isLoading,
    isError,
    refetch,
  } = useClientDocument(documentId)

  const {
    isError: scanIsError,
    isLoading: scanIsLoading,
    error: scanError,
  } = useClientDocumentScan(isScanRequested ? documentId : null)

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
    const hasScan = Boolean(document.fileUrl)
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
        {hasScan && (
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
            <span className="font-medium text-[#6B7280]">Attached file:</span>
            <p className="mt-1 text-[#16181D] break-words">
              {document.fileName ?? document.fileUrl}
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

        {/* Actions */}
        <div className="flex flex-col gap-2.5">
          {hasScan && (
            <button
              type="button"
              disabled={scanIsLoading}
              onClick={() => {
                if (document.fileUrl) {
                  window.open(document.fileUrl, '_blank', 'noopener,noreferrer')
                }
                onRequestScan()
              }}
              className="inline-flex w-full items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
            >
              <Eye size={16} strokeWidth={2} aria-hidden="true" />
              {scanIsLoading ? 'Preparing…' : 'View File'}
            </button>
          )}

          {scanIsError && (
            <div className="flex items-start gap-2 rounded-[6px] bg-red-50 p-2 text-xs font-medium text-[#DC2626] border border-red-200">
              <AlertTriangle size={14} className="shrink-0 text-[#DC2626] mt-0.5" aria-hidden="true" />
              <span>
                {scanError?.response?.status === 409
                  ? 'No scan is attached to this document.'
                  : 'The document file could not be opened.'}
              </span>
            </div>
          )}
        </div>
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