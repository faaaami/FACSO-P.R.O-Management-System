import { format } from 'date-fns'
import { ChevronLeft, ChevronRight, Eye, FileText, Paperclip } from 'lucide-react'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
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
  if (status === 'InRenewal') return 'warning'
  return 'warning'
}

function DocumentTypeLabel({ type }) {
  return (
    <span className="rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-medium text-[#6B7280] border border-[#E2E4E9]">
      {enumLabel(DOCUMENT_TYPES, type) ?? 'Official Document'}
    </span>
  )
}

function DocumentNumberText({ documentNumber }) {
  if (!documentNumber) return <span className="text-[#9CA3AF]">No number</span>
  return (
    <span className="rounded-[4px] bg-[#F7F8FA] px-1.5 py-0.5 text-[11px] font-mono font-medium text-[#6B7280] border border-[#E2E4E9]">
      {documentNumber}
    </span>
  )
}

function OwnerLabel({ doc }) {
  const owner = doc.employeeId ? 'Employee' : doc.clientEntityId ? 'Entity' : 'Company'
  return <span className="text-xs text-[#6B7280]">{owner}</span>
}

function DocumentRowAction({ onSelect }) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className="inline-flex items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-1.5 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
    >
      <Eye size={13} strokeWidth={2} aria-hidden="true" />
      Details
    </button>
  )
}

function DocumentRow({ doc, onSelect }) {
  return (
    <li className="flex flex-col gap-3 rounded-[10px] bg-white border border-[#E2E4E9] p-4 sm:hidden">
      <div className="flex items-center justify-between gap-3">
        <DocumentTypeLabel type={doc.type} />
        <StatusPill
          label={enumLabel(DOCUMENT_STATUS, doc.status) ?? 'Pending'}
          tone={documentStatusTone(doc.status)}
        />
      </div>
      <div className="space-y-1.5 text-xs">
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Document #</span>
          <DocumentNumberText documentNumber={doc.documentNumber} />
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Owner</span>
          <OwnerLabel doc={doc} />
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-[#6B7280]">Expiry</span>
          <span className="font-medium text-[#16181D]">
            {formatDate(doc.expiryDate) ?? 'No expiry'}
          </span>
        </div>
        {doc.fileUrl && (
          <div className="flex items-center gap-1.5 text-[#6B7280]">
            <Paperclip size={12} strokeWidth={1.75} aria-hidden="true" />
            <span className="truncate">{doc.fileName || 'File attached'}</span>
          </div>
        )}
      </div>
      <div className="self-end">
        <DocumentRowAction onSelect={onSelect} />
      </div>
    </li>
  )
}

function Pagination({ page, pageSize, totalCount, onPageChange }) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)

  return (
    <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-xs text-[#9CA3AF]">
        Showing {from}–{to} of {totalCount} document{totalCount === 1 ? '' : 's'}
      </p>
      <div className="flex items-center gap-2">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
        >
          <ChevronLeft size={14} strokeWidth={2} aria-hidden="true" />
          Previous
        </button>
        <span className="rounded-[6px] bg-[#F7F8FA] px-2.5 py-1 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
          Page {page} of {totalPages}
        </span>
        <button
          type="button"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
        >
          Next
          <ChevronRight size={14} strokeWidth={2} aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}

function DocumentTable({ data, isLoading, isError, onRetry, page, pageSize, onPageChange, onSelect }) {
  let content

  if (isLoading) {
    content = <LoadingState label="Loading documents…" />
  } else if (isError) {
    content = <ErrorState message="Could not load documents." onRetry={onRetry} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={FileText}
        message="No documents found."
        description="No documents match the current filter settings. Adjust or clear the filters to broaden the search."
      />
    )
  } else {
    content = (
      <>
        {/* Mobile card list */}
        <ul className="space-y-3 sm:hidden">
          {data.items.map((doc) => (
            <DocumentRow key={doc.id} doc={doc} onSelect={() => onSelect(doc.id)} />
          ))}
        </ul>

        {/* Desktop table */}
        <div className="hidden sm:block overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Document
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Document #
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Owner
                </th>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Expiry
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
              {data.items.map((doc) => (
                <tr key={doc.id} className="hover:bg-gray-50/60 transition-colors">
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <DocumentTypeLabel type={doc.type} />
                    {doc.fileUrl && (
                      <span className="ml-2 inline-flex items-center gap-1 text-[#9CA3AF]">
                        <Paperclip size={12} strokeWidth={1.75} aria-hidden="true" />
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <DocumentNumberText documentNumber={doc.documentNumber} />
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <OwnerLabel doc={doc} />
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    {formatDate(doc.expiryDate) ?? <span className="text-[#9CA3AF]">No expiry</span>}
                  </td>
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <StatusPill
                      label={enumLabel(DOCUMENT_STATUS, doc.status) ?? 'Pending'}
                      tone={documentStatusTone(doc.status)}
                    />
                  </td>
                  <td className="px-4 py-3.5 text-right">
                    <DocumentRowAction onSelect={() => onSelect(doc.id)} />
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
          onPageChange={onPageChange}
        />
      </>
    )
  }

  return (
    <section aria-label="Documents list" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-start justify-between gap-4 mb-5">
        <div className="min-w-0 flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <FileText size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">Document Register</h2>
            <p className="mt-0.5 text-xs text-[#6B7280]">Company and employee documents on file</p>
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

export default DocumentTable