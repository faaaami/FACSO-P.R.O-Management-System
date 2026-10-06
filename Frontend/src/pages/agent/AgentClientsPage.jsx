import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Building2, Mail, MapPin, Phone, Search } from 'lucide-react'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import StatusPill from '../../components/client/StatusPill'
import Pagination from '../../components/client/billing/Pagination'
import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import AgentClientDetailDrawer from '../../components/agent/clients/AgentClientDetailDrawer'
import { useAgentClients } from '../../hooks/agent/useAgentClients'

const PAGE_SIZE = 20

const PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" aria-hidden="true" />
    Agent Portal
  </span>
)

/* -------------------------------------------------------------------------
 * 1. PAGE HEADER
 * ------------------------------------------------------------------------- */
function AgentClientsHeader() {
  return (
    <AgentPageHeader
      title="Clients"
      badge={PORTAL_BADGE}
      description="Client companies in your portfolio. Search by company name or trade licence number."
    />
  )
}

/* -------------------------------------------------------------------------
 * 2. CLIENTS DIRECTORY
 * ------------------------------------------------------------------------- */
function ClientsDirectoryCard() {
  const [searchParams] = useSearchParams()
  const urlSearch = searchParams.get('search') ?? ''

  const [searchInput, setSearchInput] = useState(urlSearch)
  const [appliedSearch, setAppliedSearch] = useState(urlSearch)
  const [page, setPage] = useState(1)
  const [selectedClientId, setSelectedClientId] = useState(null)
  const { items, totalCount, isLoading, isError, refresh } = useAgentClients({
    page,
    pageSize: PAGE_SIZE,
    search: appliedSearch || undefined,
  })

  // Seed the page-level search from /clients?search=<term> so the header
  // palette can hand off to the full list. The page search remains the source
  // of truth for the list itself.
  useEffect(() => {
    setSearchInput(urlSearch)
    setAppliedSearch(urlSearch)
    setPage(1)
  }, [urlSearch])

  const handleSearch = (event) => {
    event.preventDefault()
    setAppliedSearch(searchInput.trim())
    setPage(1)
  }

  const clearSearch = () => {
    setSearchInput('')
    setAppliedSearch('')
    setPage(1)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading client companies…" />
  } else if (isError) {
    content = <ErrorState message="Could not load client companies." onRetry={() => refresh()} />
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={Building2}
        message={appliedSearch ? 'No matching companies.' : 'No client companies yet.'}
        description={
          appliedSearch
            ? 'Try a different search term.'
            : 'Client companies linked to your assigned renewal tasks will appear here.'
        }
      />
    )
  } else {
    content = (
      <ul className="divide-y divide-[#E2E4E9]">
        {items.map((company) => (
          <li
            key={company.id}
            className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between"
          >
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <Building2 size={15} className="shrink-0 text-[#6B7280]" aria-hidden="true" />
                <p className="truncate text-sm font-semibold text-[#16181D]">
                  {company.companyName}
                </p>
              </div>
              <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-[#6B7280]">
                {company.tradeLicenseNumber && (
                  <span className="font-mono">{company.tradeLicenseNumber}</span>
                )}
                {company.emirate && (
                  <span className="inline-flex items-center gap-1">
                    <MapPin size={12} aria-hidden="true" />
                    {company.emirate}
                  </span>
                )}
                {company.email && (
                  <span className="inline-flex max-w-[220px] items-center gap-1 truncate">
                    <Mail size={12} aria-hidden="true" />
                    {company.email}
                  </span>
                )}
                {company.phone && (
                  <span className="inline-flex items-center gap-1">
                    <Phone size={12} aria-hidden="true" />
                    {company.phone}
                  </span>
                )}
              </div>
            </div>
            <div className="flex shrink-0 flex-wrap items-center gap-2 self-start sm:self-center">
              <StatusPill
                label={company.isActive ? 'Active' : 'Inactive'}
                tone={company.isActive ? 'success' : 'neutral'}
              />
              <button
                type="button"
                onClick={() => setSelectedClientId(company.id)}
                className="rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
              >
                Details
                <span className="sr-only"> for {company.companyName}</span>
              </button>
            </div>
          </li>
        ))}
      </ul>
    )
  }

  return (
    <>
      <SectionCard
        title="Client Companies"
        icon={Building2}
        subtitle="Companies linked to your assigned renewal tasks"
        badge={
          <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280]">
            {totalCount}
          </span>
        }
      >
        <form
          onSubmit={handleSearch}
          className="mb-4 flex flex-wrap items-center gap-2"
          role="search"
        >
          <div className="relative min-w-[220px] flex-1">
            <label htmlFor="agentClientSearch" className="sr-only">
              Search client companies
            </label>
            <Search
              size={15}
              className="absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]"
              aria-hidden="true"
            />
            <input
              id="agentClientSearch"
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Search by company name or trade licence number…"
              className="w-full rounded-[8px] border border-[#E2E4E9] bg-white py-2 pl-9 pr-3 text-sm text-[#16181D] transition duration-150 placeholder:text-[#9CA3AF] hover:border-[#D5D8DE] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            />
          </div>
          <button
            type="submit"
            className="shrink-0 rounded-[8px] bg-[#0F9D74] px-3.5 py-2 text-sm font-semibold text-white transition duration-150 hover:bg-[#0B7A5B] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.3)] cursor-pointer"
          >
            Search
          </button>
          {appliedSearch && (
            <button
              type="button"
              onClick={clearSearch}
              className="shrink-0 rounded-[8px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.3)] cursor-pointer"
            >
              Clear
            </button>
          )}
        </form>
        {content}
        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="company"
            onPageChange={setPage}
          />
        )}
      </SectionCard>

      {selectedClientId && (
        <AgentClientDetailDrawer
          clientId={selectedClientId}
          onClose={() => setSelectedClientId(null)}
        />
      )}
    </>
  )
}

/* -------------------------------------------------------------------------
 * PAGE
 * ------------------------------------------------------------------------- */
function AgentClientsPage() {
  return (
    <div className="space-y-6">
      <AgentClientsHeader />
      <ClientsDirectoryCard />
    </div>
  )
}

export default AgentClientsPage
