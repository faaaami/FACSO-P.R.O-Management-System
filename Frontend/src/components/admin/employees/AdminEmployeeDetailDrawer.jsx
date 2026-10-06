import { useEffect, useId, useRef, useState } from 'react'
import { FileText, History, UserRound, X } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminEmployee } from '../../../hooks/admin/useAdminEmployees'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminEmployeeProfileSection from './AdminEmployeeProfileSection'
import AdminEmployeeDocumentsSection from './AdminEmployeeDocumentsSection'
import AdminEmployeeTimelineSection from './AdminEmployeeTimelineSection'
import { presentText } from '../clients/clientDisplay'
import {
  employeeName,
  employeeStatusLabel,
  employeeStatusTone,
} from './employeeDisplay'

/**
 * Admin employee detail drawer.
 *
 * Built on the shared useFocusTrap exactly as the Admin Clients drawer is, so
 * Escape, bidirectional Tab wrap, focus-on-open, focus restoration to the
 * triggering row and body scroll lock behave identically across the two Admin
 * modules. The hook is used unmodified. Overlay click-to-close is wired here
 * because the hook does not provide it.
 *
 * ONE PANEL, ONE FOCUS TRAP. There is no nested drawer: the document file opens
 * in a new browser tab, and every other destination is either a section in this
 * panel or a link out. Two stacked modal surfaces would leave little visible and
 * make it ambiguous which one Escape closes.
 *
 * THREE SECTIONS, LAZILY FETCHED. The employee detail loads on open because the
 * header needs the name and status. Documents and Timeline fetch the first time
 * their tab is activated and are then cached, so switching back is instant and a
 * user who only wants the profile never pays for the other two requests.
 *
 * THE HEADER OWNED FAILURE IS TOTAL, THE SECTION FAILURES ARE NOT. With no name
 * or status there is nothing coherent to render above the sections, so a failed
 * detail request is a full-drawer error with a retry. Documents and Timeline
 * each fail independently and leave the rest of the drawer usable — which
 * matters here because the documents route 404s outright for a soft-deleted
 * employee while the profile and the timeline still load fine.
 *
 * aria-labelledby on the tabpanel points at the active tab, but the tablist only
 * exists once the detail has loaded. During the first load and on error the panel
 * is labelled by the drawer heading instead, so it never references an element
 * that is not in the DOM.
 */
const SECTIONS = [
  { key: 'profile', label: 'Profile', icon: UserRound },
  { key: 'documents', label: 'Documents', icon: FileText },
  { key: 'timeline', label: 'Timeline', icon: History },
]

function AdminEmployeeDetailDrawer({ employeeId, onClose }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeSection, setActiveSection] = useState('profile')
  const tabRefs = useRef([])

  const { data: employee, isLoading, isError, error, refresh } =
    useAdminEmployee(employeeId)

  // A different employee means a different trail, so the tab resets with it.
  useEffect(() => {
    setActiveSection('profile')
  }, [employeeId])

  const name = employeeName(employee)
  const entityName = presentText(employee?.entityName)

  /**
   * Arrow-key navigation for the tablist. A tablist that only responds to clicks
   * is unusable by keyboard: Left/Right must move between tabs, Home/End must
   * jump to the ends, and focus must follow the selection.
   */
  function handleTabKeyDown(event) {
    const currentIndex = SECTIONS.findIndex(
      (section) => section.key === activeSection,
    )
    let nextIndex = null

    if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % SECTIONS.length
    else if (event.key === 'ArrowLeft') {
      nextIndex = (currentIndex - 1 + SECTIONS.length) % SECTIONS.length
    } else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = SECTIONS.length - 1

    if (nextIndex == null) return

    event.preventDefault()
    setActiveSection(SECTIONS[nextIndex].key)
    tabRefs.current[nextIndex]?.focus()
  }

  function renderSection() {
    if (activeSection === 'documents') {
      return <AdminEmployeeDocumentsSection employeeId={employeeId} />
    }
    if (activeSection === 'timeline') {
      return <AdminEmployeeTimelineSection employeeId={employeeId} />
    }
    return <AdminEmployeeProfileSection employee={employee} />
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
              <UserRound size={18} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2
                id={titleId}
                className="break-words text-base font-semibold tracking-tight text-[#16181D]"
              >
                {isLoading && !employee ? 'Loading employee…' : name}
              </h2>
              {employee && (
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                  <StatusPill
                    label={employeeStatusLabel(employee)}
                    tone={employeeStatusTone(employee)}
                  />
                  {entityName && (
                    <span className="min-w-0 break-words text-xs text-[#6B7280]">
                      {entityName}
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={`Close ${name} details`}
            className="shrink-0 cursor-pointer rounded-[8px] p-1.5 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        {!isLoading && !isError && (
          <div
            role="tablist"
            aria-label="Employee sections"
            onKeyDown={handleTabKeyDown}
            className="flex gap-1 overflow-x-auto border-b border-[#E2E4E9] px-3 py-2 sm:px-4"
          >
            {SECTIONS.map((section, index) => {
              const Icon = section.icon
              const isActive = section.key === activeSection

              return (
                <button
                  key={section.key}
                  ref={(node) => {
                    tabRefs.current[index] = node
                  }}
                  type="button"
                  role="tab"
                  id={`${titleId}-tab-${section.key}`}
                  aria-selected={isActive}
                  aria-controls={`${titleId}-panel`}
                  tabIndex={isActive ? 0 : -1}
                  onClick={() => setActiveSection(section.key)}
                  className={`inline-flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-[8px] px-2.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] ${
                    isActive
                      ? 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74]'
                      : 'text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D]'
                  }`}
                >
                  <Icon size={14} strokeWidth={1.75} aria-hidden="true" />
                  {section.label}
                </button>
              )
            })}
          </div>
        )}

        <div
          id={`${titleId}-panel`}
          role="tabpanel"
          aria-labelledby={
            isLoading || isError ? titleId : `${titleId}-tab-${activeSection}`
          }
          tabIndex={0}
          className="flex-1 overflow-y-auto p-5 sm:p-6"
        >
          {isLoading && <LoadingState label="Loading employee…" />}

          {isError && (
            <ErrorState
              message={extractApiErrorMessage(
                error,
                'Could not load this employee.',
              )}
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && (
            <div className="flex flex-col gap-6">
              {employee?.isDeleted && (
                <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
                  This employee is soft-deleted. Their documents cannot be
                  listed, because the backend rejects the documents route for a
                  deleted parent.
                </p>
              )}

              {renderSection()}
            </div>
          )}
        </div>
      </aside>
    </div>
  )
}

export default AdminEmployeeDetailDrawer
