import { useCallback, useEffect, useId, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { AlertCircle, Loader2, LockOpen, ShieldAlert } from 'lucide-react'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminTaskActions } from '../../../hooks/admin/useAdminTaskActions'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { formatDateTime, presentText } from './taskDisplay'

/**
 * The third write surface in this module: returning a Blocked renewal task to
 * Submitted.
 *
 * ENDPOINT: PATCH /api/v1/admin/tasks/{taskId}/unblock,
 * body { unblockReason }.
 *
 * ------------------------------------------------------------------
 * WHY THIS PANEL EXISTS, AND WHY IT IS THE ADMIN'S JOB.
 * ------------------------------------------------------------------
 * `Blocked` is not in the status chain. UpdateRenewalTaskStatusCommandHandler
 * holds `StatusChain = [Submitted, FeePaid, AwaitingApproval, Approved, Updated]`
 * and computes `Array.IndexOf` on the task's CURRENT status, so a task already in
 * `Blocked` yields -1 and every subsequent /status call is refused. The status
 * endpoint also rejects `Blocked` as a target, so it is reachable only through the
 * Agent's block route. That left the task with exactly one exit: DELETE.
 *
 * Unblock is that exit. It is Admin-only, and not as a courtesy — the handler
 * refuses a non-Admin even for the task they blocked themselves. Unblocking is
 * the decision that the blocker is genuinely gone, which is not something the
 * person who raised the blocker can be the judge of. So the Agent module gains no
 * unblock control, and this panel is the whole of the Admin surface.
 *
 * THE TARGET IS `Submitted`, NOT THE PREVIOUS STATUS. Nothing in the database
 * records what a task's status was before it was blocked — the block handler
 * overwrites it and keeps no earlier value — so restoring it would mean
 * inventing a field. Submitted is the start of the chain, and the Agent re-drives
 * the task forward from there, which is the same work they would do for a new
 * task. The panel says this plainly rather than letting the Admin assume the task
 * resumes where it left off.
 *
 * ------------------------------------------------------------------
 * THE REASON IS REQUIRED, AND THIS IS A DIALOG RATHER THAN A CONFIRMATION.
 * ------------------------------------------------------------------
 * A reason is mandatory: the backend rejects an empty, omitted or whitespace-only
 * one with a 400. The service-fee panel uses the shared ConfirmDialog because a
 * number fits in its message; a free-text reason does not, and a dialog that
 * confirmed an unstated reason would be confirming nothing. So this uses the
 * focus-trapped form-dialog shape from the billing module, with the reason typed
 * into a labelled textarea and an explicit submit.
 *
 * The 400-character cap in the schema is the SERVER's rule, not a form preference:
 * the reason is persisted into `renewal_task_histories.note` (varchar 1000) as
 * "Unblocked: {reason}" and into `audit_logs.description` (varchar 500). Both
 * writes share one SaveChanges with the status change, so a reason that overran
 * either column would abort the whole unblock with a 500. Restating a server rule
 * for immediate feedback is a different thing from inventing one — no limit here
 * that the server does not enforce.
 *
 * ------------------------------------------------------------------
 * BACKEND RESTRICTIONS ARE NOT PRE-EMPTED, EXCEPT WHERE STATE IS FACTUAL.
 * ------------------------------------------------------------------
 * The server refuses a task that is not `Blocked` with a 409, and refuses a
 * missing or soft-deleted one with a 404. Neither is pre-empted here: the panel
 * only offers itself for a task it can see IS `Blocked`, which is a statement of
 * observed state rather than a copy of the server's eligibility rules, and it
 * still lets the attempt through so the server's own message is what a reader
 * sees if the two ever disagree.
 *
 * That matters most for the repeat case. A second unblock of the same task is a
 * 409, not a silent success, because the first one already moved it out of
 * `Blocked`. The panel therefore does not present this as idempotent: the status
 * has changed under it, the control is gone, and a stale view attempting it again
 * is told why.
 *
 * ------------------------------------------------------------------
 * WHAT DISAPPEARS, AND WHERE IT GOES — SAID IN THE PANEL, NOT ASSUMED.
 * ------------------------------------------------------------------
 * BlockedReason and BlockedSince are cleared from the task, so this panel points
 * the reader at the History tab instead of promising a value that is about to
 * disappear. The block's original reason is permanent in its own history row, and
 * this unblock adds a second row beside it, so the whole story stays readable.
 * The response deliberately does not echo the cleared markers either — after a
 * successful unblock they are null, and returning them would invite a caller to
 * render something that no longer exists.
 *
 * ------------------------------------------------------------------
 * FAILURES ARE INLINE, NOT TOASTED.
 * ------------------------------------------------------------------
 * As in the other two panels: a success toast expires on its own, which suits good
 * news, but a failure must stay readable and offer a way back. So the server's own
 * message is rendered in an alert region and the dialog STAYS OPEN, with the typed
 * reason preserved. Closing on failure would discard the exact text the reader
 * needs to correct, and the error is often the only explanation — the 409 names
 * the real status, which is what distinguishes "someone else already unblocked
 * this" from "this was never blocked".
 *
 * NO NOTIFICATION AND NO REALTIME EVENT, which the panel states plainly. The
 * backend sends neither for an unblock, because it sends neither for the block it
 * mirrors. Claiming the Agent is told about it would be describing a push that
 * does not exist; the Agent sees the change on their next load.
 */

/** The server's cap. See the note on `schema` — this is not a form preference. */
const UNBLOCK_REASON_LIMIT = 400

const schema = z.object({
  unblockReason: z
    .string()
    .min(1, 'An unblock reason is required.')
    // The whitespace and length rules are tested against the TRIMMED value, while
    // the trim itself is done in onSubmit rather than in a `.transform()`. That is
    // the convention across this frontend — the billing dialogs trim in their
    // submit handlers — and a transform inside a zodResolver schema makes the
    // resolver's input and output types diverge, which is a well-known source of
    // confusing validation behaviour. The server also trims before it measures, so
    // testing the untrimmed text here would refuse values the server accepts.
    .refine((value) => value.trim().length > 0, {
      message: 'An unblock reason is required — spaces are not a reason.',
    })
    .refine((value) => value.trim().length <= UNBLOCK_REASON_LIMIT, {
      message: `Unblock reason must not exceed ${UNBLOCK_REASON_LIMIT} characters — the server rejects a longer value.`,
    }),
})

/**
 * The confirmation question, stated in the panel body rather than in a nested
 * confirm dialog: by the time the button is live the reader has already read what
 * is about to happen, so a second modal would only repeat it.
 */
const UNBLOCK_IMPACT =
  'Unblocking returns this task to Submitted and clears the blocked reason and blocked date. ' +
  'The original blocked reason stays in the task History, and the Agent can move the task forward again.'

function AdminTaskUnblockPanel({ task }) {
  const taskId = task?.id
  const isBlocked = task?.status === 'Blocked'

  const { unblock } = useAdminTaskActions()

  const [isOpen, setIsOpen] = useState(false)

  const titleId = useId()

  // Stable identity, because useFocusTrap's keydown effect depends on `onClose`.
  // An inline arrow would tear down and re-add the document listener on every
  // render of the panel — harmless, but the hook is written to take a stable
  // callback and there is no reason to make it churn.
  const closeDialog = useCallback(() => setIsOpen(false), [])

  const panelRef = useFocusTrap({ isOpen, onClose: closeDialog })

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { unblockReason: '' },
  })

  // The reason is a NEW fact on each attempt, never a prefilled default: seeding
  // the box with the blocked reason would invite a reader to confirm the opposite
  // of what they mean, and the two are genuinely different statements.
  useEffect(() => {
    if (!isOpen) return
    reset({ unblockReason: '' })
  }, [isOpen, reset])

  const isPending = unblock.isPending || isSubmitting

  function onSubmit(values) {
    setError('unblockReason', null)

    unblock.mutate(
      // Trimmed here, matching the server: it trims before it stores and before it
      // measures, so sending the padded text would be stored trimmed anyway and
      // would only inflate the length against the 400-character cap.
      { taskId, unblockReason: values.unblockReason.trim() },
      {
        onSuccess: () => {
          setIsOpen(false)
          unblock.reset()
          toast.success('Task unblocked and returned to Submitted.')
        },
        onError: (error) => {
          /*
           * A 400 is about the FIELD and belongs on the field, so the reader is
           * corrected in place rather than being shown a banner about their whole
           * submission. Everything else — 404, 409, 403 — is about STATE, not about
           * what was typed, and retyping the same reason would not change it, so
           * those go to the banner with the dialog left open.
           */
          if (error?.response?.status === 400) {
            setError('unblockReason', {
              type: 'server',
              message: extractApiErrorMessage(error, 'The unblock reason was refused.'),
            })
            return
          }

          setError('unblockReason', null)
        },
      },
    )
  }

  /*
   * RENDERED FOR EVERY STATUS, AND SELF-EXPLANATORY WHEN HIDDEN.
   *
   * Returning null for a non-Blocked task would be the tidier code, but it leaves
   * an Admin who opens a finished task unable to learn that the control is
   * missing *by design*. Saying so costs one line and answers the question the
   * silence would raise: unblocking is a decision about a blocked task, and there
   * is nothing to decide here.
   */
  if (!isBlocked) {
    return (
      <section
        aria-labelledby="admin-task-unblock-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-4"
      >
        <div className="mb-2.5 flex items-center gap-2.5">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-white text-[#6B7280]">
            <LockOpen size={15} strokeWidth={1.75} aria-hidden="true" />
          </span>
          <h3
            id="admin-task-unblock-heading"
            className="min-w-0 text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Unblock
          </h3>
        </div>

        <p className="flex items-start gap-2 text-xs text-[#6B7280]">
          <ShieldAlert size={12} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
          <span>
            This task is not blocked, so there is nothing to unblock. Only a task
            in the Blocked state can be returned to Submitted.
          </span>
        </p>
      </section>
    )
  }

  const blockedReason = presentText(task?.blockedReason)
  const blockedSince = formatDateTime(task?.blockedSince)

  return (
    <section
      aria-labelledby="admin-task-unblock-heading"
      className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-4"
    >
      <div className="mb-3 flex items-center gap-2.5">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-white text-[#16181D]">
          <LockOpen size={15} strokeWidth={1.75} aria-hidden="true" />
        </span>
        <h3
          id="admin-task-unblock-heading"
          className="min-w-0 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Unblock
        </h3>
      </div>

      {/*
        The blocker is quoted back in full before the control, because the decision
        turns on whether that specific obstacle is actually gone. This is the last
        moment this text is available: unblocking clears it from the task.
      */}
      <div className="mb-3.5 rounded-[10px] border border-amber-200 bg-amber-50/60 px-3 py-2.5 text-xs text-[#92400E]">
        <p className="font-medium">This task is blocked.</p>
        {blockedReason && <p className="mt-1 leading-relaxed">{blockedReason}</p>}
        {blockedSince && (
          <p className="mt-1 leading-relaxed">Blocked since {blockedSince}.</p>
        )}
        <p className="mt-1.5 leading-relaxed">
          The reason above is cleared from the task when you unblock, and stays in
          the task History.
        </p>
      </div>

      <p className="mb-3.5 text-xs leading-relaxed text-[#6B7280]">
        Unblocking returns this task to <strong>Submitted</strong> so the Agent can
        move it forward again. It does not restore the status the task held before
        it was blocked — that is not recorded anywhere.
      </p>

      <button
        type="button"
        onClick={() => setIsOpen(true)}
        disabled={isPending}
        className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-2 text-xs font-semibold text-white transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
      >
        Unblock task
      </button>

      <p className="mt-3 flex items-start gap-1.5 text-xs text-[#6B7280]">
        <LockOpen size={12} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
        <span>
          Unblocking records a permanent history entry and writes an audit log. It
          does not notify the Agent.
        </span>
      </p>

      {!isOpen ? null : (
        <div className="fixed inset-0 z-50 overflow-y-auto">
          <div
            className="fixed inset-0 bg-slate-900/40"
            onClick={isPending ? undefined : () => setIsOpen(false)}
            aria-hidden="true"
          />

          <div className="relative flex min-h-full items-end justify-center p-0 sm:items-center sm:p-6">
            <div
              ref={panelRef}
              role="dialog"
              aria-modal="true"
              aria-labelledby={titleId}
              tabIndex={-1}
              className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none sm:rounded-[14px] sm:p-6"
            >
              <h2
                id={titleId}
                className="text-base font-semibold tracking-tight text-[#16181D]"
              >
                Unblock this task?
              </h2>

              <p className="mt-1 text-xs leading-relaxed text-[#6B7280]">
                {UNBLOCK_IMPACT}
              </p>

              {/*
                MUTATION FAILURES LAND HERE, except a 400, which is attached to the
                field instead — see onSubmit. The dialog deliberately STAYS OPEN on
                failure: the server's message is often the only explanation, and
                closing would discard the reason the reader needs to correct.
              */}
              {unblock.isError && unblock.error?.response?.status !== 400 && (
                <div
                  role="alert"
                  className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-3"
                >
                  <AlertCircle
                    size={15}
                    strokeWidth={2}
                    className="mt-0.5 shrink-0 text-[#DC2626]"
                    aria-hidden="true"
                  />
                  <div className="min-w-0">
                    <p className="text-xs font-medium text-[#DC2626]">
                      {extractApiErrorMessage(
                        unblock.error,
                        'Could not unblock this task.',
                      )}
                    </p>
                    <p className="mt-1 text-xs leading-relaxed text-[#DC2626]">
                      The task was not changed. Your reason has been kept.
                    </p>
                  </div>
                </div>
              )}

              <form onSubmit={handleSubmit(onSubmit)} noValidate className="mt-5 flex flex-col gap-4">
                <FormField
                  label="Unblock reason"
                  htmlFor="adminTaskUnblockReason"
                  error={errors.unblockReason?.message}
                >
                  <textarea
                    id="adminTaskUnblockReason"
                    rows={3}
                    disabled={isPending}
                    className={`${errors.unblockReason ? inputErrorClass : inputClass} resize-y`}
                    {...register('unblockReason')}
                  />
                </FormField>

                {/*
                  The 400 cap is the server's, and it is stated next to the field
                  rather than discovered on submit.
                */}
                <p className="-mt-2 text-xs text-[#6B7280]">
                  Required, and spaces alone are not a reason. Up to{' '}
                  {UNBLOCK_REASON_LIMIT} characters — the reason is stored in the
                  task History, and the server rejects a longer one.
                </p>

                <div className="flex flex-col-reverse gap-2 pt-1 sm:flex-row sm:justify-end">
                  <button
                    type="button"
                    onClick={() => setIsOpen(false)}
                    disabled={isPending}
                    className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={isPending}
                    className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {isPending ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                        <span>Unblocking…</span>
                      </>
                    ) : (
                      <span>Unblock task</span>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}

export default AdminTaskUnblockPanel
