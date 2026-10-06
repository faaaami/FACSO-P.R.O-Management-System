import { useId, useState } from 'react'
import { AlertCircle, Loader2, TriangleAlert } from 'lucide-react'
import { toast } from 'sonner'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import {
  useDeleteAdminClient,
  useDeleteAdminClientEntity,
  useDeleteAdminClientContact,
} from '../../../hooks/admin/useAdminClientMutations'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * One confirmation dialog for the three soft deletes in this feature: a company,
 * a legal entity, and a client contact.
 *
 * They are one component because the decision an Admin is making is the same
 * decision three times — "this record is about to be retired, and there is no way
 * back" — and because a shared dialog is the only way to guarantee that the same
 * truthful wording is used for all of them. Three near-identical dialogs would
 * drift.
 *
 * EVERY WORD BELOW IS CHECKED AGAINST THE HANDLER. These are SOFT deletes: each
 * handler sets IsDeleted and a DeletedAt timestamp and saves. None of them
 * removes a row, and no controller action anywhere in the API restores one, so
 * the dialog says the record is retained and that it cannot be restored from this
 * application. It never says "permanently deletes", which would be false.
 *
 * The consequences differ per subject and are stated per subject rather than
 * generically, because they genuinely differ:
 *
 *   company  the handler touches ONLY the company row. Entities and contacts
 *            survive, but every nested read is scoped to a parent that is not
 *            deleted, so they become unreachable through this company — which is
 *            the same warning the drawer already shows on a deleted company.
 *            A company that is already deleted is rejected with 409.
 *
 *   entity   the handler touches ONLY the entity row. Its documents are not
 *            deleted with it.
 *
 *   contact  the handler sets IsActive = false AS WELL AS IsDeleted, so a
 *            deleted contact is not merely hidden from the list — the account is
 *            refused at sign-in. That is stated, because it is the one delete with
 *            an effect outside this UI.
 *
 * CHILD COUNTS ARE NOT INVENTED. It would be stronger to say "this company has 3
 * entities and 7 contacts", but the drawer fetches those lists lazily, one tab at
 * a time, so at the point the Overview tab offers Delete the counts are usually
 * not loaded at all. Rather than fire two extra requests on every drawer open to
 * decorate a confirmation, or state a number that is not in hand, the dialog
 * states the consequence qualitatively. `childSummary` is accepted for a caller
 * that genuinely holds the figures; the company delete passes none.
 */
const SUBJECTS = {
  company: {
    heading: 'Delete company',
    /** The verb phrase on the confirm button. */
    confirm: 'Delete company',
    pending: 'Deleting…',
    noun: 'company',
    consequence:
      'The company record is kept and marked deleted. There is no way to restore it from this application.',
    extra:
      'Its legal entities and contacts are not deleted, but they stop being reachable through this company, because those records can only be read for a company that is not deleted.',
  },
  entity: {
    heading: 'Delete legal entity',
    confirm: 'Delete entity',
    pending: 'Deleting…',
    noun: 'entity',
    consequence:
      'The entity record is kept and marked deleted. There is no way to restore it from this application.',
    extra: 'Its documents are not deleted along with it.',
  },
  contact: {
    heading: 'Delete client contact',
    confirm: 'Delete contact',
    pending: 'Deleting…',
    noun: 'contact',
    consequence:
      'The contact record is kept and marked deleted. There is no way to restore it from this application.',
    extra:
      'The account is also deactivated, so the contact can no longer sign in even with the correct password.',
  },
}

function AdminDeleteConfirmDialog({ subjectType, subjectName, clientId, entityId, contactId, childSummary, onClose }) {
  const titleId = useId()
  const noteId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  // All three mutations are created unconditionally, because hooks cannot be
  // called conditionally, and exactly one is used. Same shape as the staff form
  // dialog, which holds both a create and an update mutation for the same reason.
  const deleteCompany = useDeleteAdminClient()
  const deleteEntity = useDeleteAdminClientEntity()
  const deleteContact = useDeleteAdminClientContact()

  const [errorMessage, setErrorMessage] = useState(null)

  const subject = SUBJECTS[subjectType] ?? SUBJECTS.company

  // Every mutation is held so the disabled state and the failure text are
  // identical whichever subject is in play, and so no branch can be added without
  // them. `isPending` is an OR of all three rather than "the one for this subject"
  // because a stale pending flag from a different subject would otherwise leave
  // this dialog's button enabled while a request was in flight.
  const isPending =
    deleteCompany.isPending || deleteEntity.isPending || deleteContact.isPending

  function handleConfirm() {
    setErrorMessage(null)

    const shared = {
      onSuccess: () => {
        toast.success(`${subjectName} deleted.`)
        onClose()
      },
      onError: (error) => {
        const message = describeFailure(error, subjectType, subjectName)
        setErrorMessage(message)
        toast.error(message)
      },
    }

    if (subjectType === 'entity') {
      deleteEntity.mutate({ clientId, entityId }, shared)
      return
    }
    if (subjectType === 'contact') {
      deleteContact.mutate({ clientId, contactId }, shared)
      return
    }
    deleteCompany.mutate(clientId, shared)
  }

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={onClose}
        aria-hidden="true"
      />

      <div className="relative flex min-h-full items-end justify-center p-0 sm:items-center sm:p-6">
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          aria-describedby={noteId}
          tabIndex={-1}
          className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none sm:rounded-[14px] sm:p-6"
        >
          <h2
            id={titleId}
            className="text-base font-semibold tracking-tight text-[#16181D]"
          >
            {subject.heading}
          </h2>

          <div
            id={noteId}
            role="note"
            className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-3"
          >
            <TriangleAlert
              size={15}
              strokeWidth={2}
              className="mt-0.5 shrink-0 text-[#D97706]"
              aria-hidden="true"
            />
            <div className="min-w-0 text-xs text-[#92400E]">
              <p className="font-semibold break-words">
                {subjectName} will be removed from this {subject.noun}.
              </p>
              <p className="mt-1">{subject.consequence}</p>
              <p className="mt-1">{subject.extra}</p>
            </div>
          </div>

          {childSummary && (
            <p className="mt-3 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/70 px-3.5 py-2.5 text-xs text-[#6B7280]">
              {childSummary}
            </p>
          )}

          {errorMessage && (
            <div
              role="alert"
              className="mt-4 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
            >
              <AlertCircle
                size={14}
                strokeWidth={2}
                className="shrink-0 text-[#DC2626]"
                aria-hidden="true"
              />
              <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
            </div>
          )}

          <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={onClose}
              disabled={isPending}
              className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleConfirm}
              // Disabled while pending because these routes have no guard against
              // a repeated delete. The company route rejects a second attempt,
              // but the entity and contact routes have no such check at all, so a
              // double click would fire the request twice and write a second
              // audit entry.
              disabled={isPending}
              className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#DC2626] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#B91C1C] focus:outline-none focus:ring-2 focus:ring-[#DC2626] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isPending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                  <span>{subject.pending}</span>
                </>
              ) : (
                <span>{subject.confirm}</span>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

/**
 * A failure here is worth naming precisely, because the two causes need different
 * responses from the Admin.
 *
 * A 404 means the record is already gone, or the COMPANY is gone — every nested
 * route is scoped to a parent that is not deleted, so a deleted company makes
 * entity and contact deletes fail for a reason that has nothing to do with the
 * record in the dialog.
 *
 * A 409 only exists on the company route, which rejects deleting a company that
 * is already deleted.
 */
function describeFailure(error, subjectType, subjectName) {
  if (error?.response?.status === 409) {
    return `${subjectName} is already deleted.`
  }

  if (error?.response?.status === 404) {
    return 'This record, or its parent company, no longer exists.'
  }

  return extractApiErrorMessage(
    error,
    'Unable to delete this record. Please try again.',
  )
}

export default AdminDeleteConfirmDialog
