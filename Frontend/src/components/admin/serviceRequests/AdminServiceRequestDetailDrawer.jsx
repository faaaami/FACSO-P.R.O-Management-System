import { useEffect, useId, useRef, useState } from 'react'
import { ArrowRightCircle, Ban, Building2, FileText, History, Inbox, X } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminServiceRequest } from '../../../hooks/admin/useAdminServiceRequests'
import { useAdminServiceRequestSubjects } from '../../../hooks/admin/useAdminServiceRequestSubjects'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminServiceRequestOverviewSection from './AdminServiceRequestOverviewSection'
import AdminServiceRequestSubjectSection from './AdminServiceRequestSubjectSection'
import AdminServiceRequestLifecycleSection from './AdminServiceRequestLifecycleSection'
import AdminServiceRequestActivitySection from './AdminServiceRequestActivitySection'
import AdminServiceRequestDecisionDialog from './AdminServiceRequestDecisionDialog'
import {
  serviceRequestStatusText,
  serviceRequestStatusTone,
  serviceRequestTypeText,
  shortGuid,
} from './serviceRequestDisplay'

/**
 * Admin service-request detail drawer.
 *
 * Built on the shared useFocusTrap exactly as the Admin Tasks, Documents and
 * Employees drawers are, so Escape, bidirectional Tab wrap, focus-on-open, focus
 * restoration to the triggering row and body scroll lock behave identically
 * across the Admin modules. The hook is used unmodified; overlay click-to-close is
 * wired here because the hook does not provide it.
 *
 * ONE PANEL, ONE SCROLL CONTAINER. There is no nested drawer and no second tab
 * panel: the decision dialog is a modal, not a pane, and the focus trap already
 * keeps a modal stack so only the topmost surface answers Escape.
 *
 * FOUR TABS, THREE OF THEM LAZY. The detail loads on open because the header shows
 * the status and the type. Subject resolves its company-scoped entity and its
 * linked document only while the Subject tab is open, and Activity mounts its
 * audit query only when that tab is first activated — so opening the drawer costs
 * exactly one request. The lazy behaviour is driven by passing the active tab into
 * the queries' `enabled` rather than by skipping the hook, which a component
 * cannot legally do.
 *
 * THE DECISION CONTROLS LIVE IN A FOOTER, NOT A FIFTH TAB. The two decisions are the
 * only writes this module has, they apply to the request as a whole rather than to
 * any one section of it, and they must stay reachable from whichever tab the Admin
 * happens to be reading. A footer bar below the tab panel is rendered only while the
 * request is still Submitted, which is exactly the condition under which both
 * handlers will accept it.
 *
 * CONVERSION IS DISABLED, WITH A STATED REASON, WHEN NO DOCUMENT IS LINKED. The
 * detail DTO carries documentId, and ConvertServiceRequestCommandHandler refuses
 * with 409 "No document linked to this request." when it is absent. Hiding that
 * would strand the Admin on a button that always fails; enabling it would offer an
 * action that cannot succeed. The bar says which of the two it is, and the server
 * remains the authority — a request that has a document but whose document has since
 * been deactivated still fails, and that failure is reported from the dialog.
 *
 * THE HEADER'S FAILURE IS TOTAL. With no status or type there is nothing coherent
 * to render above the tabs, so a failed detail request is a full-drawer error with a
 * retry and the tablist is not rendered at all. Subject and Activity then each fail
 * on their own terms and leave the rest of the drawer usable.
 *
 * aria-labelledby on the tabpanel points at the active tab, but the tablist only
 * exists once the detail has loaded. During the first load and on error the panel
 * is labelled by the drawer heading instead, so it never references an element that
 * is not in the DOM.
 */
const TABS = [
  { key: 'overview', label: 'Overview', icon: FileText },
  { key: 'subject', label: 'Subject', icon: Building2 },
  { key: 'lifecycle', label: 'Lifecycle', icon: History },
  { key: 'activity', label: 'Activity (audit log)', icon: Inbox },
]

function AdminServiceRequestDetailDrawer({ requestId, onClose }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeTab, setActiveTab] = useState('overview')
  const [decisionMode, setDecisionMode] = useState(null)
  const tabRefs = useRef([])

  const { data: request, isLoading, isError, error, refresh } = useAdminServiceRequest(requestId)

  /**
   * Subject resolution is scoped to the Subject tab. Passing the active tab rather
   * than a constant keeps the hook call unconditional while its two extra queries
   * stay disabled until something can actually display them.
   */
  const subjects = useAdminServiceRequestSubjects(request, {
    detailLookups: activeTab === 'subject',
  })

  // A different request means different tabs' worth of cached data, so the tab
  // resets with it — and so does any half-open decision dialog, which would
  // otherwise still be pointed at the previous request.
  useEffect(() => {
    setActiveTab('overview')
    setDecisionMode(null)
  }, [requestId])

  const statusLabel = serviceRequestStatusText(request?.status) ?? 'Unknown'
  const typeLabel = serviceRequestTypeText(request?.type) ?? 'Service request'
  const heading = request ? `${typeLabel} · ${shortGuid(request.id) ?? ''}`.trim() : 'Loading request…'

  /**
   * Both handlers read only a Submitted request, so this is the only state in which
   * the footer exists. The server re-checks it under a row lock; this is the
   * client-side half of the same rule, so the controls are absent rather than
   * present-and-failing after a decision.
   */
  const isDecidable = request?.status === 'Submitted'
  const hasLinkedDocument = Boolean(request?.documentId)


  /**
   * Arrow-key navigation for the tablist. A tablist that only responds to clicks is
   * unusable by keyboard: Left/Right move between tabs, Home/End jump to the ends,
   * and focus follows the selection.
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

  /**
   * Only the active tab is mounted, which is what makes the lazy fetching lazy: the
   * Subject and Activity sections resolve their own data, so an unvisited tab issues
   * no request at all.
   */
  function renderTab() {
    if (activeTab === 'subject') {
      return <AdminServiceRequestSubjectSection request={request} subjects={subjects} />
    }

    if (activeTab === 'lifecycle') {
      return <AdminServiceRequestLifecycleSection request={request} />
    }

    if (activeTab === 'activity') {
      return <AdminServiceRequestActivitySection requestId={request.id} />
    }

    return <AdminServiceRequestOverviewSection request={request} />
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
                {isLoading && !request ? 'Loading request…' : heading}
              </h2>
              {request && (
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                  <StatusPill label={statusLabel} tone={serviceRequestStatusTone(request.status)} />
                  <span className="min-w-0 break-words font-mono text-xs text-[#6B7280]">
                    {shortGuid(request.id)}
                  </span>
                </div>
              )}
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={`Close ${heading} details`}
            className="shrink-0 cursor-pointer rounded-[8px] p-1.5 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        {!isLoading && !isError && (
          <div
            role="tablist"
            aria-label="Service request sections"
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

        {/* The single scroll container for the whole panel. */}
        <div
          id={`${titleId}-panel`}
          role="tabpanel"
          aria-labelledby={isLoading || isError ? titleId : `${titleId}-tab-${activeTab}`}
          tabIndex={0}
          className="flex-1 overflow-y-auto p-5 sm:p-6"
        >
          {isLoading && <LoadingState label="Loading request…" />}

          {isError && (
            <ErrorState
              message={extractApiErrorMessage(error, 'Could not load this service request.')}
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && request && renderTab()}

          {/*
            A detail that resolved but carried no usable body is a contract problem,
            not an empty request. It is reported as such rather than as a blank
            panel, which would read as "nothing to show".
          */}
          {!isLoading && !isError && !request && (
            <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
              The request succeeded but returned no service request record, so there
              is nothing to display. The request may have been deleted.
            </p>
          )}
        </div>

        {/*
          The decision bar. Rendered only while the request is Submitted, because
          that is the only status either handler accepts, and it sits outside the
          scroll container so the controls stay put while a long section scrolls.
        */}
        {isDecidable && (
          <div className="shrink-0 border-t border-[#E2E4E9] bg-[#F7F8FA] px-5 py-4 sm:px-6">
            <p className="text-xs leading-relaxed text-[#6B7280]">
              {hasLinkedDocument
                ? 'Convert this request into a renewal task, or reject it with a reason. Both are permanent — the request cannot be reopened, converted twice, or un-rejected afterwards.'
                : 'This request can be rejected with a reason. It cannot be converted: no document is linked to it, and a renewal task can only be created from one.'}
            </p>

            <div className="mt-3 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setDecisionMode('reject')}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#DC2626]"
              >
                <Ban size={15} strokeWidth={1.75} aria-hidden="true" />
                Reject request
              </button>

              {/*
                Conversion stays visible but disabled when no document is linked, with
                the reason in the paragraph above. Hiding the control entirely would
                leave a reader unable to tell that conversion exists and is merely
                unavailable here, as opposed to not being part of this module.
              */}
              <button
                type="button"
                onClick={() => setDecisionMode('convert')}
                disabled={!hasLinkedDocument}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#0F9D74] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#0B7D5D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                <ArrowRightCircle size={15} strokeWidth={1.75} aria-hidden="true" />
                Convert to renewal task
              </button>
            </div>
          </div>
        )}
      </aside>

      {decisionMode && request && (
        <AdminServiceRequestDecisionDialog
          mode={decisionMode}
          request={request}
          onClose={() => setDecisionMode(null)}
        />
      )}
    </div>
  )
}

export default AdminServiceRequestDetailDrawer
