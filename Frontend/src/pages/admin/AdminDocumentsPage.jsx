import { useEffect, useState } from 'react'
import { FileText, ShieldCheck } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import AdminDocumentFilterBar from '../../components/admin/documents/AdminDocumentFilterBar'
import AdminDocumentsTable from '../../components/admin/documents/AdminDocumentsTable'
import AdminDocumentDetailDrawer from '../../components/admin/documents/AdminDocumentDetailDrawer'
import AdminExtractionReviewDrawer from '../../components/admin/documents/AdminExtractionReviewDrawer'
import DocumentExtractionsQueue from '../../components/documents/DocumentExtractionsQueue'
import DocumentUploadCard from '../../components/documents/DocumentUploadCard'
import { useAdminExpiringDocuments } from '../../hooks/admin/useAdminDocuments'
import { useAdminDocumentOwners } from '../../hooks/admin/useAdminDocumentOwners'
import {
  useAdminPendingDocumentExtractions,
  useExtractAdminDocument,
} from '../../hooks/admin/useAdminDocumentExtraction'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal — the expiry registry, plus the D1 extraction review queue.
 *
 * BACKING QUERIES: GET /api/v1/admin/documents/expiring?days&page&pageSize&includeExpired
 *                  GET /api/v1/admin/documents/extractions?page&pageSize&status
 *
 * THE REGISTRY IS STILL A REGISTRY, NOT A GLOBAL DOCUMENT BROWSER. The backend has
 * no cross-entity Admin document list, so the only multi-row document source that
 * exists is the expiry registry. D1 adds a REVIEW QUEUE of extraction drafts —
 * files uploaded and not yet confirmed — which is a different thing entirely: a
 * draft has no owner, type or number, and confirming one does not add it here.
 * It adds it to the expiry registry, its client entity record, or its employee
 * record, as before. No global list endpoint is invented to make this look like
 * a document browser.
 *
 *  - Still no edit, renew, delete or restore of a confirmed document, and still
 *    no file action on a REGISTRY ROW. Document deletion is a one-way soft
 *    delete with NO restore route anywhere in the codebase, and the registry
 *    hard-filters deleted rows — so a delete button would be irreversible and
 *    invisible afterwards. PATCH is also PUT-like: it re-validates the whole
 *    record including a type-specific details schema, so there is no safe
 *    "change the expiry date" shortcut to expose either. A registry row has no
 *    fileUrl to gate on, which is the separate reason its table has no view
 *    action. What D1 adds is the ability to CREATE a document by confirming a
 *    draft, not to mutate one — and to READ one: the file action lives in the
 *    document detail drawer, which does carry a fileUrl, not in the list.
 *
 *  - Still only two registry filters, and both are real: the expiry window and
 *    the include-expired flag. The endpoint binds no search, no type filter, no
 *    status filter, no client/entity/employee filter and no sort, so none is
 *    offered. See AdminDocumentFilterBar for why a status filter would be
 *    actively wrong here. The extraction queue has its own status filter, which
 *    is a real parameter of a real endpoint and is kept separate from this one.
 *
 *  - `itemLabelPlural="documents"` avoids "documentss" from the shared pager.
 *
 * THE WINDOW IS NOT AN UPPER BOUND ON OVERDUE HISTORY. `days` moves only the upper
 * edge of the window, and includeExpired removes the lower edge entirely rather than
 * widening it symmetrically. Switching 30 -> 60 -> 90 with overdue included returns
 * the SAME overdue rows every time and only reaches further into the future. The
 * filter bar states this in as many words, and the empty state repeats it, because
 * the alternative reading — "60 days of overdue history" — is simply not what the
 * endpoint does.
 *
 * PAGINATION IS THE BACKEND'S, WITH A DOCUMENTED CAVEAT. The registry is ordered by
 * `expiry_date ASC` with NO tie-breaker, so documents sharing an expiry date have
 * no defined relative order and a row can in principle repeat or be skipped when
 * paging. That is a backend property of a frozen .NET layer and is deliberately not
 * worked around: sorting the page that came back would not make the pagination any
 * more consistent, and inventing a secondary sort would misrepresent the server's
 * ordering. The pager uses the page/pageSize/totalCount the endpoint actually
 * returned, and no total is ever fabricated. The extraction queue, by contrast, is
 * ordered `CreatedAt DESC, Id ASC` and is stable.
 */
const PAGE_SIZE = 20

// The three audited window values. The endpoint accepts any integer 1..365, but only
// these are offered, because an arbitrary day count is not a product decision and
// would invite the reader to assume each value behaves like a distinct bucket.
const WINDOW_DAYS = [30, 60, 90]

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

function normaliseDays(value) {
  const days = Number(value)
  return WINDOW_DAYS.includes(days) ? days : 30
}

function AdminDocumentRegistry() {
  const [days, setDays] = useState(30)
  const [includeExpired, setIncludeExpired] = useState(false)
  const [page, setPage] = useState(1)
  const [selectedDocumentId, setSelectedDocumentId] = useState(null)
  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminExpiringDocuments({ days, page, pageSize: PAGE_SIZE, includeExpired })

  // Owner names are resolved from a separately cached employee map, not from the
  // registry row, which carries owner GUIDs only. A row whose owner cannot be
  // resolved is rendered as an explicitly unresolved short id rather than a guess.
  const { resolveOwner } = useAdminDocumentOwners()

  // A new filter makes the current page number meaningless: page 4 of one result
  // set is rarely page 1 of the next, and can be past its end.
  useEffect(() => {
    setPage(1)
  }, [days, includeExpired])

  function handleDaysChange(nextDays) {
    setDays(normaliseDays(nextDays))
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading documents…" />
  } else if (isError) {
    // A failed registry is an error with a retry. It is never an empty registry and
    // never a total of zero, because either would read as a real measurement.
    content = (
      <ErrorState
        message={extractApiErrorMessage(
          error,
          'Could not load the expiry registry.',
        )}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={FileText}
        message={
          includeExpired
            ? 'Nothing overdue, and nothing expiring in this window.'
            : `Nothing expires in the next ${days} days.`
        }
        description={
          includeExpired
            ? 'With overdue included, the result covers every expired document on record plus the next ' +
              `${days} days. If you expected an expired document here, note that the overdue set is not limited by the window.`
            : `Only documents expiring within the next ${days} days are listed. Widen the window to look further ahead, or include overdue documents.`
        }
      />
    )
  } else {
    content = (
      <AdminDocumentsTable
        items={items}
        resolveOwner={resolveOwner}
        onOpenDetails={setSelectedDocumentId}
      />
    )
  }

  // The count is only trustworthy once the request succeeded. While loading it would
  // be stale or zero, and on error it would be a lie, so the badge is suppressed in
  // both cases rather than rendered as zero beside a failed list.
  const showCountBadge = !isLoading && !isError

  return (
    <>
      <SectionCard
        title="Expiry & Renewal Registry"
        icon={FileText}
        subtitle="Documents approaching or past their expiry date, across every client company and legal entity. Soonest expiry first."
        badge={
          showCountBadge ? (
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
              {totalCount.toLocaleString('en-US')}
              <span className="sr-only"> documents</span>
            </span>
          ) : null
        }
      >
        <AdminDocumentFilterBar
          days={days}
          includeExpired={includeExpired}
          onDaysChange={handleDaysChange}
          onIncludeExpiredChange={setIncludeExpired}
          isFetching={isFetching && !isLoading}
        />

        {content}

        {showCountBadge && totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="document"
            itemLabelPlural="documents"
            onPageChange={setPage}
          />
        )}
      </SectionCard>

      {selectedDocumentId && (
        <AdminDocumentDetailDrawer
          documentId={selectedDocumentId}
          onClose={() => setSelectedDocumentId(null)}
        />
      )}
    </>
  )
}

/**
 * D1 — the Admin's extraction review queue: drafts awaiting confirmation,
 * across the Admin scope.
 *
 * Kept as its own card, above the registry, because a draft is not a document.
 * A confirmed draft leaves this queue and surfaces in the registry only if its
 * expiry date puts it in that window; otherwise it is reached through the client
 * entity or employee record, which is where documents already live.
 */
function AdminExtractionQueueCard() {
  const [selectedExtractionId, setSelectedExtractionId] = useState(null)

  return (
    <>
      <DocumentExtractionsQueue
        useQueue={useAdminPendingDocumentExtractions}
        onReview={setSelectedExtractionId}
        isAgent={false}
        uploadSlot={
          <DocumentUploadCard
            useExtract={useExtractAdminDocument}
            isAgent={false}
            onExtracted={setSelectedExtractionId}
          />
        }
      />

      {selectedExtractionId && (
        <AdminExtractionReviewDrawer
          extractionId={selectedExtractionId}
          onClose={() => setSelectedExtractionId(null)}
          onConfirmed={() => setSelectedExtractionId(null)}
        />
      )}
    </>
  )
}

function AdminDocumentsPage() {
  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Documents"
        subtitle="Register new documents by upload and review, and track the expiry of every document already on record. Admin is not restricted to assigned companies."
        action={ADMIN_PORTAL_BADGE}
      />
      <AdminExtractionQueueCard />
      <AdminDocumentRegistry />
    </div>
  )
}

export default AdminDocumentsPage
