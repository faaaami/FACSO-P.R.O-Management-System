import { useEffect, useState } from 'react'
import { AlertCircle, Check, Loader2, Plus, Search, Trash2, UserRound, X } from 'lucide-react'
import { toast } from 'sonner'
import StatusPill from '../../client/StatusPill'
import Pagination from '../../client/billing/Pagination'
import AdminClientSection, { AdminClientRecordCard, AdminClientRecordList } from './AdminClientSection'
import { useAdminClientContacts } from '../../../hooks/admin/useAdminClients'
import {
  useUpdateAdminClientContact,
  useUpdateAdminClientContactApproval,
} from '../../../hooks/admin/useAdminClientMutations'
import { extractApiErrorMessage } from '../../../utils/apiError'
import {
  MISSING_VALUE,
  contactStatusLabel,
  contactStatusTone,
  displayText,
  presentText,
} from './clientDisplay'

const PAGE_SIZE = 10

/**
 * Contacts of the selected company.
 * Backing query GET /api/v1/admin/clients/{clientId}/contacts
 *
 * TWO FIELDS ARE DELIBERATELY ABSENT. The contact DTO has no position and no
 * primary-contact flag, so neither is rendered and no substitute is invented.
 * There is no `role` query parameter — the controller never accepted one — so
 * `role` is shown as the free-form string the backend actually returns.
 *
 * `isApproved` is a real and distinct state on a contact — unlike the company,
 * which has only isActive and isDeleted — so the status pill surfaces it, and an
 * unapproved-but-active contact reads as "Awaiting approval" rather than
 * borrowing the company's vocabulary.
 *
 * ONBOARDING IS THE POINT OF THIS SECTION. A contact is a sign-in account, not a
 * note on a company: it is created with a password the Admin sets, it cannot
 * sign in until it is approved, and it is deactivated by deletion. Each of those
 * is a different operation with different consequences, so they are separate
 * controls rather than one "manage" menu, and each is labelled with what it
 * actually does.
 *
 * APPROVE ENABLES THE ACCOUNT, and is not worded as verification. The contact's
 * email was already verified when the account was created, so there is no
 * verification step here and no route for one. What approval does is set
 * IsApproved AND IsActive together, which is why the button says it enables
 * sign-in. It also means approving a contact the Admin has just deactivated will
 * silently switch it back on — the reason the confirm-less inline button is only
 * offered on rows whose state the Admin can see, and the reason the deactivate
 * control stays available afterwards.
 *
 * THERE IS NO EDIT BUTTON, deliberately. UpdateClientContactCommand carries four
 * properties and its handler writes exactly one, IsActive: full name, email and
 * phone are unreachable from Admin, and isApproved belongs to the approval route.
 * A form built on that DTO would show inputs whose values are discarded on save,
 * so the only real edit — activate or deactivate — is a control of its own.
 *
 * Approval actions are hidden on an already-approved contact. There is no revoke
 * route; the same endpoint with approved = false is the reverse operation, and
 * that is what Reject is on an unapproved contact. Offering "Approve" on a
 * contact that is already approved would be a button that does nothing.
 *
 * The dialogs are opened by the drawer, which renders them as siblings of its
 * own panel so they stack above it. This section owns only the inline approval
 * and activation controls, which need no dialog.
 */
function AdminClientContactsSection({ clientId, onAddContact, onDeleteContact }) {
  const [searchInput, setSearchInput] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [errorMessage, setErrorMessage] = useState(null)

  const { items, totalCount, isLoading, isFetching, isError, error, refresh } =
    useAdminClientContacts(clientId, {
      page,
      pageSize: PAGE_SIZE,
      search: appliedSearch || undefined,
    })

  const updateContact = useUpdateAdminClientContact()
  const updateApproval = useUpdateAdminClientContactApproval()

  // Which contact is mid-flight, so only the affected row is disabled. A pending
  // mutation applies to exactly one row; disabling the whole list would grey out
  // every unrelated contact for the duration of a network round trip.
  const [pendingContactId, setPendingContactId] = useState(null)
  const [pendingAction, setPendingAction] = useState(null)

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
  const isGenuinelyEmpty = isEmpty && !hasSearched

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

  function runApproval(contact, approved) {
    setErrorMessage(null)
    setPendingContactId(contact?.id)
    setPendingAction(approved ? 'approve' : 'reject')

    updateApproval.mutate(
      { clientId, contactId: contact?.id, approved },
      {
        onSuccess: () => {
          toast.success(
            approved
              ? `${presentText(contact?.fullName) ?? 'Contact'} approved. They can now sign in.`
              : `${presentText(contact?.fullName) ?? 'Contact'} rejected.`,
          )
        },
        onError: (mutationError) => {
          const message = extractApiErrorMessage(
            mutationError,
            approved
              ? 'Unable to approve this contact. Please try again.'
              : 'Unable to reject this contact. Please try again.',
          )
          setErrorMessage(message)
          toast.error(message)
        },
        onSettled: () => {
          setPendingContactId(null)
          setPendingAction(null)
        },
      },
    )
  }

  function runToggleActive(contact) {
    setErrorMessage(null)
    const nextActive = !contact?.isActive
    const contactName = presentText(contact?.fullName) ?? 'this contact'
    setPendingContactId(contact?.id)
    setPendingAction(nextActive ? 'activate' : 'deactivate')

    updateContact.mutate(
      { clientId, contactId: contact?.id, isActive: nextActive },
      {
        onSuccess: () => {
          toast.success(
            nextActive
              ? `${contactName} reactivated.`
              : `${contactName} deactivated. They can no longer sign in.`,
          )
        },
        onError: (mutationError) => {
          const message = extractApiErrorMessage(
            mutationError,
            `Unable to ${nextActive ? 'reactivate' : 'deactivate'} this contact. Please try again.`,
          )
          setErrorMessage(message)
          toast.error(message)
        },
        onSettled: () => {
          setPendingContactId(null)
          setPendingAction(null)
        },
      },
    )
  }

  const addButton = onAddContact ? (
    <button
      type="button"
      onClick={onAddContact}
      className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
    >
      <Plus size={13} strokeWidth={2.5} aria-hidden="true" />
      Add contact
    </button>
  ) : null

  const searchForm = (
    <form onSubmit={handleSubmit} role="search" className="flex flex-wrap items-center gap-2">
      <div className="relative min-w-[180px] flex-1">
        <label htmlFor="adminContactSearch" className="sr-only">
          Search this company's contacts
        </label>
        <Search
          size={14}
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]"
          aria-hidden="true"
        />
        <input
          id="adminContactSearch"
          type="search"
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder="Search by name or email…"
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
          <span className="sr-only"> contact search</span>
        </button>
      )}
    </form>
  )

  return (
    <AdminClientSection
      title="Contacts"
      description="People registered against this company. Search covers full name and email."
      toolbar={searchForm}
      // Rows on screen means the header carries the action; no rows (and no
      // search hiding them) means the empty state carries it. Never both.
      action={!isEmpty ? addButton : null}
      emptyAction={isGenuinelyEmpty ? addButton : null}
      loading={showSpinner}
      loadingLabel="Loading contacts…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load the contacts for this company."
      isEmpty={isEmpty}
      emptyMessage={hasSearched ? 'No matching contacts.' : 'No contacts on file.'}
      emptyDescription={
        hasSearched
          ? `Nothing matches “${appliedSearch}”. The search covers full name and email only.`
          : 'This contact is created with a password set by the Admin and must be approved before they can sign in.'
      }
      emptyIcon={UserRound}
    >
      <div className="flex flex-col gap-4">
        {showRefetchHint && (
          <p className="text-xs text-[#6B7280]" role="status" aria-live="polite">
            Updating results…
          </p>
        )}

        {/* A failed inline action is shown here rather than as a toast alone: the
            row that failed stays on screen, and a toast that has already faded
            would leave the Admin with no idea which contact did not change. */}
        {errorMessage && (
          <div
            role="alert"
            className="flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-2.5"
          >
            <AlertCircle
              size={14}
              strokeWidth={2}
              className="mt-0.5 shrink-0 text-[#DC2626]"
              aria-hidden="true"
            />
            <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
          </div>
        )}

        <AdminClientRecordList items={items}>
          {(contact) => {
            const contactId = contact?.id
            const contactName = presentText(contact?.fullName) ?? 'this contact'
            const isRowPending = pendingContactId === contactId
            const isRowBusy = isRowPending || isLoading

            // A deleted contact is not a row the Admin can act on: its reads are
            // scoped to non-deleted records, so the approval and activation
            // controls would 404 and delete would fail on an already-deleted id.
            const isRowActionable = !contact?.isDeleted

            return (
              <AdminClientRecordCard
                key={contactId}
                title={presentText(contact?.fullName) ?? 'Unnamed contact'}
                subtitle={presentText(contact?.email) ?? undefined}
                trailing={
                  <StatusPill
                    label={contactStatusLabel(contact)}
                    tone={contactStatusTone(contact)}
                  />
                }
                meta={[
                  { label: 'Phone', value: displayText(contact?.phone) },
                  {
                    label: 'Role',
                    // A free-form string from the backend, not an enum and not
                    // filterable, so it is shown verbatim or as a dash.
                    value: presentText(contact?.role) ?? MISSING_VALUE,
                  },
                ]}
                actions={
                  isRowActionable ? (
                    <>
                      {/* Approve and Reject appear only while the contact is
                          unapproved. There is no revoke route, so an approved
                          contact has nothing to do here — the reverse operation
                          is the same endpoint with approved = false, which is
                          what Reject is. */}
                      {!contact?.isApproved && (
                        <>
                          <button
                            type="button"
                            onClick={() => runApproval(contact, true)}
                            disabled={isRowBusy}
                            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#0F9D74]/25 bg-[rgba(15,157,116,0.08)] px-2.5 py-1.5 text-xs font-semibold text-[#0F9D74] transition duration-150 hover:bg-[rgba(15,157,116,0.14)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            {isRowPending && pendingAction === 'approve' ? (
                              <Loader2
                                size={13}
                                strokeWidth={2.5}
                                className="animate-spin"
                                aria-hidden="true"
                              />
                            ) : (
                              <Check size={13} strokeWidth={2.5} aria-hidden="true" />
                            )}
                            Approve
                            <span className="sr-only"> {contactName}</span>
                          </button>
                          <button
                            type="button"
                            onClick={() => runApproval(contact, false)}
                            disabled={isRowBusy}
                            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            {isRowPending && pendingAction === 'reject' ? (
                              <Loader2
                                size={13}
                                strokeWidth={2.5}
                                className="animate-spin"
                                aria-hidden="true"
                              />
                            ) : (
                              <X size={13} strokeWidth={2.5} aria-hidden="true" />
                            )}
                            Reject
                            <span className="sr-only"> {contactName}</span>
                          </button>
                        </>
                      )}

                      {/* The only field the contact PATCH actually writes. The
                          label says what it does to sign-in rather than just
                          "deactivate", because a deactivated account is refused
                          at login, not merely hidden. */}
                      <button
                        type="button"
                        onClick={() => runToggleActive(contact)}
                        disabled={isRowBusy}
                        className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
                      >
                        {contact?.isActive ? 'Deactivate' : 'Reactivate'}
                        <span className="sr-only"> {contactName}</span>
                      </button>

                      {onDeleteContact && (
                        <button
                          type="button"
                          onClick={() => onDeleteContact(contact)}
                          disabled={isRowBusy}
                          className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-red-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-[#DC2626] transition duration-150 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-100 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          <Trash2 size={13} strokeWidth={2} aria-hidden="true" />
                          Delete
                          <span className="sr-only"> {contactName}</span>
                        </button>
                      )}
                    </>
                  ) : null
                }
              />
            )
          }}
        </AdminClientRecordList>

        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="contact"
            onPageChange={setPage}
          />
        )}
      </div>
    </AdminClientSection>
  )
}

export default AdminClientContactsSection
