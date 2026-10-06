import { useEffect, useState } from 'react'
import { Landmark, Plus, Search } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import Pagination from '../../client/billing/Pagination'
import AdminClientSection, { AdminClientRecordCard, AdminClientRecordList } from './AdminClientSection'
import { useAdminClientEntities } from '../../../hooks/admin/useAdminClients'
import {
  MISSING_VALUE,
  displayText,
  presentText,
  recordStatusLabel,
  recordStatusTone,
} from './clientDisplay'

const PAGE_SIZE = 10

/**
 * Legal entities of the selected company.
 * Backing query GET /api/v1/admin/clients/{clientId}/entities
 *
 * The entity DTO is thin and this list does not pretend otherwise: it carries
 * entityName, tradeLicenseNumber, emirate, isActive, isDeleted and createdAt.
 * There is no address, email, phone, establishment card or licence expiry date,
 * so none of those are rendered.
 *
 * Search is the same two-state pattern as the page: the server filters on entity
 * name or trade licence, and typing alone does not fire a request. The form is
 * passed as the section toolbar rather than inside the body, so it stays
 * reachable when the result set is empty or the request has failed.
 *
 * `refetching` lets an in-flight refetch after a search settle without blanking
 * a list the user is already reading, while the first load still shows a spinner.
 *
 * THE ADD CONTROL IS OFFERED IN EXACTLY ONE PLACE AT A TIME. With rows it is the
 * header action; with none it is the empty-state CTA, and a search that matched
 * nothing gets neither — "Add entity" beside "no matching entities" would push
 * the Admin to create a duplicate of something that already exists, which is
 * exactly the mistake a too-narrow search invites. An entity cannot be created
 * here at all while a search term is applied, so the way out is Clear.
 *
 * The dialog is opened by the drawer, not by this section: it renders as a
 * sibling of the drawer's panel so it stacks above it. See the drawer's header.
 */
function AdminClientEntitiesSection({ clientId, onSelectEntity, onAddEntity }) {
  const [searchInput, setSearchInput] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)

  const { items, totalCount, isLoading, isFetching, isError, error, refresh } =
    useAdminClientEntities(clientId, {
      page,
      pageSize: PAGE_SIZE,
      search: appliedSearch || undefined,
    })

  // A different company means the previous page and term describe nothing.
  useEffect(() => {
    setSearchInput('')
    setAppliedSearch('')
    setPage(1)
  }, [clientId])

  // TanStack Query v5 defines isLoading as `isPending && isFetching`, i.e. it
  // is already true only when there is no data yet, so it is the correct spinner
  // condition. The separate isFetching check below is the opposite case: a
  // background refetch that must not blank a list the user is already reading.
  const showSpinner = isLoading
  const showRefetchHint = isFetching && !isLoading
  const hasSearched = Boolean(appliedSearch)
  const isEmpty = !isLoading && !isError && items.length === 0

  // A genuinely empty company, as opposed to one whose entities are filtered out
  // of view by the search currently applied.
  const isGenuinelyEmpty = isEmpty && !hasSearched

  const addButton = onAddEntity ? (
    <button
      type="button"
      onClick={onAddEntity}
      className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
    >
      <Plus size={13} strokeWidth={2.5} aria-hidden="true" />
      Add entity
    </button>
  ) : null

  function handleSubmit(event) {
    event.preventDefault()
    setAppliedSearch(searchInput.trim())
    setPage(1)
  }

  function clearSearch() {
    setSearchInput('')
    setAppliedSearch('')
    setPage(1)
  }

  const searchForm = (
    <form onSubmit={handleSubmit} role="search" className="flex flex-wrap items-center gap-2">
      <div className="relative min-w-[180px] flex-1">
        <label htmlFor="adminEntitySearch" className="sr-only">
          Search this company's legal entities
        </label>
        <Search
          size={14}
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]"
          aria-hidden="true"
        />
        <input
          id="adminEntitySearch"
          type="search"
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder="Search entities…"
          className="w-full rounded-[10px] border border-[#E2E4E9] bg-white py-1.5 pl-9 pr-3 text-sm text-[#16181D] transition duration-150 placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
        />
      </div>
      <button
        type="submit"
        className="shrink-0 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
      >
        Search
      </button>
      {hasSearched && (
        <button
          type="button"
          onClick={clearSearch}
          className="shrink-0 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
        >
          Clear
          <span className="sr-only"> entity search</span>
        </button>
      )}
    </form>
  )

  return (
    <AdminClientSection
      title="Legal entities"
      description="Trading entities registered under this company. Search covers entity name and trade licence number."
      toolbar={searchForm}
      // Rows on screen means the header carries the action; no rows (and no
      // search hiding them) means the empty state carries it. Never both.
      action={!isEmpty ? addButton : null}
      emptyAction={isGenuinelyEmpty ? addButton : null}
      loading={showSpinner}
      loadingLabel="Loading legal entities…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load the legal entities for this company."
      isEmpty={isEmpty}
      emptyMessage={hasSearched ? 'No matching legal entities.' : 'No legal entities on file.'}
      emptyDescription={
        hasSearched
          ? `Nothing matches “${appliedSearch}”. The search covers entity name and trade licence number only.`
          : 'Entities registered against this company will appear here. A company does not need one to onboard a contact.'
      }
      emptyIcon={Landmark}
    >
      <div className="flex flex-col gap-4">
        {showRefetchHint && (
          <p className="text-xs text-[#6B7280]" role="status" aria-live="polite">
            Updating results…
          </p>
        )}

        <AdminClientRecordList items={items}>
          {(entity) => {
            const name = presentText(entity?.entityName) ?? 'Unnamed entity'
            return (
              <AdminClientRecordCard
                key={entity?.id}
                title={name}
                trailing={
                  <StatusPill
                    label={recordStatusLabel(entity)}
                    tone={recordStatusTone(entity)}
                  />
                }
                meta={[
                  {
                    label: 'Trade licence',
                    value: presentText(entity?.tradeLicenseNumber) ?? MISSING_VALUE,
                  },
                  { label: 'Emirate', value: displayText(entity?.emirate) },
                ]}
                onOpen={() => onSelectEntity(entity?.id, name)}
                openLabel="View entity"
              />
            )
          }}
        </AdminClientRecordList>

        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="entity"
            itemLabelPlural="entities"
            onPageChange={setPage}
          />
        )}
      </div>
    </AdminClientSection>
  )
}

export default AdminClientEntitiesSection
