import { useEffect, useId, useRef, useState } from 'react'
import {
  Building2,
  ClipboardList,
  FileText,
  LayoutGrid,
  Receipt,
  Users,
  UserRound,
  X,
} from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminClient } from '../../../hooks/admin/useAdminClients'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminClientOverviewSection from './AdminClientOverviewSection'
import AdminClientEntitiesSection from './AdminClientEntitiesSection'
import AdminClientEntityDetailPanel from './AdminClientEntityDetailPanel'
import AdminClientContactsSection from './AdminClientContactsSection'
import AdminClientContractsSection from './AdminClientContractsSection'
import AdminClientServiceReportSection from './AdminClientServiceReportSection'
import AdminClientEmployeesSection from './AdminClientEmployeesSection'
import AdminClientQuickAccess from './AdminClientQuickAccess'
import AdminClientCompanyFormDialog from './AdminClientCompanyFormDialog'
import AdminClientEntityFormDialog from './AdminClientEntityFormDialog'
import AdminClientContactFormDialog from './AdminClientContactFormDialog'
import AdminDeleteConfirmDialog from './AdminDeleteConfirmDialog'
import {
  presentText,
  recordStatusLabel,
  recordStatusTone,
} from './clientDisplay'

/**
 * Admin client-company detail drawer, and the host for every onboarding dialog
 * that acts on this company.
 *
 * Built on the shared useFocusTrap rather than the shared client/portals Drawer,
 * for two reasons: that component is capped at max-w-md (448px) with no width
 * prop, which cannot hold a company plus six sections, and it lives under
 * client/ which is out of scope for this change. useFocusTrap itself lives in
 * shared hooks/ and is used unmodified, so Escape, bidirectional Tab wrap,
 * focus-on-open, focus restoration to the triggering row and body scroll lock all
 * behave exactly as they do elsewhere.
 *
 * Overlay click-to-close is wired here because the hook does not provide it.
 *
 * ONE PANEL, ONE FOCUS TRAP. Entity detail replaces this drawer's body instead of
 * opening a second drawer: two stacked modal surfaces would leave little visible
 * and make it ambiguous which one Escape closes. Replacing the body keeps one
 * scroll container, one focus trap, and a Back control in a consistent place.
 *
 * SECTIONS FETCH LAZILY. The company detail is fetched on open because the header
 * needs it; each remaining section fetches the first time its tab is activated
 * and is then cached by TanStack Query, so switching back is instant. Firing all
 * six requests on open would pay for data the user may never look at, and would
 * make the drawer look broken on a slow connection.
 *
 * The company detail is the one request whose failure owns the whole drawer: with
 * no name, status or trade licence there is nothing coherent to render above the
 * sections, so a failure here is a full-drawer error with a retry. Every other
 * section fails independently and leaves the rest of the drawer usable.
 *
 * WHY EVERY DIALOG IS RENDERED HERE, AS A SIBLING OF THE PANEL. The onboarding
 * dialogs are all `fixed inset-0 z-50`, the same stacking level as this drawer. A
 * dialog rendered from inside the panel's own markup — which is what the sections
 * could have done — would come EARLIER in the document than the drawer's own
 * `z-50` root and be painted underneath it, so its backdrop would dim nothing
 * and its inputs would sit behind the panel. Rendering them after the panel as
 * siblings puts them later in the document, which is what actually puts them on
 * top. This is the same arrangement AdminStaffPage uses for its drawer and its
 * form dialog, and it is why the sections below receive callbacks rather than
 * owning their own dialog state: they stay presentational, and every dialog in
 * this feature is mounted from one place.
 *
 * A soft-deleted company gets no onboarding controls. Its nested entity and
 * contact routes 404, so an "Add contact" button on a deleted company would open
 * a form that can only fail; the actions are withheld rather than shown broken.
 */
const SECTIONS = [
  { key: 'overview', label: 'Overview', icon: LayoutGrid },
  { key: 'entities', label: 'Entities', icon: Building2 },
  { key: 'contacts', label: 'Contacts', icon: UserRound },
  { key: 'contracts', label: 'Contracts', icon: FileText },
  { key: 'report', label: 'Service report', icon: ClipboardList },
  { key: 'employees', label: 'Employees', icon: Users },
  { key: 'quick', label: 'Quick access', icon: Receipt },
]

function AdminClientDetailDrawer({ clientId, onClose }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeSection, setActiveSection] = useState('overview')

  // Entity detail is a replacing view inside the Entities tab, so it resets when
  // the drawer moves to a different company.
  const [selectedEntity, setSelectedEntity] = useState(null)

  const tabRefs = useRef([])

  // One place for every dialog this feature opens, so their lifecycle is a
  // single piece of state rather than a flag in each of the seven sections.
  const [isCompanyEditOpen, setIsCompanyEditOpen] = useState(false)
  const [isCompanyDeleteOpen, setIsCompanyDeleteOpen] = useState(false)
  const [entityForm, setEntityForm] = useState(null)
  const [entityDeleteTarget, setEntityDeleteTarget] = useState(null)
  const [isContactCreateOpen, setIsContactCreateOpen] = useState(false)
  const [contactDeleteTarget, setContactDeleteTarget] = useState(null)

  const { data: client, isLoading, isError, error, refresh } = useAdminClient(clientId)

  useEffect(() => {
    setActiveSection('overview')
    setSelectedEntity(null)
  }, [clientId])

  // A different company means any half-finished dialog belongs to a record this
  // drawer no longer shows, so none of them may stay open across the switch.
  useEffect(() => {
    setIsCompanyEditOpen(false)
    setIsCompanyDeleteOpen(false)
    setEntityForm(null)
    setEntityDeleteTarget(null)
    setIsContactCreateOpen(false)
    setContactDeleteTarget(null)
  }, [clientId])

  const companyName = presentText(client?.companyName) ?? 'Client company'

  // Nested routes 404 under a soft-deleted parent, so nothing that creates a
  // child is offered for one.
  const isCompanyDeleted = Boolean(client?.isDeleted)

  /**
   * Arrow-key navigation for the tablist. A tablist that only responds to clicks
   * is unusable by keyboard: Left/Right must move between tabs, Home/End must
   * jump to the ends, and focus must follow the selection.
   */
  function handleTabKeyDown(event) {
    const currentIndex = SECTIONS.findIndex((section) => section.key === activeSection)
    let nextIndex = null

    if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % SECTIONS.length
    else if (event.key === 'ArrowLeft') nextIndex = (currentIndex - 1 + SECTIONS.length) % SECTIONS.length
    else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = SECTIONS.length - 1

    if (nextIndex == null) return

    event.preventDefault()
    const nextKey = SECTIONS[nextIndex].key
    setActiveSection(nextKey)
    // Selecting a different company must not leave an entity view pointing at an
    // entity that belongs to the previous company.
    if (nextKey !== 'entities') setSelectedEntity(null)
    tabRefs.current[nextIndex]?.focus()
  }

  function selectSection(key) {
    setActiveSection(key)
    if (key !== 'entities') setSelectedEntity(null)
  }

  function renderSection() {
    if (activeSection === 'overview') {
      return (
        <AdminClientOverviewSection
          client={client}
          onEdit={() => setIsCompanyEditOpen(true)}
          onDelete={() => setIsCompanyDeleteOpen(true)}
        />
      )
    }
    if (activeSection === 'entities') {
      if (selectedEntity) {
        return (
          <AdminClientEntityDetailPanel
            clientId={clientId}
            entityId={selectedEntity.id}
            entityName={selectedEntity.name}
            onBack={() => setSelectedEntity(null)}
            onEdit={(entity) => setEntityForm({ mode: 'edit', entity })}
            onDelete={(entity) => setEntityDeleteTarget(entity)}
          />
        )
      }
      return (
        <AdminClientEntitiesSection
          clientId={clientId}
          onSelectEntity={(id, name) => setSelectedEntity({ id, name })}
          onAddEntity={() => setEntityForm({ mode: 'create', entity: null })}
        />
      )
    }
    if (activeSection === 'contacts') {
      return (
        <AdminClientContactsSection
          clientId={clientId}
          onAddContact={() => setIsContactCreateOpen(true)}
          onDeleteContact={setContactDeleteTarget}
        />
      )
    }
    if (activeSection === 'contracts') return <AdminClientContractsSection clientId={clientId} />
    if (activeSection === 'report') {
      return <AdminClientServiceReportSection clientId={clientId} />
    }
    if (activeSection === 'employees') {
      return <AdminClientEmployeesSection clientId={clientId} companyName={companyName} />
    }
    return <AdminClientQuickAccess clientId={clientId} companyName={companyName} />
  }

  return (
    <>
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
                <Building2 size={18} strokeWidth={1.75} aria-hidden="true" />
              </div>
              <div className="min-w-0">
                <h2 id={titleId} className="break-words text-base font-semibold tracking-tight text-[#16181D]">
                  {isLoading && !client ? 'Loading company…' : companyName}
                </h2>
                {client && (
                  <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                    <StatusPill
                      label={recordStatusLabel(client)}
                      tone={recordStatusTone(client)}
                    />
                    {presentText(client.tradeLicenseNumber) && (
                      <span className="font-mono text-xs text-[#6B7280]">
                        {client.tradeLicenseNumber}
                      </span>
                    )}
                    {presentText(client.emirate) && (
                      <span className="text-xs text-[#6B7280]">{client.emirate}</span>
                    )}
                  </div>
                )}
              </div>
            </div>

            <button
              type="button"
              onClick={onClose}
              aria-label={`Close ${companyName} details`}
              className="shrink-0 rounded-[8px] p-1.5 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
            >
              <X className="h-5 w-5" aria-hidden="true" />
            </button>
          </div>

          {!isLoading && !isError && (
            <div
              role="tablist"
              aria-label="Client company sections"
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
                    onClick={() => selectSection(section.key)}
                    className={`inline-flex shrink-0 items-center gap-1.5 rounded-[8px] px-2.5 py-1.5 text-xs font-semibold whitespace-nowrap transition duration-150 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer ${
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

          {/* aria-labelledby points at the active tab, but the tablist only exists
              once the company has loaded. During the first load and on error the
              panel is labelled by the drawer heading instead, so it never
              references an element that is not in the DOM. */}
          <div
            id={`${titleId}-panel`}
            role="tabpanel"
            aria-labelledby={
              isLoading || isError ? titleId : `${titleId}-tab-${activeSection}`
            }
            tabIndex={0}
            className="flex-1 overflow-y-auto p-5 sm:p-6"
          >
            {isLoading && <LoadingState label="Loading client company…" />}

            {isError && (
              <ErrorState
                message={extractApiErrorMessage(error, 'Could not load this client company.')}
                onRetry={() => refresh()}
              />
            )}

            {!isLoading && !isError && (
              <div className="flex flex-col gap-6">
                {client?.isDeleted && (
                  <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
                    This company is soft-deleted. Its legal entities and contacts will
                    report an error, because the backend scopes those routes to
                    companies that are not deleted.
                  </p>
                )}

                {renderSection()}
              </div>
            )}
          </div>
        </aside>
      </div>

      {/* Siblings, not children — see the file header on stacking. */}
      {!isCompanyDeleted && (
        <>
          {isCompanyEditOpen && (
            <AdminClientCompanyFormDialog
              mode="edit"
              client={client}
              onClose={() => setIsCompanyEditOpen(false)}
            />
          )}

          {entityForm && (
            <AdminClientEntityFormDialog
              mode={entityForm.mode}
              clientId={clientId}
              entity={entityForm.entity}
              onClose={() => setEntityForm(null)}
              // An edit made from inside the detail panel has to return the panel
              // to the list, because the row it was reached from is the row that
              // changed and the panel is about to re-read a different id.
              onSaved={() => {
                if (entityForm.mode === 'edit') setSelectedEntity(null)
                setEntityForm(null)
              }}
            />
          )}

          {isContactCreateOpen && (
            <AdminClientContactFormDialog
              clientId={clientId}
              onClose={() => setIsContactCreateOpen(false)}
              onCreated={() => {
                setIsContactCreateOpen(false)
              }}
            />
          )}
        </>
      )}

      {/* The IDs are passed explicitly rather than read back out of a cache
          inside the dialog. Every delete here is scoped by both the company and
          the child, so a dialog that guessed the parent from a cached record
          could target the wrong company, and the confirming UI is the last place
          that has to be unambiguous about what it is about to remove. */}
      {isCompanyDeleteOpen && client && (
        <AdminDeleteConfirmDialog
          subjectType="company"
          subjectName={companyName}
          clientId={clientId}
          onClose={() => setIsCompanyDeleteOpen(false)}
        />
      )}

      {entityDeleteTarget && (
        <AdminDeleteConfirmDialog
          subjectType="entity"
          subjectName={presentText(entityDeleteTarget.entityName) ?? 'this entity'}
          clientId={clientId}
          entityId={entityDeleteTarget.id}
          onClose={() => {
            setEntityDeleteTarget(null)
            setSelectedEntity(null)
          }}
        />
      )}

      {contactDeleteTarget && (
        <AdminDeleteConfirmDialog
          subjectType="contact"
          subjectName={presentText(contactDeleteTarget.fullName) ?? 'this contact'}
          clientId={clientId}
          contactId={contactDeleteTarget.id}
          onClose={() => setContactDeleteTarget(null)}
        />
      )}
    </>
  )
}

export default AdminClientDetailDrawer
