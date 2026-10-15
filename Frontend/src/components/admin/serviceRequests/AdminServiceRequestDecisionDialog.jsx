import { useId, useState } from 'react'
import { AlertCircle, ArrowRightCircle, Ban, Loader2, TriangleAlert } from 'lucide-react'
import { toast } from 'sonner'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import {
  useConvertAdminServiceRequest,
  useRejectAdminServiceRequest,
} from '../../../hooks/admin/useAdminServiceRequestMutations'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { serviceRequestTypeText, shortGuid } from './serviceRequestDisplay'

/**
 * The two decisions an Admin can take on a service request, as one dialog.
 *
 * They are one component because they are one decision with two outcomes: "this
 * request is finished with". Sharing the shell is what guarantees the same truthful
 * wording about irreversibility, the same pending behaviour and the same failure
 * handling for both. Two near-identical dialogs would drift.
 *
 * EVERY WORD IS CHECKED AGAINST THE HANDLERS, and in particular the conversion
 * dialog does NOT claim the work is scheduled, assigned or billable:
 *
 *   CreateRenewalTaskCommandHandler writes exactly one RenewalTask with
 *   Status = Submitted, AssignedStaffId = null and no ServiceFeeAmount, plus a
 *   first history row and a RenewalTaskCreated audit entry. It does not assign a
 *   staff member, does not set a fee and does not advance the status, so the
 *   dialog says the task is created unassigned and that assigning it and setting
 *   the fee are separate later steps in the Renewal Tasks module. Promising a
 *   scheduled or billable task here would be false.
 *
 *   ConvertServiceRequestCommandHandler then marks the request Converted with
 *   convertedAt and convertedRenewalTaskId, and RejectServiceRequestCommandHandler
 *   marks it Rejected with rejectedAt and rejectionReason. Both refuse any status
 *   other than Submitted and no endpoint reverses either, so both outcomes are
 *   stated as permanent and NEITHER DIALOG OFFERS AN UNDO.
 *
 * THE CONVERSION FAILURE MODES ARE LISTED BECAUSE THEY ARE THE ADMIN'S TO EXPECT.
 * CreateRenewalTaskCommandHandler can still refuse after the request has been
 * accepted: no linked document, a deleted or inactive document, an unresolvable
 * company, or an active task that already exists for that document. The convert
 * handler additionally refuses a document belonging to a different client company.
 * Each of these surfaces as a 409 and the dialog reports it inline; none of them
 * leaves a half-finished state, because the two writes share one transaction.
 *
 * THE REJECTION REASON IS REQUIRED AND IS SHOWN TO THE CLIENT. The validator
 * requires real content (whitespace alone is refused) and caps it at 1000
 * characters, the handler stores it trimmed, and it is carried in the notification
 * sent to the client company. The character counter is therefore a real limit and
 * not decoration, and the dialog says the client will see this text.
 */
const MAX_REASON_LENGTH = 1000

function AdminServiceRequestDecisionDialog({ mode, request, onClose }) {
  const titleId = useId()
  const noteId = useId()
  const reasonId = useId()
  const reasonHintId = useId()
  const errorId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  // Both mutations are created unconditionally, because hooks cannot be called
  // conditionally, and exactly one is used — the same shape as
  // AdminDeleteConfirmDialog, which holds all three of its mutations for the
  // same reason.
  const convert = useConvertAdminServiceRequest()
  const reject = useRejectAdminServiceRequest()

  const [reason, setReason] = useState('')
  const [errorMessage, setErrorMessage] = useState(null)

  const isConvert = mode === 'convert'

  // An OR of both pending flags rather than the one for this mode, so a stale
  // flag from the other decision can never leave this dialog's button enabled
  // while a request is in flight.
  const isPending = convert.isPending || reject.isPending

  const trimmedReason = reason.trim()
  const isReasonMissing = trimmedReason.length === 0
  const isReasonTooLong = reason.length > MAX_REASON_LENGTH

  /**
   * A rejection cannot be submitted without a reason, so the button is disabled
   * rather than the request being fired and refused. The 1000-character cap is
   * likewise enforced here to stop a paste being silently truncated server-side,
   * but a LENGTH is not trimmed on the way in: the limit applies to the text as
   * typed, exactly as the validator measures it.
   */
  const canSubmit = isConvert
    ? !isPending
    : !isPending && !isReasonMissing && !isReasonTooLong

  function handleConfirm() {
    setErrorMessage(null)

    if (isConvert) {
      convert.mutate(request.id, {
        onSuccess: (data) => {
          // The new task id is reported once, here, straight from the response, and
          // is never cached: this module has no route that can open that task, so a
          // stored id would only ever be text. The Lifecycle tab shows the same id
          // once the detail refetches.
          const taskRef = shortGuid(data?.renewalTaskId)
          toast.success(
            taskRef
              ? `Service request converted. New renewal task ${taskRef}.`
              : 'Service request converted to a renewal task.',
          )
          onClose()
        },
        onError: (error) => {
          const message = describeConvertFailure(error)
          setErrorMessage(message)
          toast.error(message)
        },
      })
      return
    }

    reject.mutate(
      { serviceRequestId: request.id, reason: trimmedReason },
      {
        onSuccess: () => {
          toast.success('Service request rejected.')
          onClose()
        },
        onError: (error) => {
          const message = describeRejectFailure(error)
          setErrorMessage(message)
          toast.error(message)
        },
      },
    )
  }

  return (
    <div className="fixed inset-0 z-[60] overflow-y-auto">
      <div
        className="fixed inset-0 bg-slate-900/50"
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
          aria-invalid={errorMessage ? 'true' : undefined}
          tabIndex={-1}
          className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.18)] focus:outline-none sm:rounded-[14px] sm:p-6"
        >
          <div className="flex items-start gap-3">
            <span
              className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] border ${
                isConvert
                  ? 'border-[#0F9D74]/25 bg-[rgba(15,157,116,0.10)] text-[#0F9D74]'
                  : 'border-[#DC2626]/25 bg-[rgba(220,38,38,0.10)] text-[#DC2626]'
              }`}
              aria-hidden="true"
            >
              {isConvert ? (
                <ArrowRightCircle size={16} strokeWidth={1.75} />
              ) : (
                <Ban size={16} strokeWidth={1.75} />
              )}
            </span>

            <div className="min-w-0 flex-1">
              <h2
                id={titleId}
                className="text-base font-semibold tracking-tight text-[#16181D]"
              >
                {isConvert ? 'Convert to a renewal task' : 'Reject this request'}
              </h2>
              <p className="mt-1 break-words text-xs text-[#6B7280]">
                {serviceRequestTypeText(request.type) ?? 'Service'} request
                {shortGuid(request.id) ? ` · ${shortGuid(request.id)}` : ''}
              </p>
            </div>
          </div>

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
              <p className="font-semibold break-words">This decision is permanent.</p>
              {isConvert ? (
                <>
                  <p className="mt-1">
                    A renewal task is created in the Renewal Tasks module and this
                    request is closed as converted.
                  </p>
                  <p className="mt-1">
                    The task is created unassigned, in the submitted state, with no
                    service fee set. Assigning a staff member and setting the fee are
                    separate later steps on the task itself.
                  </p>
                  <p className="mt-1">
                    The request cannot be converted again, rejected, or returned to
                    submitted by any part of this application.
                  </p>
                </>
              ) : (
                <>
                  <p className="mt-1">
                    The request is closed as rejected with the reason below, and the
                    reason is shown to the client in their portal.
                  </p>
                  <p className="mt-1">
                    The request cannot be un-rejected, converted, or returned to
                    submitted by any part of this application.
                  </p>
                </>
              )}
            </div>
          </div>

          {!isConvert && (
            <div className="mt-4">
              <label
                htmlFor={reasonId}
                className="text-sm font-semibold tracking-tight text-[#16181D]"
              >
                Reason for rejection
              </label>
              <p id={reasonHintId} className="mt-1 text-xs leading-relaxed text-[#6B7280]">
                Required. The client sees this exact text, so say what needs to change
                rather than recording an internal code. It is stored trimmed.
              </p>
              <textarea
                id={reasonId}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                rows={4}
                maxLength={MAX_REASON_LENGTH}
                aria-describedby={isReasonTooLong ? `${reasonHintId} ${errorId}` : reasonHintId}
                aria-invalid={isReasonTooLong ? 'true' : undefined}
                placeholder="e.g. The document on file has already been renewed, so no early renewal is due."
                className="mt-2 w-full resize-y rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-2 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
              />
              <p
                className={`mt-1.5 text-xs ${
                  isReasonTooLong ? 'font-medium text-[#DC2626]' : 'text-[#6B7280]'
                }`}
              >
                {reason.length} of {MAX_REASON_LENGTH} characters used.
                {isReasonMissing && reason.length > 0
                  ? ' A reason made only of spaces is not accepted.'
                  : ''}
              </p>
            </div>
          )}

          {errorMessage && (
            <div
              id={errorId}
              role="alert"
              className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
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
              // Disabled while pending: these routes refuse a second decision with a
              // 409, but firing twice would still make the Admin read two different
              // failures for the one action they took.
              disabled={!canSubmit}
              className={`inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 ${
                isConvert
                  ? 'bg-[#0F9D74] hover:bg-[#0B7D5D] focus:ring-[#0F9D74]'
                  : 'bg-[#DC2626] hover:bg-[#B91C1C] focus:ring-[#DC2626]'
              }`}
            >
              {isPending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                  <span>{isConvert ? 'Converting…' : 'Rejecting…'}</span>
                </>
              ) : (
                <span>{isConvert ? 'Convert to renewal task' : 'Reject request'}</span>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

/**
 * A conversion can fail for several genuinely different reasons, and the Admin
 * needs to be told which, because only some of them are worth retrying.
 *
 * 404 means the request is gone.
 * 409 covers the whole family: already decided, no linked document, the document
 * deleted or inactive, the document belonging to another company, or an active
 * task already existing for that document. The server's own message is the most
 * accurate thing available for these — each names a different cause — so it is
 * surfaced rather than replaced with a generic sentence.
 */
function describeConvertFailure(error) {
  if (error?.response?.status === 404) {
    return 'This service request no longer exists.'
  }

  if (error?.response?.status === 409) {
    return (
      extractApiErrorMessage(
        error,
        'This request can no longer be converted, so nothing was changed.',
      ) + ' Nothing was changed.'
    )
  }

  return extractApiErrorMessage(
    error,
    'Unable to convert this request. Please try again.',
  )
}

/**
 * A rejection fails far less often: 404 if the request is gone, 409 if it has
 * already been decided, and 400 if the reason was refused by the validator — which
 * the dialog blocks locally, so a 400 here means the rule and the UI disagree and
 * the server's message is the more useful one to show.
 */
function describeRejectFailure(error) {
  if (error?.response?.status === 404) {
    return 'This service request no longer exists.'
  }

  if (error?.response?.status === 409) {
    return (
      extractApiErrorMessage(
        error,
        'This request has already been decided, so it was not rejected.',
      ) + ' Nothing was changed.'
    )
  }

  return extractApiErrorMessage(
    error,
    'Unable to reject this request. Please try again.',
  )
}

export default AdminServiceRequestDecisionDialog
