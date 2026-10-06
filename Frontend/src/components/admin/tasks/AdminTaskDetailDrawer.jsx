import { useEffect, useId, useRef, useState } from 'react'
import { FileText, History, ListChecks, X } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminTask } from '../../../hooks/admin/useAdminTasks'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { documentTypeLabel } from '../documents/documentDisplay'
import AdminTaskOverviewSection from './AdminTaskOverviewSection'
import AdminTaskStepsSection from './AdminTaskStepsSection'
import AdminTaskHistorySection from './AdminTaskHistorySection'
import AdminTaskAssignPanel from './AdminTaskAssignPanel'
import AdminTaskServiceFeePanel from './AdminTaskServiceFeePanel'
import AdminTaskUnblockPanel from './AdminTaskUnblockPanel'
import {
  shortGuid,
  taskStatusLabel,
  taskStatusTone,
} from './taskDisplay'

/**
 * Admin renewal-task detail drawer.
 *
 * Built on the shared useFocusTrap exactly as the Admin Documents and Employees
 * drawers are, so Escape, bidirectional Tab wrap, focus-on-open, focus
 * restoration to the triggering row and body scroll lock behave identically
 * across the Admin modules. The hook is used unmodified; overlay click-to-close
 * is wired here because the hook does not provide it.
 *
 * ONE PANEL, ONE FOCUS TRAP. There is no nested drawer. The single outbound
 * destination is a link out to /clients, and the two write confirmations
 * (assignment and the service fee) are dialogs rendered inside this panel —
 * which useFocusTrap's modal stack handles correctly: whichever dialog is open
 * becomes the topmost surface, so Escape and Tab belong to it, and focus returns
 * to the control that opened it. Only one can be open at a time, because each
 * panel owns its own confirmation state.
 *
 * THE FEE PANEL SITS ABOVE ASSIGNMENT, and that order is not arbitrary: the fee
 * is a prerequisite for completing the task, assignment is who does the work.
 * Both are rendered in the Overview tab rather than given a tab of their own,
 * because both act on the task record itself and neither owns a list.
 *
 * THREE TABS, TWO OF THEM LAZILY FETCHED. The detail loads on open because the
 * header needs the status and the document identity. Steps and History mount only
 * when their tab is first activated, so opening the drawer does not pay for two
 * requests the reader may never need. Once fetched they are cached under the
 * nested ['admin','task', taskId, 'steps'|'history'] keys, so returning to a tab
 * is a cache read rather than a refetch, and the assign mutation's single prefix
 * invalidation refreshes them together with the detail.
 *
 * THERE IS NO INVOICE TAB, AND THAT IS STILL A DELIBERATE OMISSION. Invoicing is
 * a billing concern owned by the Admin Billing module, which already lists and
 * creates Service Fee invoices; duplicating it inside a task drawer would be a
 * second surface for the same records, not a feature. The reason it could not
 * have worked before was that no reachable path set `ServiceFeeAmount`, so the
 * billing form's `Updated`-only task picker was always empty — that is fixed
 * now that the fee is settable here, but the tab still belongs elsewhere.
 *
 * THE HEADER OWNED FAILURE IS TOTAL. With no status or document identity there is
 * nothing coherent to render above the tabs, so a failed detail request is a
 * full-drawer error with a retry, and the tabs are not rendered at all. Steps and
 * History then each fail independently and leave the rest of the drawer usable.
 *
 * aria-labelledby on the tabpanel points at the active tab, but the tablist only
 * exists once the detail has loaded. During the first load and on error the panel
 * is labelled by the drawer heading instead, so it never references an element
 * that is not in the DOM.
 */
const TABS = [
  { key: 'overview', label: 'Overview', icon: FileText },
  { key: 'steps', label: 'Steps', icon: ListChecks },
  { key: 'history', label: 'History', icon: History },
]

function AdminTaskDetailDrawer({ taskId, onClose }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeTab, setActiveTab] = useState('overview')
  const tabRefs = useRef([])

  const { data: task, isLoading, isError, error, refresh } = useAdminTask(taskId)

  // A different task means different tabs' worth of cached data, so the tab
  // resets with it.
  useEffect(() => {
    setActiveTab('overview')
  }, [taskId])

  const statusLabel = taskStatusLabel(task?.status) ?? 'Unknown'
  const heading = task?.document?.documentNumber
    ? `${documentTypeLabel(task.document.type) ?? 'Document'} · ${task.document.documentNumber}`
    : task
      ? `Task ${shortGuid(task.id) ?? ''}`.trim()
      : 'Loading task…'

  /**
   * Arrow-key navigation for the tablist. A tablist that only responds to clicks
   * is unusable by keyboard: Left/Right must move between tabs, Home/End must
   * jump to the ends, and focus must follow the selection.
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
   * Only the active tab is mounted, which is what makes the lazy fetch lazy: the
   * Steps and History sections call their hooks on mount, so an unvisited tab
   * issues no request at all.
   */
  function renderTab() {
    if (activeTab === 'steps') {
      return <AdminTaskStepsSection taskId={taskId} stepLogCount={task?.stepLogCount} />
    }

    if (activeTab === 'history') {
      return <AdminTaskHistorySection taskId={taskId} />
    }

    return (
      <div className="flex flex-col gap-5">
        <AdminTaskOverviewSection task={task} />
        {/*
          Unblock sits directly under the overview and ABOVE the two write panels,
          ordered by how urgent the control is rather than alphabetically: an
          Admin looking at a Blocked task opened it to resolve the block, and the
          blocked-reason banner in the overview already puts the question in
          front of them. The fee and the assignee are routine edits that can wait
          until after the blocker is gone.
        */}
        <AdminTaskUnblockPanel task={task} />
        <AdminTaskServiceFeePanel task={task} />
        <AdminTaskAssignPanel task={task} />
      </div>
    )
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
                {isLoading && !task ? 'Loading task…' : heading}
              </h2>
              {task && (
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                  <StatusPill label={statusLabel} tone={taskStatusTone(task?.status)} />
                  <span className="min-w-0 break-words font-mono text-xs text-[#6B7280]">
                    {shortGuid(task.id)}
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
            aria-label="Task sections"
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
          aria-labelledby={
            isLoading || isError ? titleId : `${titleId}-tab-${activeTab}`
          }
          tabIndex={0}
          className="flex-1 overflow-y-auto p-5 sm:p-6"
        >
          {isLoading && <LoadingState label="Loading task…" />}

          {isError && (
            <ErrorState
              message={extractApiErrorMessage(error, 'Could not load this task.')}
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && task && renderTab()}

          {/*
            A detail that resolved but carried no usable body is a contract
            problem, not an empty task. It is reported as such rather than as a
            blank panel, which would read as "nothing to show".
          */}
          {!isLoading && !isError && !task && (
            <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
              The request succeeded but returned no task record, so there is
              nothing to display. The task may have been deleted.
            </p>
          )}
        </div>
      </aside>
    </div>
  )
}

export default AdminTaskDetailDrawer
