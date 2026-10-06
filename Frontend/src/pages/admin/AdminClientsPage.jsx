import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Building2, Plus, ShieldCheck } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import AdminClientSearchBar from '../../components/admin/clients/AdminClientSearchBar'
import AdminClientsTable from '../../components/admin/clients/AdminClientsTable'
import AdminClientDetailDrawer from '../../components/admin/clients/AdminClientDetailDrawer'
import AdminClientCompanyFormDialog from '../../components/admin/clients/AdminClientCompanyFormDialog'
import { useAdminClients } from '../../hooks/admin/useAdminClients'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal client directory.
 *
 * Onboarding starts here: "New company" opens the create dialog, and on success
 * the new company is opened in the SAME drawer this page already uses. That is
 * the whole onboarding handoff — a new company has no entities and no contacts,
 * so leaving the Admin on a directory row they would have to find and click would
 * strand them at the one moment the next step (add an entity, add a contact) is
 * most obvious. `setSelectedClientId` is an existing prop of this page, so this
 * adds no navigation of its own.
 *
 * Scope notes that still shape this page:
 *  - No status filter. GET /admin/clients accepts only page, pageSize, search
 *    and includeDeleted, so a status control would have to filter the one page
 *    it received and silently under-report.
 *  - No sort control. The endpoint hardcodes created_at DESC, id ASC.
 *  - No "include deleted" toggle. Although the list and detail accept it, the
 *    nested entity and contact routes 404 for a soft-deleted company, so a
 *    deleted-company drawer would be mostly broken sections.
 *  - `itemLabelPlural="companies"` avoids "companys" from the shared pager.
 *
 * BACKING QUERY: GET /api/v1/admin/clients?page&pageSize&search
 * The company name is already on the row, so no entity-map resolution is used
 * here — every other Admin page resolves GUIDs because their DTOs carry only
 * ids, and this one does not.
 */

const PAGE_SIZE = 20

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

function ClientsDirectory() {
  const [searchParams] = useSearchParams()
  const urlSearch = searchParams.get('search') ?? ''

  // Two-state search: searchInput is the text box, appliedSearch is what the
  // server has been asked for. Only a submit moves one into the other.
  const [searchInput, setSearchInput] = useState(urlSearch)
  const [appliedSearch, setAppliedSearch] = useState(urlSearch)
  const [page, setPage] = useState(1)
  const [selectedClientId, setSelectedClientId] = useState(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)

  const { items, totalCount, isLoading, isError, error, refresh } = useAdminClients({
    page,
    pageSize: PAGE_SIZE,
    search: appliedSearch || undefined,
  })

  // Seed from /clients?search=<term> so the header global search hands off to
  // this list. Re-seeding on change also covers a browser back/forward, which
  // would otherwise leave the box showing a term the list is not filtered by.
  useEffect(() => {
    setSearchInput(urlSearch)
    setAppliedSearch(urlSearch)
    setPage(1)
  }, [urlSearch])

  function handleSearch() {
    setAppliedSearch(searchInput.trim())
    setPage(1)
  }

  function clearSearch() {
    setSearchInput('')
    setAppliedSearch('')
    setPage(1)
  }

  function handlePageChange(nextPage) {
    setPage(nextPage)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading client companies…" />
  } else if (isError) {
    // A failed list is an error with a retry. It is never an empty list, and
    // never a total of zero, because either would read as a real measurement.
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load client companies.')}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={Building2}
        message={appliedSearch ? 'No matching client companies.' : 'No client companies yet.'}
        description={
          appliedSearch
            ? `Nothing matches “${appliedSearch}”. The search covers company name and trade licence number only.`
            : 'Client companies will appear here as they are registered.'
        }
      />
    )
  } else {
    content = <AdminClientsTable items={items} onOpenDetails={setSelectedClientId} />
  }

  // The count is only trustworthy once the request succeeded. While loading it
  // would show a stale or zero figure, and on error it would be a lie, so the
  // badge is suppressed in both cases rather than rendered as "Unavailable"
  // next to a zero.
  const showCountBadge = !isLoading && !isError

  return (
    <>
      <SectionCard
        title="Client companies"
        icon={Building2}
        subtitle="Every company in the portfolio. Search covers company name and trade licence number."
        badge={
          showCountBadge ? (
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
              {totalCount.toLocaleString('en-US')}
              <span className="sr-only"> client companies</span>
            </span>
          ) : null
        }
        action={
          <button
            type="button"
            onClick={() => setIsCreateOpen(true)}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-2 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2"
          >
            <Plus size={14} strokeWidth={2.5} aria-hidden="true" />
            New company
          </button>
        }
      >
        <AdminClientSearchBar
          value={searchInput}
          applied={appliedSearch}
          onValueChange={setSearchInput}
          onSearch={handleSearch}
          onClear={clearSearch}
        />

        {content}

        {showCountBadge && totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="company"
            itemLabelPlural="companies"
            onPageChange={handlePageChange}
          />
        )}
      </SectionCard>

      {selectedClientId && (
        <AdminClientDetailDrawer
          clientId={selectedClientId}
          onClose={() => setSelectedClientId(null)}
        />
      )}

      {isCreateOpen && (
        <AdminClientCompanyFormDialog
          mode="create"
          onClose={() => setIsCreateOpen(false)}
          // The created record is opened in the drawer this page already owns, so
          // the next onboarding step — an entity, then a contact — is one click
          // away instead of requiring a search for a row that was just created.
          onSaved={(created) => {
            if (created?.id) setSelectedClientId(created.id)
          }}
        />
      )}
    </>
  )
}

function AdminClientsPage() {
  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Clients"
        subtitle="All client companies, their legal entities and contacts. Admin is not restricted to assigned companies."
        action={ADMIN_PORTAL_BADGE}
      />
      <ClientsDirectory />
    </div>
  )
}

export default AdminClientsPage
