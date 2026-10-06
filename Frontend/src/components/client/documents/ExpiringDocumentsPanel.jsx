import { format } from 'date-fns'
import { AlertTriangle, ChevronRight, FileText } from 'lucide-react'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import { useClientExpiringDocuments } from '../../../hooks/client/useClientExpiringDocuments'
import { DOCUMENT_TYPES, enumLabel } from '../enumLabels'
import { expiringStatusLabel, expiringStatusTone } from './expiringStatus'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function ExpiringDocumentsPanel({ onSelectDocument }) {
  const { data, isLoading, isError, refetch } = useClientExpiringDocuments({
    days: 30,
    page: 1,
    pageSize: 10,
  })

  let content

  if (isLoading) {
    content = <LoadingState label="Checking expiring documents…" />
  } else if (isError) {
    content = <ErrorState message="Could not load expiring documents." onRetry={() => refetch()} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={FileText}
        message="No documents expiring in the next 30 days."
        description="All licenses, visas, and registered cards are currently up to date."
      />
    )
  } else {
    content = (
      <>
        <div className="flex items-center justify-between rounded-[8px] bg-amber-50/60 border border-amber-200 px-3.5 py-2 text-xs text-[#D97706]">
          <div className="flex items-center gap-2">
            <AlertTriangle size={14} className="shrink-0 text-[#D97706]" aria-hidden="true" />
            <span className="font-medium">
              {data.totalCount} document{data.totalCount === 1 ? '' : 's'} in the window
            </span>
          </div>
          <span className="text-[11px] font-semibold uppercase tracking-wider">30 Days</span>
        </div>

        <ul className="mt-3 divide-y divide-[#E2E4E9] border-t border-b border-[#E2E4E9]">
          {data.items.map((doc) => {
            return (
              <li key={doc.id} className="flex flex-col gap-2.5 py-3 first:pt-3 last:pb-3">
                <button
                  type="button"
                  onClick={() => onSelectDocument(doc.id)}
                  className="group w-full text-left focus:outline-none focus:ring-2 focus:ring-[#0F9D74] rounded-[6px] cursor-pointer"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-[#16181D] group-hover:text-[#0F9D74] transition-colors duration-150">
                          {enumLabel(DOCUMENT_TYPES, doc.type) ?? 'Official Document'}
                        </span>
                        {doc.documentNumber && (
                          <span className="rounded-[4px] bg-[#F7F8FA] px-1.5 py-0.5 text-[11px] font-mono font-medium text-[#6B7280] border border-[#E2E4E9]">
                            {doc.documentNumber}
                          </span>
                        )}
                      </div>
                      <p className="mt-0.5 text-xs text-[#6B7280]">
                        {doc.expiryDate ? `Expires on ${formatDate(doc.expiryDate)}` : 'Expiry date pending'}
                      </p>
                    </div>
                    <ChevronRight
                      size={16}
                      strokeWidth={1.75}
                      className="mt-1 shrink-0 text-[#9CA3AF] group-hover:text-[#0F9D74] transition-colors duration-150"
                      aria-hidden="true"
                    />
                  </div>
                </button>

                <div className="flex shrink-0 items-center gap-2">
                  <span className="inline-flex items-center rounded-[6px] px-2 py-0.5 text-xs font-semibold bg-amber-50 text-[#D97706] border border-[#D97706]/20">
                    {doc.daysRemaining}d left
                  </span>
                  <StatusPill
                    label={expiringStatusLabel(doc.status)}
                    tone={expiringStatusTone(doc.status)}
                  />
                </div>
              </li>
            )
          })}
        </ul>

        {data.totalCount != null && (
          <p className="mt-3 text-right text-xs text-[#9CA3AF]">
            Showing {data.items.length} of {data.totalCount} expiring document{data.totalCount === 1 ? '' : 's'}
          </p>
        )}
      </>
    )
  }

  return (
    <section aria-label="Expiring documents" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-start justify-between gap-4 mb-5">
        <div className="min-w-0 flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <AlertTriangle size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">Expiring Documents</h2>
            <p className="mt-0.5 text-xs text-[#6B7280]">Compliance documents approaching expiry</p>
          </div>
        </div>
        {data?.totalCount != null && data.totalCount > 0 && (
          <span className="shrink-0 rounded-[6px] bg-amber-50 px-2 py-0.5 text-xs font-semibold text-[#D97706] border border-[#D97706]/20">
            {data.totalCount} urgent
          </span>
        )}
      </div>

      {content}
    </section>
  )
}

export default ExpiringDocumentsPanel