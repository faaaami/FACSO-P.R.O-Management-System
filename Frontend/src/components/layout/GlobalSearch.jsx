import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { AlertCircle, Building2, Loader2, Search, X } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { useFocusTrap } from '../../hooks/useFocusTrap'
import { getAgentClients } from '../../api/agent/clients'
import { getAdminClients } from '../../api/admin/clients'
import { extractApiErrorMessage } from '../../utils/apiError'
import AgentClientDetailDrawer from '../agent/clients/AgentClientDetailDrawer'
import { ROLE_NAVIGATION } from './navigation'

const DEBOUNCE_MS = 300
const MIN_TERM_LENGTH = 2
const RESULT_PAGE_SIZE = 5

// Only Agent and Admin have a client-company text-search endpoint we may call,
// so only those roles issue a request. Client gets navigation only. The backend
// exposes no cross-entity search, so this control is deliberately scoped and its
// placeholder never implies a global search.
const CLIENT_SEARCHERS = {
  Agent: getAgentClients,
  Admin: getAdminClients,
}

const PLACEHOLDERS = {
  Agent: 'Search client companies or jump to…',
  Admin: 'Search client companies or jump to…',
  Client: 'Jump to…',
}

function buildPageOptions(role, term) {
  const items = ROLE_NAVIGATION[role] ?? []
  const needle = term.trim().toLowerCase()
  if (!needle) return items
  return items.filter(
    (item) =>
      item.name.toLowerCase().includes(needle) || item.path.toLowerCase().includes(needle)
  )
}

/**
 * The server matches company name OR trade licence number. We additionally
 * narrow on emirate/email so the dropdown never shows rows the user did not
 * obviously type. This only ever removes matches, never invents them.
 */
function matchesTerm(client, term) {
  const needle = term.trim().toLowerCase()
  if (!needle) return false
  return Boolean(
    client.companyName?.toLowerCase().includes(needle) ||
      client.tradeLicenseNumber?.toLowerCase().includes(needle) ||
      client.email?.toLowerCase().includes(needle) ||
      client.emirate?.toLowerCase().includes(needle)
  )
}

/**
 * Honest, role-scoped header search.
 *
 * Agent      -> debounced client-company lookup (GET /agent/clients?search=)
 *               plus a "Jump to" page group; a result opens the client drawer.
 * Admin      -> debounced client-company lookup (GET /admin/clients?search=)
 *               plus a "Jump to" page group; a result hands off to the Admin
 *               Clients list at /clients?search=, which owns the authoritative
 *               filtering. The Agent drawer is not reused: its endpoints are
 *               Agent-only.
 * Client     -> "Jump to" page navigation only; no request is issued.
 *
 * There is no backend global search, so this is deliberately not presented as
 * one. See PLACEHOLDERS.
 */
function GlobalSearch() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const role = user?.role
  const searchClients = CLIENT_SEARCHERS[role]
  const canSearchClients = Boolean(searchClients)
  const isAdminSearch = role === 'Admin'

  const baseId = useId()
  const statusId = `${baseId}-status`

  const [term, setTerm] = useState('')
  const [debouncedTerm, setDebouncedTerm] = useState('')
  const [isOpen, setIsOpen] = useState(false)
  const [isMobileOpen, setIsMobileOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(0)
  // Distinguishes "the first row is highlighted by default" from "the user
  // actually chose a row", so Enter can honour the one-match/many-match rule.
  const [hasExplicitSelection, setHasExplicitSelection] = useState(false)
  const [selectedClientId, setSelectedClientId] = useState(null)

  const containerRef = useRef(null)
  // The desktop input and the mobile sheet input are both mounted at the same
  // time, so a single shared ref would be nulled out when the mobile sheet
  // unmounts. Keep them separate and focus whichever one is actually visible.
  const desktopInputRef = useRef(null)
  const mobileInputRef = useRef(null)
  const debounceRef = useRef(null)
  const shouldRestoreFocusRef = useRef(false)

  const focusSearchInput = useCallback(() => {
    const target = [desktopInputRef.current, mobileInputRef.current]
      .find((node) => node && node.offsetParent !== null)
    target?.focus()
  }, [])

  const closeMobile = useCallback(() => setIsMobileOpen(false), [])
  const mobilePanelRef = useFocusTrap({ isOpen: isMobileOpen, onClose: closeMobile })

  // Debounce. No request is issued below the minimum term length.
  useEffect(() => {
    window.clearTimeout(debounceRef.current)
    const trimmed = term.trim()
    if (!canSearchClients || trimmed.length < MIN_TERM_LENGTH) {
      setDebouncedTerm('')
      return undefined
    }
    debounceRef.current = window.setTimeout(() => setDebouncedTerm(trimmed), DEBOUNCE_MS)
    return () => window.clearTimeout(debounceRef.current)
  }, [term, canSearchClients])

  const isSearchActive = canSearchClients && debouncedTerm.length >= MIN_TERM_LENGTH

  const clientSearch = useQuery({
    // The role is part of the key so the Agent and Admin result sets cache
    // separately; they are different endpoints with different visibility.
    queryKey: [
      isAdminSearch ? 'admin' : 'agent',
      'clients',
      { page: 1, pageSize: RESULT_PAGE_SIZE, search: debouncedTerm },
    ],
    queryFn: () =>
      searchClients({ page: 1, pageSize: RESULT_PAGE_SIZE, search: debouncedTerm }),
    enabled: isSearchActive && !selectedClientId,
    staleTime: 30_000,
    retry: 1,
  })

  const pageOptions = useMemo(() => buildPageOptions(role, term), [role, term])

  const clientResults = useMemo(() => {
    if (!isSearchActive) return []
    return (clientSearch.data?.items ?? []).filter((client) =>
      matchesTerm(client, debouncedTerm)
    )
  }, [isSearchActive, debouncedTerm, clientSearch.data])

  // Flat option list drives keyboard navigation and aria-activedescendant.
  const options = useMemo(() => {
    const list = []
    clientResults.forEach((client) => {
      list.push({ kind: 'client', id: `client-${client.id}`, client })
    })
    pageOptions.forEach((item) => {
      list.push({ kind: 'page', id: `page-${item.path}`, item })
    })
    return list
  }, [clientResults, pageOptions])

  const hasOptions = options.length > 0

  useEffect(() => {
    setActiveIndex(0)
    setHasExplicitSelection(false)
  }, [term, debouncedTerm])

  // Hovering a row counts as pointing at it, so Enter opens that row.
  function highlightOption(optionId) {
    const index = options.findIndex((option) => option.id === optionId)
    if (index !== -1) {
      setActiveIndex(index)
      setHasExplicitSelection(true)
    }
  }

  const closePanel = useCallback(
    ({ restoreFocus = true } = {}) => {
      setIsOpen(false)
      if (restoreFocus) {
        window.requestAnimationFrame(focusSearchInput)
      }
    },
    [focusSearchInput]
  )

  // Close on outside click.
  useEffect(() => {
    if (!isOpen) return undefined
    function handlePointerDown(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        closePanel()
      }
    }
    document.addEventListener('mousedown', handlePointerDown)
    return () => document.removeEventListener('mousedown', handlePointerDown)
  }, [isOpen, closePanel])

  // Close on route change.
  useEffect(() => {
    setIsOpen(false)
    setIsMobileOpen(false)
  }, [location.pathname, location.search])

  // Restore focus to the search input after a result drawer closes.
  useEffect(() => {
    if (selectedClientId) return
    if (shouldRestoreFocusRef.current) {
      shouldRestoreFocusRef.current = false
      window.requestAnimationFrame(focusSearchInput)
    }
  }, [selectedClientId, focusSearchInput])

  function openClient(client) {
    // Admin has no client drawer in Phase 0, and the Agent drawer is unusable
    // for an Admin because its endpoints are Agent-only. Hand the search off to
    // the Admin Clients list, which owns the authoritative filtering.
    if (isAdminSearch) {
      goToPage(`/clients?search=${encodeURIComponent(debouncedTerm)}`)
      return
    }
    shouldRestoreFocusRef.current = true
    setSelectedClientId(client.id)
    setIsOpen(false)
    setIsMobileOpen(false)
  }

  function goToPage(path) {
    setIsOpen(false)
    setIsMobileOpen(false)
    navigate(path)
  }

  function activateOption(option) {
    if (!option) return
    if (option.kind === 'client') {
      openClient(option.client)
    } else {
      goToPage(option.item.path)
    }
  }

  function handleKeyDown(event) {
    if (event.key === 'Escape') {
      event.preventDefault()
      event.stopPropagation()
      if (isOpen) {
        // 1st Escape closes the palette...
        closePanel()
      } else if (term) {
        // ...2nd Escape clears the input.
        setTerm('')
        setDebouncedTerm('')
      }
      return
    }

    if (!isOpen) {
      if (event.key === 'ArrowDown' && term.length > 0) {
        event.preventDefault()
        setIsOpen(true)
      }
      return
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setHasExplicitSelection(true)
      setActiveIndex((index) => (hasOptions ? (index + 1) % options.length : 0))
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setHasExplicitSelection(true)
      setActiveIndex((index) =>
        hasOptions ? (index - 1 + options.length) % options.length : 0
      )
    } else if (event.key === 'Enter') {
      event.preventDefault()
      const active = options[activeIndex]

      // An explicit highlight (arrow keys / hover) always wins: the user has
      // pointed at a specific row.
      if (hasExplicitSelection && active) {
        activateOption(active)
        return
      }

      // Otherwise follow the honest default for the client results:
      // exactly one match opens it, several hand off to the full list which
      // owns the authoritative filtering.
      if (clientResults.length === 1) {
        openClient(clientResults[0])
        return
      }
      if (clientResults.length > 1) {
        goToPage(`/clients?search=${encodeURIComponent(debouncedTerm)}`)
        return
      }
      if (active) {
        activateOption(active)
      }
    }
  }

  function clearAll() {
    setTerm('')
    setDebouncedTerm('')
    setActiveIndex(0)
    setHasExplicitSelection(false)
    focusSearchInput()
  }

  const placeholder = PLACEHOLDERS[role] ?? 'Jump to…'
  const showPanel = isOpen && (hasOptions || isSearchActive || term.length > 0)

  const resultSummary = isSearchActive
    ? `${clientResults.length} client ${clientResults.length === 1 ? 'company' : 'companies'} found. ${pageOptions.length} ${pageOptions.length === 1 ? 'page' : 'pages'} available.`
    : ''

  const ariaLabel = canSearchClients
    ? 'Search client companies or jump to a page'
    : 'Jump to a page'

  /**
   * Result body. `instance` keeps the listbox id unique because the desktop
   * dropdown and the mobile sheet both render this body simultaneously in the
   * DOM (only one is visible via CSS).
   */
  function renderPanel(instance) {
    const listboxId = `${baseId}-listbox-${instance}`

    return (
      <div className="flex max-h-full flex-col overflow-hidden">
          {isSearchActive && (
            <div className="border-b border-[#E2E4E9] px-3 py-2">
              {clientSearch.isLoading ? (
                <p className="flex items-center gap-2 text-xs text-[#6B7280]">
                  <Loader2 size={13} className="animate-spin" aria-hidden="true" />
                  Searching client companies…
                </p>
              ) : clientSearch.isError ? (
                <div className="space-y-1.5">
                  <p className="flex items-start gap-1.5 text-xs font-medium text-[#DC2626]">
                    <AlertCircle
                      size={13}
                      className="mt-0.5 shrink-0"
                      aria-hidden="true"
                    />
                    {extractApiErrorMessage(
                      clientSearch.error,
                      'Could not search client companies.'
                    )}
                  </p>
                  <button
                    type="button"
                    onClick={() => clientSearch.refetch()}
                    className="rounded-[6px] border border-[#E2E4E9] bg-white px-2 py-1 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
                  >
                    Retry
                  </button>
                </div>
              ) : clientResults.length === 0 ? (
                <p className="text-xs text-[#6B7280]">
                  No client companies match “{debouncedTerm}”.
                </p>
              ) : null}
            </div>
          )}

          <div
            id={listboxId}
            role="listbox"
            aria-label="Search results"
            className="flex-1 overflow-y-auto overscroll-contain p-1.5"
          >
            {clientResults.length > 0 && (
              <div className="mb-1">
                <p className="px-2 py-1 text-[10px] font-semibold uppercase tracking-wider text-[#9CA3AF]">
                  Client companies
                </p>
                {clientResults.map((client) => {
                  const optionId = `client-${client.id}`
                  const isActive = options[activeIndex]?.id === optionId
                  return (
                    <button
                      key={client.id}
                      id={optionId}
                      type="button"
                      role="option"
                      aria-selected={isActive}
                      onMouseEnter={() => highlightOption(optionId)}
                      onClick={() => openClient(client)}
                      className={`flex min-h-[44px] w-full items-center gap-2.5 rounded-[8px] px-2 py-2 text-left transition duration-150 focus:outline-none ${
                        isActive ? 'bg-[#F7F8FA]' : 'hover:bg-[#F7F8FA]'
                      }`}
                    >
                      <Building2
                        size={15}
                        className="shrink-0 text-[#6B7280]"
                        aria-hidden="true"
                      />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-semibold text-[#16181D]">
                          {client.companyName}
                        </span>
                        <span className="block truncate text-xs text-[#6B7280]">
                          {client.tradeLicenseNumber || client.emirate || '—'}
                        </span>
                      </span>
                    </button>
                  )
                })}
              </div>
            )}

            {pageOptions.length > 0 && (
              <div>
                <p className="px-2 py-1 text-[10px] font-semibold uppercase tracking-wider text-[#9CA3AF]">
                  Jump to
                </p>
                {pageOptions.map((item) => {
                  const Icon = item.icon
                  const optionId = `page-${item.path}`
                  const isActive = options[activeIndex]?.id === optionId
                  return (
                    <button
                      key={item.path}
                      id={optionId}
                      type="button"
                      role="option"
                      aria-selected={isActive}
                      onMouseEnter={() => highlightOption(optionId)}
                      onClick={() => goToPage(item.path)}
                      className={`flex min-h-[44px] w-full items-center gap-2.5 rounded-[8px] px-2 py-2 text-left transition duration-150 focus:outline-none ${
                        isActive ? 'bg-[#F7F8FA]' : 'hover:bg-[#F7F8FA]'
                      }`}
                    >
                      <Icon
                        size={15}
                        className="shrink-0 text-[#6B7280]"
                        aria-hidden="true"
                      />
                      <span className="min-w-0 flex-1 truncate text-sm text-[#16181D]">
                        {item.name}
                      </span>
                    </button>
                  )
                })}
              </div>
            )}

            {!hasOptions && !isSearchActive && term.trim().length > 0 && (
              <p className="px-2 py-4 text-center text-xs text-[#6B7280]">
                No pages match “{term.trim()}”.
              </p>
            )}
          </div>
        </div>
    )
  }

  const activeDescendant =
    showPanel && !isMobileOpen && hasOptions ? options[activeIndex]?.id : undefined

  return (
    <>
      <button
        type="button"
        onClick={() => setIsMobileOpen(true)}
        aria-label="Open search"
        className="rounded-[8px] p-2 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer sm:hidden"
      >
        <Search size={20} aria-hidden="true" />
      </button>

      <div
        ref={containerRef}
        role="search"
        className="relative hidden max-w-md flex-1 sm:block"
      >
        <label htmlFor={`${baseId}-input`} className="sr-only">
          {ariaLabel}
        </label>
        <div className="relative">
          <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#9CA3AF]">
            <Search size={16} aria-hidden="true" />
          </div>
          <input
            ref={desktopInputRef}
            id={`${baseId}-input`}
            type="text"
            inputMode="search"
            role="combobox"
            autoComplete="off"
            aria-expanded={showPanel}
            aria-controls={`${baseId}-listbox-desktop`}
            aria-autocomplete="list"
            aria-activedescendant={activeDescendant}
            aria-describedby={statusId}
            value={term}
            onChange={(event) => {
              setTerm(event.target.value)
              setIsOpen(true)
            }}
            onFocus={() => setIsOpen(true)}
            onKeyDown={handleKeyDown}
            placeholder={placeholder}
            className="w-full rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] py-2 pl-10 pr-9 text-sm text-[#16181D] transition duration-150 placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:bg-white focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          />
          {term && (
            <button
              type="button"
              onClick={clearAll}
              aria-label="Clear search"
              className="absolute inset-y-0 right-0 flex items-center px-3 text-[#9CA3AF] transition duration-150 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-inset focus:ring-[#0F9D74] cursor-pointer"
            >
              <X size={15} aria-hidden="true" />
            </button>
          )}
        </div>

        {/* Rendered only when the mobile sheet is closed: the two surfaces are
            never visible at the same width, and rendering both would duplicate
            every option id, making aria-activedescendant ambiguous. */}
        {showPanel && !isMobileOpen && (
          <div className="absolute left-0 right-0 top-full z-50 mt-2 max-h-[70vh] overflow-hidden rounded-[12px] border border-[#E2E4E9] bg-white shadow-[0_8px_24px_rgba(28,31,38,0.10)]">
            {renderPanel('desktop')}
          </div>
        )}
      </div>

      {isMobileOpen && (
        <div className="fixed inset-0 z-50 sm:hidden">
          <div
            className="fixed inset-0 bg-slate-900/40"
            onClick={closeMobile}
            aria-hidden="true"
          />
          <div
            ref={mobilePanelRef}
            role="dialog"
            aria-modal="true"
            aria-label="Search"
            tabIndex={-1}
            className="fixed inset-0 flex flex-col bg-white focus:outline-none"
          >
            <div className="flex items-center gap-2 border-b border-[#E2E4E9] p-3">
              <div className="relative flex-1">
                <label htmlFor={`${baseId}-input-mobile`} className="sr-only">
                  {ariaLabel}
                </label>
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 text-[#9CA3AF]">
                  <Search size={16} aria-hidden="true" />
                </div>
                <input
                  ref={mobileInputRef}
                  id={`${baseId}-input-mobile`}
                  type="text"
                  inputMode="search"
                  role="combobox"
                  autoComplete="off"
                  aria-expanded
                  aria-controls={`${baseId}-listbox-mobile`}
                  aria-activedescendant={
                    hasOptions ? options[activeIndex]?.id : undefined
                  }
                  aria-describedby={statusId}
                  value={term}
                  onChange={(event) => {
                    setTerm(event.target.value)
                    setIsOpen(true)
                  }}
                  onKeyDown={handleKeyDown}
                  placeholder={placeholder}
                  className="w-full rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] py-2 pl-10 pr-10 text-base text-[#16181D] transition duration-150 placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:bg-white focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                />
                {term && (
                  <button
                    type="button"
                    onClick={clearAll}
                    aria-label="Clear search"
                    className="absolute inset-y-0 right-0 flex min-h-[44px] w-11 items-center justify-center text-[#9CA3AF] transition duration-150 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-inset focus:ring-[#0F9D74] cursor-pointer"
                  >
                    <X size={16} aria-hidden="true" />
                  </button>
                )}
              </div>
              <button
                type="button"
                onClick={closeMobile}
                aria-label="Close search"
                className="flex h-11 w-11 shrink-0 items-center justify-center rounded-[8px] text-[#6B7280] transition duration-150 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
              >
                <X size={20} aria-hidden="true" />
              </button>
            </div>
            <div className="flex-1 overflow-hidden">{renderPanel('mobile')}</div>
          </div>
        </div>
      )}

      <span id={statusId} role="status" aria-live="polite" className="sr-only">
        {resultSummary}
      </span>

      {selectedClientId && (
        <AgentClientDetailDrawer
          clientId={selectedClientId}
          onClose={() => setSelectedClientId(null)}
        />
      )}
    </>
  )
}

export default GlobalSearch
