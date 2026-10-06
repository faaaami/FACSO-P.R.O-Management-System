import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { AlertCircle, BellRing, RotateCcw, UserRound } from 'lucide-react'
import ConfirmDialog from '../../shared/ConfirmDialog'
import { useAdminStaff } from '../../../hooks/admin/useAdminStaff'
import { useAdminTaskActions } from '../../../hooks/admin/useAdminTaskActions'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * The one write surface in this module: assigning a task to an Agent.
 *
 * ENDPOINT: PATCH /api/v1/admin/tasks/{taskId}/assign, body { staffId }.
 *
 * ------------------------------------------------------------------
 * THE PICKER AND THE SERVER AGREE BY CONSTRUCTION, NOT BY COINCIDENCE.
 * ------------------------------------------------------------------
 * The backend's guard (AssignRenewalTaskCommandHandler) requires the target to be
 * a user with Role == Agent, IsActive true and IsDeleted false. The Admin staff
 * list is already filtered to exactly that set:
 *
 *   GetStaffQueryHandler: .Where(x => x.Role == Agent && !x.IsDeleted), plus
 *   .Where(x => x.IsActive) when includeInactive is false.
 *
 * So this panel requests `includeInactive: false` and needs NO client-side role or
 * active filtering — every option it offers is one the server will accept. A
 * role filter here would be redundant code restating a rule that already holds.
 *
 * THE LIST IS PAGINATED AND UNSEARCHABLE, and that is a real limit. The endpoint
 * binds page/pageSize and nothing else — no text search, no id lookup, no "fetch
 * all". So this panel requests the largest sensible page and, when the eligible
 * total exceeds it, SAYS SO instead of presenting a truncated list as complete.
 * An Agent beyond the page cannot be selected from here. This is disclosed in
 * place rather than hidden, because a picker that silently omits people is worse
 * than one that admits a limit.
 *
 * The current assignee is NOT excluded from the options. Reassigning is a normal,
 * reversible operation, and hiding the incumbent would make "reassign to the same
 * Agent" impossible to express — although the submit button does refuse that
 * specific no-op, because it would send a notification and a history row for
 * nothing. That is a courtesy guard on a pointless action, not a business rule.
 *
 * ------------------------------------------------------------------
 * BACKEND RESTRICTIONS ARE NOT PRE-EMPTED.
 * ------------------------------------------------------------------
 * The handler rejects assigning a task whose status is `Updated` ("Cannot assign a
 * completed renewal task."). This panel does NOT hide or disable itself on that
 * status: a client-side eligibility rule would be a second, quietly divergent
 * copy of a server rule, and it would drift the moment the backend changed. The
 * restriction is stated as information, the attempt is allowed through, and if the
 * server refuses, its actual message is shown. The backend is the authority on
 * what is assignable; the browser is not.
 *
 * FAILURES ARE INLINE, NOT TOASTED. A success toast disappears on its own, which
 * is fine for good news. A failure is the opposite: it must stay readable, it must
 * survive long enough to be read, and it must offer a way back. So the server's
 * real message is rendered in an alert region beside the control, and
 * "Try again" re-opens the confirmation rather than re-issuing the write — no
 * unconfirmed request is ever sent.
 *
 * THE CONFIRMATION IS NOT DECORATIVE. Assignment writes a permanent history row,
 * an audit-log row, a persisted notification for the Agent and a SignalR event,
 * so the dialog names the receiving Agent and states the notification explicitly.
 * It stays open and un-cancellable while the request is in flight, which is what
 * prevents a double submit. The shared ConfirmDialog nests inside the drawer's
 * own focus trap, and useFocusTrap's modal stack makes it the topmost surface, so
 * Escape and Tab belong to the dialog and not the drawer behind it.
 */
const PAGE_SIZE = 100

function AdminTaskAssignPanel({ task }) {
  const taskId = task?.id
  const currentAssignee = task?.assignedStaff ?? null

  const { assign } = useAdminTaskActions()
  const {
    items: eligibleStaff,
    totalCount: eligibleTotal,
    isLoading,
    isError,
    error: listError,
    refresh,
  } = useAdminStaff({ includeInactive: false, pageSize: PAGE_SIZE })

  const [selectedStaffId, setSelectedStaffId] = useState('')
  const [confirmingStaffId, setConfirmingStaffId] = useState(null)

  // Sorted by name purely for pickability. This is a dropdown, not a record list,
  // so the presentation order is ours to choose; the underlying set is untouched.
  const options = useMemo(
    () =>
      [...eligibleStaff]
        .filter((member) => member?.id)
        .sort((a, b) => (a.fullName ?? '').localeCompare(b.fullName ?? '')),
    [eligibleStaff],
  )

  const isSubmitting = assign.isPending
  const selectedOption = options.find((member) => member.id === selectedStaffId) ?? null
  const confirmingOption = options.find((member) => member.id === confirmingStaffId) ?? null
  const listIsTruncated = eligibleTotal > options.length

  function handleSelectionChange(value) {
    setSelectedStaffId(value)
    // A stale error must not sit under a newly chosen Agent, or it would read as
    // a verdict on the new choice.
    assign.reset()
  }

  function handleClear() {
    setSelectedStaffId('')
    assign.reset()
  }

  function handleRequestConfirm() {
    if (!selectedStaffId || isSubmitting) return
    setConfirmingStaffId(selectedStaffId)
  }

  function handleCancelConfirm() {
    setConfirmingStaffId(null)
  }

  function handleConfirm() {
    // Guarded twice over: the ConfirmDialog disables its own buttons while
    // isLoading, and a second click landing in the same tick is caught here.
    if (!confirmingStaffId || isSubmitting) return

    const staffId = confirmingStaffId

    assign.mutate(
      { taskId, staffId },
      {
        onSuccess: (data) => {
          setConfirmingStaffId(null)
          setSelectedStaffId('')
          assign.reset()
          toast.success(
            data?.staffName
              ? `Task assigned to ${data.staffName}. They have been notified.`
              : 'Task assigned. The Agent has been notified.',
          )
        },
        onError: () => {
          // Close the dialog on failure so the inline error is what the user is
          // looking at. The dialog's buttons re-enable when isLoading clears, and
          // leaving it open would invite a duplicate submit with no new context.
          setConfirmingStaffId(null)
        },
      },
    )
  }

  const isSameAsCurrentAssignee =
    Boolean(selectedStaffId) && selectedStaffId === (currentAssignee?.id ?? null)

  return (
    <section
      aria-labelledby="admin-task-assign-heading"
      className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-4"
    >
      <div className="mb-3 flex items-center gap-2.5">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-white text-[#16181D]">
          <UserRound size={15} strokeWidth={1.75} aria-hidden="true" />
        </span>
        <h3
          id="admin-task-assign-heading"
          className="min-w-0 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Assignment
        </h3>
      </div>

      <p className="mb-3.5 text-xs text-[#6B7280]">
        {currentAssignee?.fullName ? (
          <>
            Currently assigned to{' '}
            <span className="font-semibold text-[#16181D]">{currentAssignee.fullName}</span>
            {currentAssignee.email ? ` (${currentAssignee.email})` : null}. Choosing a
            different Agent reassigns the task.
          </>
        ) : (
          'No Agent is assigned to this task yet.'
        )}
      </p>

      {/*
        A verified backend restriction, stated as information. It is deliberately
        NOT turned into a disabled control: the server decides what is assignable
        and its own rejection message is surfaced below if the attempt is made.
      */}
      {task?.status === 'Updated' && (
        <p className="mb-3.5 flex items-start gap-2 rounded-[10px] border border-amber-200 bg-amber-50/60 px-3 py-2.5 text-xs text-[#92400E]">
          <AlertCircle size={13} strokeWidth={2} aria-hidden="true" className="mt-0.5 shrink-0" />
          <span>
            The backend refuses to assign a task in the Updated (completed) state.
            You can still submit; if it does, the server&rsquo;s reason is shown
            here.
          </span>
        </p>
      )}

      <div className="flex flex-col gap-2.5 sm:flex-row sm:items-end">
        <div className="min-w-0 flex-1">
          <label
            htmlFor="admin-task-assign-staff"
            className="mb-1.5 block text-xs font-semibold text-[#6B7280]"
          >
            Assign to Agent
          </label>
          <select
            id="admin-task-assign-staff"
            value={selectedStaffId}
            disabled={isLoading || isSubmitting}
            onChange={(event) => handleSelectionChange(event.target.value)}
            className="w-full cursor-pointer rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-2 text-xs font-medium text-[#16181D] transition duration-150 hover:border-[#0F9D74]/25 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            <option value="">
              {isLoading
                ? 'Loading eligible Agents…'
                : isError
                  ? 'Could not load Agents'
                  : 'Select an Agent'}
            </option>
            {options.map((member) => (
              <option key={member.id} value={member.id}>
                {member.fullName}
                {member.email ? ` · ${member.email}` : ''}
              </option>
            ))}
          </select>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <button
            type="button"
            onClick={handleRequestConfirm}
            disabled={!selectedStaffId || isSubmitting || isSameAsCurrentAssignee}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-2 text-xs font-semibold text-white transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isSubmitting ? 'Assigning…' : 'Assign'}
            <span className="sr-only">
              {selectedOption
                ? ` task to ${selectedOption.fullName}`
                : ' task (choose an Agent first)'}
            </span>
          </button>

          {selectedStaffId && (
            <button
              type="button"
              onClick={handleClear}
              disabled={isSubmitting}
              className="cursor-pointer rounded-[8px] px-2 py-2 text-xs font-semibold text-[#6B7280] transition duration-150 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
            >
              Clear
            </button>
          )}
        </div>
      </div>

      {isSameAsCurrentAssignee && (
        <p className="mt-2 text-xs text-[#6B7280]">
          This task is already assigned to that Agent. Choose a different Agent,
          or clear the selection.
        </p>
      )}

      {listIsTruncated && !isError && (
        <p className="mt-2 text-xs text-[#92400E]">
          Showing the first {options.length.toLocaleString('en-US')} of{' '}
          {eligibleTotal.toLocaleString('en-US')} eligible Agents. This endpoint
          has no search or &ldquo;fetch all&rdquo; option, so an Agent beyond this
          page cannot be selected here.
        </p>
      )}

      {isError && (
        <div
          role="alert"
          className="mt-3 flex items-start gap-2 rounded-[10px] border border-red-200 bg-red-50/60 px-3 py-2.5 text-xs text-[#B91C1C]"
        >
          <AlertCircle size={13} strokeWidth={2} aria-hidden="true" className="mt-0.5 shrink-0" />
          <div className="min-w-0">
            <p className="font-medium">
              {extractApiErrorMessage(listError, 'Could not load the eligible Agents.')}
            </p>
            <p className="mt-0.5">
              Assignment is unavailable until the Agent list loads.
            </p>
            <button
              type="button"
              onClick={() => refresh()}
              className="mt-1.5 inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2 py-1 text-[11px] font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              <RotateCcw size={11} strokeWidth={2} aria-hidden="true" />
              Retry
            </button>
          </div>
        </div>
      )}

      {/*
        MUTATION FAILURES LAND HERE, not in a toast. A toast would expire before a
        slow reader had finished it, and the message is the server's own — often
        the only explanation of why a particular assignment was refused.
      */}
      {assign.isError && (
        <div
          role="alert"
          className="mt-3 flex items-start gap-2 rounded-[10px] border border-red-200 bg-red-50/60 px-3 py-2.5 text-xs text-[#B91C1C]"
        >
          <AlertCircle size={13} strokeWidth={2} aria-hidden="true" className="mt-0.5 shrink-0" />
          <div className="min-w-0 flex-1">
            <p className="font-medium">
              {extractApiErrorMessage(assign.error, 'Could not assign this task.')}
            </p>
            <p className="mt-0.5">
              The task was not changed. Nothing was notified.
            </p>
            {selectedStaffId && (
              <button
                type="button"
                onClick={() => {
                  assign.reset()
                  setConfirmingStaffId(selectedStaffId)
                }}
                className="mt-1.5 inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2 py-1 text-[11px] font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
              >
                <RotateCcw size={11} strokeWidth={2} aria-hidden="true" />
                Try again
              </button>
            )}
          </div>
        </div>
      )}

      <p className="mt-3 flex items-start gap-1.5 text-xs text-[#6B7280]">
        <BellRing size={12} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
        <span>
          Assigning or reassigning records a permanent history entry, writes an
          audit log and sends the Agent a notification.
        </span>
      </p>

      {/*
        Rendered LAST, after the panel's own content, so this fixed overlay is the
        final painted surface inside the drawer and sits above it in the stacking
        order. useFocusTrap's modal stack independently makes it the surface that
        owns Escape and Tab while open.
      */}
      <ConfirmDialog
        open={Boolean(confirmingStaffId)}
        title="Assign this task?"
        tone="warning"
        confirmLabel="Assign and notify"
        cancelLabel="Cancel"
        isLoading={isSubmitting}
        message={
          confirmingOption
            ? `${confirmingOption.fullName} will be assigned to this task and notified immediately. ` +
              'This records a history entry, writes an audit log and cannot be undone without reassigning.'
            : 'The selected Agent will be assigned to this task and notified immediately.'
        }
        onConfirm={handleConfirm}
        onCancel={handleCancelConfirm}
      />
    </section>
  )
}

export default AdminTaskAssignPanel
