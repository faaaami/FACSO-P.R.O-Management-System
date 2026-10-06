import { useState } from 'react'
import { AlertTriangle, CalendarClock, FileText } from 'lucide-react'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import StatusPill from '../../components/client/StatusPill'
import Pagination from '../../components/client/billing/Pagination'
import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import AgentDocumentFilters from '../../components/agent/documents/AgentDocumentFilters'
import AgentDocumentTable from '../../components/agent/documents/AgentDocumentTable'
import AgentDocumentDetailDrawer from '../../components/agent/documents/AgentDocumentDetailDrawer'
import AgentExtractionReviewDrawer from '../../components/agent/documents/AgentExtractionReviewDrawer'
import DocumentExtractionsQueue from '../../components/documents/DocumentExtractionsQueue'
import DocumentUploadCard from '../../components/documents/DocumentUploadCard'
import {
  documentTypeLabel,
  formatDate,
} from '../../components/agent/documents/documentDisplay'
import { useAgentDocuments } from '../../hooks/agent/useAgentDocuments'
import { useAgentExpiringDocuments } from '../../hooks/agent/useAgentExpiringDocuments'
import { useAgentEntityMaps } from '../../hooks/agent/useAgentEntityMaps'
import {
  useAgentPendingDocumentExtractions,
  useExtractAgentDocument,
} from '../../hooks/agent/useAgentDocumentExtraction'

const PAGE_SIZE = 20
const EXPIRING_PAGE_SIZE = 10
const EXPIRY_WINDOWS = [30, 60, 90]

const PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" aria-hidden="true" />
    Agent Portal
  </span>
)

function CountBadge({ value }) {
  return (
    <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280]">
      {value}
    </span>
  )
}

function AgentDocumentsHeader() {
  return (
    <AgentPageHeader
      title="Documents"
      badge={PORTAL_BADGE}
      description="Register and track the company and employee documents attached to the renewal tasks assigned to you. Upload a file to extract its details, then confirm it here."
    />
  )
}

function DocumentsListCard({ filters, onChange, onClear }) {
  const [page, setPage] = useState(1)
  const [selectedDocumentId, setSelectedDocumentId] = useState(null)
  const { items, totalCount, isLoading, isError, refresh } = useAgentDocuments({
    page,
    pageSize: PAGE_SIZE,
    type: filters.type,
    status: filters.status,
    expiresBefore: filters.expiresBefore,
  })
  const { resolveOwner } = useAgentEntityMaps()

  const hasActiveFilters = Boolean(filters.type || filters.status || filters.expiresBefore)

  const updateFilters = (next) => {
    onChange(next)
    setPage(1)
  }

  const clearFilters = () => {
    onClear()
    setPage(1)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading documents…" />
  } else if (isError) {
    content = <ErrorState message="Could not load documents." onRetry={() => refresh()} />
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={FileText}
        message={hasActiveFilters ? 'No matching documents.' : 'No documents yet.'}
        description={
          hasActiveFilters
            ? 'Try widening or clearing the filters.'
            : 'Documents for your portfolio will appear here.'
        }
      />
    )
  } else {
    content = (
      <AgentDocumentTable
        items={items}
        onSelect={setSelectedDocumentId}
        resolveOwner={resolveOwner}
      />
    )
  }

  return (
    <>
      <SectionCard
        title="Documents"
        icon={FileText}
        subtitle="Documents attached to the renewal tasks assigned to you"
        badge={<CountBadge value={totalCount} />}
      >
        <AgentDocumentFilters
          filters={filters}
          onChange={updateFilters}
          onClear={clearFilters}
          isDirty={hasActiveFilters}
        />
        <div className="mt-4">{content}</div>
        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="document"
            onPageChange={setPage}
          />
        )}
      </SectionCard>

      {selectedDocumentId && (
        <AgentDocumentDetailDrawer
          documentId={selectedDocumentId}
          onClose={() => setSelectedDocumentId(null)}
        />
      )}
    </>
  )
}

function ExpiringDocumentsCard() {
  const [days, setDays] = useState(30)
  const [includeExpired, setIncludeExpired] = useState(false)
  const [page, setPage] = useState(1)

  const { items, totalCount, isLoading, isError, refresh } = useAgentExpiringDocuments({
    days,
    page,
    pageSize: EXPIRING_PAGE_SIZE,
    includeExpired,
  })

  // Changing the window or the expired toggle starts a new result set.
  function handleWindowChange(value) {
    setDays(value)
    setPage(1)
  }

  function handleIncludeExpiredChange(event) {
    setIncludeExpired(event.target.checked)
    setPage(1)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading expiring documents…" />
  } else if (isError) {
    content = <ErrorState message="Could not load expiring documents." onRetry={() => refresh()} />
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={CalendarClock}
        message="Nothing expiring soon."
        description={
          includeExpired
            ? `No documents in your assigned tasks are expiring or already expired within ${days} days.`
            : `No documents in your assigned tasks expire in the next ${days} days. Try a longer window.`
        }
      />
    )
  } else {
    content = (
      <ul className="divide-y divide-[#E2E4E9]">
        {items.map((document) => {
          // `status` is the backend's own Expired / ExpiringSoon / Active label
          // and stays authoritative for expiry state. `daysRemaining` is signed
          // and floored (negative means days past expiry), so the status branch
          // is taken first and a negative number is never rendered as "Nd left".
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
    )
  }

  return (
    <SectionCard
      title="Expiring Soon"
      icon={AlertTriangle}
      subtitle={`Documents in your assigned tasks expiring within ${days} days${includeExpired ? ', including already expired' : ''}`}
      badge={<CountBadge value={totalCount} />}
      action={
        <div className="flex flex-wrap items-center gap-3">
          <label className="flex cursor-pointer items-center gap-1.5 text-xs text-[#6B7280]">
            <input
              type="checkbox"
              checked={includeExpired}
              onChange={handleIncludeExpiredChange}
              className="h-3.5 w-3.5 rounded border-[#E2E4E9] accent-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            />
            Include expired
          </label>
          <select
            value={days}
            onChange={(event) => handleWindowChange(Number(event.target.value))}
            aria-label="Expiry window"
            className="rounded-[8px] border border-[#E2E4E9] bg-white px-2 py-1.5 text-xs text-[#16181D] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          >
            {EXPIRY_WINDOWS.map((window) => (
              <option key={window} value={window}>
                {window} days
              </option>
            ))}
          </select>
        </div>
      }
    >
      {content}
      {totalCount > EXPIRING_PAGE_SIZE && (
        <Pagination
          page={page}
          pageSize={EXPIRING_PAGE_SIZE}
          totalCount={totalCount}
          itemLabel="document"
          onPageChange={setPage}
        />
      )}
    </SectionCard>
  )
}

/**
 * D1 — the Agent's document registration surface: upload a file, review what
 * extraction found, confirm it into a real document.
 *
 * This sits ABOVE the document list rather than inside it. A draft is not a
 * document and has no owner, type or number yet, so it is shown in its own
 * queue; the confirmed result appears in the list below through the normal
 * query invalidation. The two never share a list because a reviewer must be able
 * to tell "not registered yet" from "registered".
 *
 * The queue is resumable: a draft stays Pending until it is confirmed or its
 * window passes, so an Agent can leave and come back without losing the upload.
 */
function AgentExtractionQueueCard() {
  const [selectedExtractionId, setSelectedExtractionId] = useState(null)

  return (
    <>
      <DocumentExtractionsQueue
        useQueue={useAgentPendingDocumentExtractions}
        onReview={setSelectedExtractionId}
        isAgent
        uploadSlot={
          <DocumentUploadCard
            useExtract={useExtractAgentDocument}
            isAgent
            onExtracted={setSelectedExtractionId}
          />
        }
      />

      {selectedExtractionId && (
        <AgentExtractionReviewDrawer
          extractionId={selectedExtractionId}
          onClose={() => setSelectedExtractionId(null)}
          onConfirmed={() => setSelectedExtractionId(null)}
        />
      )}
    </>
  )
}

function AgentDocumentsPage() {
  const [filters, setFilters] = useState({})

  return (
    <div className="space-y-6">
      <AgentDocumentsHeader />
      <AgentExtractionQueueCard />
      <DocumentsListCard
        filters={filters}
        onChange={setFilters}
        onClear={() => setFilters({})}
      />
      <ExpiringDocumentsCard />
    </div>
  )
}

export default AgentDocumentsPage
