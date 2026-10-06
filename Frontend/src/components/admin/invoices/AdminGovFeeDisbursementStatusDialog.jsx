import { useEffect, useState } from 'react'
import ConfirmDialog from '../../shared/ConfirmDialog'
import { useUpdateAdminGovFeeDisbursementStatus } from '../../../hooks/admin/useAdminBillingMutations'
import { extractApiErrorMessage } from '../../../utils/apiError'
import {
  BILLING_ACTION_CONFIRMS,
  GOV_FEE_STATUS_ADVANCE_MEANING,
  GOV_FEE_STATUS_ADVANCE_REQUEST_FIELDS_NOTE,
  IRREVERSIBLE_ACTIONS,
  govFeeStatusText,
  moneyParts,
  nextGovFeeStatus,
  presentText,
  truncate,
} from './billingDisplay'

/**
 * Confirmation for advancing one government-fee disbursement by a single step.
 *
 * ------------------------------------------------------------------
 * THE READER DOES NOT CHOOSE A STATUS. THERE IS NOTHING TO PICK.
 * ------------------------------------------------------------------
 * UpdateGovFeeDisbursementStatusCommandHandler switches on the CURRENT status and
 * permits exactly one target per case — PaidByFirm → InvoicedToClient,
 * InvoicedToClient → Reimbursed, Reimbursed → 409 — and the request record carries
 * a single `Status` and nothing else. So this dialog renders NO status control and
 * NO dropdown. The target is derived from `nextGovFeeStatus(status)` and the
 * dialog's whole job is to state the one move that is about to happen and ask
 * whether to make it.
 *
 * A picker would be strictly worse than no picker: every option except one is a
 * guaranteed 409, so it would be a control whose only function is to be wrong.
 *
 * THE DIALOG RENDERS NOTHING AT ALL WHEN THERE IS NO NEXT STATUS, rather than
 * showing a disabled confirmation. `Reimbursed` is terminal, and an unrecognised
 * status from a future backend version also yields null, so this dialog declines
 * to appear for either. The drawer does not render the trigger button in those
 * cases either and says why with GOV_FEE_TERMINAL_NOTE — the two guards are
 * deliberately independent, so neither depends on the other being present.
 *
 * ------------------------------------------------------------------
 * WHY THE BODY IS ONE COMPOSED STRING.
 * ------------------------------------------------------------------
 * ConfirmDialog is a shared, completed component and renders `message` as text, so
 * this dialog assembles its explanation as a single string rather than passing
 * elements. That turns out to suit the content: the four things worth saying are
 * four consecutive sentences, and a paragraph reads better than four stacked
 * callout boxes in a 448px panel.
 *
 * WHY NO FORM AND NO TOAST HERE. There is no input to validate, so there is no
 * inline field-error mapping to do; and the success and error toasts belong to
 * useAdminBillingMutations, which guarantees all ten billing mutations report
 * themselves exactly once rather than relying on each caller to remember. The one
 * thing left for this component is the error BANNER, which a toast cannot
 * substitute for because it stays on screen while the dialog is still open.
 */
function AdminGovFeeDisbursementStatusDialog({ open, disbursement, onClose, onAdvanced }) {
  const [errorMessage, setErrorMessage] = useState(null)

  const advanceStatus = useUpdateAdminGovFeeDisbursementStatus()

  const currentStatus = disbursement?.status ?? null
  const nextStatus = nextGovFeeStatus(currentStatus)

  // A fresh dialog must not open showing the previous attempt's failure, and a
  // different record must not inherit it either. Keyed on the id as well as the
  // open flag, so moving from one disbursement to another inside the same drawer
  // clears the banner even though the dialog never fully unmounted.
  useEffect(() => {
    setErrorMessage(null)
  }, [open, disbursement?.id])

  if (!nextStatus) {
    return null
  }

  const currentLabel = govFeeStatusText(currentStatus) ?? 'an unknown status'
  const nextLabel = govFeeStatusText(nextStatus) ?? nextStatus
  const confirm = BILLING_ACTION_CONFIRMS[IRREVERSIBLE_ACTIONS.GOV_FEE_STATUS]

  /**
   * A government fee carries no invoice number, so the description is the only
   * human identifier this record has. It is truncated rather than dropped so that
   * confirming the right fee is still possible when the drawer is showing more
   * than one.
   */
  const name = truncate(presentText(disbursement?.feeDescription), 60)
  const money = moneyParts(disbursement?.amount, disbursement?.currency)
  const isSubmitting = advanceStatus.isPending

  const message = [
    name ? `${name} — ${money.formatted}.` : null,
    `This moves it from ${currentLabel} to ${nextLabel}.`,
    GOV_FEE_STATUS_ADVANCE_MEANING[nextStatus],
    confirm.irreversible,
    GOV_FEE_STATUS_ADVANCE_REQUEST_FIELDS_NOTE,
  ]
    .filter(Boolean)
    .join(' ')

  function handleConfirm() {
    setErrorMessage(null)

    advanceStatus.mutate(
      { disbursementId: disbursement?.id, status: nextStatus },
      {
        onSuccess: () => {
          // The dialog closes on success so the reader is returned to the drawer,
          // which re-reads the record: the status advance, its timestamp and the
          // audit row are all visible there a moment later.
          onAdvanced?.()
          onClose()
        },
        onError: (requestError) => {
          // The server's own 409 names the transition it refused, so its message is
          // kept rather than replaced — for this endpoint the specific text IS the
          // diagnosis, and a generic string would discard the only useful detail.
          setErrorMessage(
            extractApiErrorMessage(
              requestError,
              'Could not advance this disbursement status.',
            ),
          )
        },
      },
    )
  }

  return (
    <>
      <ConfirmDialog
        open={open}
        title={confirm.title}
        message={message}
        confirmLabel={confirm.confirmLabel}
        tone={confirm.tone}
        isLoading={isSubmitting}
        onConfirm={handleConfirm}
        onCancel={onClose}
      />

      {/*
        Rendered OUTSIDE ConfirmDialog rather than inside it, because ConfirmDialog
        accepts a single text message and this needs to be an announced live region.
        `role="alert"` means a screen reader interrupts with the server's own words
        the moment they appear, which matters here: the request has already failed
        and the dialog is still open, so the failure is otherwise invisible to
        anyone not watching the toast, which has already been announced.

        It is FIXED and floating rather than sitting in the panel's flow, because
        ConfirmDialog's own copy gives the message area to the confirmation text and
        offers nowhere to put an error. That is also why its background is nearly
        opaque where the drawers use bg-red-50/60: this one sits over a dimmed
        scrim, and a 60%-opaque red over a dark backdrop would read as muddy rather
        than as an alert.
      */}
      {errorMessage && (
        <div
          role="alert"
          className="fixed bottom-6 left-1/2 z-[60] w-[min(28rem,calc(100vw-2rem))] -translate-x-1/2 rounded-[10px] border border-red-200 bg-red-50/95 px-4 py-2.5 shadow-[0_8px_24px_rgba(28,31,38,0.10)]"
        >
          <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
        </div>
      )}
    </>
  )
}

export default AdminGovFeeDisbursementStatusDialog
