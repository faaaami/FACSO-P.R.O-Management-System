import { useEffect, useState } from 'react'
import { ScanLine } from 'lucide-react'
import SectionCard from '../client/SectionCard'
import LoadingState from '../client/LoadingState'
import ErrorState from '../client/ErrorState'
import EmptyState from '../client/EmptyState'
import Pagination from '../client/billing/Pagination'
import PendingExtractionsTable from './PendingExtractionsTable'
import { extractApiErrorMessage } from '../../utils/apiError'

const PAGE_SIZE = 20

/**
 * The three states the endpoint accepts. The default is Pending, which is the
 * review queue itself; the other two are there so a reviewer can see what they
 * already handled and what was lost, rather than having those drafts vanish.
 */
const STATUS_TABS = [
  { value: 'Pending', label: 'Awaiting review' },
  { value: 'Confirmed', label: 'Confirmed' },
  { value: 'Expired', label: 'Expired' },
]

/**
 * The document-extraction review queue. SHARED BY AGENT AND ADMIN.
 *
 * A RESUMABLE LIST OF DRAFTS, NOT A DOCUMENT BROWSER. Every row is an
 * extraction that has not become a document yet; the documents themselves stay
 * where they already live (the expiry registry for Admin, the entity and
 * employee records for both). Nothing here widens anyone's document scope.
 *
 * `useQueue` is passed in rather than imported so this one component serves
 * both portals' role-scoped endpoints: useAgentPendingDocumentExtractions sees
 * the Agent's own drafts (plus legacy unowned ones, which are still theirs to
 * confirm), useAdminPendingDocumentExtractions sees the Admin scope. The Agent
 * gets the same UI without being handed a broader list.
 *
 * `uploadSlot` is the portal's upload control, so the Agent's and the Admin's
 * can differ in wording while the queue around them stays identical.
 */
function DocumentExtractionsQueue({ useQueue, onReview, uploadSlot, isAgent }) {
  const [status, setStatus] = useState('Pending')
  const [page, setPage] = useState(1)
  const [isFetching, setIsFetching] = useState(false)

  const { items, totalCount, isLoading, isError, error, refresh } = useQueue({
    page,
    pageSize: PAGE_SIZE,
    status,
  })

  // A tab switch makes the current page number meaningless: page 4 of the review
  // queue is rarely page 1 of the confirmed list, and can be past its end.
  useEffect(() => {
    setPage(1)
  }, [status])

  function changeTab(nextStatus) {
    if (nextStatus === status) return
    setIsFetching(true)
    setStatus(nextStatus)
  }

  // The tab is only reported as "busy" until the list stops being a first load,
  // so the control does not flash on a background refetch of the current tab.
  useEffect(() => {
    if (isFetching && !isLoading) {
      setIsFetching(false)
    }
  }, [isFetching, isLoading])

  let content
  if (isLoading) {
    content = <LoadingState label="Loading extraction drafts…" />
  } else if (isError) {
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load the review queue.')}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    const emptyByStatus = {
      Pending: isAgent
        ? 'No documents are waiting for your review.'
        : 'No documents are waiting for review.',
      Confirmed: 'No drafts have been confirmed yet.',
      Expired: 'No drafts have expired.',
    }
    content = (
      <EmptyState
        icon={ScanLine}
        message={emptyByStatus[status] ?? 'Nothing here.'}
        description={
          status === 'Pending'
            ? 'Upload a passport, visa, labour card, Emirates ID, trade licence or establishment card and extraction will fill in what it can. You confirm or correct every value before anything is recorded.'
            : 'Drafts appear here once their status changes.'
        }
      />
    )
  } else {
    content = <PendingExtractionsTable items={items} onReview={onReview} />
  }

  const showCount = !isLoading && !isError

  return (
    <SectionCard
      title="Document Extraction Queue"
      icon={ScanLine}
      subtitle={
        isAgent
          ? 'Files you have uploaded that are not yet documents. Confirm one to add it to the register.'
          : 'Extraction drafts across the Admin scope. A review queue only — documents stay on the expiry registry and their owner records.'
      }
      badge={
        showCount ? (
          <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
            {totalCount.toLocaleString('en-US')}
            <span className="sr-only"> drafts</span>
          </span>
        ) : null
      }
    >
      {uploadSlot}

      <div
        role="tablist"
        aria-label="Draft status"
        className="mb-4 flex flex-wrap gap-1.5"
      >
        {STATUS_TABS.map((tab) => {
          const active = status === tab.value
          return (
            <button
              key={tab.value}
              type="button"
              role="tab"
              aria-selected={active}
              disabled={isFetching}
              onClick={() => changeTab(tab.value)}
              className={`cursor-pointer rounded-[8px] border px-3 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed ${
                active
                  ? 'border-[#1C1F26] bg-[#1C1F26] text-white'
                  : 'border-[#E2E4E9] bg-white text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D]'
              }`}
            >
              {tab.label}
            </button>
          )
        })}
      </div>

      {content}

      {showCount && totalCount > PAGE_SIZE && (
        <Pagination
          page={page}
          pageSize={PAGE_SIZE}
          totalCount={totalCount}
          itemLabel="draft"
          itemLabelPlural="drafts"
          onPageChange={setPage}
        />
      )}
    </SectionCard>
  )
}

export default DocumentExtractionsQueue
