import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { AlertCircle, Banknote, Lock, RotateCcw } from 'lucide-react'
import ConfirmDialog from '../../shared/ConfirmDialog'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { formatMoney } from '../../client/billing/format'
import { useAdminTaskActions } from '../../../hooks/admin/useAdminTaskActions'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { displayText } from './taskDisplay'

/**
 * The second write surface in this module: setting the Service Fee amount on a
 * renewal task.
 *
 * ENDPOINT: PATCH /api/v1/admin/tasks/{taskId}/service-fee, body { amount }.
 *
 * ------------------------------------------------------------------
 * WHY THIS PANEL EXISTS AT ALL.
 * ------------------------------------------------------------------
 * UpdateRenewalTaskStatusCommandHandler refuses to move a task to `Updated` unless
 * `ServiceFeeAmount > 0`. Before this endpoint the field was written by nothing
 * reachable through the API — only by dev seeders and test fixtures — so that gate
 * could never be satisfied and the last status was unreachable in any
 * API-driven environment. This panel supplies that input. The gate itself is
 * untouched: completion still refuses a task with no fee, exactly as before.
 *
 * This panel does NOT complete the task. Reaching `Updated` is a separate status
 * write, and there is no completion control here on purpose — see
 * useAdminTaskActions.js for why that is still a workflow decision rather than a
 * missing implementation.
 *
 * ------------------------------------------------------------------
 * THE SERVER'S AMOUNT RULES, AND WHY THERE IS NO MAXIMUM.
 * ------------------------------------------------------------------
 * `SetRenewalTaskServiceFeeCommandValidator` enforces exactly two things:
 * greater than 0, and no more than 2 decimal places. There is deliberately no
 * business ceiling, and this panel does not invent one. The 2-decimal limit is
 * not cosmetic: `renewal_tasks.ServiceFeeAmount` is a plain nullable `numeric`
 * column, so nothing at the storage layer would round a third decimal — but the
 * Service Fee invoice created at completion is `numeric(18,2)`, and it would. The
 * server rejects the value rather than truncating it, so the figure an Admin
 * reads back is the figure that gets billed.
 *
 * These three rules are the form's Zod schema. They restate a server rule for
 * immediate feedback, which is a different thing from inventing one: no rule
 * exists here that the server does not enforce.
 *
 * ------------------------------------------------------------------
 * BACKEND RESTRICTIONS ARE NOT PRE-EMPTED, EXCEPT WHERE STATE IS FACTUAL.
 * ------------------------------------------------------------------
 * The handler rejects a task in `Updated` ("Cannot change the service fee of a
 * completed renewal task.") as a 409. The assign panel states that restriction
 * as information and still allows the attempt, because a second client-side copy
 * of a server rule drifts the moment the backend changes. That reasoning applies
 * here unchanged, so the control is NOT disabled on `Updated` and the server's
 * own rejection is what is shown if the attempt goes through.
 *
 * What this panel does gate is the no-op. Submitting the amount the task already
 * carries would write a history row and an audit row describing a change that did
 * not happen, so it is refused with an explanation — the same courtesy guard the
 * assignment panel applies to "reassign to the current Agent". That is a guard on
 * a pointless action, not a business rule.
 *
 * Blocked IS NOT GATED, and deliberately so. Blocked sits outside the status chain
 * (see useAdminTasks.js), so it is not a state this fee advances — but the task
 * still has to be finished eventually, and refusing the amount here would only
 * re-create the deadlock this endpoint was added to remove.
 *
 * ------------------------------------------------------------------
 * FAILURES ARE INLINE, NOT TOASTED.
 * ------------------------------------------------------------------
 * Identical to the assignment panel: a success toast expires on its own, which is
 * fine for good news, whereas a failure must stay readable and offer a way back.
 * So the server's real message is rendered in an alert region beside the control,
 * and "Try again" re-opens the confirmation rather than re-issuing the write — no
 * unconfirmed request is ever sent. The typed value is deliberately NOT cleared on
 * failure, so the reader does not have to retype the figure they are being
 * refused over.
 *
 * THE INPUT IS SEEDED FROM THE CURRENT AMOUNT, and it is a local default, not a
 * controlled value: the drawer's refetch after a successful write updates the
 * displayed figure without stomping on what is in the box.
 *
 * The confirmation names the amount and states that the change is permanent,
 * because the backend writes a history row and an audit row. It stays open and
 * un-cancellable while the request is in flight, which is what prevents a double
 * submit. The shared ConfirmDialog nests inside the drawer's own focus trap, and
 * useFocusTrap's modal stack makes it the topmost surface.
 */

/**
 * True when the TYPED text carries more than two decimal places.
 *
 * Tested on the raw string rather than on Number(value), because the question is
 * what was entered: 10.10 and 10.1 are the same number and both legitimately
 * pass, whereas "0.005" is genuinely finer than the numeric(18,2) column it is
 * heading for. The input is type="number" with step="0.01", which constrains the
 * spinner but not the keyboard, so the typed value still has to be checked.
 */
function hasMoreThanTwoDecimals(text) {
  const separatorIndex = text.indexOf('.')
  if (separatorIndex < 0) return false
  return text.length - separatorIndex - 1 > 2
}

const schema = z.object({
  amount: z
    .string()
    .min(1, 'Amount is required.')
    .refine((value) => Number.isFinite(Number(value)), {
      message: 'Amount must be a number.',
    })
    .refine((value) => Number(value) > 0, {
      message: 'Amount must be greater than 0.',
    })
    .refine((value) => !hasMoreThanTwoDecimals(value), {
      message: 'Amount must not have more than 2 decimal places.',
    }),
})

function AdminTaskServiceFeePanel({ task }) {
  const taskId = task?.id
  const isCompleted = task?.status === 'Updated'

  // Number.isFinite rather than a truthiness test: 0 is a finite number, and the
  // field is nullable, so only null/undefined/NaN mean "never set".
  const hasFee = Number.isFinite(task?.serviceFeeAmount)
  const currentFee = hasFee ? task.serviceFeeAmount : null

  const { setServiceFee } = useAdminTaskActions()

  const [confirmingAmount, setConfirmingAmount] = useState(null)

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    // A local default, not a controlled value, so the drawer's post-write refetch
    // updates the displayed figure without overwriting what is in the box.
    defaultValues: {
      amount: hasFee ? String(currentFee) : '',
    },
  })

  // useWatch, not getValues: the no-op guard below has to know the CURRENTLY
  // typed text to decide whether the button is live, and getValues() read during
  // render does not subscribe to changes — the control would keep whatever state
  // the last render happened to compute until something unrelated re-rendered it.
  const typedAmount = useWatch({ control, name: 'amount' }) ?? ''

  const isSubmitting = setServiceFee.isPending

  function handleRequestConfirm(values) {
    setServiceFee.reset()
    setConfirmingAmount(Number(values.amount))
  }

  function handleCancelConfirm() {
    setConfirmingAmount(null)
  }

  function handleConfirm() {
    // Guarded twice over: the ConfirmDialog disables its own buttons while
    // isLoading, and a second click landing in the same tick is caught here.
    if (confirmingAmount == null || isSubmitting) return

    const amount = confirmingAmount

    setServiceFee.mutate(
      { taskId, amount },
      {
        onSuccess: (data) => {
          setConfirmingAmount(null)
          reset({ amount: '' })
          setServiceFee.reset()
          toast.success(
            `Service fee set to ${formatMoney(data?.serviceFeeAmount ?? amount)}.`,
          )
        },
        onError: () => {
          // Close the dialog on failure so the inline error is what the user is
          // looking at. The dialog's buttons re-enable when isLoading clears, and
          // leaving it open would invite a duplicate submit with no new context.
          setConfirmingAmount(null)
        },
      },
    )
  }

  /**
   * A "Try again" re-opens the confirmation with what is already in the box
   * rather than re-issuing the write. That value necessarily passed the schema
   * to have reached the server in the first place, so re-running the resolver
   * here would only ever re-derive a value already known to be acceptable.
   */
  function handleRetry() {
    setServiceFee.reset()
    handleRequestConfirm({ amount: typedAmount })
  }

  const isUnchanged =
    hasFee && typedAmount !== '' && Number(typedAmount) === Number(currentFee)

  return (
    <section
      aria-labelledby="admin-task-service-fee-heading"
      className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-4"
    >
      <div className="mb-3 flex items-center gap-2.5">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-white text-[#16181D]">
          <Banknote size={15} strokeWidth={1.75} aria-hidden="true" />
        </span>
        <h3
          id="admin-task-service-fee-heading"
          className="min-w-0 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Service fee
        </h3>
      </div>

      <p className="mb-3.5 text-xs text-[#6B7280]">
        {hasFee ? (
          <>
            The service fee for this task is currently{' '}
            <span className="font-semibold text-[#16181D]">
              {formatMoney(currentFee)}
            </span>
            . The backend requires a fee before this task can be completed, and
            uses this figure on the Service Fee invoice.
          </>
        ) : (
          <>
            No service fee has been set for this task. The backend requires one
            before it can be completed, so it is currently blocked from reaching
            that status.
          </>
        )}
      </p>

      {isCompleted && (
        <p className="mb-3.5 flex items-start gap-2 rounded-[10px] border border-amber-200 bg-amber-50/60 px-3 py-2.5 text-xs text-[#92400E]">
          <Lock size={13} strokeWidth={2} aria-hidden="true" className="mt-0.5 shrink-0" />
          <span>
            This task is in the Updated (completed) state and the backend freezes
            its service fee. You can still submit; if you do, the server&rsquo;s
            reason is shown here.
          </span>
        </p>
      )}

      <form
        onSubmit={handleSubmit(handleRequestConfirm)}
        noValidate
        className="flex flex-col gap-2.5"
      >
        <FormField
          label="Amount (AED)"
          htmlFor="admin-task-service-fee-amount"
          error={errors.amount?.message}
        >
          <input
            id="admin-task-service-fee-amount"
            type="number"
            step="0.01"
            inputMode="decimal"
            placeholder="0.00"
            disabled={isSubmitting}
            aria-describedby="admin-task-service-fee-rules"
            className={errors.amount ? inputErrorClass : inputClass}
            {...register('amount')}
          />
        </FormField>

        <p id="admin-task-service-fee-rules" className="text-xs text-[#6B7280]">
          Must be greater than 0, with no more than 2 decimal places. There is no
          maximum on the server; the limit here is the two decimals the invoice
          column stores.
        </p>

        <div className="flex items-center gap-2">
          <button
            type="submit"
            disabled={isSubmitting || isUnchanged}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-2 text-xs font-semibold text-white transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isSubmitting
              ? 'Saving…'
              : hasFee
                ? 'Change'
                : 'Set'}
            <span className="sr-only">
              {typedAmount !== '' ? ` the service fee to ${typedAmount}` : ' the service fee (enter an amount first)'}
            </span>
          </button>
        </div>
      </form>

      {isUnchanged && (
        <p className="mt-2 text-xs text-[#6B7280]">
          That is the amount this task already carries. Enter a different figure to
          record a change — resubmitting the same one would write a history entry
          and an audit log for nothing.
        </p>
      )}

      {/*
        MUTATION FAILURES LAND HERE, not in a toast. A toast would expire before a
        slow reader had finished it, and the message is the server's own — often
        the only explanation of why a particular amount was refused.
      */}
      {setServiceFee.isError && (
        <div
          role="alert"
          className="mt-3 flex items-start gap-2 rounded-[10px] border border-red-200 bg-red-50/60 px-3 py-2.5 text-xs text-[#B91C1C]"
        >
          <AlertCircle size={13} strokeWidth={2} aria-hidden="true" className="mt-0.5 shrink-0" />
          <div className="min-w-0 flex-1">
            <p className="font-medium">
              {extractApiErrorMessage(setServiceFee.error, 'Could not set the service fee.')}
            </p>
            <p className="mt-0.5">
              The task was not changed. Your amount has been kept, so you can
              correct it and try again.
            </p>
            <button
              type="button"
              onClick={handleRetry}
              className="mt-1.5 inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2 py-1 text-[11px] font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              <RotateCcw size={11} strokeWidth={2} aria-hidden="true" />
              Try again
            </button>
          </div>
        </div>
      )}

      <p className="mt-3 flex items-start gap-1.5 text-xs text-[#6B7280]">
        <Banknote size={12} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
        <span>
          Setting or changing the fee records a permanent history entry and writes
          an audit log. It does not notify the Agent.
        </span>
      </p>

      {/*
        Rendered LAST, after the panel's own content, so this fixed overlay is the
        final painted surface inside the drawer and sits above it in the stacking
        order. useFocusTrap's modal stack independently makes it the surface that
        owns Escape and Tab while open.
      */}
      <ConfirmDialog
        open={confirmingAmount != null}
        title={hasFee ? 'Change the service fee?' : 'Set the service fee?'}
        tone="warning"
        confirmLabel={hasFee ? 'Change fee' : 'Set fee'}
        cancelLabel="Cancel"
        isLoading={isSubmitting}
        message={
          confirmingAmount != null
            ? `The service fee for this task will be set to ${formatMoney(confirmingAmount)}. ` +
              'This records a permanent history entry and an audit log.'
            : displayText(null)
        }
        onConfirm={handleConfirm}
        onCancel={handleCancelConfirm}
      />
    </section>
  )
}

export default AdminTaskServiceFeePanel
