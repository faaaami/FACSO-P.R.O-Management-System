import { Link } from 'react-router-dom'
import { CalendarClock } from 'lucide-react'
import SectionCard from '../../client/SectionCard'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import StatusPill from '../../client/StatusPill'
import { useAgentExpiringDocuments } from '../../../hooks/agent/useAgentExpiringDocuments'
import { documentTypeLabel, formatDate } from '../documents/documentDisplay'

const PREVIEW_WINDOW_DAYS = 30
const PREVIEW_SIZE = 5

/**
 * Compact "expiring soon" preview for the dashboard. The full list, filters and
 * pagination live on the documents page, which this panel links to.
 */
function DashboardExpiringPreview() {
  const { items, totalCount, isLoading, isError, refresh } = useAgentExpiringDocuments({
    days: PREVIEW_WINDOW_DAYS,
    page: 1,
    pageSize: PREVIEW_SIZE,
    includeExpired: false,
  })

  let content
  if (isLoading) {
    content = <LoadingState label="Loading expiring documents…" />
  } else if (isError) {
    content = (
      <ErrorState message="Could not load expiring documents." onRetry={() => refresh()} />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={CalendarClock}
        message="Nothing expiring soon."
        description={`No documents in your assigned tasks expire in the next ${PREVIEW_WINDOW_DAYS} days.`}
      />
    )
  } else {
    content = (
      <>
        <ul className="divide-y divide-[#E2E4E9]">
          {items.map((document) => {
            // `status` is the backend's own Expired / ExpiringSoon / Active label
            // and stays authoritative for expiry state. `daysRemaining` is signed
            // and floored, so the status branch is taken first and a negative
            // number is never rendered as "Nd left".
            const isExpired = document.status === 'Expired'
            const daysLeft =
              typeof document.daysRemaining === 'number' ? document.daysRemaining : null
            return (
              <li key={document.id} className="flex items-center justify-between gap-2 py-2.5">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-[#16181D]">
                    {documentTypeLabel(document.type)}
                  </p>
                  <p className="truncate text-xs text-[#6B7280]">
                    {document.documentNumber ? `${document.documentNumber} - ` : ''}
                    {document.expiryDate ? `Expires ${formatDate(document.expiryDate)}` : 'No expiry date'}
                  </p>
                </div>
                <StatusPill
                  label={isExpired ? 'Expired' : daysLeft === null ? 'Unknown' : `${daysLeft}d left`}
                  tone={isExpired ? 'danger' : 'warning'}
                />
              </li>
            )
          })}
        </ul>
        <div className="mt-3 flex justify-end">
          <Link
            to="/documents"
            className="rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          >
            View all documents
          </Link>
        </div>
      </>
    )
  }

  return (
    <SectionCard
      title="Expiring Soon"
      icon={CalendarClock}
      subtitle={`Documents expiring within ${PREVIEW_WINDOW_DAYS} days`}
      badge={
        <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280]">
          {totalCount}
        </span>
      }
    >
      {content}
    </SectionCard>
  )
}

export default DashboardExpiringPreview
