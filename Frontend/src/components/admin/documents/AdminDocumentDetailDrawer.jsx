import { useEffect, useId, useRef, useState } from 'react'
import { FileText, History, Link2, UserRound, X } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminDocument } from '../../../hooks/admin/useAdminDocuments'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminDocumentOverviewSection from './AdminDocumentOverviewSection'
import AdminDocumentOwnerSection from './AdminDocumentOwnerSection'
import AdminDocumentVersionsSection from './AdminDocumentVersionsSection'
import AdminDocumentDependenciesSection from './AdminDocumentDependenciesSection'
import { documentTitle, documentTypeLabel, storedStatusLabel, storedStatusTone } from './documentDisplay'

/**
 * Admin document detail drawer.
 *
 * Built on the shared useFocusTrap exactly as the Admin Employees drawer is, so
 * Escape, bidirectional Tab wrap, focus-on-open, focus restoration to the triggering
 * row and body scroll lock behave identically across the Admin modules. The hook is
 * used unmodified; overlay click-to-close is wired here because the hook does not
 * provide it.
 *
 * ONE PANEL, ONE FOCUS TRAP. There is no nested drawer: the file opens in a new
 * browser tab from the Overview tab, and every other destination is either a
 * section in this panel or a link out.
 *
 * FOUR TABS, LAZILY FETCHED. The document detail loads on open because the header
 * needs the type, number and status. Owner, Versions and Dependencies each fetch
 * the first time their tab is activated and are then cached, so a user who only
 * wants the record never pays for the owner lookup — which is the more expensive
 * of the three, since it can chain an employee detail and a company detail.
 *
 * THE HEADER OWNED FAILURE IS TOTAL. With no document number, type or status there
 * is nothing coherent to render above the tabs, so a failed detail request is a
 * full-drawer error with a retry. The Owner and Versions tabs then each fail
 * independently and leave the rest of the drawer usable.
 *
 * THE HEADER STATUS IS THE DETAIL ENDPOINT'S, not the registry's. The pill here
 * reads "Overdue" where the table behind it reads "Expired" — both preserved from
 * their own endpoints, neither converted. The Overview tab carries the visible
 * provenance note explaining the difference.
 *
 * aria-labelledby on the tabpanel points at the active tab, but the tablist only
 * exists once the detail has loaded. During the first load and on error the panel is
 * labelled by the drawer heading instead, so it never references an element that is
 * not in the DOM.
 */
const TABS = [
  { key: 'overview', label: 'Overview', icon: FileText },
  { key: 'owner', label: 'Owner', icon: UserRound },
  { key: 'versions', label: 'Versions', icon: History },
  { key: 'dependencies', label: 'Dependencies', icon: Link2 },
]

function AdminDocumentDetailDrawer({ documentId, onClose }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeTab, setActiveTab] = useState('overview')
  const tabRefs = useRef([])

  const { data: document, isLoading, isError, error, refresh } =
    useAdminDocument(documentId)

  // A different document means different tabs' worth of cached data, so the tab
  // resets with it.
  useEffect(() => {
    setActiveTab('overview')
  }, [documentId])

  const title = documentTitle(document)
  const type = documentTypeLabel(document?.type)

  /**
   * Arrow-key navigation for the tablist. A tablist that only responds to clicks is
   * unusable by keyboard: Left/Right must move between tabs, Home/End must jump to
   * the ends, and focus must follow the selection.
   */
  function handleTabKeyDown(event) {
    const currentIndex = TABS.findIndex((tab) => tab.key === activeTab)
    let nextIndex = null

    if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % TABS.length
    else if (event.key === 'ArrowLeft') {
      nextIndex = (currentIndex - 1 + TABS.length) % TABS.length
    } else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = TABS.length - 1

    if (nextIndex == null) return

    event.preventDefault()
    setActiveTab(TABS[nextIndex].key)
    tabRefs.current[nextIndex]?.focus()
  }

  function renderTab() {
    if (activeTab === 'owner') {
      return <AdminDocumentOwnerSection document={document} />
    }
    if (activeTab === 'versions') {
      return <AdminDocumentVersionsSection documentId={documentId} />
    }
    if (activeTab === 'dependencies') {
      return <AdminDocumentDependenciesSection documentId={documentId} />
    }
    return <AdminDocumentOverviewSection document={document} />
  }

  return (
    <div className="fixed inset-0 z-50">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={onClose}
        aria-hidden="true"
      />

      <aside
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className="fixed inset-y-0 right-0 z-50 flex w-full max-w-[680px] flex-col border-l border-[#E2E4E9] bg-white shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none"
      >
        <div className="flex items-start justify-between gap-4 border-b border-[#E2E4E9] p-5 sm:p-6">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-[#F7F8FA] text-[#16181D]">
              <FileText size={18} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2
                id={titleId}
                className="break-words text-base font-semibold tracking-tight text-[#16181D]"
              >
                {isLoading && !document ? 'Loading document…' : title}
              </h2>
              {document && (
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                  <StatusPill
                    label={storedStatusLabel(document?.status) ?? 'Unknown'}
                    tone={storedStatusTone(document?.status)}
                  />
                  {type && (
                    <span className="min-w-0 break-words text-xs text-[#6B7280]">
                      {type}
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={`Close ${title} details`}
            className="shrink-0 cursor-pointer rounded-[8px] p-1.5 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        {!isLoading && !isError && (
          <div
            role="tablist"
            aria-label="Document sections"
            onKeyDown={handleTabKeyDown}
            className="flex gap-1 overflow-x-auto border-b border-[#E2E4E9] px-3 py-2 sm:px-4"
          >
            {TABS.map((tab, index) => {
              const Icon = tab.icon
              const isActive = tab.key === activeTab

              return (
                <button
                  key={tab.key}
                  ref={(node) => {
                    tabRefs.current[index] = node
                  }}
                  type="button"
                  role="tab"
                  id={`${titleId}-tab-${tab.key}`}
                  aria-selected={isActive}
                  aria-controls={`${titleId}-panel`}
                  tabIndex={isActive ? 0 : -1}
                  onClick={() => setActiveTab(tab.key)}
                  className={`inline-flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-[8px] px-2.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] ${
                    isActive
                      ? 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74]'
                      : 'text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D]'
                  }`}
                >
                  <Icon size={14} strokeWidth={1.75} aria-hidden="true" />
                  {tab.label}
                </button>
              )
            })}
          </div>
        )}

        <div
          id={`${titleId}-panel`}
          role="tabpanel"
          aria-labelledby={
            isLoading || isError ? titleId : `${titleId}-tab-${activeTab}`
          }
          tabIndex={0}
          className="flex-1 overflow-y-auto p-5 sm:p-6"
        >
          {isLoading && <LoadingState label="Loading document…" />}

          {isError && (
            <ErrorState
              message={extractApiErrorMessage(
                error,
                'Could not load this document.',
              )}
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && (
            <div className="flex flex-col gap-6">
              {document?.isDeleted && (
                <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
                  This document is soft-deleted. Its file cannot be opened, because
                  the scan route rejects a deleted document.
                </p>
              )}

              {renderTab()}
            </div>
          )}
        </div>
      </aside>
    </div>
  )
}

export default AdminDocumentDetailDrawer
