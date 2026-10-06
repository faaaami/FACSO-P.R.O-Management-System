import { useEffect, useState } from 'react'
import { Building2, Search } from 'lucide-react'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import StatusPill from '../../client/StatusPill'
import Pagination from '../../client/billing/Pagination'
import { useAgentClientEntities } from '../../../hooks/agent/useAgentClient'
import { extractApiErrorMessage } from '../../../utils/apiError'

const PAGE_SIZE = 10

/**
 * Paginated, searchable list of a client company's legal entities as visible to
 * the Agent.
 *
 * Scope honesty: the backend only returns entities that are reachable from the
 * Agent's assigned renewal tasks via a linked document. An empty result
 * therefore does NOT mean the company has no entities, so the empty copy must
 * never claim that.
 */
function AgentClientEntitiesSection({ clientId }) {
  const [page, setPage] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')

  const { items, totalCount, isLoading, isError, error, refresh } =
    useAgentClientEntities(clientId, {
      page,
      pageSize: PAGE_SIZE,
      search: appliedSearch || undefined,
    })

  // A new search term invalidates the current page offset.
  useEffect(() => {
    setPage(1)
  }, [appliedSearch])

  function handleSearch(event) {
    event.preventDefault()
    setAppliedSearch(searchInput.trim())
  }

  function clearSearch() {
    setSearchInput('')
    setAppliedSearch('')
    setPage(1)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading entities…" />
  } else if (isError) {
    content = (
      <ErrorState
        message={extractApiErrorMessage(
          error,
          'Could not load entities for this client.'
        )}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={Building2}
        message="No linked entities"
        description={
          appliedSearch
            ? 'No entities match your search. Entities appear when your assigned renewal tasks reference their documents.'
            : 'Entities appear when your assigned renewal tasks reference their documents.'
        }
      />
    )
  } else {
    content = (
      <>
        <ul className="divide-y divide-[#E2E4E9]">
          {items.map((entity) => (
            <li
              key={entity.id}
              className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between"
            >
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <Building2
                    size={15}
                    className="shrink-0 text-[#6B7280]"
                    aria-hidden="true"
                  />
                  <p className="truncate text-sm font-semibold text-[#16181D]">
                    {entity.entityName}
                  </p>
                </div>
                <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-[#6B7280]">
                  {entity.tradeLicenseNumber && (
                    <span className="font-mono">{entity.tradeLicenseNumber}</span>
                  )}
                  {entity.emirate && <span>{entity.emirate}</span>}
                </div>
              </div>
              <div className="shrink-0 self-start sm:self-center">
                <StatusPill
                  label={entity.isActive ? 'Active' : 'Inactive'}
                  tone={entity.isActive ? 'success' : 'neutral'}
                />
              </div>
            </li>
          ))}
        </ul>

        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="entity"
            onPageChange={setPage}
          />
        )}
      </>
    )
  }

  return (
    <section aria-labelledby="agent-client-entities-heading" className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <h3
          id="agent-client-entities-heading"
          className="text-sm font-semibold text-[#16181D]"
        >
          Legal Entities
        </h3>
        <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280]">
          {totalCount}
        </span>
      </div>

      <form
        onSubmit={handleSearch}
        role="search"
        className="flex flex-wrap items-center gap-2"
      >
        <div className="relative min-w-[180px] flex-1">
          <label htmlFor="agentEntitySearch" className="sr-only">
            Search legal entities by name or trade licence number
          </label>
          <Search
            size={14}
            className="absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]"
            aria-hidden="true"
          />
          <input
            id="agentEntitySearch"
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Search entities…"
            className="w-full rounded-[8px] border border-[#E2E4E9] bg-white py-1.5 pl-9 pr-3 text-sm text-[#16181D] transition duration-150 placeholder:text-[#9CA3AF] hover:border-[#D5D8DE] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          />
        </div>
        <button
          type="submit"
          className="shrink-0 rounded-[8px] bg-[#0F9D74] px-3 py-1.5 text-xs font-semibold text-white transition duration-150 hover:bg-[#0B7A5B] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.3)] cursor-pointer"
        >
          Search
        </button>
        {appliedSearch && (
          <button
            type="button"
            onClick={clearSearch}
            className="shrink-0 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.3)] cursor-pointer"
          >
            Clear
          </button>
        )}
      </form>

      {content}
    </section>
  )
}

export default AgentClientEntitiesSection
